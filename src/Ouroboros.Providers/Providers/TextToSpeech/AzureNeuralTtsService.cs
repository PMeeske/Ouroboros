// <copyright file="AzureNeuralTtsService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.CognitiveServices.Speech;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Azure Cognitive Services Neural TTS service.
/// Provides high-quality, natural-sounding voices including Jenny (Cortana-like).
/// </summary>
public sealed class AzureNeuralTtsService : ITextToSpeechService, IDisposable
{
    private readonly string _subscriptionKey;
    private readonly string _region;
    private readonly string _voiceName;
    private SpeechSynthesizer? _synthesizer;
    private SpeechConfig? _config;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureNeuralTtsService"/> class.
    /// </summary>
    /// <param name="subscriptionKey">Azure Speech subscription key.</param>
    /// <param name="region">Azure region (e.g., "eastus").</param>
    /// <param name="persona">Persona name for voice selection.</param>
    public AzureNeuralTtsService(string subscriptionKey, string region, string persona = "Ouroboros")
    {
        _subscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
        _region = region ?? throw new ArgumentNullException(nameof(region));

        // Select voice based on persona - Jenny is closest to Cortana
        _voiceName = persona.ToUpperInvariant() switch
        {
            "OUROBOROS" => "en-US-JennyNeural",    // Cortana-like voice!
            "ARIA" => "en-US-AriaNeural",          // Expressive female
            "ECHO" => "en-GB-SoniaNeural",         // UK female
            "SAGE" => "en-US-SaraNeural",          // Calm female
            "ATLAS" => "en-US-GuyNeural",          // Male
            _ => "en-US-JennyNeural"
        };

        InitializeSynthesizer();
    }

    private void InitializeSynthesizer()
    {
        _config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        _config.SpeechSynthesisVoiceName = _voiceName;
        _synthesizer = new SpeechSynthesizer(_config);
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
            // Use SSML with mythic Cortana-style: lighter voice, ethereal with hall effect
            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='en-US'>
                <voice name='{_voiceName}'>
                    <mstts:express-as style='friendly' styledegree='0.8'>
                        <prosody rate='-5%' pitch='+8%' volume='+3%'>
                            <mstts:audioduration value='1.1'/>
                            {System.Security.SecurityElement.Escape(text)}
                        </prosody>
                    </mstts:express-as>
                    <mstts:audioeffect type='eq_car'/>
                </voice>
            </speak>";

            Console.WriteLine($"  [Azure TTS] Speaking: {text[..Math.Min(50, text.Length)]}...");

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Console.WriteLine($"  [Azure TTS Error] {cancellation.Reason}: {cancellation.ErrorDetails}");
            }
            else
            {
                Console.WriteLine($"  [Azure TTS] Result: {result.Reason}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [Azure TTS Exception] {ex.Message}");
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
            string ssml;
            if (isWhisper)
            {
                // Whispering style: softer, lower volume, slower, with whispering effect
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='en-US'>
                    <voice name='{_voiceName}'>
                        <mstts:express-as style='whispering' styledegree='1.0'>
                            <prosody rate='-15%' pitch='-5%' volume='-20%'>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                    </voice>
                </speak>";
            }
            else
            {
                // Normal Cortana-style: lighter voice, ethereal with hall effect
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='en-US'>
                    <voice name='{_voiceName}'>
                        <mstts:express-as style='friendly' styledegree='0.8'>
                            <prosody rate='-5%' pitch='+8%' volume='+3%'>
                                <mstts:audioduration value='1.1'/>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                        <mstts:audioeffect type='eq_car'/>
                    </voice>
                </speak>";
            }

            var logPrefix = isWhisper ? "[Azure TTS 💭]" : "[Azure TTS]";
            Console.WriteLine($"  {logPrefix} Speaking: {text[..Math.Min(50, text.Length)]}...");

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                Console.WriteLine($"  {logPrefix} Error: {cancellation.Reason}: {cancellation.ErrorDetails}");
            }
            else
            {
                Console.WriteLine($"  {logPrefix} Result: {result.Reason}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [Azure TTS Exception] {ex.Message}");
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

            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
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
    }
}
