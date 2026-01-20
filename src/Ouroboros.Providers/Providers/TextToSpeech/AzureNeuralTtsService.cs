// <copyright file="AzureNeuralTtsService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Azure Cognitive Services Neural TTS service.
/// Provides high-quality, natural-sounding voices including Jenny (Cortana-like).
/// </summary>
public sealed class AzureNeuralTtsService : ITextToSpeechService, IDisposable
{
    private readonly string _subscriptionKey;
    private readonly string _region;
    private string _voiceName;
    private string _culture;
    private SpeechSynthesizer? _synthesizer;
    private SpeechConfig? _config;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the culture for voice selection and language.
    /// </summary>
    public string Culture
    {
        get => _culture;
        set
        {
            _culture = value;
            UpdateVoiceForCulture();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureNeuralTtsService"/> class.
    /// </summary>
    /// <param name="subscriptionKey">Azure Speech subscription key.</param>
    /// <param name="region">Azure region (e.g., "eastus").</param>
    /// <param name="persona">Persona name for voice selection.</param>
    /// <param name="culture">Culture for language selection (e.g., "de-DE").</param>
    public AzureNeuralTtsService(string subscriptionKey, string region, string persona = "Ouroboros", string? culture = null)
    {
        _subscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _culture = culture ?? "en-US";

        // Select voice based on persona and culture
        _voiceName = SelectVoice(persona, _culture);

        InitializeSynthesizer();
    }

    private static string SelectVoice(string persona, string culture)
    {
        bool isGerman = culture.Equals("de-DE", StringComparison.OrdinalIgnoreCase);

        return persona.ToUpperInvariant() switch
        {
            "OUROBOROS" => isGerman ? "de-DE-KatjaNeural" : "en-US-JennyNeural",
            "ARIA" => isGerman ? "de-DE-AmalaNeural" : "en-US-AriaNeural",
            "ECHO" => isGerman ? "de-DE-LouisaNeural" : "en-GB-SoniaNeural",
            "SAGE" => isGerman ? "de-DE-ElkeNeural" : "en-US-SaraNeural",
            "ATLAS" => isGerman ? "de-DE-ConradNeural" : "en-US-GuyNeural",
            _ => isGerman ? "de-DE-KatjaNeural" : "en-US-JennyNeural"
        };
    }

    private void UpdateVoiceForCulture()
    {
        // Extract persona from current voice name
        string persona = _voiceName switch
        {
            var v when v.Contains("Jenny") || v.Contains("Katja") => "OUROBOROS",
            var v when v.Contains("Aria") || v.Contains("Amala") => "ARIA",
            var v when v.Contains("Sonia") || v.Contains("Louisa") => "ECHO",
            var v when v.Contains("Sara") || v.Contains("Elke") => "SAGE",
            var v when v.Contains("Guy") || v.Contains("Conrad") => "ATLAS",
            _ => "OUROBOROS"
        };

        _voiceName = SelectVoice(persona, _culture);

        // Reinitialize synthesizer with new voice
        _synthesizer?.Dispose();
        InitializeSynthesizer();
    }

    private AudioConfig? _audioConfig;

    private void InitializeSynthesizer()
    {
        // Dispose old synthesizer first before disposing audio config
        _synthesizer?.Dispose();
        _synthesizer = null;
        _audioConfig?.Dispose();
        _audioConfig = null;

        _config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        _config.SpeechSynthesisVoiceName = _voiceName;

        // Explicitly use default speaker output for audio playback
        _audioConfig = AudioConfig.FromDefaultSpeakerOutput();
        _synthesizer = new SpeechSynthesizer(_config, _audioConfig);
    }

    /// <inheritdoc/>
    public string ProviderName => "Azure Neural TTS";

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableVoices => new[]
    {
        "en-US-JennyNeural",
        "en-US-AriaNeural",
        "en-US-GuyNeural",
        "en-US-SaraNeural",
        "en-GB-SoniaNeural",
        "de-DE-KatjaNeural",
        "de-DE-ConradNeural",
        "de-DE-AmalaNeural",
        "de-DE-ElkeNeural",
        "de-DE-LouisaNeural",
    };

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => new[] { "wav", "mp3", "ogg" };

    /// <inheritdoc/>
    public int MaxInputLength => 10000;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrEmpty(_subscriptionKey) && !string.IsNullOrEmpty(_region));

    /// <summary>
    /// Speaks text directly to the default audio output (blocking until complete).
    /// </summary>
    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_synthesizer == null) InitializeSynthesizer();

        try
        {
            bool isGerman = _culture.StartsWith("de", StringComparison.OrdinalIgnoreCase);

            // Cortana-style: calm, warm, slightly ethereal, intelligent
            var ssml = isGerman
                ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                    <voice name='{_voiceName}'>
                        <prosody rate='-5%' pitch='+5%' volume='+5%'>
                            {System.Security.SecurityElement.Escape(text)}
                        </prosody>
                    </voice>
                </speak>"
                : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                    <voice name='{_voiceName}'>
                        <mstts:express-as style='assistant' styledegree='1.2'>
                            <prosody rate='-5%' pitch='+5%'>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                    </voice>
                </speak>";

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Console.WriteLine($"  [!] Azure TTS Canceled: {cancellation.Reason}: {cancellation.ErrorDetails}");
            }
            else if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine($"  [✓] Azure TTS: Audio played ({result.AudioData.Length} bytes)");
            }
            else
            {
                Console.WriteLine($"  [?] Azure TTS Result: {result.Reason}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Azure TTS Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Speaks text directly to the default audio output with optional whisper style.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="isWhisper">If true, uses a whispering/soft style for inner thoughts.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SpeakAsync(string text, bool isWhisper, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_synthesizer == null) InitializeSynthesizer();

        try
        {
            bool isGerman = _culture.StartsWith("de", StringComparison.OrdinalIgnoreCase);
            string ssml;

            if (isWhisper)
            {
                // Cortana-style whisper: intimate, wise, slightly ethereal
                ssml = isGerman
                    ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{_voiceName}'>
                            <mstts:express-as style='whispering' styledegree='0.6'>
                                <prosody rate='-8%' pitch='+3%' volume='-15%'>
                                    {System.Security.SecurityElement.Escape(text)}
                                </prosody>
                            </mstts:express-as>
                        </voice>
                    </speak>"
                    : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{_voiceName}'>
                            <mstts:express-as style='whispering' styledegree='0.6'>
                                <prosody rate='-8%' pitch='+3%' volume='-15%'>
                                    {System.Security.SecurityElement.Escape(text)}
                                </prosody>
                            </mstts:express-as>
                        </voice>
                    </speak>";
            }
            else
            {
                // Cortana-style answers: calm, confident, warm
                ssml = isGerman
                    ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{_voiceName}'>
                            <prosody rate='-5%' pitch='+5%' volume='+5%'>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </voice>
                    </speak>"
                    : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{_voiceName}'>
                            <mstts:express-as style='assistant' styledegree='1.2'>
                                <prosody rate='-5%' pitch='+5%'>
                                    {System.Security.SecurityElement.Escape(text)}
                                </prosody>
                            </mstts:express-as>
                        </voice>
                    </speak>";
            }

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Console.WriteLine($"  [!] Azure TTS Canceled (whisper): {cancellation.Reason}: {cancellation.ErrorDetails}");
            }
            else if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                Console.WriteLine($"  [✓] Azure TTS (whisper): Audio played ({result.AudioData.Length} bytes)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Azure TTS Exception (whisper): {ex.Message}");
        }
    }

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

        if (_synthesizer == null) InitializeSynthesizer();

        try
        {
            // For Azure, we use _voiceName directly since TtsVoice enum is for OpenAI
            var voice = _voiceName;
            var rate = options?.Speed ?? 1.0;
            var ratePercent = (int)((rate - 1.0) * 50);

            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{_culture}'>
                <voice name='{voice}'>
                    <prosody rate='{ratePercent:+0;-0;0}%'>{System.Security.SecurityElement.Escape(text)}</prosody>
                </voice>
            </speak>";

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                return Result<SpeechResult, string>.Success(new SpeechResult(
                    result.AudioData,
                    "audio/wav",
                    result.AudioDuration.TotalSeconds));
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                return Result<SpeechResult, string>.Failure($"Speech synthesis canceled: {cancellation.ErrorDetails}");
            }

            return Result<SpeechResult, string>.Failure("Speech synthesis failed");
        }
        catch (Exception ex)
        {
            return Result<SpeechResult, string>.Failure($"Azure TTS error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToFileAsync(
        string text,
        string outputPath,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        var result = await SynthesizeAsync(text, options, ct);
        if (result.IsSuccess)
        {
            await File.WriteAllBytesAsync(outputPath, result.Value.AudioData, ct);
            return Result<string, string>.Success(outputPath);
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
        var result = await SynthesizeAsync(text, options, ct);
        if (result.IsSuccess)
        {
            await outputStream.WriteAsync(result.Value.AudioData, ct);
            return Result<string, string>.Success("audio/wav");
        }

        return Result<string, string>.Failure(result.Error ?? "Unknown error");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _synthesizer?.Dispose();
        _synthesizer = null;
        _audioConfig?.Dispose();
        _audioConfig = null;
    }
}
