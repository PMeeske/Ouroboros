// <copyright file="TapoEmbodimentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Core.Monads;

using Unit = Ouroboros.Core.EmbodiedInteraction.Unit;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Embodiment provider that uses Tapo smart devices as the state source.
/// Implements the repository-like IEmbodimentProvider interface, allowing
/// the Tapo REST API to serve as the persistence/state layer for embodiment.
/// </summary>
public sealed class TapoEmbodimentProvider : IEmbodimentProvider
{
    private readonly TapoRestClient _tapoClient;
    private readonly IVisionModel? _visionModel;
    private readonly ITtsModel? _ttsModel;
    private readonly ILogger<TapoEmbodimentProvider>? _logger;
    private readonly TapoVisionModelConfig _visionConfig;

    private readonly Subject<PerceptionData> _perceptions = new();
    private readonly Subject<EmbodimentProviderEvent> _events = new();
    private readonly Dictionary<string, SensorInfo> _sensors = new();
    private readonly Dictionary<string, ActuatorInfo> _actuators = new();
    private readonly Dictionary<string, bool> _activeSensors = new();

    private bool _isConnected;
    private bool _disposed;
    private string? _sessionId;
    private EmbodimentCapabilities _capabilities;

    /// <summary>
    /// Initializes a new TapoEmbodimentProvider.
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
            // Note: The actual authentication would need the server password
            // For now, we check if we can reach the server
            var devicesResult = await _tapoClient.GetDevicesAsync(ct);
            
            if (devicesResult.IsFailure)
            {
                // Try without authentication first to see if server is reachable
                _logger?.LogWarning("Could not get devices - authentication may be required");
            }

            // Build sensor/actuator inventory from devices
            await RefreshDeviceInventoryAsync(ct);

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
    public async Task<Result<Unit>> DisconnectAsync(CancellationToken ct = default)
    {
        if (!_isConnected) return Result<Unit>.Success(Unit.Default);

        try
        {
            _isConnected = false;
            _sensors.Clear();
            _actuators.Clear();
            _activeSensors.Clear();
            _sessionId = null;

            RaiseEvent(EmbodimentProviderEventType.Disconnected);

            await Task.CompletedTask;
            return Result<Unit>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error disconnecting from Tapo provider");
            return Result<Unit>.Failure($"Disconnect failed: {ex.Message}");
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
    public Task<Result<Unit>> ActivateSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<Unit>.Failure("Provider is disposed"));
        if (!_isConnected) return Task.FromResult(Result<Unit>.Failure("Not connected"));

        if (!_sensors.ContainsKey(sensorId))
        {
            return Task.FromResult(Result<Unit>.Failure($"Sensor '{sensorId}' not found"));
        }

        _activeSensors[sensorId] = true;
        
        // Update sensor state
        var sensor = _sensors[sensorId];
        _sensors[sensorId] = sensor with { IsActive = true };

        RaiseEvent(EmbodimentProviderEventType.SensorActivated, 
            new Dictionary<string, object> { ["sensorId"] = sensorId });

        _logger?.LogInformation("Activated sensor: {SensorId}", sensorId);
        return Task.FromResult(Result<Unit>.Success(Unit.Default));
    }

    /// <inheritdoc/>
    public Task<Result<Unit>> DeactivateSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<Unit>.Failure("Provider is disposed"));

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

        return Task.FromResult(Result<Unit>.Success(Unit.Default));
    }

    /// <inheritdoc/>
    public async Task<Result<PerceptionData>> ReadSensorAsync(string sensorId, CancellationToken ct = default)
    {
        if (_disposed) return Result<PerceptionData>.Failure("Provider is disposed");
        if (!_isConnected) return Result<PerceptionData>.Failure("Not connected");

        if (!_sensors.TryGetValue(sensorId, out var sensor))
        {
            return Result<PerceptionData>.Failure($"Sensor '{sensorId}' not found");
        }

        if (!_activeSensors.GetValueOrDefault(sensorId, false))
        {
            return Result<PerceptionData>.Failure($"Sensor '{sensorId}' is not active");
        }

        // Create perception based on sensor modality
        var perception = new PerceptionData(
            sensorId,
            sensor.Modality,
            DateTime.UtcNow,
            new byte[0], // Placeholder - actual data would come from camera stream
            new Dictionary<string, object>
            {
                ["source"] = "tapo",
                ["deviceName"] = sensorId
            });

        _perceptions.OnNext(perception);

        await Task.CompletedTask;
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
            Result<Core.Learning.Unit>? result = null;

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

        var result = await _tapoClient.LoginAsync(serverPassword, ct);
        if (result.IsSuccess)
        {
            _sessionId = result.Value;
            await RefreshDeviceInventoryAsync(ct);
        }

        return result;
    }

    private async Task RefreshDeviceInventoryAsync(CancellationToken ct)
    {
        _sensors.Clear();
        _actuators.Clear();

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

    private async Task<Result<Core.Learning.Unit>> ExecuteTurnOnAsync(string deviceId, CancellationToken ct)
    {
        // Try color bulb first, then regular bulb, then plug
        var colorResult = await _tapoClient.ColorLightBulbs.TurnOnAsync(deviceId, ct);
        if (colorResult.IsSuccess) return colorResult;

        var bulbResult = await _tapoClient.LightBulbs.TurnOnAsync(deviceId, ct);
        if (bulbResult.IsSuccess) return bulbResult;

        return await _tapoClient.Plugs.TurnOnAsync(deviceId, ct);
    }

    private async Task<Result<Core.Learning.Unit>> ExecuteTurnOffAsync(string deviceId, CancellationToken ct)
    {
        var colorResult = await _tapoClient.ColorLightBulbs.TurnOffAsync(deviceId, ct);
        if (colorResult.IsSuccess) return colorResult;

        var bulbResult = await _tapoClient.LightBulbs.TurnOffAsync(deviceId, ct);
        if (bulbResult.IsSuccess) return bulbResult;

        return await _tapoClient.Plugs.TurnOffAsync(deviceId, ct);
    }

    private Task<Result<Core.Learning.Unit>> ExecuteSetColorAsync(
        string deviceId, byte r, byte g, byte b, CancellationToken ct)
    {
        return _tapoClient.ColorLightBulbs.SetColorAsync(
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isConnected = false;
        _sensors.Clear();
        _actuators.Clear();
        _activeSensors.Clear();

        _perceptions.OnCompleted();
        _events.OnCompleted();

        _perceptions.Dispose();
        _events.Dispose();

        _logger?.LogInformation("TapoEmbodimentProvider disposed");
    }
}
