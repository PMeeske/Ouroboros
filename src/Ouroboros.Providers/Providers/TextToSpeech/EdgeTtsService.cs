// <copyright file="EdgeTtsService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Polly;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Free neural TTS service using Microsoft Edge's TTS API.
/// Provides high-quality neural voices (same as Azure) without API keys.
/// Requires internet connection but no billing/subscription.
/// NOTE: Microsoft may block this unofficial API (403 errors) - use Azure TTS for production.
/// </summary>
public sealed class EdgeTtsService : ITextToSpeechService, IDisposable
{
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string WssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";

    private readonly string _voice;
    private readonly string _outputFormat;
    private bool _disposed;

    // Circuit breaker - if Edge TTS is blocked, don't spam retries
    private static readonly ResiliencePipeline CircuitBreakerPipeline = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(60),
            MinimumThroughput = 2,
            BreakDuration = TimeSpan.FromMinutes(5), // Stay broken for 5 min if blocked
            OnOpened = args =>
            {
                IsCircuitOpen = true;
                Console.WriteLine($"  [TTS] Edge TTS circuit OPEN - disabled for 5 minutes (likely blocked by Microsoft)");
                return default;
            },
            OnClosed = args =>
            {
                IsCircuitOpen = false;
                return default;
            },
        })
        .Build();

    /// <summary>
    /// Gets whether the Edge TTS circuit breaker is open (service blocked/unavailable).
    /// </summary>
    public static bool IsCircuitOpen { get; private set; }

    /// <summary>
    /// Available Edge TTS neural voices.
    /// </summary>
    public static class Voices
    {
        // English (US)
        public const string JennyNeural = "en-US-JennyNeural";
        public const string AriaNeural = "en-US-AriaNeural";
        public const string GuyNeural = "en-US-GuyNeural";
        public const string DavisNeural = "en-US-DavisNeural";
        public const string AmberNeural = "en-US-AmberNeural";
        public const string AnaNeural = "en-US-AnaNeural";
        public const string AshleyNeural = "en-US-AshleyNeural";
        public const string BrandonNeural = "en-US-BrandonNeural";
        public const string ChristopherNeural = "en-US-ChristopherNeural";
        public const string CoraNeural = "en-US-CoraNeural";
        public const string ElizabethNeural = "en-US-ElizabethNeural";
        public const string EricNeural = "en-US-EricNeural";
        public const string JacobNeural = "en-US-JacobNeural";
        public const string MichelleNeural = "en-US-MichelleNeural";
        public const string MonicaNeural = "en-US-MonicaNeural";
        public const string SaraNeural = "en-US-SaraNeural";

        // English (UK)
        public const string SoniaNeural = "en-GB-SoniaNeural";
        public const string RyanNeural = "en-GB-RyanNeural";
        public const string LibbyNeural = "en-GB-LibbyNeural";

        // German
        public const string KatjaNeural = "de-DE-KatjaNeural";
        public const string ConradNeural = "de-DE-ConradNeural";
        public const string AmalaNeural = "de-DE-AmalaNeural";

        // Default Cortana-like voice
        public const string Default = JennyNeural;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeTtsService"/> class.
    /// </summary>
    /// <param name="voice">Voice name (e.g., "en-US-JennyNeural"). Uses Jenny (Cortana-like) by default.</param>
    /// <param name="outputFormat">Audio format. Default is "audio-24khz-48kbitrate-mono-mp3".</param>
    public EdgeTtsService(string? voice = null, string? outputFormat = null)
    {
        _voice = voice ?? Voices.Default;
        _outputFormat = outputFormat ?? "audio-24khz-48kbitrate-mono-mp3";
    }

    /// <inheritdoc/>
    public string ProviderName => "Microsoft Edge TTS (Free Neural)";

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableVoices => new[]
    {
        Voices.JennyNeural,
        Voices.AriaNeural,
        Voices.GuyNeural,
        Voices.SaraNeural,
        Voices.SoniaNeural,
        Voices.KatjaNeural,
    };

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => new[] { "mp3", "wav", "ogg" };

    /// <inheritdoc/>
    public int MaxInputLength => 10000;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(!IsCircuitOpen); // Not available if circuit is open (blocked)

    /// <inheritdoc/>
    public async Task<Result<SpeechResult, string>> SynthesizeAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<SpeechResult, string>.Failure("Text cannot be empty");
        }

        // Fast-fail if circuit is open
        if (IsCircuitOpen)
        {
            return Result<SpeechResult, string>.Failure("Edge TTS temporarily unavailable (circuit open)");
        }

        try
        {
            byte[] audioData = await CircuitBreakerPipeline.ExecuteAsync(
                async token => await SynthesizeInternalAsync(text, options, token),
                ct);
            return Result<SpeechResult, string>.Success(new SpeechResult(audioData, "mp3"));
        }
        catch (Exception ex)
        {
            return Result<SpeechResult, string>.Failure($"Edge TTS error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToFileAsync(
        string text,
        string outputPath,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        Result<SpeechResult, string> result = await SynthesizeAsync(text, options, ct);

        if (result.IsSuccess)
        {
            try
            {
                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(outputPath, result.Value.AudioData, ct);
                return Result<string, string>.Success(outputPath);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to save audio: {ex.Message}");
            }
        }

        return Result<string, string>.Failure(result.Error ?? "Unknown error");
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        Result<SpeechResult, string> result = await SynthesizeAsync(text, options, ct);

        if (result.IsSuccess)
        {
            await outputStream.WriteAsync(result.Value.AudioData, ct);
            return Result<string, string>.Success("mp3");
        }

        return Result<string, string>.Failure(result.Error ?? "Unknown error");
    }

    private async Task<byte[]> SynthesizeInternalAsync(
        string text,
        TextToSpeechOptions? options,
        CancellationToken ct)
    {
        string voice = _voice;
        double rate = options?.Speed ?? 1.0;
        bool isWhisper = options?.IsWhisper ?? false;

        // Convert rate to percentage (+0% is normal, +50% is 1.5x, -50% is 0.5x)
        int ratePercent = (int)((rate - 1.0) * 100);
        string rateString = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";

        // Pitch adjustment for whisper mode
        string pitchString = isWhisper ? "-10Hz" : "+0Hz";
        string volumeString = isWhisper ? "-20%" : "+0%";

        // Build SSML
        string ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
<voice name='{voice}'>
<prosody rate='{rateString}' pitch='{pitchString}' volume='{volumeString}'>
{System.Security.SecurityElement.Escape(text)}
</prosody>
</voice>
</speak>";

        string requestId = Guid.NewGuid().ToString("N");
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        using ClientWebSocket ws = new();
        ws.Options.SetRequestHeader("Pragma", "no-cache");
        ws.Options.SetRequestHeader("Cache-Control", "no-cache");
        ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        ws.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
        ws.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        ws.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");

        string url = $"{WssUrl}?TrustedClientToken={TrustedClientToken}&ConnectionId={requestId}";
        await ws.ConnectAsync(new Uri(url), ct);

        // Send config message
        string configMessage = $"X-Timestamp:{timestamp}\r\n" +
            "Content-Type:application/json; charset=utf-8\r\n" +
            "Path:speech.config\r\n\r\n" +
            JsonSerializer.Serialize(new
            {
                context = new
                {
                    synthesis = new
                    {
                        audio = new
                        {
                            metadataoptions = new { sentenceBoundaryEnabled = false, wordBoundaryEnabled = false },
                            outputFormat = _outputFormat
                        }
                    }
                }
            });

        await ws.SendAsync(
            Encoding.UTF8.GetBytes(configMessage),
            WebSocketMessageType.Text,
            true,
            ct);

        // Send SSML message
        string ssmlMessage = $"X-RequestId:{requestId}\r\n" +
            $"X-Timestamp:{timestamp}\r\n" +
            "Content-Type:application/ssml+xml\r\n" +
            "Path:ssml\r\n\r\n" +
            ssml;

        await ws.SendAsync(
            Encoding.UTF8.GetBytes(ssmlMessage),
            WebSocketMessageType.Text,
            true,
            ct);

        // Receive audio data
        using MemoryStream audioStream = new();
        byte[] buffer = new byte[8192];
        bool audioStarted = false;

        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await ws.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Binary data contains header + audio
                // Find the header/data separator (double CRLF or "Path:audio\r\n")
                int headerEnd = FindHeaderEnd(buffer, result.Count);
                if (headerEnd > 0 && headerEnd < result.Count)
                {
                    audioStream.Write(buffer, headerEnd, result.Count - headerEnd);
                    audioStarted = true;
                }
                else if (audioStarted)
                {
                    audioStream.Write(buffer, 0, result.Count);
                }
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Check for turn.end which signals completion
                if (message.Contains("Path:turn.end"))
                {
                    break;
                }
            }
        }

        return audioStream.ToArray();
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        // Look for "Path:audio\r\n" followed by data
        byte[] pattern = Encoding.ASCII.GetBytes("Path:audio\r\n");

        for (int i = 0; i <= length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i + pattern.Length;
            }
        }

        return -1;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
