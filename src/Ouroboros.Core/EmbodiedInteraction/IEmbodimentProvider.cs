// <copyright file="IEmbodimentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Core.EmbodiedInteraction;

/// <summary>
/// Repository-like abstraction for embodiment state and capabilities.
/// Provides a general interface for different embodiment backends
/// (Tapo cameras, virtual sensors, robotic systems, etc.).
/// </summary>
public interface IEmbodimentProvider : IDisposable
{
    /// <summary>
    /// Gets the unique identifier of this embodiment provider.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Gets the human-readable name of the provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets whether the provider is currently connected and operational.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the embodiment backend and initializes resources.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<EmbodimentCapabilities>> ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnects from the embodiment backend and releases resources.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit>> DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current state of the embodiment.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the current embodiment state.</returns>
    Task<Result<EmbodimentState>> GetStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the available sensors from this provider.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the list of available sensors.</returns>
    Task<Result<IReadOnlyList<SensorInfo>>> GetSensorsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the available actuators from this provider.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the list of available actuators.</returns>
    Task<Result<IReadOnlyList<ActuatorInfo>>> GetActuatorsAsync(CancellationToken ct = default);

    /// <summary>
    /// Activates a sensor by ID.
    /// </summary>
    /// <param name="sensorId">The sensor identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit>> ActivateSensorAsync(string sensorId, CancellationToken ct = default);

    /// <summary>
    /// Deactivates a sensor by ID.
    /// </summary>
    /// <param name="sensorId">The sensor identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit>> DeactivateSensorAsync(string sensorId, CancellationToken ct = default);

    /// <summary>
    /// Reads perception data from a sensor.
    /// </summary>
    /// <param name="sensorId">The sensor identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the perception data.</returns>
    Task<Result<PerceptionData>> ReadSensorAsync(string sensorId, CancellationToken ct = default);

    /// <summary>
    /// Executes an action through an actuator.
    /// </summary>
    /// <param name="actuatorId">The actuator identifier.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the action outcome.</returns>
    Task<Result<ActionOutcome>> ExecuteActionAsync(
        string actuatorId,
        ActuatorAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Observable stream of perception events from all active sensors.
    /// </summary>
    IObservable<PerceptionData> Perceptions { get; }

    /// <summary>
    /// Observable stream of provider state changes.
    /// </summary>
    IObservable<EmbodimentProviderEvent> Events { get; }
}

/// <summary>
/// Capabilities supported by an embodiment provider.
/// </summary>
[Flags]
public enum EmbodimentCapabilities
{
    /// <summary>No special capabilities.</summary>
    None = 0,

    /// <summary>Can capture video frames.</summary>
    VideoCapture = 1 << 0,

    /// <summary>Can capture audio.</summary>
    AudioCapture = 1 << 1,

    /// <summary>Can output audio/speech.</summary>
    AudioOutput = 1 << 2,

    /// <summary>Can perform vision analysis.</summary>
    VisionAnalysis = 1 << 3,

    /// <summary>Can detect motion.</summary>
    MotionDetection = 1 << 4,

    /// <summary>Can control lighting.</summary>
    LightingControl = 1 << 5,

    /// <summary>Can control power (plugs/switches).</summary>
    PowerControl = 1 << 6,

    /// <summary>Can perform pan/tilt/zoom.</summary>
    PTZControl = 1 << 7,

    /// <summary>Supports two-way audio communication.</summary>
    TwoWayAudio = 1 << 8,

    /// <summary>Supports streaming video.</summary>
    VideoStreaming = 1 << 9
}

/// <summary>
/// Information about a sensor from the provider.
/// </summary>
/// <param name="SensorId">Unique sensor identifier.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Modality">Sensor modality type.</param>
/// <param name="IsActive">Whether the sensor is currently active.</param>
/// <param name="Capabilities">Capabilities this sensor provides.</param>
/// <param name="Properties">Additional sensor properties.</param>
public sealed record SensorInfo(
    string SensorId,
    string Name,
    SensorModality Modality,
    bool IsActive,
    EmbodimentCapabilities Capabilities,
    IReadOnlyDictionary<string, object>? Properties = null);

/// <summary>
/// Information about an actuator from the provider.
/// </summary>
/// <param name="ActuatorId">Unique actuator identifier.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Modality">Actuator modality type.</param>
/// <param name="IsActive">Whether the actuator is currently active.</param>
/// <param name="Capabilities">Capabilities this actuator provides.</param>
/// <param name="SupportedActions">List of supported action types.</param>
/// <param name="Properties">Additional actuator properties.</param>
public sealed record ActuatorInfo(
    string ActuatorId,
    string Name,
    ActuatorModality Modality,
    bool IsActive,
    EmbodimentCapabilities Capabilities,
    IReadOnlyList<string> SupportedActions,
    IReadOnlyDictionary<string, object>? Properties = null);

/// <summary>
/// Perception data from a sensor.
/// </summary>
/// <param name="SensorId">Source sensor identifier.</param>
/// <param name="Modality">Perception modality.</param>
/// <param name="Timestamp">When the perception occurred.</param>
/// <param name="Data">Raw perception data.</param>
/// <param name="Metadata">Additional metadata.</param>
public sealed record PerceptionData(
    string SensorId,
    SensorModality Modality,
    DateTime Timestamp,
    object Data,
    IReadOnlyDictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Gets the data as a specific type.
    /// </summary>
    public T? GetDataAs<T>() where T : class => Data as T;

    /// <summary>
    /// Gets the data as bytes (for raw frames/audio).
    /// </summary>
    public byte[]? GetBytes() => Data as byte[];
}

/// <summary>
/// An action to execute through an actuator.
/// </summary>
/// <param name="ActionType">Type of action (e.g., "speak", "turn_on", "set_color").</param>
/// <param name="Parameters">Action parameters.</param>
public sealed record ActuatorAction(
    string ActionType,
    IReadOnlyDictionary<string, object>? Parameters = null)
{
    /// <summary>
    /// Creates a speak action.
    /// </summary>
    public static ActuatorAction Speak(string text, string? emotion = null) =>
        new("speak", new Dictionary<string, object>
        {
            ["text"] = text,
            ["emotion"] = emotion ?? "neutral"
        });

    /// <summary>
    /// Creates a power on action.
    /// </summary>
    public static ActuatorAction TurnOn() => new("turn_on");

    /// <summary>
    /// Creates a power off action.
    /// </summary>
    public static ActuatorAction TurnOff() => new("turn_off");

    /// <summary>
    /// Creates a set color action.
    /// </summary>
    public static ActuatorAction SetColor(byte r, byte g, byte b) =>
        new("set_color", new Dictionary<string, object>
        {
            ["red"] = r,
            ["green"] = g,
            ["blue"] = b
        });
}

/// <summary>
/// Outcome of an actuator action.
/// </summary>
/// <param name="ActuatorId">Actuator that executed the action.</param>
/// <param name="ActionType">Type of action executed.</param>
/// <param name="Success">Whether the action succeeded.</param>
/// <param name="Duration">How long the action took.</param>
/// <param name="Result">Result data if any.</param>
/// <param name="Error">Error message if failed.</param>
public sealed record ActionOutcome(
    string ActuatorId,
    string ActionType,
    bool Success,
    TimeSpan Duration,
    object? Result = null,
    string? Error = null);

/// <summary>
/// Event from an embodiment provider.
/// </summary>
/// <param name="EventType">Type of event.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="Details">Event details.</param>
public sealed record EmbodimentProviderEvent(
    EmbodimentProviderEventType EventType,
    DateTime Timestamp,
    IReadOnlyDictionary<string, object>? Details = null);

/// <summary>
/// Types of embodiment provider events.
/// </summary>
public enum EmbodimentProviderEventType
{
    /// <summary>Provider connected.</summary>
    Connected,

    /// <summary>Provider disconnected.</summary>
    Disconnected,

    /// <summary>Sensor activated.</summary>
    SensorActivated,

    /// <summary>Sensor deactivated.</summary>
    SensorDeactivated,

    /// <summary>Error occurred.</summary>
    Error,

    /// <summary>State changed.</summary>
    StateChanged,

    /// <summary>Motion detected.</summary>
    MotionDetected,

    /// <summary>Person detected.</summary>
    PersonDetected
}
