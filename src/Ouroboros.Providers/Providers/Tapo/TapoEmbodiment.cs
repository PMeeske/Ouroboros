// <copyright file="TapoEmbodiment.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Core.Monads;

using Unit = Ouroboros.Core.Learning.Unit;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Tapo-based embodiment that provides video, audio, and voice capabilities
/// through Tapo smart cameras and devices with speaker capabilities.
/// Integrates with the VirtualSelf and EmbodimentController systems.
/// </summary>
public sealed class TapoEmbodiment : IDisposable
{
    private readonly TapoRestClient _tapoClient;
    private readonly IVisionModel? _visionModel;
    private readonly ITtsModel? _ttsModel;
    private readonly VirtualSelf _virtualSelf;
    private readonly ILogger<TapoEmbodiment>? _logger;

    private readonly Subject<TapoCameraFrame> _videoFrames = new();
    private readonly Subject<TapoCameraAudio> _audioChunks = new();
    private readonly Subject<VisionAnalysisResult> _visionResults = new();

    private TapoCameraConfig? _cameraConfig;
    private TapoVoiceOutputConfig? _voiceConfig;
    private bool _isCapturing;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TapoEmbodiment"/> class.
    /// </summary>
    /// <param name="tapoClient">The Tapo REST API client.</param>
    /// <param name="virtualSelf">The virtual self for embodiment state.</param>
    /// <param name="visionModel">Optional vision model for analyzing camera feeds.</param>
    /// <param name="ttsModel">Optional TTS model for voice output.</param>
    /// <param name="logger">Optional logger.</param>
    public TapoEmbodiment(
        TapoRestClient tapoClient,
        VirtualSelf virtualSelf,
        IVisionModel? visionModel = null,
        ITtsModel? ttsModel = null,
        ILogger<TapoEmbodiment>? logger = null)
    {
        _tapoClient = tapoClient ?? throw new ArgumentNullException(nameof(tapoClient));
        _virtualSelf = virtualSelf ?? throw new ArgumentNullException(nameof(virtualSelf));
        _visionModel = visionModel;
        _ttsModel = ttsModel;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the embodiment is currently capturing video/audio.
    /// </summary>
    public bool IsCapturing => _isCapturing;

    /// <summary>
    /// Gets the current camera configuration.
    /// </summary>
    public TapoCameraConfig? CameraConfig => _cameraConfig;

    /// <summary>
    /// Gets the current voice output configuration.
    /// </summary>
    public TapoVoiceOutputConfig? VoiceConfig => _voiceConfig;

    /// <summary>
    /// Observable stream of video frames from the camera.
    /// </summary>
    public IObservable<TapoCameraFrame> VideoFrames => _videoFrames.AsObservable();

    /// <summary>
    /// Observable stream of audio chunks from the camera.
    /// </summary>
    public IObservable<TapoCameraAudio> AudioChunks => _audioChunks.AsObservable();

    /// <summary>
    /// Observable stream of vision analysis results.
    /// </summary>
    public IObservable<VisionAnalysisResult> VisionResults => _visionResults.AsObservable();

    /// <summary>
    /// Configures the camera for video/audio capture.
    /// </summary>
    /// <param name="config">Camera configuration.</param>
    /// <returns>Result indicating success or failure.</returns>
    public Result<Unit> ConfigureCamera(TapoCameraConfig config)
    {
        if (_disposed) return Result<Unit>.Failure("Embodiment is disposed");
        if (config == null) return Result<Unit>.Failure("Configuration is required");

        _cameraConfig = config;
        _logger?.LogInformation(
            "Configured Tapo camera: {CameraName}, Quality: {Quality}, Vision: {VisionModel}",
            config.CameraName,
            config.StreamQuality,
            config.VisionModel);

        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Configures voice output through a Tapo device.
    /// </summary>
    /// <param name="config">Voice output configuration.</param>
    /// <returns>Result indicating success or failure.</returns>
    public Result<Unit> ConfigureVoiceOutput(TapoVoiceOutputConfig config)
    {
        if (_disposed) return Result<Unit>.Failure("Embodiment is disposed");
        if (config == null) return Result<Unit>.Failure("Configuration is required");

        _voiceConfig = config;
        _logger?.LogInformation(
            "Configured Tapo voice output: {DeviceName}, Volume: {Volume}",
            config.DeviceName,
            config.Volume);

        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Starts capturing video and audio from the configured camera.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit>> StartCaptureAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<Unit>.Failure("Embodiment is disposed");
        if (_cameraConfig == null) return Result<Unit>.Failure("Camera not configured. Call ConfigureCamera first.");
        if (_isCapturing) return Result<Unit>.Failure("Already capturing");

        try
        {
            // Verify the camera device exists
            var devicesResult = await _tapoClient.GetDevicesAsync(ct);
            if (devicesResult.IsFailure)
            {
                return Result<Unit>.Failure($"Failed to get devices: {devicesResult.Error}");
            }

            var camera = devicesResult.Value.FirstOrDefault(d => d.Name == _cameraConfig.CameraName);
            if (camera == null)
            {
                return Result<Unit>.Failure($"Camera '{_cameraConfig.CameraName}' not found");
            }

            // Verify it's a camera device type
            if (!IsCameraDevice(camera.DeviceType))
            {
                return Result<Unit>.Failure($"Device '{_cameraConfig.CameraName}' is not a camera device");
            }

            _isCapturing = true;
            _virtualSelf.ActivateSensor(SensorModality.Visual);
            if (_cameraConfig.EnableAudio)
            {
                _virtualSelf.ActivateSensor(SensorModality.Audio);
            }

            _virtualSelf.SetState(EmbodimentState.Observing);

            _logger?.LogInformation("Started capture from Tapo camera: {CameraName}", _cameraConfig.CameraName);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start capture from camera");
            return Result<Unit>.Failure($"Failed to start capture: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops capturing video and audio.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit>> StopCaptureAsync(CancellationToken ct = default)
    {
        if (!_isCapturing) return Result<Unit>.Success(Unit.Value);

        try
        {
            _isCapturing = false;
            _virtualSelf.DeactivateSensor(SensorModality.Visual);
            _virtualSelf.DeactivateSensor(SensorModality.Audio);
            _virtualSelf.SetState(EmbodimentState.Awake);

            _logger?.LogInformation("Stopped capture from Tapo camera");
            await Task.CompletedTask; // Placeholder for async cleanup
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping capture");
            return Result<Unit>.Failure($"Failed to stop capture: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a video frame and optionally analyzes it with the vision model.
    /// </summary>
    /// <param name="frameData">Raw frame data (JPEG).</param>
    /// <param name="width">Frame width.</param>
    /// <param name="height">Frame height.</param>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the vision analysis if available.</returns>
    public async Task<Result<Option<VisionAnalysisResult>>> ProcessVideoFrameAsync(
        byte[] frameData,
        int width,
        int height,
        long frameNumber,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<Option<VisionAnalysisResult>>.Failure("Embodiment is disposed");
        if (!_isCapturing) return Result<Option<VisionAnalysisResult>>.Failure("Not capturing");
        if (_cameraConfig == null) return Result<Option<VisionAnalysisResult>>.Failure("Camera not configured");

        var frame = new TapoCameraFrame(
            frameData,
            width,
            height,
            frameNumber,
            DateTime.UtcNow,
            _cameraConfig.CameraName);

        _videoFrames.OnNext(frame);

        // Analyze with vision model if available
        if (_visionModel != null)
        {
            var options = new VisionAnalysisOptions(
                IncludeDescription: true,
                DetectObjects: true,
                DetectFaces: _cameraConfig.EnablePersonDetection,
                ClassifyScene: true);

            var analysisResult = await _visionModel.AnalyzeImageAsync(frameData, "jpeg", options, ct);

            if (analysisResult.IsSuccess)
            {
                var result = analysisResult.Value;
                _visionResults.OnNext(result);

                // Publish to virtual self
                _virtualSelf.PublishVisualPerception(
                    result.Description,
                    result.Objects,
                    result.Faces,
                    result.SceneType,
                    result.Faces.FirstOrDefault()?.Emotion,
                    result.Confidence,
                    frameData);

                return Result<Option<VisionAnalysisResult>>.Success(
                    Option<VisionAnalysisResult>.Some(result));
            }

            _logger?.LogWarning("Vision analysis failed: {Error}", analysisResult.Error);
        }

        return Result<Option<VisionAnalysisResult>>.Success(Option<VisionAnalysisResult>.None());
    }

    /// <summary>
    /// Processes an audio chunk from the camera.
    /// </summary>
    /// <param name="audioData">Raw audio data (PCM).</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="duration">Duration of the audio.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public Task<Result<Unit>> ProcessAudioChunkAsync(
        byte[] audioData,
        int sampleRate,
        int channels,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        if (_disposed) return Task.FromResult(Result<Unit>.Failure("Embodiment is disposed"));
        if (!_isCapturing) return Task.FromResult(Result<Unit>.Failure("Not capturing"));
        if (_cameraConfig == null) return Task.FromResult(Result<Unit>.Failure("Camera not configured"));

        var audioChunk = new TapoCameraAudio(
            audioData,
            sampleRate,
            channels,
            duration,
            DateTime.UtcNow,
            _cameraConfig.CameraName);

        _audioChunks.OnNext(audioChunk);

        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    /// <summary>
    /// Speaks text through the configured voice output device.
    /// Requires a TTS model to be configured.
    /// </summary>
    /// <param name="text">Text to speak.</param>
    /// <param name="emotion">Optional emotion/style.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the synthesized speech.</returns>
    public async Task<Result<SynthesizedSpeech>> SpeakAsync(
        string text,
        string? emotion = null,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<SynthesizedSpeech>.Failure("Embodiment is disposed");
        if (_voiceConfig == null) return Result<SynthesizedSpeech>.Failure("Voice output not configured");
        if (_ttsModel == null) return Result<SynthesizedSpeech>.Failure("TTS model not available");

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<SynthesizedSpeech>.Failure("Text cannot be empty");
        }

        try
        {
            _virtualSelf.SetState(EmbodimentState.Speaking);

            var voiceConfig = new VoiceConfig(
                Voice: "default",
                Speed: 1.0,
                Volume: _voiceConfig.Volume / 100.0,
                Style: emotion ?? "neutral");

            var result = await _ttsModel.SynthesizeAsync(text, voiceConfig, ct);

            if (result.IsSuccess)
            {
                _logger?.LogInformation(
                    "Synthesized speech for Tapo output: {TextLength} chars, {Duration}",
                    text.Length,
                    result.Value.Duration);
                return Result<SynthesizedSpeech>.Success(result.Value);
            }

            return Result<SynthesizedSpeech>.Failure(result.Error);
        }
        finally
        {
            _virtualSelf.SetState(EmbodimentState.Awake);
        }
    }

    /// <summary>
    /// Creates a BodySchema that describes the Tapo embodiment capabilities.
    /// </summary>
    /// <returns>A BodySchema representing the Tapo embodiment.</returns>
    public BodySchema CreateBodySchema()
    {
        var schema = new BodySchema()
            .WithCapability(Capability.Reasoning)
            .WithCapability(Capability.Remembering);

        // Add camera sensor if configured
        if (_cameraConfig != null)
        {
            schema = schema.WithSensor(new SensorDescriptor(
                $"tapo-camera-{_cameraConfig.CameraName}",
                SensorModality.Visual,
                $"Tapo Camera ({_cameraConfig.CameraName})",
                true,
                new HashSet<Capability> { Capability.Seeing },
                new Dictionary<string, object>
                {
                    ["quality"] = _cameraConfig.StreamQuality.ToString(),
                    ["visionModel"] = _cameraConfig.VisionModel
                }));

            if (_cameraConfig.EnableAudio)
            {
                schema = schema.WithSensor(new SensorDescriptor(
                    $"tapo-mic-{_cameraConfig.CameraName}",
                    SensorModality.Audio,
                    $"Tapo Camera Microphone ({_cameraConfig.CameraName})",
                    true,
                    new HashSet<Capability> { Capability.Hearing }));
            }
        }

        // Add voice actuator if configured
        if (_voiceConfig != null && _ttsModel != null)
        {
            schema = schema.WithActuator(new ActuatorDescriptor(
                $"tapo-speaker-{_voiceConfig.DeviceName}",
                ActuatorModality.Voice,
                $"Tapo Speaker ({_voiceConfig.DeviceName})",
                true,
                new HashSet<Capability> { Capability.Speaking }));
        }

        return schema;
    }

    /// <summary>
    /// Gets the list of available Tapo camera devices.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the list of camera devices.</returns>
    public async Task<Result<IReadOnlyList<TapoDevice>>> GetAvailableCamerasAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<IReadOnlyList<TapoDevice>>.Failure("Embodiment is disposed");

        var devicesResult = await _tapoClient.GetDevicesAsync(ct);
        if (devicesResult.IsFailure)
        {
            return Result<IReadOnlyList<TapoDevice>>.Failure(devicesResult.Error);
        }

        var cameras = devicesResult.Value
            .Where(d => IsCameraDevice(d.DeviceType))
            .ToList();

        return Result<IReadOnlyList<TapoDevice>>.Success(cameras);
    }

    /// <summary>
    /// Determines whether a device type is a camera.
    /// </summary>
    private static bool IsCameraDevice(TapoDeviceType deviceType)
    {
        return deviceType switch
        {
            TapoDeviceType.C100 or
            TapoDeviceType.C200 or
            TapoDeviceType.C210 or
            TapoDeviceType.C220 or
            TapoDeviceType.C310 or
            TapoDeviceType.C320 or
            TapoDeviceType.C420 or
            TapoDeviceType.C500 or
            TapoDeviceType.C520 => true,
            _ => false
        };
    }

    /// <summary>
    /// Disposes the embodiment resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isCapturing = false;

        _videoFrames.OnCompleted();
        _audioChunks.OnCompleted();
        _visionResults.OnCompleted();

        _videoFrames.Dispose();
        _audioChunks.Dispose();
        _visionResults.Dispose();

        _logger?.LogInformation("Tapo embodiment disposed");
    }
}
