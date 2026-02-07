// <copyright file="TapoEmbodimentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Core.Monads;

// Unit types:
// - Ouroboros.Core.EmbodiedInteraction.Unit (EmbodimentUnit.Default) - used by IEmbodimentProvider interface
// - Ouroboros.Core.Learning.Unit (Unit.Value) - used by Tapo device operations
using EmbodimentUnit = Ouroboros.Core.EmbodiedInteraction.Unit;
using TapoUnit = Ouroboros.Core.Learning.Unit;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Embodiment provider that uses Tapo smart devices as the state source.
/// Implements the repository-like IEmbodimentProvider interface, allowing
/// the Tapo REST API to serve as the persistence/state layer for embodiment.
/// Supports direct RTSP camera connections for C200/C210/etc. cameras.
/// </summary>
public sealed class TapoEmbodimentProvider : IEmbodimentProvider
{
    private readonly TapoRestClient? _tapoClient;
    private readonly ITapoRtspClientFactory? _rtspClientFactory;
    private readonly IVisionModel? _visionModel;
    private readonly ITtsModel? _ttsModel;
    private readonly ILogger<TapoEmbodimentProvider>? _logger;
    private readonly TapoVisionModelConfig _visionConfig;
    private readonly string? _username;
    private readonly string? _password;

    private readonly Subject<PerceptionData> _perceptions = new();
    private readonly Subject<EmbodimentProviderEvent> _events = new();
    private readonly Dictionary<string, SensorInfo> _sensors = new();
    private readonly Dictionary<string, ActuatorInfo> _actuators = new();
    private readonly Dictionary<string, bool> _activeSensors = new();
    private readonly Dictionary<string, TapoCameraPtzClient> _ptzClients = new();

    private bool _isConnected;
    private bool _disposed;
    private string? _sessionId;
    private EmbodimentCapabilities _capabilities;

    /// <summary>
    /// Initializes a new TapoEmbodimentProvider with REST client.
    /// </summary>
    /// <param name="tapoClient">The Tapo REST API client (state source).</param>
    /// <param name="providerId">Unique provider identifier.</param>
    /// <param name="visionModel">Optional vision model for frame analysis.</param>
    /// <param name="ttsModel">Optional TTS model for voice output.</param>
    /// <param name="visionConfig">Vision model configuration.</param>
    /// <param name="logger">Optional logger.</param>
    public TapoEmbodimentProvider(
        TapoRestClient tapoClient,
        string providerId = "tapo",
        IVisionModel? visionModel = null,
        ITtsModel? ttsModel = null,
        TapoVisionModelConfig? visionConfig = null,
        ILogger<TapoEmbodimentProvider>? logger = null)
    {
        _tapoClient = tapoClient ?? throw new ArgumentNullException(nameof(tapoClient));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        _visionModel = visionModel;
        _ttsModel = ttsModel;
        _visionConfig = visionConfig ?? TapoVisionModelConfig.CreateDefault();
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new TapoEmbodimentProvider with RTSP camera support.
    /// </summary>
    /// <param name="rtspClientFactory">Factory for creating RTSP clients for cameras.</param>
    /// <param name="providerId">Unique provider identifier.</param>
    /// <param name="visionModel">Optional vision model for frame analysis.</param>
    /// <param name="ttsModel">Optional TTS model for voice output.</param>
    /// <param name="visionConfig">Vision model configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="username">Camera account username for PTZ control.</param>
    /// <param name="password">Camera account password for PTZ control.</param>
    public TapoEmbodimentProvider(
        ITapoRtspClientFactory rtspClientFactory,
        string providerId = "tapo-rtsp",
        IVisionModel? visionModel = null,
        ITtsModel? ttsModel = null,
        TapoVisionModelConfig? visionConfig = null,
        ILogger<TapoEmbodimentProvider>? logger = null,
        string? username = null,
        string? password = null)
    {
        _rtspClientFactory = rtspClientFactory ?? throw new ArgumentNullException(nameof(rtspClientFactory));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        _visionModel = visionModel;
        _ttsModel = ttsModel;
        _visionConfig = visionConfig ?? TapoVisionModelConfig.CreateDefault();
        _logger = logger;
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Initializes a new TapoEmbodimentProvider with both REST API and RTSP camera support.
    /// This allows simultaneous access to cameras (via RTSP) and other devices (via REST API).
    /// </summary>
    /// <param name="tapoClient">Optional Tapo REST API client for lights, plugs, etc.</param>
    /// <param name="rtspClientFactory">Optional factory for RTSP camera clients.</param>
    /// <param name="providerId">Unique provider identifier.</param>
    /// <param name="visionModel">Optional vision model for frame analysis.</param>
    /// <param name="ttsModel">Optional TTS model for voice output.</param>
    /// <param name="visionConfig">Vision model configuration.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="username">Camera account username for PTZ control.</param>
    /// <param name="password">Camera account password for PTZ control.</param>
    public TapoEmbodimentProvider(
        TapoRestClient? tapoClient,
        ITapoRtspClientFactory? rtspClientFactory,
        string providerId = "tapo",
        IVisionModel? visionModel = null,
        ITtsModel? ttsModel = null,
        TapoVisionModelConfig? visionConfig = null,
        ILogger<TapoEmbodimentProvider>? logger = null,
        string? username = null,
        string? password = null)
    {
        if (tapoClient == null && rtspClientFactory == null)
        {
            throw new ArgumentException("At least one of tapoClient or rtspClientFactory must be provided");
        }

        _tapoClient = tapoClient;
        _rtspClientFactory = rtspClientFactory;
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        _visionModel = visionModel;
        _ttsModel = ttsModel;
        _visionConfig = visionConfig ?? TapoVisionModelConfig.CreateDefault();
        _logger = logger;
        _username = username;
        _password = password;
    }

    /// <summary>
    /// Gets the RTSP client factory (if using RTSP mode).
    /// </summary>
    public ITapoRtspClientFactory? RtspClientFactory => _rtspClientFactory;

    /// <summary>
    /// Gets the REST client (if configured).
    /// </summary>
    public TapoRestClient? RestClient => _tapoClient;

    /// <inheritdoc/>
    public string ProviderId { get; }

    /// <inheritdoc/>
    public string ProviderName => "Tapo Smart Devices";

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public IObservable<PerceptionData> Perceptions => _perceptions.AsObservable();

    /// <inheritdoc/>
    public IObservable<EmbodimentProviderEvent> Events => _events.AsObservable();

    /// <inheritdoc/>
    public async Task<Result<EmbodimentCapabilities>> ConnectAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<EmbodimentCapabilities>.Failure("Provider is disposed");
        if (_isConnected) return Result<EmbodimentCapabilities>.Success(_capabilities);

        try
        {
            // Initialize both RTSP cameras AND REST API devices if both are configured
            // RTSP provides direct camera streaming, REST API provides smart plugs/lights control

            if (_rtspClientFactory != null)
            {
                // RTSP mode for direct camera access
                _logger?.LogInformation("Initializing RTSP camera connections...");

                // Initialize PTZ clients for cameras that support pan/tilt
                await InitializePtzClientsAsync(ct);

                await RefreshRtspCameraInventoryAsync(ct);
            }

            if (_tapoClient != null)
            {
                // REST API mode for smart home devices (lights, plugs, etc.)
                _logger?.LogInformation("Initializing REST API device connections...");
                var devicesResult = await _tapoClient.GetDevicesAsync(ct);

                if (devicesResult.IsFailure)
                {
                    // Log warning but continue - REST API is optional if RTSP cameras are available
                    _logger?.LogWarning("Could not get REST API devices: {Error}. Smart home device control may be unavailable.",
                        devicesResult.Error);
                }
                else
                {
                    // Build sensor/actuator inventory from devices
                    await RefreshDeviceInventoryAsync(ct);
                }
            }

            // At least one client should be configured (validated in constructor)
            if (_rtspClientFactory == null && _tapoClient == null)
            {
                return Result<EmbodimentCapabilities>.Failure("No Tapo client configured");
            }

            _isConnected = true;
            _capabilities = DetermineCapabilities();

            RaiseEvent(EmbodimentProviderEventType.Connected);

            _logger?.LogInformation(
                "Connected to Tapo provider with capabilities: {Capabilities}",
                _capabilities);

            return Result<EmbodimentCapabilities>.Success(_capabilities);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to connect to Tapo provider");
            return Result<EmbodimentCapabilities>.Failure($"Connection failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<EmbodimentUnit>> DisconnectAsync(CancellationToken ct = default)
    {
        if (!_isConnected) return Result<EmbodimentUnit>.Success(EmbodimentUnit.Default);

        try
        {
            _isConnected = false;
            _sensors.Clear();
            _actuators.Clear();
            _activeSensors.Clear();
            _sessionId = null;

            RaiseEvent(EmbodimentProviderEventType.Disconnected);

            await Task.CompletedTask;
            return Result<EmbodimentUnit>.Success(EmbodimentUnit.Default);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disconnecting from Tapo provider");
            return Result<EmbodimentUnit>.Failure($"Disconnect failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<EmbodimentState>> GetStateAsync(CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<EmbodimentState>.Failure("Provider is disposed"));

        var state = _isConnected ? EmbodimentState.Awake : EmbodimentState.Dormant;

        if (_activeSensors.Any(s => s.Value))
        {
            state = EmbodimentState.Observing;
        }

        return Task.FromResult(Result<EmbodimentState>.Success(state));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<SensorInfo>>> GetSensorsAsync(CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<IReadOnlyList<SensorInfo>>.Failure("Provider is disposed"));

        var sensors = _sensors.Values.ToList();
        return Task.FromResult(Result<IReadOnlyList<SensorInfo>>.Success(sensors));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<ActuatorInfo>>> GetActuatorsAsync(CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<IReadOnlyList<ActuatorInfo>>.Failure("Provider is disposed"));

        var actuators = _actuators.Values.ToList();
        return Task.FromResult(Result<IReadOnlyList<ActuatorInfo>>.Success(actuators));
    }

    /// <inheritdoc/>
    public Task<Result<EmbodimentUnit>> ActivateSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<EmbodimentUnit>.Failure("Provider is disposed"));
        if (!_isConnected) return Task.FromResult(Result<EmbodimentUnit>.Failure("Not connected"));

        if (!_sensors.ContainsKey(sensorId))
        {
            return Task.FromResult(Result<EmbodimentUnit>.Failure($"Sensor '{sensorId}' not found"));
        }

        _activeSensors[sensorId] = true;

        // Update sensor state
        var sensor = _sensors[sensorId];
        _sensors[sensorId] = sensor with { IsActive = true };

        RaiseEvent(EmbodimentProviderEventType.SensorActivated,
            new Dictionary<string, object> { ["sensorId"] = sensorId });

        _logger?.LogInformation("Activated sensor: {SensorId}", sensorId);
        return Task.FromResult(Result<EmbodimentUnit>.Success(EmbodimentUnit.Default));
    }

    /// <inheritdoc/>
    public Task<Result<EmbodimentUnit>> DeactivateSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<EmbodimentUnit>.Failure("Provider is disposed"));

        if (_activeSensors.ContainsKey(sensorId))
        {
            _activeSensors[sensorId] = false;

            if (_sensors.TryGetValue(sensorId, out var sensor))
            {
                _sensors[sensorId] = sensor with { IsActive = false };
            }

            RaiseEvent(EmbodimentProviderEventType.SensorDeactivated,
                new Dictionary<string, object> { ["sensorId"] = sensorId });
        }

        return Task.FromResult(Result<EmbodimentUnit>.Success(EmbodimentUnit.Default));
    }

    /// <inheritdoc/>
    public Task<Result<PerceptionData>> ReadSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<PerceptionData>.Failure("Provider is disposed"));
        if (!_isConnected) return Task.FromResult(Result<PerceptionData>.Failure("Not connected"));

        if (!_sensors.TryGetValue(sensorId, out var sensor))
        {
            return Task.FromResult(Result<PerceptionData>.Failure($"Sensor '{sensorId}' not found"));
        }

        if (!_activeSensors.GetValueOrDefault(sensorId, false))
        {
            return Task.FromResult(Result<PerceptionData>.Failure($"Sensor '{sensorId}' is not active"));
        }

        // Check if this is an RTSP camera sensor
        if (_rtspClientFactory != null && sensor.Modality == SensorModality.Visual)
        {
            return ReadRtspCameraFrameAsync(sensorId, sensor, ct);
        }

        // Sensor data retrieval for Tapo REST API devices is not implemented yet.
        // To avoid emitting misleading empty sensor payloads, report this as a failure.
        return Task.FromResult(Result<PerceptionData>.Failure(
            $"Sensor data retrieval is not implemented for sensor '{sensorId}' in TapoEmbodimentProvider"));
    }

    /// <summary>
    /// Reads a frame from an RTSP camera sensor.
    /// </summary>
    private async Task<Result<PerceptionData>> ReadRtspCameraFrameAsync(
        string sensorId,
        SensorInfo sensor,
        CancellationToken ct)
    {
        var cameraName = sensorId;
        var rtspClient = _rtspClientFactory?.GetClient(cameraName);

        if (rtspClient == null)
        {
            return Result<PerceptionData>.Failure($"RTSP client not found for camera '{cameraName}'");
        }

        var frameResult = await rtspClient.CaptureFrameAsync(ct);
        if (frameResult.IsFailure)
        {
            return Result<PerceptionData>.Failure($"Frame capture failed: {frameResult.Error}");
        }

        var frame = frameResult.Value;

        // Create perception data from the frame
        var perception = new PerceptionData(
            SensorId: sensorId,
            Modality: SensorModality.Visual,
            Timestamp: frame.Timestamp,
            Data: frame.Data,
            Metadata: new Dictionary<string, object>
            {
                ["width"] = frame.Width,
                ["height"] = frame.Height,
                ["frameNumber"] = frame.FrameNumber,
                ["cameraName"] = frame.CameraName,
                ["contentType"] = "image/jpeg"
            });

        // Emit the perception
        _perceptions.OnNext(perception);

        _logger?.LogDebug("Captured frame from {Camera}: {Width}x{Height}, {Size} bytes",
            cameraName, frame.Width, frame.Height, frame.Data.Length);

        return Result<PerceptionData>.Success(perception);
    }

    /// <inheritdoc/>
    public async Task<Result<ActionOutcome>> ExecuteActionAsync(
        string actuatorId,
        ActuatorAction action,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<ActionOutcome>.Failure("Provider is disposed");
        if (!_isConnected) return Result<ActionOutcome>.Failure("Not connected");

        if (!_actuators.TryGetValue(actuatorId, out var actuator))
        {
            return Result<ActionOutcome>.Failure($"Actuator '{actuatorId}' not found");
        }

        var startTime = DateTime.UtcNow;

        try
        {
            Result<TapoUnit>? result = null;

            // Route action to appropriate Tapo operation
            switch (action.ActionType.ToLowerInvariant())
            {
                case "turn_on":
                    result = await ExecuteTurnOnAsync(actuatorId, ct);
                    break;

                case "turn_off":
                    result = await ExecuteTurnOffAsync(actuatorId, ct);
                    break;

                case "set_color":
                    if (action.Parameters != null &&
                        action.Parameters.TryGetValue("red", out var r) &&
                        action.Parameters.TryGetValue("green", out var g) &&
                        action.Parameters.TryGetValue("blue", out var b))
                    {
                        result = await ExecuteSetColorAsync(
                            actuatorId,
                            Convert.ToByte(r),
                            Convert.ToByte(g),
                            Convert.ToByte(b),
                            ct);
                    }
                    else
                    {
                        return Result<ActionOutcome>.Failure("Color parameters required");
                    }
                    break;

                case "speak":
                    if (_ttsModel != null && action.Parameters?.TryGetValue("text", out var text) == true)
                    {
                        var emotion = action.Parameters.TryGetValue("emotion", out var e) ? e?.ToString() : null;
                        var speechResult = await SynthesizeSpeechAsync(text?.ToString() ?? "", emotion, ct);

                        var duration = DateTime.UtcNow - startTime;
                        return Result<ActionOutcome>.Success(new ActionOutcome(
                            actuatorId,
                            action.ActionType,
                            speechResult.IsSuccess,
                            duration,
                            speechResult.IsSuccess ? speechResult.Value : null,
                            speechResult.IsFailure ? speechResult.Error : null));
                    }
                    else
                    {
                        return Result<ActionOutcome>.Failure("TTS model not available or text not provided");
                    }

                case "pan_left":
                case "pan_right":
                case "tilt_up":
                case "tilt_down":
                case "ptz_move":
                case "ptz_stop":
                case "ptz_home":
                case "ptz_go_to_preset":
                case "ptz_set_preset":
                case "ptz_patrol_sweep":
                {
                    var ptzResult = await ExecutePtzActionAsync(actuatorId, action, ct);
                    var ptzElapsed = DateTime.UtcNow - startTime;
                    if (ptzResult.IsSuccess)
                    {
                        return Result<ActionOutcome>.Success(new ActionOutcome(
                            actuatorId,
                            action.ActionType,
                            ptzResult.Value.Success,
                            ptzElapsed,
                            ptzResult.Value,
                            ptzResult.Value.Success ? null : ptzResult.Value.Message));
                    }

                    return Result<ActionOutcome>.Success(new ActionOutcome(
                        actuatorId,
                        action.ActionType,
                        false,
                        ptzElapsed,
                        Error: ptzResult.Error));
                }

                default:
                    return Result<ActionOutcome>.Failure($"Unsupported action type: {action.ActionType}");
            }

            var elapsed = DateTime.UtcNow - startTime;

            if (result?.IsSuccess == true)
            {
                _logger?.LogInformation(
                    "Executed action {Action} on actuator {Actuator} in {Duration}ms",
                    action.ActionType,
                    actuatorId,
                    elapsed.TotalMilliseconds);

                return Result<ActionOutcome>.Success(new ActionOutcome(
                    actuatorId,
                    action.ActionType,
                    true,
                    elapsed));
            }

            return Result<ActionOutcome>.Success(new ActionOutcome(
                actuatorId,
                action.ActionType,
                false,
                elapsed,
                Error: result?.Error ?? "Unknown error"));
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger?.LogError(ex, "Failed to execute action {Action} on actuator {Actuator}",
                action.ActionType, actuatorId);

            return Result<ActionOutcome>.Success(new ActionOutcome(
                actuatorId,
                action.ActionType,
                false,
                elapsed,
                Error: ex.Message));
        }
    }

    /// <summary>
    /// Processes a video frame through the vision model.
    /// </summary>
    public async Task<Result<VisionAnalysisResult>> AnalyzeFrameAsync(
        string sensorId,
        byte[] frameData,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<VisionAnalysisResult>.Failure("Provider is disposed");
        if (_visionModel == null) return Result<VisionAnalysisResult>.Failure("Vision model not available");

        var options = new VisionAnalysisOptions(
            IncludeDescription: true,
            DetectObjects: _visionConfig.EnableObjectDetection,
            DetectFaces: _visionConfig.EnableFaceDetection,
            ClassifyScene: _visionConfig.EnableSceneClassification,
            MaxObjects: _visionConfig.MaxObjectsPerFrame,
            ConfidenceThreshold: _visionConfig.ConfidenceThreshold);

        var result = await _visionModel.AnalyzeImageAsync(frameData, "jpeg", options, ct);

        if (result.IsSuccess)
        {
            // Emit perception
            var perception = new PerceptionData(
                sensorId,
                SensorModality.Visual,
                DateTime.UtcNow,
                result.Value,
                new Dictionary<string, object>
                {
                    ["visionModel"] = _visionConfig.VisionModel,
                    ["description"] = result.Value.Description
                });

            _perceptions.OnNext(perception);

            return Result<VisionAnalysisResult>.Success(result.Value);
        }

        return Result<VisionAnalysisResult>.Failure(result.Error);
    }

    /// <summary>
    /// Authenticates with the Tapo REST API server.
    /// </summary>
    /// <param name="serverPassword">The server password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<string>> AuthenticateAsync(string serverPassword, CancellationToken ct = default)
    {
        if (_disposed) return Result<string>.Failure("Provider is disposed");

        if (_tapoClient == null)
        {
            return Result<string>.Failure("REST client not configured - using RTSP mode");
        }

        var result = await _tapoClient.LoginAsync(serverPassword, ct);
        if (result.IsSuccess)
        {
            _sessionId = result.Value;
            await RefreshDeviceInventoryAsync(ct);
        }

        return result;
    }

    private Task RefreshRtspCameraInventoryAsync(CancellationToken ct)
    {
        _sensors.Clear();
        _actuators.Clear();

        if (_rtspClientFactory == null)
        {
            return Task.CompletedTask;
        }

        foreach (var cameraName in _rtspClientFactory.GetCameraNames())
        {
            var rtspClient = _rtspClientFactory.GetClient(cameraName);
            if (rtspClient == null) continue;

            // Register visual sensor for camera
            var sensorCapabilities = EmbodimentCapabilities.VideoCapture | EmbodimentCapabilities.VisionAnalysis;
            _sensors[cameraName] = new SensorInfo(
                cameraName,
                $"Tapo Camera ({cameraName})",
                SensorModality.Visual,
                false,
                sensorCapabilities,
                new Dictionary<string, object>
                {
                    ["deviceType"] = "RTSP Camera",
                    ["ipAddress"] = rtspClient.CameraIp,
                    ["rtspUrl"] = rtspClient.RtspUrl,
                    ["visionModel"] = _visionConfig.VisionModel
                });

            // Register PTZ actuator if this is a PTZ-capable camera
            var ptzActuatorId = $"{cameraName}-ptz";
            if (_ptzClients.ContainsKey(cameraName))
            {
                var ptzActions = new List<string>
                {
                    "pan_left", "pan_right", "tilt_up", "tilt_down",
                    "ptz_move", "ptz_stop", "ptz_home",
                    "ptz_go_to_preset", "ptz_set_preset", "ptz_patrol_sweep"
                };

                _actuators[ptzActuatorId] = new ActuatorInfo(
                    ptzActuatorId,
                    $"Tapo Camera PTZ ({cameraName})",
                    ActuatorModality.Motor,
                    true,
                    EmbodimentCapabilities.PTZControl,
                    ptzActions,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = "RTSP Camera PTZ",
                        ["ipAddress"] = rtspClient.CameraIp,
                        ["panRange"] = "360°",
                        ["tiltRange"] = "114°"
                    });

                _logger?.LogInformation(
                    "Registered PTZ actuator for camera: {CameraName} at {Ip}",
                    cameraName, rtspClient.CameraIp);
            }

            _logger?.LogInformation("Registered RTSP camera: {CameraName} at {Ip}",
                cameraName, rtspClient.CameraIp);
        }

        return Task.CompletedTask;
    }

    private async Task RefreshDeviceInventoryAsync(CancellationToken ct)
    {
        _sensors.Clear();
        _actuators.Clear();

        if (_tapoClient == null)
        {
            return;
        }

        var devicesResult = await _tapoClient.GetDevicesAsync(ct);
        if (devicesResult.IsFailure)
        {
            _logger?.LogWarning("Could not refresh device inventory: {Error}", devicesResult.Error);
            return;
        }

        foreach (var device in devicesResult.Value)
        {
            var deviceId = device.Name;

            if (IsCameraDevice(device.DeviceType))
            {
                // Camera provides visual and audio sensors
                _sensors[deviceId] = new SensorInfo(
                    deviceId,
                    $"Tapo Camera ({device.Name})",
                    SensorModality.Visual,
                    false,
                    EmbodimentCapabilities.VideoCapture | EmbodimentCapabilities.VisionAnalysis,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress,
                        ["visionModel"] = _visionConfig.VisionModel
                    });

                // Camera mic
                _sensors[$"{deviceId}-mic"] = new SensorInfo(
                    $"{deviceId}-mic",
                    $"Tapo Camera Mic ({device.Name})",
                    SensorModality.Audio,
                    false,
                    EmbodimentCapabilities.AudioCapture | EmbodimentCapabilities.TwoWayAudio,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress
                    });

                // Camera speaker (actuator)
                _actuators[$"{deviceId}-speaker"] = new ActuatorInfo(
                    $"{deviceId}-speaker",
                    $"Tapo Camera Speaker ({device.Name})",
                    ActuatorModality.Voice,
                    false,
                    EmbodimentCapabilities.AudioOutput | EmbodimentCapabilities.TwoWayAudio,
                    ["speak"],
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString()
                    });
            }
            else if (IsLightDevice(device.DeviceType))
            {
                // Light provides visual actuator
                var supportedActions = new List<string> { "turn_on", "turn_off", "set_brightness" };
                if (IsColorLightDevice(device.DeviceType))
                {
                    supportedActions.Add("set_color");
                    supportedActions.Add("set_color_temperature");
                }

                _actuators[deviceId] = new ActuatorInfo(
                    deviceId,
                    $"Tapo Light ({device.Name})",
                    ActuatorModality.Visual,
                    false,
                    EmbodimentCapabilities.LightingControl,
                    supportedActions,
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress
                    });
            }
            else if (IsPlugDevice(device.DeviceType))
            {
                // Plug provides power control actuator
                _actuators[deviceId] = new ActuatorInfo(
                    deviceId,
                    $"Tapo Plug ({device.Name})",
                    ActuatorModality.Motor, // Using Motor for power control
                    false,
                    EmbodimentCapabilities.PowerControl,
                    ["turn_on", "turn_off"],
                    new Dictionary<string, object>
                    {
                        ["deviceType"] = device.DeviceType.ToString(),
                        ["ipAddress"] = device.IpAddress
                    });
            }
        }

        _logger?.LogInformation(
            "Refreshed device inventory: {SensorCount} sensors, {ActuatorCount} actuators",
            _sensors.Count,
            _actuators.Count);
    }

    private EmbodimentCapabilities DetermineCapabilities()
    {
        var caps = EmbodimentCapabilities.None;

        foreach (var sensor in _sensors.Values)
        {
            caps |= sensor.Capabilities;
        }

        foreach (var actuator in _actuators.Values)
        {
            caps |= actuator.Capabilities;
        }

        if (_visionModel != null)
        {
            caps |= EmbodimentCapabilities.VisionAnalysis;
        }

        if (_ttsModel != null)
        {
            caps |= EmbodimentCapabilities.AudioOutput;
        }

        return caps;
    }

    private async Task<Result<TapoUnit>> ExecuteTurnOnAsync(string deviceId, CancellationToken ct)
    {
        // Try color bulb first, then regular bulb, then plug
        Result<TapoUnit> colorResult = await _tapoClient!.ColorLightBulbs.TurnOnAsync(deviceId, ct);
        if (colorResult.IsSuccess) return colorResult;

        Result<TapoUnit> bulbResult = await _tapoClient!.LightBulbs.TurnOnAsync(deviceId, ct);
        if (bulbResult.IsSuccess) return bulbResult;

        return await _tapoClient!.Plugs.TurnOnAsync(deviceId, ct);
    }

    private async Task<Result<TapoUnit>> ExecuteTurnOffAsync(string deviceId, CancellationToken ct)
    {
        Result<TapoUnit> colorResult = await _tapoClient!.ColorLightBulbs.TurnOffAsync(deviceId, ct);
        if (colorResult.IsSuccess) return colorResult;

        Result<TapoUnit> bulbResult = await _tapoClient!.LightBulbs.TurnOffAsync(deviceId, ct);
        if (bulbResult.IsSuccess) return bulbResult;

        return await _tapoClient!.Plugs.TurnOffAsync(deviceId, ct);
    }

    private Task<Result<TapoUnit>> ExecuteSetColorAsync(
        string deviceId, byte r, byte g, byte b, CancellationToken ct)
    {
        return _tapoClient!.ColorLightBulbs.SetColorAsync(
            deviceId,
            new Color { Red = r, Green = g, Blue = b },
            ct);
    }

    private async Task<Result<SynthesizedSpeech>> SynthesizeSpeechAsync(
        string text, string? emotion, CancellationToken ct)
    {
        if (_ttsModel == null)
        {
            return Result<SynthesizedSpeech>.Failure("TTS model not available");
        }

        var config = new VoiceConfig(Style: emotion ?? "neutral");
        var result = await _ttsModel.SynthesizeAsync(text, config, ct);

        if (result.IsSuccess)
        {
            return Result<SynthesizedSpeech>.Success(result.Value);
        }

        return Result<SynthesizedSpeech>.Failure(result.Error);
    }

    private void RaiseEvent(
        EmbodimentProviderEventType eventType,
        IReadOnlyDictionary<string, object>? details = null)
    {
        _events.OnNext(new EmbodimentProviderEvent(eventType, DateTime.UtcNow, details));
    }

    private static bool IsCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C100 or TapoDeviceType.C200 or TapoDeviceType.C210 or
        TapoDeviceType.C220 or TapoDeviceType.C310 or TapoDeviceType.C320 or
        TapoDeviceType.C420 or TapoDeviceType.C500 or TapoDeviceType.C520;

    private static bool IsLightDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.L510 or TapoDeviceType.L520 or TapoDeviceType.L530 or
        TapoDeviceType.L535 or TapoDeviceType.L610 or TapoDeviceType.L630 or
        TapoDeviceType.L900 or TapoDeviceType.L920 or TapoDeviceType.L930;

    private static bool IsColorLightDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.L530 or TapoDeviceType.L535 or TapoDeviceType.L630 or
        TapoDeviceType.L900 or TapoDeviceType.L920 or TapoDeviceType.L930;

    private static bool IsPlugDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.P100 or TapoDeviceType.P105 or TapoDeviceType.P110 or
        TapoDeviceType.P110M or TapoDeviceType.P115 or TapoDeviceType.P300 or
        TapoDeviceType.P304 or TapoDeviceType.P304M or TapoDeviceType.P316;

    /// <summary>
    /// Determines whether the given device type is a PTZ-capable camera (has pan/tilt motors).
    /// C100 is fixed, C310/C320/C420 are outdoor fixed cameras.
    /// C200, C210, C500, C520 have pan/tilt motors.
    /// </summary>
    private static bool IsPtzCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C200 or TapoDeviceType.C210 or
        TapoDeviceType.C500 or TapoDeviceType.C520;

    /// <summary>
    /// Checks whether a camera is known to be PTZ-capable based on its name or device config.
    /// For RTSP-only cameras (no REST API device list), we check the appsettings device config.
    /// </summary>
    private bool IsCameraNamePtzCapable(string cameraName)
    {
        // Check if the camera is registered in our PTZ clients already
        if (_ptzClients.ContainsKey(cameraName))
            return true;

        // For RTSP cameras, we assume PTZ capability if "C200", "C210", "C500", or "C520"
        // appears in the camera name or device_type config
        var upperName = cameraName.ToUpperInvariant();
        return upperName.Contains("C200") || upperName.Contains("C210") ||
               upperName.Contains("C500") || upperName.Contains("C520");
    }

    private async Task InitializePtzClientsAsync(CancellationToken ct)
    {
        if (_rtspClientFactory == null) return;
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
        {
            _logger?.LogWarning("PTZ credentials not provided, skipping PTZ initialization");
            return;
        }

        foreach (var cameraName in _rtspClientFactory.GetCameraNames())
        {
            var rtspClient = _rtspClientFactory.GetClient(cameraName);
            if (rtspClient == null) continue;

            try
            {
                var ptzClient = new TapoCameraPtzClient(
                    rtspClient.CameraIp,
                    _username,
                    _password);

                var initResult = await ptzClient.InitializeAsync(ct);
                if (initResult.IsSuccess)
                {
                    _ptzClients[cameraName] = ptzClient;
                    _logger?.LogInformation(
                        "PTZ control initialized for camera {CameraName} at {Ip}",
                        cameraName, rtspClient.CameraIp);
                }
                else
                {
                    _logger?.LogWarning(
                        "PTZ not available for camera {CameraName}: {Error}",
                        cameraName, initResult.Error);
                    ptzClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Failed to initialize PTZ for camera {CameraName}", cameraName);
            }
        }
    }

    private async Task<Result<PtzMoveResult>> ExecutePtzActionAsync(
        string actuatorId,
        ActuatorAction action,
        CancellationToken ct)
    {
        // Extract camera name from actuator ID (e.g., "Camera1-ptz" -> "Camera1")
        var cameraName = actuatorId.EndsWith("-ptz", StringComparison.OrdinalIgnoreCase)
            ? actuatorId[..^4]
            : actuatorId;

        if (!_ptzClients.TryGetValue(cameraName, out var ptzClient))
        {
            return Result<PtzMoveResult>.Failure(
                $"No PTZ client available for camera '{cameraName}'");
        }

        var speed = GetFloatParam(action, "speed", 0.5f);
        var durationMs = GetIntParam(action, "duration_ms", 500);

        return action.ActionType.ToLowerInvariant() switch
        {
            "pan_left" => await ptzClient.PanLeftAsync(speed, durationMs, ct),
            "pan_right" => await ptzClient.PanRightAsync(speed, durationMs, ct),
            "tilt_up" => await ptzClient.TiltUpAsync(speed, durationMs, ct),
            "tilt_down" => await ptzClient.TiltDownAsync(speed, durationMs, ct),
            "ptz_move" => await ptzClient.MoveAsync(
                GetFloatParam(action, "pan_speed", 0f),
                GetFloatParam(action, "tilt_speed", 0f),
                durationMs, ct),
            "ptz_stop" => await ptzClient.StopAsync(ct),
            "ptz_home" => await ptzClient.GoToHomeAsync(ct),
            "ptz_go_to_preset" => await ptzClient.GoToPresetAsync(
                GetStringParam(action, "preset_token", "1"), ct),
            "ptz_set_preset" => await ptzClient.SetPresetAsync(
                GetStringParam(action, "preset_name", "preset_1"), ct),
            "ptz_patrol_sweep" => await ptzClient.PatrolSweepAsync(speed, ct),
            _ => Result<PtzMoveResult>.Failure($"Unknown PTZ action: {action.ActionType}")
        };
    }

    private static float GetFloatParam(ActuatorAction action, string key, float defaultValue)
    {
        if (action.Parameters?.TryGetValue(key, out var value) == true)
        {
            return Convert.ToSingle(value);
        }
        return defaultValue;
    }

    private static int GetIntParam(ActuatorAction action, string key, int defaultValue)
    {
        if (action.Parameters?.TryGetValue(key, out var value) == true)
        {
            return Convert.ToInt32(value);
        }
        return defaultValue;
    }

    private static string GetStringParam(ActuatorAction action, string key, string defaultValue)
    {
        if (action.Parameters?.TryGetValue(key, out var value) == true)
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isConnected = false;
        _sensors.Clear();
        _actuators.Clear();
        _activeSensors.Clear();

        // Dispose PTZ clients
        foreach (var ptzClient in _ptzClients.Values)
        {
            ptzClient.Dispose();
        }
        _ptzClients.Clear();

        _perceptions.OnCompleted();
        _events.OnCompleted();

        _perceptions.Dispose();
        _events.Dispose();

        _logger?.LogInformation("TapoEmbodimentProvider disposed");
    }
}
