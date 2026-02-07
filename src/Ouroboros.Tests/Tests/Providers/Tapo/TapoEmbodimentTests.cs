// <copyright file="TapoEmbodimentTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers.Tapo;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Providers.Tapo;
using Xunit;

/// <summary>
/// Tests for the TapoEmbodiment class.
/// </summary>
[Trait("Category", "Unit")]
public class TapoEmbodimentTests
{
    private readonly Mock<ILogger<TapoEmbodiment>> _mockLogger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TapoEmbodimentTests()
    {
        _mockLogger = new Mock<ILogger<TapoEmbodiment>>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");

        // Act
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.Should().NotBeNull();
        embodiment.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullTapoClient_ThrowsArgumentNullException()
    {
        // Arrange
        using var virtualSelf = new VirtualSelf("test-agent");

        // Act
        Action act = () => new TapoEmbodiment(null!, virtualSelf);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tapoClient");
    }

    [Fact]
    public void Constructor_WithNullVirtualSelf_ThrowsArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);

        // Act
        Action act = () => new TapoEmbodiment(tapoClient, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("virtualSelf");
    }

    #endregion

    #region ConfigureCamera Tests

    [Fact]
    public void ConfigureCamera_WithValidConfig_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var config = new TapoCameraConfig("test-camera");

        // Act
        var result = embodiment.ConfigureCamera(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        embodiment.CameraConfig.Should().Be(config);
    }

    [Fact]
    public void ConfigureCamera_WithNullConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = embodiment.ConfigureCamera(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    #endregion

    #region ConfigureVoiceOutput Tests

    [Fact]
    public void ConfigureVoiceOutput_WithValidConfig_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var config = new TapoVoiceOutputConfig("test-speaker");

        // Act
        var result = embodiment.ConfigureVoiceOutput(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        embodiment.VoiceConfig.Should().Be(config);
    }

    [Fact]
    public void ConfigureVoiceOutput_WithNullConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = embodiment.ConfigureVoiceOutput(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    #endregion

    #region StartCaptureAsync Tests

    [Fact]
    public async Task StartCaptureAsync_WithoutCameraConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = await embodiment.StartCaptureAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Camera not configured");
    }

    [Fact]
    public async Task StartCaptureAsync_WithCameraConfigButNotAuthenticated_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var config = new TapoCameraConfig("test-camera");
        embodiment.ConfigureCamera(config);

        // Act
        var result = await embodiment.StartCaptureAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        // Should fail because not authenticated with Tapo REST API
    }

    #endregion

    #region StopCaptureAsync Tests

    [Fact]
    public async Task StopCaptureAsync_WhenNotCapturing_ReturnsSuccess()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = await embodiment.StopCaptureAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ProcessVideoFrameAsync Tests

    [Fact]
    public async Task ProcessVideoFrameAsync_WhenNotCapturing_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var frameData = new byte[100];

        // Act
        var result = await embodiment.ProcessVideoFrameAsync(frameData, 640, 480, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not capturing");
    }

    #endregion

    #region SpeakAsync Tests

    [Fact]
    public async Task SpeakAsync_WithoutVoiceConfig_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var result = await embodiment.SpeakAsync("Hello, world!");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Voice output not configured");
    }

    [Fact]
    public async Task SpeakAsync_WithoutTtsModel_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var voiceConfig = new TapoVoiceOutputConfig("test-speaker");
        embodiment.ConfigureVoiceOutput(voiceConfig);

        // Act
        var result = await embodiment.SpeakAsync("Hello, world!");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("TTS model not available");
    }

    [Fact]
    public async Task SpeakAsync_WithEmptyText_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        var mockTtsModel = new MockTtsModel();
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf, ttsModel: mockTtsModel);

        var voiceConfig = new TapoVoiceOutputConfig("test-speaker");
        embodiment.ConfigureVoiceOutput(voiceConfig);

        // Act
        var result = await embodiment.SpeakAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    #endregion

    #region CreateBodySchema Tests

    [Fact]
    public void CreateBodySchema_WithoutConfig_ReturnsBasicSchema()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        var schema = embodiment.CreateBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.HasCapability(Capability.Reasoning).Should().BeTrue();
        schema.HasCapability(Capability.Remembering).Should().BeTrue();
    }

    [Fact]
    public void CreateBodySchema_WithCameraConfig_IncludesVisualSensor()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        var cameraConfig = new TapoCameraConfig("living-room-camera", EnableAudio: true);
        embodiment.ConfigureCamera(cameraConfig);

        // Act
        var schema = embodiment.CreateBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.Sensors.Should().ContainKey("tapo-camera-living-room-camera");
        schema.Sensors.Should().ContainKey("tapo-mic-living-room-camera");
        schema.HasCapability(Capability.Seeing).Should().BeTrue();
        schema.HasCapability(Capability.Hearing).Should().BeTrue();
    }

    [Fact]
    public void CreateBodySchema_WithVoiceConfig_IncludesVoiceActuator()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        var mockTtsModel = new MockTtsModel();
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf, ttsModel: mockTtsModel);

        var voiceConfig = new TapoVoiceOutputConfig("living-room-speaker");
        embodiment.ConfigureVoiceOutput(voiceConfig);

        // Act
        var schema = embodiment.CreateBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.Actuators.Should().ContainKey("tapo-speaker-living-room-speaker");
        schema.HasCapability(Capability.Speaking).Should().BeTrue();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Act
        embodiment.Dispose();

        // Assert - disposed embodiment should return failures
        var configResult = embodiment.ConfigureCamera(new TapoCameraConfig("test"));
        configResult.IsFailure.Should().BeTrue();
        configResult.Error.Should().Contain("disposed");
    }

    #endregion

    #region Observable Tests

    [Fact]
    public void VideoFrames_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.VideoFrames.Should().NotBeNull();
    }

    [Fact]
    public void AudioChunks_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.AudioChunks.Should().NotBeNull();
    }

    [Fact]
    public void VisionResults_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var virtualSelf = new VirtualSelf("test-agent");
        using var embodiment = new TapoEmbodiment(tapoClient, virtualSelf);

        // Assert
        embodiment.VisionResults.Should().NotBeNull();
    }

    #endregion
}

/// <summary>
/// Tests for TapoVisionModelConfig.
/// </summary>
[Trait("Category", "Unit")]
public class TapoVisionModelConfigTests
{
    [Fact]
    public void CreateDefault_ReturnsConfigWithDefaultVisionModel()
    {
        // Act
        var config = TapoVisionModelConfig.CreateDefault();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.DefaultVisionModel);
        config.VisionModel.Should().Be("llava:13b");
    }

    [Fact]
    public void CreateLightweight_ReturnsConfigWithLightweightVisionModel()
    {
        // Act
        var config = TapoVisionModelConfig.CreateLightweight();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.LightweightVisionModel);
        config.VisionModel.Should().Be("llava:7b");
    }

    [Fact]
    public void CreateHighQuality_ReturnsConfigWithHighQualityVisionModel()
    {
        // Act
        var config = TapoVisionModelConfig.CreateHighQuality();

        // Assert
        config.VisionModel.Should().Be(TapoVisionModelConfig.HighQualityVisionModel);
        config.VisionModel.Should().Be("llava:34b");
    }

    [Fact]
    public void DefaultConfig_HasExpectedDefaults()
    {
        // Act
        var config = TapoVisionModelConfig.CreateDefault();

        // Assert
        config.OllamaEndpoint.Should().Be("http://localhost:11434");
        config.RequestTimeout.Should().Be(TimeSpan.FromSeconds(120));
        config.EnableObjectDetection.Should().BeTrue();
        config.EnableFaceDetection.Should().BeTrue();
        config.EnableSceneClassification.Should().BeTrue();
        config.MaxObjectsPerFrame.Should().Be(20);
        config.ConfidenceThreshold.Should().Be(0.5);
    }
}

/// <summary>
/// Tests for TapoCameraConfig and TapoVoiceOutputConfig.
/// </summary>
[Trait("Category", "Unit")]
public class TapoConfigTests
{
    [Fact]
    public void TapoCameraConfig_HasCorrectDefaults()
    {
        // Act
        var config = new TapoCameraConfig("test-camera");

        // Assert
        config.CameraName.Should().Be("test-camera");
        config.StreamQuality.Should().Be(CameraStreamQuality.HD);
        config.EnableAudio.Should().BeTrue();
        config.EnableMotionDetection.Should().BeTrue();
        config.EnablePersonDetection.Should().BeTrue();
        config.FrameRate.Should().Be(15);
        config.VisionModel.Should().Be("llava:13b");
    }

    [Fact]
    public void TapoVoiceOutputConfig_HasCorrectDefaults()
    {
        // Act
        var config = new TapoVoiceOutputConfig("test-speaker");

        // Assert
        config.DeviceName.Should().Be("test-speaker");
        config.Volume.Should().Be(75);
        config.SampleRate.Should().Be(16000);
    }
}

/// <summary>
/// Tests for camera device type detection.
/// </summary>
[Trait("Category", "Unit")]
public class TapoDeviceTypeTests
{
    [Theory]
    [InlineData(TapoDeviceType.C100)]
    [InlineData(TapoDeviceType.C200)]
    [InlineData(TapoDeviceType.C210)]
    [InlineData(TapoDeviceType.C220)]
    [InlineData(TapoDeviceType.C310)]
    [InlineData(TapoDeviceType.C320)]
    [InlineData(TapoDeviceType.C420)]
    [InlineData(TapoDeviceType.C500)]
    [InlineData(TapoDeviceType.C520)]
    public void CameraDeviceTypes_AreRecognized(TapoDeviceType deviceType)
    {
        // Assert - these should all be camera device types starting with 'C'
        deviceType.ToString().Should().StartWith("C");
    }

    [Theory]
    [InlineData(TapoDeviceType.L510)]
    [InlineData(TapoDeviceType.L530)]
    [InlineData(TapoDeviceType.P100)]
    [InlineData(TapoDeviceType.P110)]
    [InlineData(TapoDeviceType.P300)]
    public void NonCameraDeviceTypes_AreDistinct(TapoDeviceType deviceType)
    {
        // Assert - these should not be camera device types
        deviceType.ToString().Should().NotStartWith("C");
    }
}

/// <summary>
/// Mock TTS model for testing.
/// </summary>
file class MockTtsModel : ITtsModel
{
    public string ModelName => "MockTTS";
    public bool SupportsStreaming => false;
    public bool SupportsEmotions => false;

    public Task<Result<IReadOnlyList<VoiceInfo>, string>> GetVoicesAsync(
        string? language = null,
        CancellationToken ct = default)
    {
        var voices = new List<VoiceInfo>
        {
            new("default", "Default Voice", "en-US", "neutral", [])
        };
        return Task.FromResult(Result<IReadOnlyList<VoiceInfo>, string>.Success(voices));
    }

    public Task<Result<SynthesizedSpeech, string>> SynthesizeAsync(
        string text,
        VoiceConfig? config = null,
        CancellationToken ct = default)
    {
        var speech = new SynthesizedSpeech(
            text,
            new byte[100],
            "wav",
            16000,
            TimeSpan.FromSeconds(text.Length * 0.1),
            DateTime.UtcNow);
        return Task.FromResult(Result<SynthesizedSpeech, string>.Success(speech));
    }

    public IObservable<byte[]> SynthesizeStreaming(
        string text,
        VoiceConfig? config = null,
        CancellationToken ct = default)
    {
        return System.Reactive.Linq.Observable.Empty<byte[]>();
    }
}

/// <summary>
/// Tests for TapoEmbodimentProvider (repository pattern implementation).
/// </summary>
[Trait("Category", "Unit")]
public class TapoEmbodimentProviderTests
{
    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);

        // Act
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Assert
        provider.Should().NotBeNull();
        provider.ProviderId.Should().Be("tapo");
        provider.ProviderName.Should().Be("Tapo Smart Devices");
        provider.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithCustomProviderId_UsesProvidedId()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);

        // Act
        using var provider = new TapoEmbodimentProvider(tapoClient, "custom-tapo");

        // Assert
        provider.ProviderId.Should().Be("custom-tapo");
    }

    [Fact]
    public void Constructor_WithNullTapoClient_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TapoEmbodimentProvider((TapoRestClient)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tapoClient");
    }

    [Fact]
    public async Task GetStateAsync_WhenNotConnected_ReturnsDormant()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.GetStateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(EmbodimentState.Dormant);
    }

    [Fact]
    public async Task GetSensorsAsync_WhenNotConnected_ReturnsEmptyList()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.GetSensorsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActuatorsAsync_WhenNotConnected_ReturnsEmptyList()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.GetActuatorsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ReturnsSuccess()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.DisconnectAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateSensorAsync_WhenNotConnected_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = await provider.ActivateSensorAsync("camera1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task ExecuteActionAsync_WhenNotConnected_ReturnsFailure()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        var action = ActuatorAction.TurnOn();

        // Act
        var result = await provider.ExecuteActionAsync("light1", action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public void Perceptions_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Assert
        provider.Perceptions.Should().NotBeNull();
    }

    [Fact]
    public void Events_Observable_IsAvailable()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Assert
        provider.Events.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispose_DisposesResources()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        provider.Dispose();

        // Assert - disposed provider should return failures
        var result = await provider.GetStateAsync();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disposed");
    }
}

/// <summary>
/// Tests for EmbodimentAggregate with TapoEmbodimentProvider.
/// </summary>
[Trait("Category", "Unit")]
public class EmbodimentAggregateWithTapoTests
{
    [Fact]
    public void Constructor_CreatesAggregateWithInactiveStatus()
    {
        // Arrange & Act
        using var aggregate = new EmbodimentAggregate("test-aggregate", "Test Aggregate");

        // Assert
        aggregate.AggregateId.Should().Be("test-aggregate");
        aggregate.Name.Should().Be("Test Aggregate");
        aggregate.State.Status.Should().Be(AggregateStatus.Inactive);
    }

    [Fact]
    public void RegisterProvider_WithValidProvider_Succeeds()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = aggregate.RegisterProvider(provider);

        // Assert
        result.IsSuccess.Should().BeTrue();
        aggregate.Providers.Should().ContainKey(provider.ProviderId);
    }

    [Fact]
    public void RegisterProvider_WithNullProvider_ReturnsFailure()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");

        // Act
        var result = aggregate.RegisterProvider(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    [Fact]
    public void RegisterProvider_DuplicateProvider_ReturnsFailure()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        aggregate.RegisterProvider(provider);

        // Create another provider with same ID
        using var provider2 = new TapoEmbodimentProvider(tapoClient, "tapo");

        // Act
        var result = aggregate.RegisterProvider(provider2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already registered");
    }

    [Fact]
    public void ToBodySchema_ReturnsSchemaWithCapabilities()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");

        // Act
        var schema = aggregate.ToBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.HasCapability(Capability.Reasoning).Should().BeTrue();
        schema.HasCapability(Capability.Remembering).Should().BeTrue();
    }

    [Fact]
    public void DomainEvents_Observable_EmitsProviderRegistered()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        var receivedEvents = new List<EmbodimentDomainEvent>();
        using var sub = aggregate.DomainEvents.Subscribe(e => receivedEvents.Add(e));

        // Act
        aggregate.RegisterProvider(provider);

        // Assert
        receivedEvents.Should().Contain(e => e.EventType == EmbodimentDomainEventType.ProviderRegistered);
    }

    [Fact]
    public void Dispose_DisposesAllProviders()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        var provider = new TapoEmbodimentProvider(tapoClient);
        var aggregate = new EmbodimentAggregate("test-aggregate");
        aggregate.RegisterProvider(provider);

        // Act
        aggregate.Dispose();

        // Assert - aggregate should be disposed
        aggregate.State.Status.Should().Be(AggregateStatus.Inactive);
    }
}
