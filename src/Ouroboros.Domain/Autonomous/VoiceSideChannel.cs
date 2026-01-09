// <copyright file="VoiceSideChannel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Voice configuration for a persona.
/// </summary>
public sealed record PersonaVoice(
    string PersonaName,
    string VoiceId,
    float Rate = 1.0f,
    float Pitch = 1.0f,
    int Volume = 100);

/// <summary>
/// Message to be spoken on the voice side channel.
/// </summary>
public sealed record VoiceMessage(
    string Text,
    string? PersonaName = null,
    VoicePriority Priority = VoicePriority.Normal,
    bool Interrupt = false);

/// <summary>
/// Priority levels for voice messages.
/// </summary>
public enum VoicePriority
{
    /// <summary>Background announcements that can be skipped.</summary>
    Low = 0,

    /// <summary>Normal conversational output.</summary>
    Normal = 1,

    /// <summary>Important notifications.</summary>
    High = 2,

    /// <summary>Critical alerts that should interrupt.</summary>
    Critical = 3
}

/// <summary>
/// Delegate for voice synthesis.
/// </summary>
public delegate Task VoiceSynthesizer(string text, PersonaVoice voice, CancellationToken ct);

/// <summary>
/// Fire-and-forget voice side channel for parallel audio playback.
/// Components publish messages; the channel handles async TTS playback
/// without blocking the main processing flow.
/// </summary>
public sealed class VoiceSideChannel : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, PersonaVoice> DefaultVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        // Rate: 0.5=slow (-5), 1.0=normal (0), 1.5=fast (+5) - made distinct for SAPI
        ["Ouroboros"] = new("Ouroboros", "onyx", 1.0f, 1.0f, 100),   // Normal speed
        ["Aria"] = new("Aria", "nova", 1.3f, 1.1f, 100),             // Faster, higher
        ["Echo"] = new("Echo", "echo", 0.7f, 0.9f, 100),             // Slower, lower
        ["Sage"] = new("Sage", "onyx", 0.8f, 0.95f, 95),             // Thoughtful, slower
        ["Atlas"] = new("Atlas", "onyx", 1.2f, 0.85f, 100),          // Confident, faster
        ["System"] = new("System", "alloy", 1.4f, 1.0f, 90),         // Fast system messages
        ["User"] = new("User", "nova", 1.3f, 1.1f, 100),             // User persona - faster rate, uses Hedda
    };

    private readonly Channel<VoiceMessage> _channel;
    private readonly ConcurrentDictionary<string, PersonaVoice> _voices;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;
    private readonly SemaphoreSlim _speechLock = new(1, 1);

    /// <summary>
    /// Global speech lock shared across all voice services to prevent overlap.
    /// </summary>
    public static SemaphoreSlim GlobalSpeechLock { get; } = new(1, 1);

    private VoiceSynthesizer? _synthesizer;
    private Func<string, CancellationToken, Task<string>>? _llmSanitizer;
    private string _defaultPersona = "Ouroboros";
    private bool _enabled = true;
    private bool _useLlmSanitization = true;
    private bool _disposed;

    /// <summary>
    /// Gets whether the channel is enabled.
    /// </summary>
    public bool IsEnabled => _enabled && _synthesizer != null;

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    public int QueueDepth => _channel.Reader.Count;

    /// <summary>
    /// Event raised when a message is spoken.
    /// </summary>
    public event EventHandler<VoiceMessage>? MessageSpoken;

    /// <summary>
    /// Event raised when playback is skipped (queue overflow or disabled).
    /// </summary>
    public event EventHandler<VoiceMessage>? MessageSkipped;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceSideChannel"/> class.
    /// </summary>
    public VoiceSideChannel(int maxQueueSize = 10)
    {
        _channel = Channel.CreateBounded<VoiceMessage>(new BoundedChannelOptions(maxQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _voices = new ConcurrentDictionary<string, PersonaVoice>(DefaultVoices, StringComparer.OrdinalIgnoreCase);
        _cts = new CancellationTokenSource();
        _processingTask = ProcessQueueAsync(_cts.Token);
    }

    /// <summary>
    /// Sets the voice synthesizer callback.
    /// </summary>
    public void SetSynthesizer(VoiceSynthesizer synthesizer)
    {
        _synthesizer = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));
    }

    /// <summary>
    /// Sets the LLM-based sanitizer for natural voice output.
    /// The function takes text and returns a voice-friendly condensed version.
    /// </summary>
    public void SetLlmSanitizer(Func<string, CancellationToken, Task<string>> sanitizer)
    {
        _llmSanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
    }

    /// <summary>
    /// Enables or disables LLM-based sanitization (falls back to regex if disabled).
    /// </summary>
    public void SetUseLlmSanitization(bool use)
    {
        _useLlmSanitization = use;
    }

    /// <summary>
    /// Sets the default persona for messages without explicit persona.
    /// </summary>
    public void SetDefaultPersona(string personaName)
    {
        _defaultPersona = personaName;
    }

    /// <summary>
    /// Registers or updates a persona voice configuration.
    /// </summary>
    public void RegisterVoice(PersonaVoice voice)
    {
        _voices[voice.PersonaName] = voice;
    }

    /// <summary>
    /// Gets the voice for a persona.
    /// </summary>
    public PersonaVoice GetVoice(string? personaName)
    {
        var name = personaName ?? _defaultPersona;
        return _voices.TryGetValue(name, out var voice)
            ? voice
            : _voices.TryGetValue(_defaultPersona, out var defaultVoice)
                ? defaultVoice
                : DefaultVoices["Ouroboros"];
    }

    /// <summary>
    /// Enables or disables voice output.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    /// <summary>
    /// Publishes a message to be spoken (fire-and-forget).
    /// </summary>
    public void Say(string text, string? persona = null, VoicePriority priority = VoicePriority.Normal)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var message = new VoiceMessage(SanitizeForSpeech(text), persona ?? _defaultPersona, priority);

        if (!_enabled || _synthesizer == null)
        {
            MessageSkipped?.Invoke(this, message);
            return;
        }

        // Non-blocking write - drops oldest if full
        _channel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Speaks a message and waits for completion (blocking).
    /// Uses Rx-based SpeechQueue for proper serialization.
    /// Optionally uses LLM to create natural, condensed voice output.
    /// </summary>
    public async Task SayAndWaitAsync(string text, string? persona = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!_enabled || _synthesizer == null) return;

        // First do basic sanitization (emojis, code blocks, etc.)
        var sanitized = SanitizeForSpeech(text);
        if (string.IsNullOrWhiteSpace(sanitized)) return;

        // Store original for reference - the full text remains the system truth
        var originalForVoice = sanitized;

        // If LLM sanitizer is available and enabled, use it for natural condensation
        // Note: This only affects what is SPOKEN, not what is saved to memory/history
        if (_useLlmSanitization && _llmSanitizer != null && sanitized.Length > 100)
        {
            try
            {
                var llmSanitized = await SanitizeWithLlmAsync(sanitized, ct);
                if (!string.IsNullOrWhiteSpace(llmSanitized))
                {
                    // Show the condensed voice version (original is preserved in system)
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  🔊 [Speaking]: {llmSanitized}");
                    Console.ResetColor();
                    sanitized = llmSanitized;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceSideChannel] LLM sanitization failed: {ex.Message}");
                // Fall back to regex-sanitized version
            }
        }

        var personaName = persona ?? _defaultPersona;

        // Initialize SpeechQueue synthesizer if needed
        SpeechQueue.Instance.SetSynthesizer(async (t, p, c) =>
        {
            var voice = GetVoice(p);
            await _synthesizer(t, voice, c);
        });

        // Use Rx queue for proper serialization
        await SpeechQueue.Instance.EnqueueAndWaitAsync(sanitized, personaName, ct);
    }

    /// <summary>
    /// Uses the LLM to create a natural, condensed voice-friendly version of the text.
    /// </summary>
    private async Task<string> SanitizeWithLlmAsync(string text, CancellationToken ct)
    {
        if (_llmSanitizer == null) return text;

        var prompt = $@"Convert this text into a brief, natural spoken response. Rules:
- Keep it under 2-3 sentences unless the content is complex
- Remove technical details, URLs, file paths
- Make it conversational and easy to listen to
- Preserve the key message and any important information
- Don't add phrases like ""Here's a summary"" - just give the natural speech version
- If it's already short and natural, return it mostly as-is

Text to convert:
{text}

Voice-friendly version:";

        return await _llmSanitizer(prompt, ct);
    }

    /// <summary>
    /// Sanitizes text for speech - removes emojis, code blocks.
    /// </summary>
    private static string SanitizeForSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Remove persona tags like [AutoUser], [Ouroboros-A], [coordinator] etc.
        var sanitized = System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]+\]\s*", "");

        // Condense URLs to just "link" - handles http, https, and www URLs
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"https?://[^\s<>\""]+|www\.[^\s<>\""]+",
            "link",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove markdown link syntax [text](url) -> just keep text
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\[([^\]]+)\]\([^)]+\)", "$1");

        // Remove code blocks
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"```[\s\S]*?```", " ");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"`[^`]+`", " ");

        // Remove emojis - keep only ASCII printable characters and common extended Latin
        var sb = new System.Text.StringBuilder();
        foreach (var c in sanitized)
        {
            // Keep ASCII printable (32-126) and extended Latin (192-255)
            if ((c >= 32 && c <= 126) || (c >= 192 && c <= 255))
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append(' ');
            }
        }

        // Normalize whitespace
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>
    /// Publishes a critical message that interrupts current playback.
    /// </summary>
    public void Interrupt(string text, string? persona = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var message = new VoiceMessage(text, persona ?? _defaultPersona, VoicePriority.Critical, Interrupt: true);

        if (!_enabled || _synthesizer == null)
        {
            MessageSkipped?.Invoke(this, message);
            return;
        }

        // For interrupts, clear the queue first
        while (_channel.Reader.TryRead(out var skipped))
        {
            MessageSkipped?.Invoke(this, skipped);
        }

        _channel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Waits for the queue to drain (useful for shutdown).
    /// </summary>
    public async Task DrainAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (_channel.Reader.Count > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(ct))
            {
                if (!_enabled || _synthesizer == null)
                {
                    MessageSkipped?.Invoke(this, message);
                    continue;
                }

                try
                {
                    var voice = GetVoice(message.PersonaName);
                    await _synthesizer(message.Text, voice, ct);
                    MessageSpoken?.Invoke(this, message);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    // Log but don't crash the channel
                    MessageSkipped?.Invoke(this, message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _channel.Writer.Complete();
        _cts.Cancel();

        try
        {
            await _processingTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            // Force stop
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cts.Dispose();
    }
}

/// <summary>
/// Extension methods for voice side channel integration.
/// </summary>
public static class VoiceSideChannelExtensions
{
    /// <summary>
    /// Speaks with the specified persona's voice.
    /// </summary>
    public static void SayAs(this VoiceSideChannel channel, string persona, string text)
    {
        channel.Say(text, persona);
    }

    /// <summary>
    /// Speaks a system announcement.
    /// </summary>
    public static void Announce(this VoiceSideChannel channel, string text)
    {
        channel.Say(text, "System", VoicePriority.High);
    }

    /// <summary>
    /// Speaks a low-priority background message.
    /// </summary>
    public static void Whisper(this VoiceSideChannel channel, string text, string? persona = null)
    {
        channel.Say(text, persona, VoicePriority.Low);
    }
}
