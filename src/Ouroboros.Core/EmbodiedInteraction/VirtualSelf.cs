// <copyright file="VirtualSelf.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Core.EmbodiedInteraction;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents the agent's virtual embodiment state.
/// </summary>
public enum EmbodimentState
{
    /// <summary>Not active, no sensors running.</summary>
    Dormant,

    /// <summary>Sensors active, ready for interaction.</summary>
    Awake,

    /// <summary>Actively listening for audio input.</summary>
    Listening,

    /// <summary>Processing visual input.</summary>
    Observing,

    /// <summary>Generating speech output.</summary>
    Speaking,

    /// <summary>Processing/thinking.</summary>
    Processing,

    /// <summary>Multiple modalities active.</summary>
    FullyEngaged,
}

/// <summary>
/// Represents the virtual body schema - the agent's model of its own embodiment.
/// </summary>
/// <param name="Id">Unique identifier for this virtual self instance.</param>
/// <param name="Name">Display name/persona.</param>
/// <param name="State">Current embodiment state.</param>
/// <param name="Capabilities">Available sensory/motor capabilities.</param>
/// <param name="ActiveSensors">Currently active sensor modalities.</param>
/// <param name="ActiveActuators">Currently active output modalities.</param>
/// <param name="AttentionFocus">Current focus of attention.</param>
/// <param name="EnergyLevel">Simulated energy/resource level (0-1).</param>
/// <param name="CreatedAt">When this virtual self was instantiated.</param>
/// <param name="LastActiveAt">Last activity timestamp.</param>
public sealed record VirtualSelfState(
    Guid Id,
    string Name,
    EmbodimentState State,
    IReadOnlySet<string> Capabilities,
    IReadOnlySet<SensorModality> ActiveSensors,
    IReadOnlySet<ActuatorModality> ActiveActuators,
    AttentionFocus? AttentionFocus,
    double EnergyLevel,
    DateTime CreatedAt,
    DateTime LastActiveAt)
{
    /// <summary>
    /// Creates a default virtual self state.
    /// </summary>
    public static VirtualSelfState CreateDefault(string name = "Ouroboros") =>
        new(
            Guid.NewGuid(),
            name,
            EmbodimentState.Dormant,
            new HashSet<string> { "audio_input", "audio_output", "visual_input", "text_input", "text_output" },
            new HashSet<SensorModality>(),
            new HashSet<ActuatorModality>(),
            null,
            1.0,
            DateTime.UtcNow,
            DateTime.UtcNow);

    /// <summary>
    /// Returns a new state with updated modality.
    /// </summary>
    public VirtualSelfState WithState(EmbodimentState newState) =>
        this with { State = newState, LastActiveAt = DateTime.UtcNow };

    /// <summary>
    /// Returns a new state with sensor activated.
    /// </summary>
    public VirtualSelfState WithSensorActive(SensorModality sensor)
    {
        var newSensors = new HashSet<SensorModality>(ActiveSensors) { sensor };
        return this with { ActiveSensors = newSensors, LastActiveAt = DateTime.UtcNow };
    }

    /// <summary>
    /// Returns a new state with sensor deactivated.
    /// </summary>
    public VirtualSelfState WithSensorInactive(SensorModality sensor)
    {
        var newSensors = new HashSet<SensorModality>(ActiveSensors);
        newSensors.Remove(sensor);
        return this with { ActiveSensors = newSensors, LastActiveAt = DateTime.UtcNow };
    }

    /// <summary>
    /// Returns a new state with attention focus updated.
    /// </summary>
    public VirtualSelfState WithAttention(AttentionFocus focus) =>
        this with { AttentionFocus = focus, LastActiveAt = DateTime.UtcNow };

    /// <summary>
    /// Gets whether the agent is currently perceiving.
    /// </summary>
    public bool IsPerceiving => ActiveSensors.Count > 0;

    /// <summary>
    /// Gets whether the agent can hear.
    /// </summary>
    public bool CanHear => Capabilities.Contains("audio_input");

    /// <summary>
    /// Gets whether the agent can see.
    /// </summary>
    public bool CanSee => Capabilities.Contains("visual_input");

    /// <summary>
    /// Gets whether the agent can speak.
    /// </summary>
    public bool CanSpeak => Capabilities.Contains("audio_output");
}

/// <summary>
/// Sensor modality types.
/// </summary>
public enum SensorModality
{
    /// <summary>Microphone/audio input.</summary>
    Audio,

    /// <summary>Camera/video input.</summary>
    Visual,

    /// <summary>Text/keyboard input.</summary>
    Text,

    /// <summary>Touch/haptic input.</summary>
    Haptic,

    /// <summary>Proprioceptive (internal state) sensing.</summary>
    Proprioceptive,
}

/// <summary>
/// Actuator modality types.
/// </summary>
public enum ActuatorModality
{
    /// <summary>Speech/audio output.</summary>
    Voice,

    /// <summary>Text output.</summary>
    Text,

    /// <summary>Visual output (avatar, display).</summary>
    Visual,

    /// <summary>Motor/movement output.</summary>
    Motor,
}

/// <summary>
/// Represents the current focus of attention.
/// </summary>
/// <param name="Modality">Primary modality of attention.</param>
/// <param name="Target">What the attention is focused on.</param>
/// <param name="Intensity">How intensely focused (0-1).</param>
/// <param name="StartedAt">When attention was directed here.</param>
public sealed record AttentionFocus(
    SensorModality Modality,
    string Target,
    double Intensity,
    DateTime StartedAt)
{
    /// <summary>
    /// Duration of current attention focus.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - StartedAt;
}

/// <summary>
/// Multimodal perception event from any sensor.
/// </summary>
public abstract record PerceptionEvent(
    Guid Id,
    SensorModality Modality,
    DateTime Timestamp,
    double Confidence);

/// <summary>
/// Audio perception from microphone.
/// </summary>
public sealed record AudioPerception(
    Guid Id,
    DateTime Timestamp,
    double Confidence,
    string TranscribedText,
    string? DetectedLanguage,
    double? SpeakerEmbedding,
    TimeSpan Duration,
    bool IsFinal) : PerceptionEvent(Id, SensorModality.Audio, Timestamp, Confidence);

/// <summary>
/// Visual perception from camera/video.
/// </summary>
public sealed record VisualPerception(
    Guid Id,
    DateTime Timestamp,
    double Confidence,
    string Description,
    IReadOnlyList<DetectedObject> Objects,
    IReadOnlyList<DetectedFace> Faces,
    string? SceneType,
    string? DominantEmotion,
    byte[]? RawFrame) : PerceptionEvent(Id, SensorModality.Visual, Timestamp, Confidence);

/// <summary>
/// Text input perception.
/// </summary>
public sealed record TextPerception(
    Guid Id,
    DateTime Timestamp,
    double Confidence,
    string Text,
    string? Source) : PerceptionEvent(Id, SensorModality.Text, Timestamp, Confidence);

/// <summary>
/// Detected object in visual perception.
/// </summary>
/// <param name="Label">Object class label.</param>
/// <param name="Confidence">Detection confidence.</param>
/// <param name="BoundingBox">Bounding box (x, y, width, height) normalized 0-1.</param>
/// <param name="Attributes">Additional attributes.</param>
public sealed record DetectedObject(
    string Label,
    double Confidence,
    (double X, double Y, double Width, double Height) BoundingBox,
    IReadOnlyDictionary<string, string>? Attributes);

/// <summary>
/// Detected face in visual perception.
/// </summary>
/// <param name="FaceId">Tracking ID for the face.</param>
/// <param name="Confidence">Detection confidence.</param>
/// <param name="BoundingBox">Face bounding box.</param>
/// <param name="Emotion">Detected emotion if available.</param>
/// <param name="Age">Estimated age if available.</param>
/// <param name="IsKnown">Whether this is a recognized person.</param>
/// <param name="PersonId">ID of recognized person if known.</param>
public sealed record DetectedFace(
    string FaceId,
    double Confidence,
    (double X, double Y, double Width, double Height) BoundingBox,
    string? Emotion,
    int? Age,
    bool IsKnown,
    string? PersonId);

/// <summary>
/// Fused multimodal perception combining multiple sensor inputs.
/// </summary>
/// <param name="Id">Unique ID.</param>
/// <param name="Timestamp">When fusion occurred.</param>
/// <param name="AudioPerceptions">Audio inputs in this window.</param>
/// <param name="VisualPerceptions">Visual inputs in this window.</param>
/// <param name="TextPerceptions">Text inputs in this window.</param>
/// <param name="IntegratedUnderstanding">Combined understanding from all modalities.</param>
/// <param name="Confidence">Overall confidence.</param>
public sealed record FusedPerception(
    Guid Id,
    DateTime Timestamp,
    IReadOnlyList<AudioPerception> AudioPerceptions,
    IReadOnlyList<VisualPerception> VisualPerceptions,
    IReadOnlyList<TextPerception> TextPerceptions,
    string IntegratedUnderstanding,
    double Confidence)
{
    /// <summary>
    /// Gets whether this perception includes audio.
    /// </summary>
    public bool HasAudio => AudioPerceptions.Count > 0;

    /// <summary>
    /// Gets whether this perception includes visual.
    /// </summary>
    public bool HasVisual => VisualPerceptions.Count > 0;

    /// <summary>
    /// Gets the combined transcript from audio.
    /// </summary>
    public string CombinedTranscript =>
        string.Join(" ", AudioPerceptions.Where(a => a.IsFinal).Select(a => a.TranscribedText));

    /// <summary>
    /// Gets dominant modality by count.
    /// </summary>
    public SensorModality DominantModality
    {
        get
        {
            var counts = new[]
            {
                (SensorModality.Audio, AudioPerceptions.Count),
                (SensorModality.Visual, VisualPerceptions.Count),
                (SensorModality.Text, TextPerceptions.Count),
            };
            return counts.OrderByDescending(c => c.Item2).First().Item1;
        }
    }
}

/// <summary>
/// The Virtual Self - an embodied agent with multimodal perception and action.
/// Integrates microphone (STT), camera (vision), and voice (TTS) capabilities.
/// </summary>
public sealed class VirtualSelf : IDisposable
{
    private readonly BehaviorSubject<VirtualSelfState> _stateSubject;
    private readonly Subject<PerceptionEvent> _perceptionSubject = new();
    private readonly Subject<FusedPerception> _fusedPerceptionSubject = new();
    private readonly List<PerceptionEvent> _perceptionBuffer = new();
    private readonly TimeSpan _fusionWindow;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new Virtual Self.
    /// </summary>
    /// <param name="name">Persona name.</param>
    /// <param name="fusionWindowMs">Time window for fusing perceptions (ms).</param>
    public VirtualSelf(string name = "Ouroboros", int fusionWindowMs = 500)
    {
        _stateSubject = new BehaviorSubject<VirtualSelfState>(VirtualSelfState.CreateDefault(name));
        _fusionWindow = TimeSpan.FromMilliseconds(fusionWindowMs);

        // Set up perception fusion pipeline
        SetupFusionPipeline();
    }

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public VirtualSelfState CurrentState => _stateSubject.Value;

    /// <summary>
    /// Observable stream of state changes.
    /// </summary>
    public IObservable<VirtualSelfState> State => _stateSubject.AsObservable();

    /// <summary>
    /// Observable stream of raw perceptions.
    /// </summary>
    public IObservable<PerceptionEvent> Perceptions => _perceptionSubject.AsObservable();

    /// <summary>
    /// Observable stream of fused multimodal perceptions.
    /// </summary>
    public IObservable<FusedPerception> FusedPerceptions => _fusedPerceptionSubject.AsObservable();

    /// <summary>
    /// Activates a sensor modality.
    /// </summary>
    public Result<VirtualSelfState, string> ActivateSensor(SensorModality modality)
    {
        if (_disposed) return Result<VirtualSelfState, string>.Failure("Virtual self is disposed");

        var newState = _stateSubject.Value.WithSensorActive(modality);
        _stateSubject.OnNext(newState);
        return Result<VirtualSelfState, string>.Success(newState);
    }

    /// <summary>
    /// Deactivates a sensor modality.
    /// </summary>
    public Result<VirtualSelfState, string> DeactivateSensor(SensorModality modality)
    {
        if (_disposed) return Result<VirtualSelfState, string>.Failure("Virtual self is disposed");

        var newState = _stateSubject.Value.WithSensorInactive(modality);
        _stateSubject.OnNext(newState);
        return Result<VirtualSelfState, string>.Success(newState);
    }

    /// <summary>
    /// Sets the embodiment state.
    /// </summary>
    public void SetState(EmbodimentState state)
    {
        if (_disposed) return;
        _stateSubject.OnNext(_stateSubject.Value.WithState(state));
    }

    /// <summary>
    /// Focuses attention on a target.
    /// </summary>
    public void FocusAttention(SensorModality modality, string target, double intensity = 1.0)
    {
        if (_disposed) return;
        var focus = new AttentionFocus(modality, target, intensity, DateTime.UtcNow);
        _stateSubject.OnNext(_stateSubject.Value.WithAttention(focus));
    }

    /// <summary>
    /// Publishes an audio perception (from STT).
    /// </summary>
    public void PublishAudioPerception(
        string transcribedText,
        double confidence = 1.0,
        string? language = null,
        TimeSpan? duration = null,
        bool isFinal = true)
    {
        if (_disposed) return;

        var perception = new AudioPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            confidence,
            transcribedText,
            language,
            null,
            duration ?? TimeSpan.Zero,
            isFinal);

        PublishPerception(perception);
    }

    /// <summary>
    /// Publishes a visual perception (from vision model).
    /// </summary>
    public void PublishVisualPerception(
        string description,
        IReadOnlyList<DetectedObject>? objects = null,
        IReadOnlyList<DetectedFace>? faces = null,
        string? sceneType = null,
        string? emotion = null,
        double confidence = 1.0,
        byte[]? rawFrame = null)
    {
        if (_disposed) return;

        var perception = new VisualPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            confidence,
            description,
            objects ?? Array.Empty<DetectedObject>(),
            faces ?? Array.Empty<DetectedFace>(),
            sceneType,
            emotion,
            rawFrame);

        PublishPerception(perception);
    }

    /// <summary>
    /// Publishes a text perception.
    /// </summary>
    public void PublishTextPerception(string text, string? source = null, double confidence = 1.0)
    {
        if (_disposed) return;

        var perception = new TextPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            confidence,
            text,
            source);

        PublishPerception(perception);
    }

    private void PublishPerception(PerceptionEvent perception)
    {
        lock (_lock)
        {
            _perceptionBuffer.Add(perception);
        }

        _perceptionSubject.OnNext(perception);
    }

    private void SetupFusionPipeline()
    {
        // Buffer and fuse perceptions at regular intervals
        Observable.Interval(_fusionWindow)
            .Subscribe(_ => FusePerceptions());
    }

    private void FusePerceptions()
    {
        List<PerceptionEvent> toFuse;
        lock (_lock)
        {
            if (_perceptionBuffer.Count == 0) return;

            var cutoff = DateTime.UtcNow - _fusionWindow;
            toFuse = _perceptionBuffer.Where(p => p.Timestamp >= cutoff).ToList();
            _perceptionBuffer.Clear();
        }

        if (toFuse.Count == 0) return;

        var audioPerceptions = toFuse.OfType<AudioPerception>().ToList();
        var visualPerceptions = toFuse.OfType<VisualPerception>().ToList();
        var textPerceptions = toFuse.OfType<TextPerception>().ToList();

        // Build integrated understanding
        var understanding = BuildIntegratedUnderstanding(audioPerceptions, visualPerceptions, textPerceptions);
        var avgConfidence = toFuse.Average(p => p.Confidence);

        var fused = new FusedPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            audioPerceptions,
            visualPerceptions,
            textPerceptions,
            understanding,
            avgConfidence);

        _fusedPerceptionSubject.OnNext(fused);
    }

    private static string BuildIntegratedUnderstanding(
        IReadOnlyList<AudioPerception> audio,
        IReadOnlyList<VisualPerception> visual,
        IReadOnlyList<TextPerception> text)
    {
        var parts = new List<string>();

        if (audio.Count > 0)
        {
            var transcript = string.Join(" ", audio.Where(a => a.IsFinal).Select(a => a.TranscribedText));
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                parts.Add($"[Heard] {transcript}");
            }
        }

        if (visual.Count > 0)
        {
            var desc = visual.Last().Description;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                parts.Add($"[Saw] {desc}");
            }

            var objects = visual.SelectMany(v => v.Objects).Select(o => o.Label).Distinct().ToList();
            if (objects.Count > 0)
            {
                parts.Add($"[Objects] {string.Join(", ", objects)}");
            }

            var faces = visual.SelectMany(v => v.Faces).ToList();
            if (faces.Count > 0)
            {
                parts.Add($"[Faces] {faces.Count} detected");
            }
        }

        if (text.Count > 0)
        {
            var combined = string.Join(" ", text.Select(t => t.Text));
            parts.Add($"[Read] {combined}");
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : "[No perceptions]";
    }

    /// <summary>
    /// Disposes the virtual self.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _perceptionSubject.OnCompleted();
        _fusedPerceptionSubject.OnCompleted();
        _stateSubject.OnCompleted();

        _perceptionSubject.Dispose();
        _fusedPerceptionSubject.Dispose();
        _stateSubject.Dispose();
    }
}
