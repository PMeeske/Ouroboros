// <copyright file="EmbodimentControllerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.EmbodiedInteraction;

using System.Reactive.Linq;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for EmbodimentController integration.
/// </summary>
[Trait("Category", "Unit")]
public class EmbodimentControllerTests
{
    [Fact]
    public void EmbodimentController_InitializesWithVirtualSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();

        // Act
        using var controller = new EmbodimentController(self, schema);

        // Assert
        Assert.Same(self, controller.VirtualSelf);
        Assert.Same(schema, controller.BodySchema);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void EmbodimentController_RegisterAudioSensor_ReturnsSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);
        var sttModel = new MockSttModel();
        var config = new AudioSensorConfig();
        using var sensor = new AudioSensor(sttModel, self, config);

        // Act
        var result = controller.RegisterAudioSensor("mic1", sensor);

        // Assert
        Assert.Same(controller, result); // Fluent interface
    }

    [Fact]
    public void EmbodimentController_RegisterVisualSensor_ReturnsSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);

        // Act
        var result = controller.RegisterVisualSensor("cam1", sensor);

        // Assert
        Assert.Same(controller, result); // Fluent interface
    }

    [Fact]
    public void EmbodimentController_RegisterVoiceActuator_ReturnsSelf()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = controller.RegisterVoiceActuator("voice1", actuator);

        // Assert
        Assert.Same(controller, result); // Fluent interface
    }

    [Fact]
    public async Task EmbodimentController_OnTextInput_EmitsPerception()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateConversational();
        using var controller = new EmbodimentController(self, schema);

        var receivedPerceptions = new List<UnifiedPerception>();
        using var sub = controller.Perceptions.Subscribe(p => receivedPerceptions.Add(p));

        // Act
        controller.OnTextInput("Hello, AI!", "user");
        await Task.Delay(50); // Allow Rx propagation

        // Assert
        Assert.Single(receivedPerceptions);
        Assert.Equal(SensorModality.Text, receivedPerceptions[0].Modality);
        var textPerception = receivedPerceptions[0].Perception as TextPerception;
        Assert.NotNull(textPerception);
        Assert.Equal("Hello, AI!", textPerception.Text);
    }

    [Fact]
    public async Task EmbodimentController_SpeakAsync_WithNoActuator_ReturnsError()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateConversational();
        using var controller = new EmbodimentController(self, schema);

        // Act
        var result = await controller.SpeakAsync("Hello");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("No voice actuator", result.Error);
    }

    [Fact]
    public async Task EmbodimentController_SpeakAsync_WithActuator_Succeeds()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);

        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);
        controller.RegisterVoiceActuator("voice1", actuator);

        // Act
        var result = await controller.SpeakAsync("Hello, world!");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, world!", result.Value.Text);
    }

    [Fact]
    public async Task EmbodimentController_ExecuteActionAsync_VoiceAction()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        using var controller = new EmbodimentController(self, schema);

        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);
        controller.RegisterVoiceActuator("voice1", actuator);

        var request = new ActionRequest(
            "voice1",
            ActuatorModality.Voice,
            "Testing speech output");

        // Act
        var result = await controller.ExecuteActionAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void EmbodimentController_Dispose_DisposesComponents()
    {
        // Arrange
        var self = new VirtualSelf("test-agent");
        var schema = BodySchema.CreateMultimodal();
        var controller = new EmbodimentController(self, schema);

        var ttsModel = new MockTtsModel();
        var actuator = new VoiceActuator(ttsModel, self);
        controller.RegisterVoiceActuator("voice1", actuator);

        // Act
        controller.Dispose();

        // Assert - attempting to subscribe to disposed stream should throw
        Assert.Throws<ObjectDisposedException>(() =>
        {
            controller.Perceptions.Subscribe(_ => { });
        });
    }
}

/// <summary>
/// Tests for VoiceActuator.
/// </summary>
[Trait("Category", "Unit")]
public class VoiceActuatorTests
{
    [Fact]
    public void VoiceActuator_InitializesWithDefaults()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();

        // Act
        using var actuator = new VoiceActuator(ttsModel, self);

        // Assert
        Assert.Equal("MockTTS", actuator.ModelName);
        Assert.False(actuator.IsSpeaking);
        Assert.Equal(1.0, actuator.Config.Speed);
    }

    [Fact]
    public void VoiceActuator_Configure_UpdatesConfig()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        var newConfig = new VoiceConfig(
            Voice: "custom-voice",
            Speed: 1.5,
            Pitch: 1.2);

        // Act
        actuator.Configure(newConfig);

        // Assert
        Assert.Equal("custom-voice", actuator.Config.Voice);
        Assert.Equal(1.5, actuator.Config.Speed);
        Assert.Equal(1.2, actuator.Config.Pitch);
    }

    [Fact]
    public void VoiceActuator_SetSpeed_ClampsValue()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        actuator.SetSpeed(3.0); // Above max

        // Assert
        Assert.Equal(2.0, actuator.Config.Speed); // Clamped to max
    }

    [Fact]
    public async Task VoiceActuator_SpeakAsync_SynthesizesSpeech()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = await actuator.SpeakAsync("Hello, world!");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, world!", result.Value.Text);
        Assert.NotEmpty(result.Value.AudioData);
    }

    [Fact]
    public async Task VoiceActuator_SpeakAsync_EmptyText_ReturnsError()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = await actuator.SpeakAsync("");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error);
    }

    [Fact]
    public async Task VoiceActuator_SpeakAsync_EmitsSpeechOutput()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        var receivedSpeech = new List<SynthesizedSpeech>();
        using var sub = actuator.SpeechOutput.Subscribe(s => receivedSpeech.Add(s));

        // Act
        await actuator.SpeakAsync("Test message");
        await Task.Delay(50); // Allow Rx propagation

        // Assert
        Assert.Single(receivedSpeech);
        Assert.Equal("Test message", receivedSpeech[0].Text);
    }

    [Fact]
    public async Task VoiceActuator_GetVoicesAsync_ReturnsVoices()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        var result = await actuator.GetVoicesAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
    }

    [Fact]
    public void VoiceActuator_SetVoice_UpdatesConfig()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var ttsModel = new MockTtsModel();
        using var actuator = new VoiceActuator(ttsModel, self);

        // Act
        actuator.SetVoice("new-voice-id");

        // Assert
        Assert.Equal("new-voice-id", actuator.Config.Voice);
    }
}

/// <summary>
/// Tests for AudioSensor.
/// </summary>
[Trait("Category", "Unit")]
public class AudioSensorTests
{
    [Fact]
    public void AudioSensor_InitializesWithDefaults()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var sttModel = new MockSttModel();
        var config = new AudioSensorConfig();

        // Act
        using var sensor = new AudioSensor(sttModel, self, config);

        // Assert
        Assert.Equal("MockSTT", sensor.ModelName);
        Assert.False(sensor.IsListening);
    }
}

/// <summary>
/// Tests for VisualSensor.
/// </summary>
[Trait("Category", "Unit")]
public class VisualSensorTests
{
    [Fact]
    public void VisualSensor_InitializesWithDefaults()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();

        // Act
        using var sensor = new VisualSensor(visionModel, self, config);

        // Assert
        Assert.Equal("MockVision", sensor.ModelName);
        Assert.False(sensor.IsObserving);
    }

    [Fact]
    public void VisualSensor_StartObserving_Succeeds()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);

        // Act
        var result = sensor.StartObserving();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(sensor.IsObserving);
    }

    [Fact]
    public void VisualSensor_StopObserving_Succeeds()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);
        sensor.StartObserving();

        // Act
        var result = sensor.StopObserving();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(sensor.IsObserving);
    }

    [Fact]
    public void VisualSensor_FocusOn_UpdatesVirtualSelfAttention()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var visionModel = new MockVisionModel();
        var config = new VisualSensorConfig();
        using var sensor = new VisualSensor(visionModel, self, config);

        // Act
        sensor.FocusOn("user_face");

        // Assert
        Assert.NotNull(self.CurrentState.AttentionFocus);
        Assert.Equal("user_face", self.CurrentState.AttentionFocus.Target);
    }
}

/// <summary>
/// Mock STT model for testing.
/// </summary>
public class MockSttModel : ISttModel
{
    public string ModelName => "MockSTT";
    public bool SupportsStreaming => false;

    public Task<Result<TranscriptionResult, string>> TranscribeAsync(
        string audioFilePath,
        string? language = null,
        CancellationToken ct = default)
    {
        var result = new TranscriptionResult(
            "Mock transcription from file",
            0.95,
            language ?? "en-US",
            true,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            null);
        return Task.FromResult(Result<TranscriptionResult, string>.Success(result));
    }

    public Task<Result<TranscriptionResult, string>> TranscribeAsync(
        byte[] audioData,
        string format,
        int sampleRate,
        string? language = null,
        CancellationToken ct = default)
    {
        var result = new TranscriptionResult(
            "Mock transcription from bytes",
            0.95,
            language ?? "en-US",
            true,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            null);
        return Task.FromResult(Result<TranscriptionResult, string>.Success(result));
    }

    public IStreamingTranscription CreateStreamingSession(string? language = null)
    {
        return new MockStreamingTranscription();
    }
}

/// <summary>
/// Mock streaming transcription.
/// </summary>
public class MockStreamingTranscription : IStreamingTranscription
{
    public IObservable<TranscriptionResult> Results =>
        Observable.Empty<TranscriptionResult>();

    public IObservable<VoiceActivity> VoiceActivity =>
        Observable.Empty<VoiceActivity>();

    public string AccumulatedTranscript => "";
    public bool IsActive => false;

    public Task PushAudioAsync(byte[] audioData, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task EndAudioAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Mock vision model for testing.
/// </summary>
public class MockVisionModel : IVisionModel
{
    public string ModelName => "MockVision";
    public bool SupportsStreaming => false;

    public Task<Result<VisionAnalysisResult, string>> AnalyzeImageAsync(
        byte[] imageData,
        string format,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        var result = new VisionAnalysisResult(
            "Mock scene description",
            Array.Empty<DetectedObject>(),
            Array.Empty<DetectedFace>(),
            "office",
            ["gray", "white"],
            null,
            0.9,
            50);
        return Task.FromResult(Result<VisionAnalysisResult, string>.Success(result));
    }

    public Task<Result<VisionAnalysisResult, string>> AnalyzeImageFileAsync(
        string filePath,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        var result = new VisionAnalysisResult(
            "Mock scene from file",
            Array.Empty<DetectedObject>(),
            Array.Empty<DetectedFace>(),
            "office",
            ["gray", "white"],
            null,
            0.9,
            100);
        return Task.FromResult(Result<VisionAnalysisResult, string>.Success(result));
    }

    public Task<Result<string, string>> AnswerQuestionAsync(
        byte[] imageData,
        string format,
        string question,
        CancellationToken ct = default)
    {
        return Task.FromResult(Result<string, string>.Success($"Mock answer to: {question}"));
    }

    public Task<Result<IReadOnlyList<DetectedObject>, string>> DetectObjectsAsync(
        byte[] imageData,
        string format,
        int maxObjects = 20,
        CancellationToken ct = default)
    {
        var objects = new List<DetectedObject>
        {
            new("mock-object", 0.9, (0.1, 0.1, 0.2, 0.2), null)
        };
        return Task.FromResult(Result<IReadOnlyList<DetectedObject>, string>.Success(objects));
    }

    public Task<Result<IReadOnlyList<DetectedFace>, string>> DetectFacesAsync(
        byte[] imageData,
        string format,
        bool analyzeEmotion = true,
        CancellationToken ct = default)
    {
        var faces = new List<DetectedFace>();
        return Task.FromResult(Result<IReadOnlyList<DetectedFace>, string>.Success(faces));
    }
}

/// <summary>
/// Mock TTS model for testing.
/// </summary>
public class MockTtsModel : ITtsModel
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
            new byte[100], // Dummy audio
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
        return Observable.Empty<byte[]>();
    }
}
