// <copyright file="VoiceActuator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Core.EmbodiedInteraction;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ouroboros.Core.Monads;

/// <summary>
/// Voice/TTS configuration.
/// </summary>
/// <param name="Voice">Voice name/ID.</param>
/// <param name="Speed">Speech speed (0.5-2.0, 1.0 = normal).</param>
/// <param name="Pitch">Voice pitch adjustment.</param>
/// <param name="Volume">Output volume (0.0-1.0).</param>
/// <param name="Language">Language code.</param>
/// <param name="Style">Speech style (neutral, cheerful, sad, etc.).</param>
/// <param name="EnableSSML">Enable SSML markup support.</param>
public sealed record VoiceConfig(
    string Voice = "default",
    double Speed = 1.0,
    double Pitch = 1.0,
    double Volume = 1.0,
    string Language = "en-US",
    string Style = "neutral",
    bool EnableSSML = false);

/// <summary>
/// Synthesized speech output.
/// </summary>
/// <param name="Text">The text that was synthesized.</param>
/// <param name="AudioData">The audio data.</param>
/// <param name="Format">Audio format (wav, mp3, opus).</param>
/// <param name="SampleRate">Sample rate.</param>
/// <param name="Duration">Speech duration.</param>
/// <param name="Timestamp">When synthesized.</param>
public sealed record SynthesizedSpeech(
    string Text,
    byte[] AudioData,
    string Format,
    int SampleRate,
    TimeSpan Duration,
    DateTime Timestamp);

/// <summary>
/// Speech synthesis request.
/// </summary>
/// <param name="Text">Text to synthesize.</param>
/// <param name="Priority">Priority (higher = more urgent).</param>
/// <param name="Emotion">Optional emotion to convey.</param>
/// <param name="Interruptible">Can this be interrupted by barge-in.</param>
public sealed record SpeechRequest(
    string Text,
    int Priority = 0,
    string? Emotion = null,
    bool Interruptible = true);

/// <summary>
/// Interface for Text-to-Speech models.
/// </summary>
public interface ITtsModel
{
    /// <summary>
    /// Gets the model name/identifier.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Gets available voices.
    /// </summary>
    Task<Result<IReadOnlyList<VoiceInfo>, string>> GetVoicesAsync(
        string? language = null,
        CancellationToken ct = default);

    /// <summary>
    /// Synthesizes speech from text.
    /// </summary>
    Task<Result<SynthesizedSpeech, string>> SynthesizeAsync(
        string text,
        VoiceConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Synthesizes speech with streaming output.
    /// </summary>
    IObservable<byte[]> SynthesizeStreaming(
        string text,
        VoiceConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets whether this model supports streaming output.
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Gets whether this model supports emotional expression.
    /// </summary>
    bool SupportsEmotions { get; }
}

/// <summary>
/// Information about an available voice.
/// </summary>
/// <param name="Id">Voice identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Language">Language code.</param>
/// <param name="Gender">Voice gender.</param>
/// <param name="SupportedStyles">Supported speech styles.</param>
public sealed record VoiceInfo(
    string Id,
    string Name,
    string Language,
    string? Gender,
    IReadOnlyList<string>? SupportedStyles);

/// <summary>
/// Voice actuator that generates speech output via TTS.
/// </summary>
public sealed class VoiceActuator : IDisposable
{
    private readonly ITtsModel _ttsModel;
    private readonly VirtualSelf _virtualSelf;
    private readonly Subject<SynthesizedSpeech> _speechOutput = new();
    private readonly Subject<SpeechRequest> _speechQueue = new();
    private VoiceConfig _config;
    private bool _isSpeaking;
    private CancellationTokenSource? _speakingCts;
    private bool _disposed;

    /// <summary>
    /// Initializes a new voice actuator.
    /// </summary>
    /// <param name="ttsModel">The TTS model for synthesis.</param>
    /// <param name="virtualSelf">The virtual self.</param>
    /// <param name="config">Voice configuration.</param>
    public VoiceActuator(
        ITtsModel ttsModel,
        VirtualSelf virtualSelf,
        VoiceConfig? config = null)
    {
        _ttsModel = ttsModel ?? throw new ArgumentNullException(nameof(ttsModel));
        _virtualSelf = virtualSelf ?? throw new ArgumentNullException(nameof(virtualSelf));
        _config = config ?? new VoiceConfig();
    }

    /// <summary>
    /// Gets the TTS model name.
    /// </summary>
    public string ModelName => _ttsModel.ModelName;

    /// <summary>
    /// Gets whether currently speaking.
    /// </summary>
    public bool IsSpeaking => _isSpeaking;

    /// <summary>
    /// Gets the current voice configuration.
    /// </summary>
    public VoiceConfig Config => _config;

    /// <summary>
    /// Observable stream of synthesized speech.
    /// </summary>
    public IObservable<SynthesizedSpeech> SpeechOutput => _speechOutput.AsObservable();

    /// <summary>
    /// Sets the voice configuration.
    /// </summary>
    public void Configure(VoiceConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Sets speech speed.
    /// </summary>
    public void SetSpeed(double speed)
    {
        _config = _config with { Speed = Math.Clamp(speed, 0.5, 2.0) };
    }

    /// <summary>
    /// Sets speech style/emotion.
    /// </summary>
    public void SetStyle(string style)
    {
        _config = _config with { Style = style };
    }

    /// <summary>
    /// Speaks the given text.
    /// </summary>
    public async Task<Result<SynthesizedSpeech, string>> SpeakAsync(
        string text,
        string? emotion = null,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<SynthesizedSpeech, string>.Failure("Actuator is disposed");
        if (string.IsNullOrWhiteSpace(text)) 
            return Result<SynthesizedSpeech, string>.Failure("Text cannot be empty");

        // Set speaking state
        _isSpeaking = true;
        _speakingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _virtualSelf.SetState(EmbodimentState.Speaking);

        try
        {
            var config = emotion != null 
                ? _config with { Style = emotion } 
                : _config;

            var result = await _ttsModel.SynthesizeAsync(text, config, _speakingCts.Token);

            if (result.IsSuccess)
            {
                _speechOutput.OnNext(result.Value);
            }

            return result;
        }
        finally
        {
            _isSpeaking = false;
            _speakingCts?.Dispose();
            _speakingCts = null;
            _virtualSelf.SetState(EmbodimentState.Awake);
        }
    }

    /// <summary>
    /// Speaks with streaming output for lower latency.
    /// </summary>
    public IObservable<byte[]> SpeakStreaming(
        string text,
        string? emotion = null,
        CancellationToken ct = default)
    {
        if (_disposed || !_ttsModel.SupportsStreaming)
        {
            return Observable.Empty<byte[]>();
        }

        var config = emotion != null 
            ? _config with { Style = emotion } 
            : _config;

        _isSpeaking = true;
        _virtualSelf.SetState(EmbodimentState.Speaking);

        return _ttsModel.SynthesizeStreaming(text, config, ct)
            .Finally(() =>
            {
                _isSpeaking = false;
                _virtualSelf.SetState(EmbodimentState.Awake);
            });
    }

    /// <summary>
    /// Interrupts current speech (barge-in).
    /// </summary>
    public void Interrupt()
    {
        if (!_isSpeaking) return;

        _speakingCts?.Cancel();
        _isSpeaking = false;
        _virtualSelf.SetState(EmbodimentState.Awake);
    }

    /// <summary>
    /// Gets available voices.
    /// </summary>
    public Task<Result<IReadOnlyList<VoiceInfo>, string>> GetVoicesAsync(
        string? language = null,
        CancellationToken ct = default)
    {
        return _ttsModel.GetVoicesAsync(language, ct);
    }

    /// <summary>
    /// Sets the voice by ID.
    /// </summary>
    public void SetVoice(string voiceId)
    {
        _config = _config with { Voice = voiceId };
    }

    /// <summary>
    /// Disposes the actuator.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _speakingCts?.Cancel();
        _speakingCts?.Dispose();

        _speechOutput.OnCompleted();
        _speechQueue.OnCompleted();

        _speechOutput.Dispose();
        _speechQueue.Dispose();
    }
}
