// <copyright file="AudioSensor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Core.EmbodiedInteraction;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ouroboros.Core.Monads;

/// <summary>
/// Configuration for the audio sensor (microphone).
/// </summary>
/// <param name="SampleRate">Audio sample rate in Hz.</param>
/// <param name="Channels">Number of audio channels.</param>
/// <param name="Language">Language hint for STT.</param>
/// <param name="EnableVAD">Enable voice activity detection.</param>
/// <param name="SilenceThresholdMs">Silence threshold for VAD in ms.</param>
/// <param name="MaxRecordingDurationMs">Maximum recording duration in ms.</param>
/// <param name="EnableInterimResults">Enable streaming interim results.</param>
public sealed record AudioSensorConfig(
    int SampleRate = 16000,
    int Channels = 1,
    string? Language = null,
    bool EnableVAD = true,
    int SilenceThresholdMs = 1500,
    int MaxRecordingDurationMs = 30000,
    bool EnableInterimResults = true);

/// <summary>
/// Audio chunk from microphone.
/// </summary>
/// <param name="Data">Raw audio data.</param>
/// <param name="SampleRate">Sample rate.</param>
/// <param name="Channels">Number of channels.</param>
/// <param name="Timestamp">Capture timestamp.</param>
/// <param name="IsFinal">Is this the final chunk.</param>
public sealed record AudioChunk(
    byte[] Data,
    int SampleRate,
    int Channels,
    DateTime Timestamp,
    bool IsFinal);

/// <summary>
/// Voice activity detection event.
/// </summary>
public enum VoiceActivity
{
    /// <summary>No speech detected.</summary>
    Silence,

    /// <summary>Speech started.</summary>
    SpeechStart,

    /// <summary>Speech ongoing.</summary>
    Speaking,

    /// <summary>Speech ended.</summary>
    SpeechEnd,
}

/// <summary>
/// Transcription result from STT model.
/// </summary>
/// <param name="Text">Transcribed text.</param>
/// <param name="Confidence">Confidence score 0-1.</param>
/// <param name="Language">Detected language.</param>
/// <param name="IsFinal">Is this a final result.</param>
/// <param name="StartTime">Start time offset.</param>
/// <param name="EndTime">End time offset.</param>
/// <param name="Words">Word-level timestamps if available.</param>
public sealed record TranscriptionResult(
    string Text,
    double Confidence,
    string? Language,
    bool IsFinal,
    TimeSpan StartTime,
    TimeSpan EndTime,
    IReadOnlyList<WordTiming>? Words);

/// <summary>
/// Word-level timing information.
/// </summary>
/// <param name="Word">The word.</param>
/// <param name="StartTime">Start time.</param>
/// <param name="EndTime">End time.</param>
/// <param name="Confidence">Confidence.</param>
public sealed record WordTiming(
    string Word,
    TimeSpan StartTime,
    TimeSpan EndTime,
    double Confidence);

/// <summary>
/// Interface for Speech-to-Text models.
/// </summary>
public interface ISttModel
{
    /// <summary>
    /// Gets the model name/identifier.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Transcribes an audio file.
    /// </summary>
    Task<Result<TranscriptionResult, string>> TranscribeAsync(
        string audioFilePath,
        string? language = null,
        CancellationToken ct = default);

    /// <summary>
    /// Transcribes audio bytes.
    /// </summary>
    Task<Result<TranscriptionResult, string>> TranscribeAsync(
        byte[] audioData,
        string format,
        int sampleRate,
        string? language = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a streaming transcription session.
    /// </summary>
    IStreamingTranscription CreateStreamingSession(string? language = null);

    /// <summary>
    /// Gets whether this model supports streaming.
    /// </summary>
    bool SupportsStreaming { get; }
}

/// <summary>
/// Interface for streaming transcription.
/// </summary>
public interface IStreamingTranscription : IAsyncDisposable
{
    /// <summary>
    /// Observable stream of transcription results.
    /// </summary>
    IObservable<TranscriptionResult> Results { get; }

    /// <summary>
    /// Observable stream of voice activity.
    /// </summary>
    IObservable<VoiceActivity> VoiceActivity { get; }

    /// <summary>
    /// Pushes audio data to the transcription stream.
    /// </summary>
    Task PushAudioAsync(byte[] audioData, CancellationToken ct = default);

    /// <summary>
    /// Signals end of audio stream.
    /// </summary>
    Task EndAudioAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets accumulated final transcript.
    /// </summary>
    string AccumulatedTranscript { get; }

    /// <summary>
    /// Gets whether the session is active.
    /// </summary>
    bool IsActive { get; }
}

/// <summary>
/// Audio sensor that captures microphone input and performs STT.
/// </summary>
public sealed class AudioSensor : IDisposable
{
    private readonly AudioSensorConfig _config;
    private readonly ISttModel _sttModel;
    private readonly VirtualSelf _virtualSelf;
    private readonly Subject<AudioChunk> _audioChunks = new();
    private readonly Subject<TranscriptionResult> _transcriptions = new();
    private readonly Subject<VoiceActivity> _voiceActivity = new();
    private IStreamingTranscription? _streamingSession;
    private bool _isListening;
    private bool _disposed;

    /// <summary>
    /// Initializes a new audio sensor.
    /// </summary>
    /// <param name="sttModel">The STT model for transcription.</param>
    /// <param name="virtualSelf">The virtual self to publish perceptions to.</param>
    /// <param name="config">Sensor configuration.</param>
    public AudioSensor(
        ISttModel sttModel,
        VirtualSelf virtualSelf,
        AudioSensorConfig? config = null)
    {
        _sttModel = sttModel ?? throw new ArgumentNullException(nameof(sttModel));
        _virtualSelf = virtualSelf ?? throw new ArgumentNullException(nameof(virtualSelf));
        _config = config ?? new AudioSensorConfig();
    }

    /// <summary>
    /// Gets the STT model name.
    /// </summary>
    public string ModelName => _sttModel.ModelName;

    /// <summary>
    /// Gets whether currently listening.
    /// </summary>
    public bool IsListening => _isListening;

    /// <summary>
    /// Observable stream of audio chunks.
    /// </summary>
    public IObservable<AudioChunk> AudioChunks => _audioChunks.AsObservable();

    /// <summary>
    /// Observable stream of transcriptions.
    /// </summary>
    public IObservable<TranscriptionResult> Transcriptions => _transcriptions.AsObservable();

    /// <summary>
    /// Observable stream of voice activity.
    /// </summary>
    public IObservable<VoiceActivity> VoiceActivityEvents => _voiceActivity.AsObservable();

    /// <summary>
    /// Starts listening for audio input.
    /// </summary>
    public async Task<Result<Unit, string>> StartListeningAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<Unit, string>.Failure("Sensor is disposed");
        if (_isListening) return Result<Unit, string>.Failure("Already listening");

        try
        {
            if (_sttModel.SupportsStreaming)
            {
                _streamingSession = _sttModel.CreateStreamingSession(_config.Language);

                // Wire up streaming results to our subjects
                _streamingSession.Results.Subscribe(result =>
                {
                    _transcriptions.OnNext(result);

                    if (result.IsFinal)
                    {
                        // Publish to virtual self
                        _virtualSelf.PublishAudioPerception(
                            result.Text,
                            result.Confidence,
                            result.Language,
                            result.EndTime - result.StartTime,
                            result.IsFinal);
                    }
                });

                _streamingSession.VoiceActivity.Subscribe(activity =>
                {
                    _voiceActivity.OnNext(activity);
                });
            }

            _isListening = true;
            _virtualSelf.ActivateSensor(SensorModality.Audio);
            _virtualSelf.SetState(EmbodimentState.Listening);

            return Result<Unit, string>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to start listening: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops listening.
    /// </summary>
    public async Task<Result<Unit, string>> StopListeningAsync(CancellationToken ct = default)
    {
        if (!_isListening) return Result<Unit, string>.Success(Unit.Default);

        try
        {
            if (_streamingSession != null)
            {
                await _streamingSession.EndAudioAsync(ct);
                await _streamingSession.DisposeAsync();
                _streamingSession = null;
            }

            _isListening = false;
            _virtualSelf.DeactivateSensor(SensorModality.Audio);

            return Result<Unit, string>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to stop listening: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes an audio chunk (push from microphone).
    /// </summary>
    public async Task<Result<Unit, string>> ProcessAudioChunkAsync(
        byte[] audioData,
        bool isFinal = false,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<Unit, string>.Failure("Sensor is disposed");
        if (!_isListening) return Result<Unit, string>.Failure("Not listening");

        try
        {
            var chunk = new AudioChunk(
                audioData,
                _config.SampleRate,
                _config.Channels,
                DateTime.UtcNow,
                isFinal);

            _audioChunks.OnNext(chunk);

            if (_streamingSession != null)
            {
                await _streamingSession.PushAudioAsync(audioData, ct);

                if (isFinal)
                {
                    await _streamingSession.EndAudioAsync(ct);
                }
            }

            return Result<Unit, string>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to process audio: {ex.Message}");
        }
    }

    /// <summary>
    /// Transcribes a complete audio file.
    /// </summary>
    public async Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<TranscriptionResult, string>.Failure("Sensor is disposed");

        var result = await _sttModel.TranscribeAsync(filePath, _config.Language, ct);

        if (result.IsSuccess)
        {
            _transcriptions.OnNext(result.Value);
            _virtualSelf.PublishAudioPerception(
                result.Value.Text,
                result.Value.Confidence,
                result.Value.Language,
                result.Value.EndTime - result.Value.StartTime,
                true);
        }

        return result;
    }

    /// <summary>
    /// Disposes the sensor.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isListening = false;
        _streamingSession?.DisposeAsync().AsTask().Wait();

        _audioChunks.OnCompleted();
        _transcriptions.OnCompleted();
        _voiceActivity.OnCompleted();

        _audioChunks.Dispose();
        _transcriptions.Dispose();
        _voiceActivity.Dispose();
    }
}
