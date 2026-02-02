// <copyright file="EmbodimentController.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Core.EmbodiedInteraction;

using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Ouroboros.Core.Monads;

/// <summary>
/// Unified perception event from any sensor.
/// </summary>
/// <param name="Source">Source sensor ID.</param>
/// <param name="Modality">Sensor modality.</param>
/// <param name="Perception">The perception event.</param>
/// <param name="Timestamp">When perceived.</param>
public sealed record UnifiedPerception(
    string Source,
    SensorModality Modality,
    PerceptionEvent Perception,
    DateTime Timestamp);

/// <summary>
/// Action request to an actuator.
/// </summary>
/// <param name="TargetActuator">Target actuator ID.</param>
/// <param name="Modality">Output modality.</param>
/// <param name="Content">Content to output.</param>
/// <param name="Parameters">Additional parameters.</param>
public sealed record ActionRequest(
    string TargetActuator,
    ActuatorModality Modality,
    object Content,
    IReadOnlyDictionary<string, object>? Parameters = null);

/// <summary>
/// Result of an action.
/// </summary>
/// <param name="Request">The original request.</param>
/// <param name="Success">Whether action succeeded.</param>
/// <param name="Error">Error message if failed.</param>
/// <param name="Duration">How long the action took.</param>
public sealed record ActionResult(
    ActionRequest Request,
    bool Success,
    string? Error = null,
    TimeSpan? Duration = null);

/// <summary>
/// Unit type for void results in Result monad.
/// </summary>
public readonly struct Unit
{
    /// <summary>
    /// The single unit value.
    /// </summary>
    public static readonly Unit Default = new();
}

/// <summary>
/// Controller that orchestrates all sensors, actuators, and the virtual self.
/// Acts as the central nervous system coordinating embodied interaction.
/// </summary>
public sealed class EmbodimentController : IDisposable
{
    private readonly VirtualSelf _virtualSelf;
    private readonly BodySchema _bodySchema;
    private readonly ConcurrentDictionary<string, AudioSensor> _audioSensors = new();
    private readonly ConcurrentDictionary<string, VisualSensor> _visualSensors = new();
    private readonly ConcurrentDictionary<string, VoiceActuator> _voiceActuators = new();
    private readonly Subject<UnifiedPerception> _perceptions = new();
    private readonly Subject<ActionResult> _actionResults = new();
    private readonly List<IDisposable> _subscriptions = [];
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Initializes the embodiment controller.
    /// </summary>
    public EmbodimentController(VirtualSelf virtualSelf, BodySchema bodySchema)
    {
        _virtualSelf = virtualSelf ?? throw new ArgumentNullException(nameof(virtualSelf));
        _bodySchema = bodySchema ?? throw new ArgumentNullException(nameof(bodySchema));
    }

    /// <summary>
    /// Gets the virtual self.
    /// </summary>
    public VirtualSelf VirtualSelf => _virtualSelf;

    /// <summary>
    /// Gets the body schema.
    /// </summary>
    public BodySchema BodySchema => _bodySchema;

    /// <summary>
    /// Gets whether the controller is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Observable of all unified perceptions.
    /// </summary>
    public IObservable<UnifiedPerception> Perceptions => _perceptions.AsObservable();

    /// <summary>
    /// Observable of action results.
    /// </summary>
    public IObservable<ActionResult> ActionResults => _actionResults.AsObservable();

    /// <summary>
    /// Registers an audio sensor.
    /// </summary>
    public EmbodimentController RegisterAudioSensor(string id, AudioSensor sensor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _audioSensors[id] = sensor;

        // Subscribe to sensor transcriptions and convert to unified perceptions
        var sub = sensor.Transcriptions
            .Select(t => new UnifiedPerception(
                id,
                SensorModality.Audio,
                new AudioPerception(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    t.Confidence,
                    t.Text,
                    t.Language,
                    null,
                    t.EndTime - t.StartTime,
                    t.IsFinal),
                DateTime.UtcNow))
            .Subscribe(_perceptions);

        _subscriptions.Add(sub);
        return this;
    }

    /// <summary>
    /// Registers a visual sensor.
    /// </summary>
    public EmbodimentController RegisterVisualSensor(string id, VisualSensor sensor)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _visualSensors[id] = sensor;

        // Subscribe to sensor analysis results and convert to unified perceptions
        var sub = sensor.AnalysisResults
            .Select(v => new UnifiedPerception(
                id,
                SensorModality.Visual,
                new VisualPerception(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    v.Confidence,
                    v.Description,
                    v.Objects,
                    v.Faces,
                    v.SceneType,
                    v.Faces.FirstOrDefault()?.Emotion,
                    null),
                DateTime.UtcNow))
            .Subscribe(_perceptions);

        _subscriptions.Add(sub);
        return this;
    }

    /// <summary>
    /// Registers a voice actuator.
    /// </summary>
    public EmbodimentController RegisterVoiceActuator(string id, VoiceActuator actuator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _voiceActuators[id] = actuator;
        return this;
    }

    /// <summary>
    /// Starts all sensors and begins perception processing.
    /// </summary>
    public async Task<Result<Unit, string>> StartAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<Unit, string>.Failure("Controller is disposed");
        if (_isRunning) return Result<Unit, string>.Failure("Already running");

        try
        {
            // Start all audio sensors
            foreach (var (id, sensor) in _audioSensors)
            {
                var result = await sensor.StartListeningAsync(ct);
                if (!result.IsSuccess)
                    return Result<Unit, string>.Failure($"Failed to start audio sensor {id}: {result.Error}");
            }

            // Start all visual sensors
            foreach (var (id, sensor) in _visualSensors)
            {
                var result = sensor.StartObserving();
                if (!result.IsSuccess)
                    return Result<Unit, string>.Failure($"Failed to start visual sensor {id}: {result.Error}");
            }

            // Subscribe perceptions to virtual self
            var perceptionSub = _perceptions
                .Subscribe(p =>
                {
                    // VirtualSelf has specific methods for each perception type
                    switch (p.Perception)
                    {
                        case AudioPerception audio:
                            _virtualSelf.PublishAudioPerception(
                                audio.TranscribedText,
                                audio.Confidence,
                                audio.DetectedLanguage,
                                audio.Duration,
                                audio.IsFinal);
                            break;

                        case VisualPerception visual:
                            _virtualSelf.PublishVisualPerception(
                                visual.Description,
                                visual.Objects,
                                visual.Faces,
                                visual.SceneType,
                                visual.DominantEmotion,
                                visual.Confidence,
                                visual.RawFrame);
                            break;

                        case TextPerception text:
                            _virtualSelf.PublishTextPerception(
                                text.Text,
                                text.Source,
                                text.Confidence);
                            break;
                    }
                });
            _subscriptions.Add(perceptionSub);

            _isRunning = true;
            _virtualSelf.SetState(EmbodimentState.Awake);

            return Result<Unit, string>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to start: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops all sensors.
    /// </summary>
    public async Task<Result<Unit, string>> StopAsync(CancellationToken ct = default)
    {
        if (!_isRunning) return Result<Unit, string>.Success(Unit.Default);

        try
        {
            _virtualSelf.SetState(EmbodimentState.Dormant);

            foreach (var sensor in _audioSensors.Values)
                await sensor.StopListeningAsync(ct);

            foreach (var sensor in _visualSensors.Values)
                sensor.StopObserving();

            _isRunning = false;
            return Result<Unit, string>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to stop: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes an action through an actuator.
    /// </summary>
    public async Task<ActionResult> ExecuteActionAsync(
        ActionRequest request,
        CancellationToken ct = default)
    {
        if (_disposed)
        {
            return new ActionResult(request, false, "Controller is disposed");
        }

        var start = DateTime.UtcNow;

        try
        {
            switch (request.Modality)
            {
                case ActuatorModality.Voice:
                    return await ExecuteVoiceActionAsync(request, ct);

                case ActuatorModality.Text:
                    // Text output is handled externally
                    return new ActionResult(request, true, Duration: TimeSpan.Zero);

                default:
                    return new ActionResult(request, false, $"Unsupported modality: {request.Modality}");
            }
        }
        catch (Exception ex)
        {
            return new ActionResult(request, false, ex.Message, DateTime.UtcNow - start);
        }
    }

    /// <summary>
    /// Speaks text through a voice actuator.
    /// </summary>
    public async Task<Result<SynthesizedSpeech, string>> SpeakAsync(
        string text,
        string? emotion = null,
        string? actuatorId = null,
        CancellationToken ct = default)
    {
        var actuator = actuatorId != null
            ? _voiceActuators.GetValueOrDefault(actuatorId)
            : _voiceActuators.Values.FirstOrDefault();

        if (actuator == null)
            return Result<SynthesizedSpeech, string>.Failure("No voice actuator available");

        return await actuator.SpeakAsync(text, emotion, ct);
    }

    /// <summary>
    /// Gets a fused perception from all active sensors.
    /// </summary>
    public async Task<Result<FusedPerception, string>> GetFusedPerceptionAsync(
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        if (!_isRunning)
            return Result<FusedPerception, string>.Failure("Controller not running");

        try
        {
            // Wait for fused perception from VirtualSelf
            var fused = await _virtualSelf.FusedPerceptions
                .Timeout(timeout)
                .FirstOrDefaultAsync()
                .ToTask(ct);

            if (fused == null)
                return Result<FusedPerception, string>.Failure("No perceptions available in timeout period");

            return Result<FusedPerception, string>.Success(fused);
        }
        catch (TimeoutException)
        {
            return Result<FusedPerception, string>.Failure("Timeout waiting for perceptions");
        }
        catch (Exception ex)
        {
            return Result<FusedPerception, string>.Failure($"Failed to fuse perceptions: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a text input and generates a response.
    /// </summary>
    public void OnTextInput(string text, string source = "user")
    {
        var perception = new TextPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            1.0,
            text,
            source);

        _virtualSelf.PublishTextPerception(text, source);
        _perceptions.OnNext(new UnifiedPerception(
            source,
            SensorModality.Text,
            perception,
            DateTime.UtcNow));
    }

    private async Task<ActionResult> ExecuteVoiceActionAsync(
        ActionRequest request,
        CancellationToken ct)
    {
        var start = DateTime.UtcNow;

        var actuator = _voiceActuators.GetValueOrDefault(request.TargetActuator)
            ?? _voiceActuators.Values.FirstOrDefault();

        if (actuator == null)
            return new ActionResult(request, false, "No voice actuator found");

        var text = request.Content?.ToString() ?? string.Empty;
        var emotion = request.Parameters?.GetValueOrDefault("emotion")?.ToString();

        var result = await actuator.SpeakAsync(text, emotion, ct);

        return new ActionResult(
            request,
            result.IsSuccess,
            result.IsSuccess ? null : result.Error,
            DateTime.UtcNow - start);
    }

    /// <summary>
    /// Disposes all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var sub in _subscriptions)
            sub.Dispose();

        foreach (var sensor in _audioSensors.Values)
            sensor.Dispose();

        foreach (var sensor in _visualSensors.Values)
            sensor.Dispose();

        foreach (var actuator in _voiceActuators.Values)
            actuator.Dispose();

        _perceptions.OnCompleted();
        _actionResults.OnCompleted();

        _perceptions.Dispose();
        _actionResults.Dispose();
    }
}
