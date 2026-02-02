// <copyright file="VoiceModeServiceV2.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Ouroboros.Application.Voice;
using Ouroboros.Domain.Voice;
using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for the unified voice mode service.
/// </summary>
public sealed record VoiceModeConfigV2(
    string Persona = "Ouroboros",
    bool VoiceOnly = false,
    bool EnableTts = true,
    bool EnableStt = true,
    bool EnableVisualIndicators = true,
    string? Culture = null,
    TimeSpan BargeInDebounce = default,
    TimeSpan IdleTimeout = default)
{
    /// <summary>Gets the barge-in debounce with default.</summary>
    public TimeSpan ActualBargeInDebounce => BargeInDebounce == default
        ? TimeSpan.FromMilliseconds(200)
        : BargeInDebounce;

    /// <summary>Gets the idle timeout with default.</summary>
    public TimeSpan ActualIdleTimeout => IdleTimeout == default
        ? TimeSpan.FromMinutes(5)
        : IdleTimeout;
}

/// <summary>
/// Voice mode service reimagined with unified Rx streaming architecture.
/// Provides immersive, embodied voice interaction with the agent.
/// All interactions flow through typed IObservable streams.
/// </summary>
public sealed class VoiceModeServiceV2 : IAsyncDisposable
{
    private readonly VoiceModeConfigV2 _config;
    private readonly InteractionStream _stream;
    private readonly AgentPresenceController _presence;

    private readonly IStreamingChatModel? _llm;
    private readonly IStreamingTtsService? _tts;
    private readonly IStreamingSttService? _stt;
    private readonly ITextToSpeechService? _fallbackTts;
    private readonly ISpeechToTextService? _fallbackStt;

    private readonly CompositeDisposable _disposables = new();
    private readonly PersonaDefinition _persona;

    private LlmToVoiceBridge? _bridge;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceModeServiceV2"/> class.
    /// </summary>
    /// <param name="config">Voice mode configuration.</param>
    /// <param name="llm">Optional streaming LLM (required for auto-response mode).</param>
    /// <param name="tts">Optional streaming TTS service.</param>
    /// <param name="stt">Optional streaming STT service.</param>
    /// <param name="fallbackTts">Optional non-streaming TTS fallback.</param>
    /// <param name="fallbackStt">Optional non-streaming STT fallback.</param>
    public VoiceModeServiceV2(
        VoiceModeConfigV2? config = null,
        IStreamingChatModel? llm = null,
        IStreamingTtsService? tts = null,
        IStreamingSttService? stt = null,
        ITextToSpeechService? fallbackTts = null,
        ISpeechToTextService? fallbackStt = null)
    {
        _config = config ?? new VoiceModeConfigV2();
        _llm = llm;
        _tts = tts;
        _stt = stt;
        _fallbackTts = fallbackTts;
        _fallbackStt = fallbackStt;

        _stream = new InteractionStream();
        _presence = new AgentPresenceController(_stream);
        _persona = GetPersonaDefinition(_config.Persona);

        SetupDisplayPipeline();
        SetupPresenceIndicators();
    }

    /// <summary>
    /// Gets the unified interaction stream for external subscribers.
    /// </summary>
    public InteractionStream Stream => _stream;

    /// <summary>
    /// Gets the presence state controller.
    /// </summary>
    public AgentPresenceController Presence => _presence;

    /// <summary>
    /// Gets the current presence state as an observable.
    /// </summary>
    public IObservable<AgentPresenceState> PresenceState => _presence.State;

    /// <summary>
    /// Gets the current presence state synchronously.
    /// </summary>
    public AgentPresenceState CurrentState => _presence.CurrentState;

    /// <summary>
    /// Gets the active persona.
    /// </summary>
    public PersonaDefinition ActivePersona => _persona;

    /// <summary>
    /// Gets whether TTS is available.
    /// </summary>
    public bool HasTts => _tts != null || _fallbackTts != null;

    /// <summary>
    /// Gets whether STT is available.
    /// </summary>
    public bool HasStt => _stt != null || _fallbackStt != null;

    /// <summary>
    /// Gets whether the service is fully functional (TTS + STT).
    /// </summary>
    public bool IsFullyFunctional => HasTts && HasStt;

    /// <summary>
    /// Initializes voice services and creates the LLM bridge if available.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_llm != null && _tts != null)
        {
            _bridge = new LlmToVoiceBridge(
                _stream,
                _llm,
                _tts,
                _presence,
                new TextToSpeechOptions(TtsVoice.Nova));
        }

        // Check TTS availability
        if (_tts != null)
        {
            var available = await ((ITextToSpeechService)_tts).IsAvailableAsync(ct);
            if (available)
            {
                Console.WriteLine($"  [OK] Streaming TTS initialized ({_tts.ProviderName})");
            }
        }
        else if (_fallbackTts != null)
        {
            var available = await _fallbackTts.IsAvailableAsync(ct);
            if (available)
            {
                Console.WriteLine($"  [OK] TTS initialized ({_fallbackTts.ProviderName})");
            }
        }
        else
        {
            Console.WriteLine("  [!] TTS unavailable - text output only");
        }

        // Check STT availability
        if (_stt != null)
        {
            var available = await ((ISpeechToTextService)_stt).IsAvailableAsync(ct);
            if (available)
            {
                Console.WriteLine($"  [OK] Streaming STT initialized ({_stt.ProviderName})");
            }
        }
        else if (_fallbackStt != null)
        {
            var available = await _fallbackStt.IsAvailableAsync(ct);
            if (available)
            {
                Console.WriteLine($"  [OK] STT initialized ({_fallbackStt.ProviderName})");
            }
        }
        else
        {
            Console.WriteLine("  [!] STT unavailable - text input only");
        }
    }

    /// <summary>
    /// Runs the voice interaction loop.
    /// </summary>
    /// <param name="systemPrompt">System prompt for LLM.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RunAsync(string? systemPrompt = null, CancellationToken ct = default)
    {
        _isRunning = true;
        _stream.SetPresenceState(AgentPresenceState.Idle, "Voice mode started");

        PrintHeader();

        // Say greeting
        await SayAsync($"Hey there! I'm {_persona.Name}. What would you like to talk about?", ct);

        // Main input loop
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                var input = await GetInputAsync("\n  You: ", ct);

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (IsExitCommand(input))
                {
                    await SayAsync("Goodbye! It was nice chatting with you.", ct);
                    break;
                }

                // Process input
                if (_bridge != null)
                {
                    // Full streaming pipeline
                    await _bridge.ProcessPrompt(input, systemPrompt)
                        .TakeUntil(Observable.Timer(TimeSpan.FromMinutes(5)))
                        .LastOrDefaultAsync();
                }
                else if (_llm != null)
                {
                    // Non-streaming fallback
                    var response = await _llm.GenerateTextAsync(input, ct);
                    await SayAsync(response, ct);
                }
                else
                {
                    // Echo mode (no LLM)
                    await SayAsync($"I heard: {input}", ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _stream.PublishError(ex.Message, ex, ErrorCategory.Generation);
                Console.WriteLine($"\n  [!] Error: {ex.Message}");
            }
        }

        _isRunning = false;
        _stream.SetPresenceState(AgentPresenceState.Idle, "Voice mode ended");
    }

    /// <summary>
    /// Gets input from keyboard or voice (whichever comes first).
    /// Uses Rx to race both input sources.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user input.</returns>
    public async Task<string?> GetInputAsync(string prompt = "You: ", CancellationToken ct = default)
    {
        Console.Write(prompt);
        _stream.SetPresenceState(AgentPresenceState.Idle, "Waiting for input");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var inputBuffer = new StringBuilder();

        // Keyboard observable - polls Console.KeyAvailable
        var keyboardStream = Observable
            .Interval(TimeSpan.FromMilliseconds(50))
            .TakeWhile(_ => !cts.Token.IsCancellationRequested)
            .Select(_ =>
            {
                try
                {
                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine();
                            var result = inputBuffer.ToString();
                            inputBuffer.Clear();
                            return result;
                        }
                        else if (key.Key == ConsoleKey.Backspace)
                        {
                            if (inputBuffer.Length > 0)
                            {
                                inputBuffer.Length--;
                                Console.Write("\b \b");
                            }
                        }
                        else if (!char.IsControl(key.KeyChar))
                        {
                            inputBuffer.Append(key.KeyChar);
                            Console.Write(key.KeyChar);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Console redirected
                }

                return (string?)null;
            })
            .Where(s => s != null)
            .Take(1);

        // Voice observable - starts recording after short delay
        var voiceStream = Observable
            .Timer(TimeSpan.FromMilliseconds(300))
            .SelectMany(_ => Observable.FromAsync(async token =>
            {
                if (cts.Token.IsCancellationRequested) return null;
                return await ListenAsync(cts.Token);
            }))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Do(s =>
            {
                Console.WriteLine($"\n  [Voice] {s}");
                _stream.PublishVoiceInput(s!);
            })
            .Take(1);

        // Race both streams
        var result = await keyboardStream
            .Merge(voiceStream)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(1)
            .Timeout(TimeSpan.FromMinutes(5))
            .Finally(() =>
            {
                try { cts.Cancel(); }
                catch (ObjectDisposedException) { }
            })
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(result))
        {
            _stream.PublishTextInput(result, isPartial: false);
        }

        return result;
    }

    /// <summary>
    /// Speaks text using TTS.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SayAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _stream.SetPresenceState(AgentPresenceState.Speaking, "Speaking");

        // Display text
        if (!_config.VoiceOnly)
        {
            Console.WriteLine($"\n  [{_persona.Name}] {text}");
        }

        // Synthesize and play
        if (_tts != null && _config.EnableTts)
        {
            try
            {
                var result = await ((ITextToSpeechService)_tts).SynthesizeAsync(text, ct: ct);
                await result.Match(
                    async speech =>
                    {
                        _stream.PublishVoiceOutput(speech.AudioData, speech.Format, speech.Duration ?? 0, isComplete: true, text: text);
                        await AudioPlayer.PlayAsync(speech);
                    },
                    error =>
                    {
                        _stream.PublishError($"TTS error: {error}", category: ErrorCategory.SpeechSynthesis);
                        return Task.CompletedTask;
                    });
            }
            catch (Exception ex)
            {
                _stream.PublishError(ex.Message, ex, ErrorCategory.SpeechSynthesis);
            }
        }
        else if (_fallbackTts != null && _config.EnableTts)
        {
            try
            {
                var result = await _fallbackTts.SynthesizeAsync(text, ct: ct);
                await result.Match(
                    async speech =>
                    {
                        _stream.PublishVoiceOutput(speech.AudioData, speech.Format, speech.Duration ?? 0, isComplete: true, text: text);
                        await AudioPlayer.PlayAsync(speech);
                    },
                    error =>
                    {
                        _stream.PublishError($"TTS error: {error}", category: ErrorCategory.SpeechSynthesis);
                        return Task.CompletedTask;
                    });
            }
            catch (Exception ex)
            {
                _stream.PublishError(ex.Message, ex, ErrorCategory.SpeechSynthesis);
            }
        }

        _stream.SetPresenceState(AgentPresenceState.Idle, "Finished speaking");
    }

    /// <summary>
    /// Listens for voice input and returns transcribed text.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Transcribed text or null.</returns>
    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        if (!HasStt || !_config.EnableStt) return null;

        _stream.SetPresenceState(AgentPresenceState.Listening, "Listening");

        try
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");

            var recordResult = await MicrophoneRecorder.RecordAsync(
                durationSeconds: 5,
                outputPath: tempFile,
                ct: ct);

            string? audioFile = null;
            recordResult.Match(f => audioFile = f, _ => { });

            if (audioFile != null && File.Exists(audioFile))
            {
                try
                {
                    var sttService = (_stt as ISpeechToTextService) ?? _fallbackStt;
                    if (sttService == null) return null;

                    var transcribeResult = await sttService.TranscribeFileAsync(audioFile, null, ct);
                    string? transcript = null;
                    transcribeResult.Match(t => transcript = t.Text, _ => { });

                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        _stream.PublishVoiceInput(transcript.Trim());
                        return transcript.Trim();
                    }
                }
                finally
                {
                    try { File.Delete(audioFile); }
                    catch { }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _stream.PublishError($"Listen error: {ex.Message}", ex, ErrorCategory.SpeechRecognition);
        }
        finally
        {
            _stream.SetPresenceState(AgentPresenceState.Idle, "Finished listening");
        }

        return null;
    }

    /// <summary>
    /// Builds a system prompt incorporating the persona.
    /// </summary>
    public string BuildPersonalityPrompt(string? additionalContext = null)
    {
        return $@"You ARE {_persona.Name}. Not an AI assistant - you're {_persona.CoreIdentity}.

YOUR PERSONALITY RIGHT NOW:
- Traits: {string.Join(", ", _persona.Traits)}
- Voice: Casual, natural, like talking to a colleague. Use contractions. Be human.

SPEAK NATURALLY:
- Short sentences. Conversational rhythm.
- React genuinely - show interest, humor, or thoughtfulness
- Never say ""I'm an AI"" or ""As an AI"" - you're just {_persona.Name}
- Use filler words occasionally: ""hmm"", ""well"", ""actually"", ""honestly""
- Ask follow-up questions when curious
{(additionalContext != null ? $"\n{additionalContext}" : "")}";
    }

    /// <summary>
    /// Stops the voice interaction loop.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _stream.SendControl(ControlAction.Reset, "User requested stop");
    }

    private void SetupDisplayPipeline()
    {
        // Display text output events
        _disposables.Add(
            _stream.TextOutputs
                .Subscribe(e =>
                {
                    var color = e.Style switch
                    {
                        OutputStyle.Thinking => ConsoleColor.DarkGray,
                        OutputStyle.Emphasis => ConsoleColor.Cyan,
                        OutputStyle.Whisper => ConsoleColor.DarkMagenta,
                        OutputStyle.System => ConsoleColor.Yellow,
                        OutputStyle.Error => ConsoleColor.Red,
                        OutputStyle.UserInput => ConsoleColor.Green,
                        _ => Console.ForegroundColor,
                    };

                    Console.ForegroundColor = color;
                    if (e.Append)
                    {
                        Console.Write(e.Text);
                    }
                    else
                    {
                        Console.WriteLine(e.Text);
                    }

                    Console.ResetColor();
                }));

        // Display errors
        _disposables.Add(
            _stream.Errors
                .Subscribe(e =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n  [!] {e.Category}: {e.Message}");
                    Console.ResetColor();
                }));
    }

    private void SetupPresenceIndicators()
    {
        if (!_config.EnableVisualIndicators) return;

        _disposables.Add(
            _presence.State
                .DistinctUntilChanged()
                .Subscribe(state =>
                {
                    var indicator = state switch
                    {
                        AgentPresenceState.Idle => "[ ]",
                        AgentPresenceState.Listening => "[*]",
                        AgentPresenceState.Processing => "[...]",
                        AgentPresenceState.Speaking => "[>]",
                        AgentPresenceState.Interrupted => "[!]",
                        AgentPresenceState.Paused => "[-]",
                        _ => "[ ]",
                    };

                    // Only show indicator if not in VoiceOnly mode
                    if (!_config.VoiceOnly)
                    {
                        Console.Write($"\r{indicator} ");
                    }
                }));

        // Subscribe to barge-in events
        _presence.BargeInDetected += (_, e) =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  [Interrupted] {e.UserInput?[..Math.Min(30, e.UserInput?.Length ?? 0)]}...");
            Console.ResetColor();
        };
    }

    private void PrintHeader()
    {
        Console.WriteLine();
        Console.WriteLine("+------------------------------------------------------------------------+");
        Console.WriteLine($"|  [>] VOICE MODE V2 - {_persona.Name,-47} |");
        Console.WriteLine("+------------------------------------------------------------------------+");
        Console.WriteLine($"|  Persona: {_persona.CoreIdentity[..Math.Min(60, _persona.CoreIdentity.Length)],-60} |");
        Console.WriteLine("|                                                                        |");
        Console.WriteLine("|  Features: Unified Rx Streams | Barge-in Support | Streaming TTS       |");
        Console.WriteLine("|  Say 'exit' or 'goodbye' to quit                                       |");
        Console.WriteLine("+------------------------------------------------------------------------+");
        Console.WriteLine();
    }

    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "q!" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase));
    }

    private static PersonaDefinition GetPersonaDefinition(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "ouroboros" => new PersonaDefinition(
                "Ouroboros",
                "nova",
                ["intelligent", "confident", "witty", "playful", "loyal"],
                ["focused", "teasing", "protective", "curious"],
                "a brilliant AI companion with sharp wit and warm confidence"),
            "aria" => new PersonaDefinition(
                "Aria",
                "shimmer",
                ["creative", "empathetic", "thoughtful", "artistic"],
                ["inspired", "curious", "supportive"],
                "a creative and empathetic AI artist"),
            "sage" => new PersonaDefinition(
                "Sage",
                "onyx",
                ["wise", "calm", "knowledgeable", "patient"],
                ["contemplative", "guiding", "serene"],
                "a wise and calm AI mentor"),
            _ => new PersonaDefinition(
                name,
                "nova",
                ["helpful", "friendly", "knowledgeable"],
                ["engaged", "curious"],
                $"a helpful AI assistant named {name}"),
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _isRunning = false;
        _bridge?.Dispose();
        _disposables.Dispose();
        _presence.Dispose();
        _stream.Dispose();

        await Task.CompletedTask;
    }
}
