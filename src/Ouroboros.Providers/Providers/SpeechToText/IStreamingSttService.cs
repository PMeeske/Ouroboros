// <copyright file="IStreamingSttService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Audio chunk for streaming speech-to-text.
/// </summary>
/// <param name="Data">Raw audio data.</param>
/// <param name="Format">Audio format (e.g., "pcm16", "wav").</param>
/// <param name="SampleRate">Sample rate in Hz.</param>
/// <param name="Channels">Number of audio channels (1 for mono, 2 for stereo).</param>
/// <param name="IsFinal">Whether this is the final chunk in a recording session.</param>
public sealed record AudioChunk(
    byte[] Data,
    string Format,
    int SampleRate,
    int Channels = 1,
    bool IsFinal = false);

/// <summary>
/// Transcription event for streaming STT with partial/final distinction.
/// </summary>
/// <param name="Text">The transcribed text (may be partial).</param>
/// <param name="IsFinal">Whether this is a final result (stable) or interim (may change).</param>
/// <param name="Confidence">Confidence score (0.0-1.0).</param>
/// <param name="Offset">Time offset from start of audio.</param>
/// <param name="Duration">Duration of this transcription segment.</param>
/// <param name="Language">Detected language code.</param>
public sealed record TranscriptionEvent(
    string Text,
    bool IsFinal,
    double Confidence,
    TimeSpan Offset,
    TimeSpan Duration,
    string? Language = null);

/// <summary>
/// Voice activity detection event.
/// </summary>
/// <param name="Activity">The type of voice activity detected.</param>
/// <param name="Timestamp">When the activity was detected.</param>
/// <param name="Confidence">Confidence in the detection (0.0-1.0).</param>
public sealed record VoiceActivityEvent(
    VoiceActivity Activity,
    DateTimeOffset Timestamp,
    double Confidence = 1.0);

/// <summary>
/// Types of voice activity.
/// </summary>
public enum VoiceActivity
{
    /// <summary>Speech has started.</summary>
    SpeechStart,

    /// <summary>Speech has ended.</summary>
    SpeechEnd,

    /// <summary>Silence/no speech detected.</summary>
    Silence,

    /// <summary>Background noise detected (no speech).</summary>
    Noise,
}

/// <summary>
/// Configuration for streaming transcription.
/// </summary>
/// <param name="Language">Language hint (ISO 639-1 code).</param>
/// <param name="EnableInterimResults">Whether to emit partial/interim results.</param>
/// <param name="PunctuationMode">Punctuation mode: "auto", "none", "explicit".</param>
/// <param name="ProfanityFilter">Whether to filter profanity.</param>
/// <param name="SpeakerDiarization">Whether to identify different speakers.</param>
/// <param name="MaxSpeakers">Maximum number of speakers to identify.</param>
/// <param name="ModelSize">Model size hint: "tiny", "base", "small", "medium", "large".</param>
public sealed record StreamingTranscriptionOptions(
    string? Language = null,
    bool EnableInterimResults = true,
    string PunctuationMode = "auto",
    bool ProfanityFilter = false,
    bool SpeakerDiarization = false,
    int MaxSpeakers = 2,
    string ModelSize = "base");

/// <summary>
/// Extended STT interface that supports reactive streaming transcription.
/// Enables real-time voice input processing.
/// </summary>
public interface IStreamingSttService : ISpeechToTextService
{
    /// <summary>
    /// Creates a continuous transcription stream from audio chunks.
    /// Emits partial results as speech is recognized in real-time.
    /// </summary>
    /// <param name="audioStream">Observable of audio chunks.</param>
    /// <param name="options">Streaming transcription options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Observable of transcription events (partial and final).</returns>
    IObservable<TranscriptionEvent> StreamTranscription(
        IObservable<AudioChunk> audioStream,
        StreamingTranscriptionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Voice activity detection stream - detects speech start/end events.
    /// Useful for determining when to start/stop recording.
    /// </summary>
    /// <param name="audioStream">Observable of audio chunks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Observable of voice activity events.</returns>
    IObservable<VoiceActivityEvent> DetectVoiceActivity(
        IObservable<AudioChunk> audioStream,
        CancellationToken ct = default);

    /// <summary>
    /// Starts a streaming transcription session that can be fed audio chunks.
    /// Returns a session that can be used to push audio and receive results.
    /// </summary>
    /// <param name="options">Streaming transcription options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A streaming session for interactive transcription.</returns>
    Task<IStreamingTranscriptionSession> StartStreamingSessionAsync(
        StreamingTranscriptionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets whether streaming transcription is supported by this provider.
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Gets whether voice activity detection is supported.
    /// </summary>
    bool SupportsVoiceActivityDetection { get; }
}

/// <summary>
/// A streaming transcription session for interactive audio input.
/// </summary>
public interface IStreamingTranscriptionSession : IAsyncDisposable
{
    /// <summary>
    /// Gets the observable stream of transcription results.
    /// </summary>
    IObservable<TranscriptionEvent> Results { get; }

    /// <summary>
    /// Gets the observable stream of voice activity events.
    /// </summary>
    IObservable<VoiceActivityEvent> VoiceActivity { get; }

    /// <summary>
    /// Pushes an audio chunk to the transcription session.
    /// </summary>
    /// <param name="chunk">The audio chunk to process.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PushAudioAsync(AudioChunk chunk, CancellationToken ct = default);

    /// <summary>
    /// Signals that no more audio will be sent, triggering final processing.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task EndAudioAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the accumulated final transcription text.
    /// </summary>
    string AccumulatedText { get; }

    /// <summary>
    /// Gets whether the session is currently active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Resets the session for a new transcription.
    /// </summary>
    void Reset();
}

/// <summary>
/// Extension methods for streaming STT services.
/// </summary>
public static class StreamingSttExtensions
{
    /// <summary>
    /// Filters a transcription stream to only final results.
    /// </summary>
    /// <param name="stream">The transcription event stream.</param>
    /// <returns>Observable of only final transcription events.</returns>
    public static IObservable<TranscriptionEvent> FinalResultsOnly(
        this IObservable<TranscriptionEvent> stream)
    {
        return System.Reactive.Linq.Observable
            .Where(stream, e => e.IsFinal);
    }

    /// <summary>
    /// Extracts just the text from transcription events.
    /// </summary>
    /// <param name="stream">The transcription event stream.</param>
    /// <returns>Observable of transcribed text strings.</returns>
    public static IObservable<string> TextOnly(
        this IObservable<TranscriptionEvent> stream)
    {
        return System.Reactive.Linq.Observable.Select(stream, e => e.Text)
            .Where(static text => !string.IsNullOrWhiteSpace(text));
    }

    /// <summary>
    /// Filters voice activity to speech boundaries (start and end only).
    /// </summary>
    /// <param name="stream">The voice activity stream.</param>
    /// <returns>Observable of speech start/end events only.</returns>
    public static IObservable<VoiceActivityEvent> SpeechBoundariesOnly(
        this IObservable<VoiceActivityEvent> stream)
    {
        return System.Reactive.Linq.Observable.Where(
            stream,
            static e => e.Activity is VoiceActivity.SpeechStart or VoiceActivity.SpeechEnd);
    }

    /// <summary>
    /// Detects speech segments between start and end events.
    /// </summary>
    /// <param name="stream">The voice activity stream.</param>
    /// <returns>Observable of speech segment durations.</returns>
    public static IObservable<TimeSpan> SpeechSegmentDurations(
        this IObservable<VoiceActivityEvent> stream)
    {
        return System.Reactive.Linq.Observable.Buffer(stream.SpeechBoundariesOnly(), 2)
            .Where(static buffer => buffer.Count == 2 &&
                             buffer[0].Activity == VoiceActivity.SpeechStart &&
                             buffer[1].Activity == VoiceActivity.SpeechEnd)
            .Select(static buffer => buffer[1].Timestamp - buffer[0].Timestamp);
    }
}
