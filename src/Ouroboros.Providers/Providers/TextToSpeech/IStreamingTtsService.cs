// <copyright file="IStreamingTtsService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// A chunk of synthesized audio with metadata for streaming TTS.
/// </summary>
/// <param name="AudioData">The raw audio data.</param>
/// <param name="Format">The audio format (e.g., "pcm16", "mp3", "wav").</param>
/// <param name="DurationSeconds">Duration of this chunk in seconds.</param>
/// <param name="Text">The text this chunk represents (for debugging/display).</param>
/// <param name="IsSentenceEnd">Indicates a natural pause point (sentence boundary).</param>
/// <param name="IsComplete">True if this is the final chunk.</param>
public sealed record SpeechChunk(
    byte[] AudioData,
    string Format,
    double DurationSeconds,
    string? Text = null,
    bool IsSentenceEnd = false,
    bool IsComplete = false);

/// <summary>
/// Extended TTS interface that supports reactive streaming synthesis.
/// Enables real-time voice output as LLM generates text.
/// </summary>
public interface IStreamingTtsService : ITextToSpeechService
{
    /// <summary>
    /// Creates a reactive stream that consumes text chunks and produces audio chunks.
    /// Perfect for piping LLM streaming output directly to voice synthesis.
    /// </summary>
    /// <remarks>
    /// This method buffers incoming text into sentence-sized chunks for natural
    /// TTS prosody, then synthesizes each sentence and emits the audio.
    /// </remarks>
    /// <param name="textStream">Observable of text chunks to synthesize.</param>
    /// <param name="options">TTS options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Observable of audio chunks.</returns>
    IObservable<SpeechChunk> StreamSynthesis(
        IObservable<string> textStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Synthesizes text incrementally, emitting audio as each sentence is ready.
    /// Uses sentence-boundary detection for natural pauses between chunks.
    /// </summary>
    /// <param name="text">Complete text to synthesize.</param>
    /// <param name="options">TTS options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Observable of audio chunks with sentence boundaries marked.</returns>
    IObservable<SpeechChunk> StreamSynthesisIncremental(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Synthesizes a single sentence/chunk immediately and returns the audio.
    /// Lower latency than full synthesis, suitable for streaming pipelines.
    /// </summary>
    /// <param name="text">The text chunk to synthesize (should be sentence-sized).</param>
    /// <param name="options">TTS options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The speech chunk result.</returns>
    Task<Result<SpeechChunk, string>> SynthesizeChunkAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Stops any ongoing synthesis immediately (for barge-in support).
    /// </summary>
    void InterruptSynthesis();

    /// <summary>
    /// Gets whether synthesis is currently in progress.
    /// </summary>
    bool IsSynthesizing { get; }

    /// <summary>
    /// Gets whether streaming synthesis is supported by this provider.
    /// </summary>
    bool SupportsStreaming { get; }
}

/// <summary>
/// Extension methods for streaming TTS services.
/// </summary>
public static class StreamingTtsExtensions
{
    /// <summary>
    /// Sentence boundary detection pattern for natural TTS chunking.
    /// </summary>
    private static readonly char[] SentenceEnders = ['.', '!', '?', '\n'];
    private const int MinChunkSize = 15;
    private const int MaxChunkSize = 200;

    /// <summary>
    /// Buffers a token stream into sentence-sized chunks for natural TTS.
    /// </summary>
    /// <param name="tokens">The incoming token stream.</param>
    /// <returns>Observable of sentence-sized text chunks.</returns>
    public static IObservable<string> BufferIntoSentences(this IObservable<string> tokens)
    {
        return System.Reactive.Linq.Observable.Create<string>(observer =>
        {
            var buffer = new System.Text.StringBuilder();

            return tokens.Subscribe(
                onNext: token =>
                {
                    buffer.Append(token);

                    var text = buffer.ToString();
                    var lastEnder = text.LastIndexOfAny(SentenceEnders);

                    // Emit if we have a sentence boundary and enough content
                    if (lastEnder >= MinChunkSize)
                    {
                        var sentence = text[..(lastEnder + 1)].Trim();
                        if (!string.IsNullOrWhiteSpace(sentence))
                        {
                            observer.OnNext(sentence);
                        }

                        // Keep remainder in buffer
                        buffer.Clear();
                        if (lastEnder + 1 < text.Length)
                        {
                            buffer.Append(text[(lastEnder + 1)..]);
                        }
                    }
                    else if (buffer.Length > MaxChunkSize)
                    {
                        // Force emit if buffer is too large (no sentence boundary found)
                        var chunk = text.Trim();
                        if (!string.IsNullOrWhiteSpace(chunk))
                        {
                            observer.OnNext(chunk);
                        }

                        buffer.Clear();
                    }
                },
                onError: observer.OnError,
                onCompleted: () =>
                {
                    // Flush remaining buffer
                    var remaining = buffer.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(remaining))
                    {
                        observer.OnNext(remaining);
                    }

                    observer.OnCompleted();
                });
        });
    }

    /// <summary>
    /// Splits text into sentences for incremental synthesis.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <returns>Enumerable of sentences.</returns>
    public static IEnumerable<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var pattern = new System.Text.RegularExpressions.Regex(
            @"(?<=[.!?])\s+|(?<=\n)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var sentences = pattern.Split(text);

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }
}
