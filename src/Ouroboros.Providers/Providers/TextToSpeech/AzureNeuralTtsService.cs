// <copyright file="AzureNeuralTtsService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Microsoft.CognitiveServices.Speech;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Azure Cognitive Services Neural TTS service.
/// Provides high-quality, natural-sounding voices including Jenny (Cortana-like).
/// Implements streaming TTS for real-time voice synthesis.
/// </summary>
public sealed class AzureNeuralTtsService : IStreamingTtsService, IDisposable
{
    private volatile bool _isSynthesizing;
    private CancellationTokenSource? _currentSynthesisCts;
    private readonly SemaphoreSlim _speechLock = new(1, 1);
    private readonly string _subscriptionKey;
    private readonly string _region;
    private string _voiceName;
    private string _culture;
    private SpeechSynthesizer? _synthesizer;
    private SpeechConfig? _config;
    private bool _disposed;

    // Circuit breaker state - shared across all instances for global rate limit protection
    private static readonly ResiliencePipeline CircuitBreakerPipeline = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 2,
            BreakDuration = TimeSpan.FromSeconds(60),
            OnOpened = args =>
            {
                IsCircuitOpen = true;
                Console.WriteLine($"  [TTS] Circuit OPEN - Azure TTS disabled for 60s due to rate limiting");
                return default;
            },
            OnClosed = args =>
            {
                IsCircuitOpen = false;
                Console.WriteLine($"  [TTS] Circuit CLOSED - Azure TTS re-enabled");
                return default;
            },
            OnHalfOpened = args =>
            {
                Console.WriteLine($"  [TTS] Circuit HALF-OPEN - Testing Azure TTS...");
                return default;
            },
        })
        .Build();

    // Polly retry policy for 429 rate limiting with exponential backoff
    private static readonly ResiliencePipeline RetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = args =>
            {
                Console.WriteLine($"  [TTS] Rate limited, retrying in {args.RetryDelay.TotalSeconds:F1}s (attempt {args.AttemptNumber + 1}/3)...");
                return default;
            },
        })
        .Build();

    /// <summary>
    /// Gets whether the circuit breaker is currently open (Azure TTS disabled).
    /// </summary>
    public static bool IsCircuitOpen { get; private set; }

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
    /// Stops any currently playing speech.
    /// </summary>
    public async Task StopSpeakingAsync()
    {
        if (_isSynthesizing && _synthesizer != null)
        {
            try
            {
                _currentSynthesisCts?.Cancel();
                await _synthesizer.StopSpeakingAsync();
            }
            catch
            {
                // Ignore errors during stop
            }
        }
    }

    /// <summary>
    /// Speaks text directly to the default audio output (blocking until complete).
    /// Cancels any previous speech to prevent overlapping.
    /// </summary>
    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Cancel any ongoing speech first
        await StopSpeakingAsync();

        // Use semaphore to ensure only one speech at a time
        await _speechLock.WaitAsync(ct);
        try
        {
            _isSynthesizing = true;
            _currentSynthesisCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if (_synthesizer == null) InitializeSynthesizer();

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

            // Use circuit breaker + retry with exponential backoff for 429 rate limiting
            await CircuitBreakerPipeline.ExecuteAsync(async _ =>
            {
                await RetryPipeline.ExecuteAsync(async token =>
                {
                    using SpeechSynthesisResult result = await _synthesizer!.SpeakSsmlAsync(ssml);

                    if (result.Reason == ResultReason.Canceled)
                    {
                        SpeechSynthesisCancellationDetails cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                        string errorDetails = cancellation.ErrorDetails ?? string.Empty;

                        // Throw on 429 to trigger retry and circuit breaker
                        if (errorDetails.Contains("429") || errorDetails.Contains("Too many requests"))
                        {
                            throw new HttpRequestException($"Rate limited: {errorDetails}");
                        }

                        Console.WriteLine($"  [!] Azure TTS canceled: {cancellation.Reason} - {errorDetails}");
                    }
                    else if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Azure TTS] Audio synthesis completed, {result.AudioData.Length} bytes");
                    }
                }, ct);
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            // Circuit is open - skip Azure TTS entirely
            Console.WriteLine($"  [!] Azure TTS circuit open - using fallback");
            throw;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Rate limited"))
        {
            // All retries exhausted - re-throw so fallback TTS can handle
            Console.WriteLine($"  [!] Azure TTS rate limit exceeded after retries");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Azure TTS exception: {ex.Message}");
            throw;
        }
        finally
        {
            _isSynthesizing = false;
            _currentSynthesisCts?.Dispose();
            _currentSynthesisCts = null;
            _speechLock.Release();
        }
    }

    /// <summary>
    /// Speaks text directly to the default audio output with optional whisper style.
    /// Cancels any previous speech to prevent overlapping.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="isWhisper">If true, uses a whispering/soft style for inner thoughts.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SpeakAsync(string text, bool isWhisper, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Cancel any ongoing speech first
        await StopSpeakingAsync();

        // Use semaphore to ensure only one speech at a time
        await _speechLock.WaitAsync(ct);
        try
        {
            _isSynthesizing = true;
            _currentSynthesisCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            if (_synthesizer == null) InitializeSynthesizer();

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

            // Use circuit breaker + retry with exponential backoff for 429 rate limiting
            await CircuitBreakerPipeline.ExecuteAsync(async _ =>
            {
                await RetryPipeline.ExecuteAsync(async token =>
                {
                    using SpeechSynthesisResult result = await _synthesizer!.SpeakSsmlAsync(ssml);

                    if (result.Reason == ResultReason.Canceled)
                    {
                        SpeechSynthesisCancellationDetails cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                        string errorDetails = cancellation.ErrorDetails ?? string.Empty;

                        // Throw on 429 to trigger retry and circuit breaker
                        if (errorDetails.Contains("429") || errorDetails.Contains("Too many requests"))
                        {
                            throw new HttpRequestException($"Rate limited: {errorDetails}");
                        }

                        Console.WriteLine($"  [!] Azure TTS canceled: {cancellation.Reason} - {errorDetails}");
                    }
                    else if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Azure TTS] Audio synthesis completed, {result.AudioData.Length} bytes");
                    }
                }, ct);
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            // Circuit is open - skip Azure TTS entirely
            Console.WriteLine($"  [!] Azure TTS circuit open - using fallback");
            throw;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Rate limited"))
        {
            // All retries exhausted - re-throw so fallback TTS can handle
            Console.WriteLine($"  [!] Azure TTS rate limit exceeded after retries");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Azure TTS exception: {ex.Message}");
            throw;
        }
        finally
        {
            _isSynthesizing = false;
            _currentSynthesisCts?.Dispose();
            _currentSynthesisCts = null;
            _speechLock.Release();
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
            var voice = _voiceName;
            var rate = options?.Speed ?? 1.0;
            var isWhisper = options?.IsWhisper ?? false;
            bool isGerman = _culture.StartsWith("de", StringComparison.OrdinalIgnoreCase);

            string ssml;
            if (isWhisper)
            {
                // Cortana-style whisper: intimate, wise, slightly ethereal (for inner thoughts)
                var whisperRate = -8 + (int)((rate - 1.0) * 50);
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                    <voice name='{voice}'>
                        <mstts:express-as style='whispering' styledegree='0.6'>
                            <prosody rate='{whisperRate:+0;-0;0}%' pitch='+3%' volume='-15%'>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                    </voice>
                </speak>";
            }
            else
            {
                // Cortana-style: calm, warm, slightly ethereal, intelligent
                var normalRate = -5 + (int)((rate - 1.0) * 50);
                ssml = isGerman
                    ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{voice}'>
                            <prosody rate='{normalRate:+0;-0;0}%' pitch='+5%' volume='+5%'>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </voice>
                    </speak>"
                    : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{voice}'>
                            <mstts:express-as style='assistant' styledegree='1.2'>
                                <prosody rate='{normalRate:+0;-0;0}%' pitch='+5%'>
                                    {System.Security.SecurityElement.Escape(text)}
                                </prosody>
                            </mstts:express-as>
                        </voice>
                    </speak>";
            }

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

    // ========================================================================
    // IStreamingTtsService Implementation
    // ========================================================================

    /// <inheritdoc/>
    public bool IsSynthesizing => _isSynthesizing;

    /// <inheritdoc/>
    public bool SupportsStreaming => true;

    /// <inheritdoc/>
    public IObservable<SpeechChunk> StreamSynthesis(
        IObservable<string> textStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        return textStream
            .BufferIntoSentences()
            .SelectMany(sentence => Observable.FromAsync<SpeechChunk?>(async token =>
            {
                var result = await SynthesizeChunkAsync(sentence, options, token);
                return result.IsSuccess ? result.Value : null;
            }))
            .Where(chunk => chunk != null)
            .Select(chunk => chunk!);
    }

    /// <inheritdoc/>
    public IObservable<SpeechChunk> StreamSynthesisIncremental(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        return Observable.Create<SpeechChunk>(async (observer, token) =>
        {
            var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, token).Token;
            _currentSynthesisCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCt);

            try
            {
                _isSynthesizing = true;
                var sentences = StreamingTtsExtensions.SplitIntoSentences(text).ToList();

                for (int i = 0; i < sentences.Count; i++)
                {
                    if (_currentSynthesisCts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    var sentence = sentences[i];
                    var isLast = i == sentences.Count - 1;

                    var result = await SynthesizeChunkAsync(sentence, options, _currentSynthesisCts.Token);

                    if (result.IsSuccess)
                    {
                        var chunk = result.Value;
                        var finalChunk = new SpeechChunk(
                            chunk.AudioData,
                            chunk.Format,
                            chunk.DurationSeconds,
                            chunk.Text,
                            IsSentenceEnd: true,
                            IsComplete: isLast);
                        observer.OnNext(finalChunk);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Azure TTS] Chunk synthesis error: {result.Error}");
                    }
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
            finally
            {
                _isSynthesizing = false;
            }
        });
    }

    /// <inheritdoc/>
    public async Task<Result<SpeechChunk, string>> SynthesizeChunkAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<SpeechChunk, string>.Failure("Text cannot be empty");
        }

        if (_synthesizer == null) InitializeSynthesizer();

        try
        {
            _isSynthesizing = true;
            var voice = _voiceName;
            var rate = options?.Speed ?? 1.0;
            var isWhisper = options?.IsWhisper ?? false;
            bool isGerman = _culture.StartsWith("de", StringComparison.OrdinalIgnoreCase);

            string ssml;
            if (isWhisper)
            {
                // Cortana-style whisper: intimate, wise, slightly ethereal (for inner thoughts)
                var whisperRate = -8 + (int)((rate - 1.0) * 50);
                ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                    xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                    <voice name='{voice}'>
                        <mstts:express-as style='whispering' styledegree='0.6'>
                            <prosody rate='{whisperRate:+0;-0;0}%' pitch='+3%' volume='-15%'>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </mstts:express-as>
                    </voice>
                </speak>";
            }
            else
            {
                // Cortana-style: calm, warm, slightly ethereal, intelligent
                var normalRate = -5 + (int)((rate - 1.0) * 50);
                ssml = isGerman
                    ? $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{voice}'>
                            <prosody rate='{normalRate:+0;-0;0}%' pitch='+5%' volume='+5%'>
                                {System.Security.SecurityElement.Escape(text)}
                            </prosody>
                        </voice>
                    </speak>"
                    : $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'
                        xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{_culture}'>
                        <voice name='{voice}'>
                            <mstts:express-as style='assistant' styledegree='1.2'>
                                <prosody rate='{normalRate:+0;-0;0}%' pitch='+5%'>
                                    {System.Security.SecurityElement.Escape(text)}
                                </prosody>
                            </mstts:express-as>
                        </voice>
                    </speak>";
            }

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                return Result<SpeechChunk, string>.Success(new SpeechChunk(
                    result.AudioData,
                    "audio/wav",
                    result.AudioDuration.TotalSeconds,
                    Text: text,
                    IsSentenceEnd: true,
                    IsComplete: false));
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                return Result<SpeechChunk, string>.Failure($"Speech synthesis canceled: {cancellation.ErrorDetails}");
            }

            return Result<SpeechChunk, string>.Failure("Speech synthesis failed");
        }
        catch (Exception ex)
        {
            return Result<SpeechChunk, string>.Failure($"Azure TTS error: {ex.Message}");
        }
        finally
        {
            _isSynthesizing = false;
        }
    }

    /// <inheritdoc/>
    public void InterruptSynthesis()
    {
        try
        {
            _currentSynthesisCts?.Cancel();
            _isSynthesizing = false;
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _currentSynthesisCts?.Cancel();
        _currentSynthesisCts?.Dispose();
        _synthesizer?.Dispose();
        _synthesizer = null;
        _speechLock.Dispose();
    }
}
