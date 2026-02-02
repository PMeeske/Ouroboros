// <copyright file="LlmToVoiceBridge.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using Ouroboros.Domain.Voice;
using Ouroboros.Providers;
using Ouroboros.Providers.TextToSpeech;

namespace Ouroboros.Application.Voice;

/// <summary>
/// Bridges streaming LLM output directly to streaming TTS.
/// Handles sentence buffering, back-pressure, and interruption (barge-in).
/// </summary>
public sealed class LlmToVoiceBridge : IDisposable
{
    private readonly InteractionStream _stream;
    private readonly IStreamingChatModel _llm;
    private readonly IStreamingTtsService _tts;
    private readonly AgentPresenceController _presence;

    private readonly CompositeDisposable _disposables = new();
    private readonly TextToSpeechOptions _ttsOptions;

    // Sentence boundary detection
    private static readonly char[] SentenceEnders = ['.', '!', '?', '\n'];
    private const int MinChunkSize = 15;
    private const int MaxChunkSize = 200;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmToVoiceBridge"/> class.
    /// </summary>
    /// <param name="stream">The interaction stream for publishing events.</param>
    /// <param name="llm">The streaming LLM model.</param>
    /// <param name="tts">The streaming TTS service.</param>
    /// <param name="presence">The presence state controller.</param>
    /// <param name="ttsOptions">Optional TTS options.</param>
    public LlmToVoiceBridge(
        InteractionStream stream,
        IStreamingChatModel llm,
        IStreamingTtsService tts,
        AgentPresenceController presence,
        TextToSpeechOptions? ttsOptions = null)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tts = tts ?? throw new ArgumentNullException(nameof(tts));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _ttsOptions = ttsOptions ?? new TextToSpeechOptions(TtsVoice.Nova);
    }

    /// <summary>
    /// Processes a user prompt through the LLM → TTS pipeline.
    /// Streams tokens to display AND voice simultaneously.
    /// </summary>
    /// <param name="prompt">The user's prompt.</param>
    /// <param name="systemPrompt">Optional system prompt.</param>
    /// <param name="correlationId">Correlation ID for event tracking.</param>
    /// <returns>Observable stream of interaction events.</returns>
    public IObservable<InteractionEvent> ProcessPrompt(
        string prompt,
        string? systemPrompt = null,
        Guid? correlationId = null)
    {
        var corrId = correlationId ?? Guid.NewGuid();

        return Observable.Create<InteractionEvent>(async (observer, ct) =>
        {
            var linkedCt = CancellationTokenSource.CreateLinkedTokenSource(
                ct, _presence.ProcessingCancellation).Token;

            var textBuffer = new StringBuilder();
            var sentenceBuffer = new StringBuilder();

            try
            {
                // Build the full prompt
                var fullPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                    ? prompt
                    : $"{systemPrompt}\n\nUser: {prompt}";

                // Get LLM token stream
                var tokenStream = _llm.StreamReasoningContent(fullPrompt, linkedCt);

                // Process tokens
                await tokenStream
                    .Do(token =>
                    {
                        if (linkedCt.IsCancellationRequested) return;

                        textBuffer.Append(token);
                        sentenceBuffer.Append(token);

                        // Emit token for display
                        var responseEvent = new AgentResponseEvent
                        {
                            TextChunk = token,
                            IsComplete = false,
                            IsSentenceEnd = false,
                            Source = InteractionSource.Agent,
                            CorrelationId = corrId,
                        };
                        observer.OnNext(responseEvent);
                        _stream.PublishResponse(token, isComplete: false, isSentenceEnd: false, correlationId: corrId);

                        // Check for sentence boundary
                        var text = sentenceBuffer.ToString();
                        var lastEnder = text.LastIndexOfAny(SentenceEnders);

                        if (lastEnder >= MinChunkSize)
                        {
                            var sentence = text[..(lastEnder + 1)].Trim();
                            if (!string.IsNullOrWhiteSpace(sentence))
                            {
                                // Signal sentence end for TTS
                                var sentenceEndEvent = new AgentResponseEvent
                                {
                                    TextChunk = string.Empty,
                                    IsComplete = false,
                                    IsSentenceEnd = true,
                                    Source = InteractionSource.Agent,
                                    CorrelationId = corrId,
                                };
                                observer.OnNext(sentenceEndEvent);
                                _stream.PublishResponse(string.Empty, isComplete: false, isSentenceEnd: true, correlationId: corrId);

                                // Synthesize the sentence
                                _ = SynthesizeAndPlayAsync(sentence, observer, corrId, linkedCt);
                            }

                            // Keep remainder in buffer
                            sentenceBuffer.Clear();
                            if (lastEnder + 1 < text.Length)
                            {
                                sentenceBuffer.Append(text[(lastEnder + 1)..]);
                            }
                        }
                        else if (sentenceBuffer.Length > MaxChunkSize)
                        {
                            // Force emit if buffer is too large
                            var chunk = text.Trim();
                            if (!string.IsNullOrWhiteSpace(chunk))
                            {
                                _ = SynthesizeAndPlayAsync(chunk, observer, corrId, linkedCt);
                            }

                            sentenceBuffer.Clear();
                        }
                    })
                    .LastOrDefaultAsync();

                // Flush remaining sentence buffer
                var remaining = sentenceBuffer.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(remaining))
                {
                    await SynthesizeAndPlayAsync(remaining, observer, corrId, linkedCt);
                }

                // Signal completion
                var completeEvent = new AgentResponseEvent
                {
                    TextChunk = string.Empty,
                    IsComplete = true,
                    Source = InteractionSource.Agent,
                    CorrelationId = corrId,
                };
                observer.OnNext(completeEvent);
                _stream.PublishResponse(string.Empty, isComplete: true, correlationId: corrId);

                observer.OnCompleted();
            }
            catch (OperationCanceledException)
            {
                // Interrupted - expected during barge-in
                _tts.InterruptSynthesis();

                var interruptEvent = new ControlEvent
                {
                    Action = ControlAction.InterruptSpeech,
                    Reason = "Processing cancelled",
                    Source = InteractionSource.System,
                };
                observer.OnNext(interruptEvent);
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                _stream.PublishError(ex.Message, ex, ErrorCategory.Generation);
                observer.OnError(ex);
            }
        });
    }

    /// <summary>
    /// Processes a prompt and returns the complete response text.
    /// Voice output happens concurrently.
    /// </summary>
    /// <param name="prompt">The user's prompt.</param>
    /// <param name="systemPrompt">Optional system prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete response text.</returns>
    public async Task<string> ProcessPromptAndGetResponseAsync(
        string prompt,
        string? systemPrompt = null,
        CancellationToken ct = default)
    {
        var textBuffer = new StringBuilder();

        await ProcessPrompt(prompt, systemPrompt)
            .OfType<AgentResponseEvent>()
            .Where(e => !string.IsNullOrEmpty(e.TextChunk))
            .Do(e => textBuffer.Append(e.TextChunk))
            .TakeUntil(Observable.Create<System.Reactive.Unit>(o =>
            {
                ct.Register(() => o.OnNext(System.Reactive.Unit.Default));
                return Disposable.Empty;
            }))
            .LastOrDefaultAsync();

        return textBuffer.ToString();
    }

    /// <summary>
    /// Creates a reactive pipeline that automatically processes user input
    /// and generates voice responses.
    /// </summary>
    /// <param name="systemPrompt">The system prompt to use.</param>
    /// <returns>A subscription that processes inputs.</returns>
    public IDisposable CreateAutoPipeline(string? systemPrompt = null)
    {
        return _stream.CompletedUserInputText
            .SelectMany(input => ProcessPrompt(input, systemPrompt))
            .Subscribe(
                _ => { },
                ex => _stream.PublishError(ex.Message, ex, ErrorCategory.Generation));
    }

    private async Task SynthesizeAndPlayAsync(
        string text,
        IObserver<InteractionEvent> observer,
        Guid correlationId,
        CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        try
        {
            var result = await _tts.SynthesizeChunkAsync(text, _ttsOptions, ct);

            await result.Match(
                async chunk =>
                {
                    if (ct.IsCancellationRequested) return;

                    var voiceEvent = new VoiceOutputEvent
                    {
                        AudioChunk = chunk.AudioData,
                        Format = chunk.Format,
                        DurationSeconds = chunk.DurationSeconds,
                        Text = text,
                        IsComplete = chunk.IsComplete,
                        Source = InteractionSource.Agent,
                        CorrelationId = correlationId,
                    };

                    observer.OnNext(voiceEvent);
                    _stream.PublishVoiceOutput(
                        chunk.AudioData,
                        chunk.Format,
                        chunk.DurationSeconds,
                        chunk.IsComplete,
                        text: text);

                    await Task.CompletedTask;
                },
                error =>
                {
                    _stream.PublishError($"TTS error: {error}", category: ErrorCategory.SpeechSynthesis);
                    return Task.CompletedTask;
                });
        }
        catch (OperationCanceledException)
        {
            // Expected during interruption
        }
        catch (Exception ex)
        {
            _stream.PublishError($"TTS error: {ex.Message}", ex, ErrorCategory.SpeechSynthesis);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposables.Dispose();
    }
}

/// <summary>
/// Extension methods for LlmToVoiceBridge.
/// </summary>
public static class LlmToVoiceBridgeExtensions
{
    /// <summary>
    /// Creates a bridge from the given components.
    /// </summary>
    public static LlmToVoiceBridge CreateBridge(
        this InteractionStream stream,
        IStreamingChatModel llm,
        IStreamingTtsService tts,
        AgentPresenceController presence,
        TextToSpeechOptions? options = null)
    {
        return new LlmToVoiceBridge(stream, llm, tts, presence, options);
    }
}
