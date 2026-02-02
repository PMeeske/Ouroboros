// <copyright file="VirtualSelfTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.EmbodiedInteraction;

using System.Reactive.Linq;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for VirtualSelf and embodiment components.
/// </summary>
[Trait("Category", "Unit")]
public class VirtualSelfTests
{
    [Fact]
    public void VirtualSelf_InitializesWithDefaultState()
    {
        // Arrange & Act
        using var self = new VirtualSelf("test-agent");

        // Assert
        Assert.Equal("test-agent", self.CurrentState.Name);
        Assert.Equal(EmbodimentState.Dormant, self.CurrentState.State);
    }

    [Fact]
    public void VirtualSelf_SetState_UpdatesState()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");

        // Act
        self.SetState(EmbodimentState.Awake);

        // Assert
        Assert.Equal(EmbodimentState.Awake, self.CurrentState.State);
    }

    [Fact]
    public async Task VirtualSelf_PublishTextPerception_EmitsToStream()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var received = new List<PerceptionEvent>();

        using var sub = self.Perceptions.Subscribe(p => received.Add(p));

        // Act
        self.PublishTextPerception("Hello", "user");

        // Small delay for Rx
        await Task.Delay(50);

        // Assert
        Assert.Single(received);
        var textPerception = Assert.IsType<TextPerception>(received[0]);
        Assert.Equal("Hello", textPerception.Text);
    }

    [Fact]
    public async Task VirtualSelf_PublishAudioPerception_EmitsToStream()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var received = new List<PerceptionEvent>();

        using var sub = self.Perceptions.Subscribe(p => received.Add(p));

        // Act
        self.PublishAudioPerception("Testing audio", 0.95, "en-US", TimeSpan.FromSeconds(1));

        // Small delay for Rx
        await Task.Delay(50);

        // Assert
        Assert.Single(received);
        var audioPerception = Assert.IsType<AudioPerception>(received[0]);
        Assert.Equal("Testing audio", audioPerception.TranscribedText);
        Assert.Equal(0.95, audioPerception.Confidence);
    }

    [Fact]
    public async Task VirtualSelf_PublishVisualPerception_EmitsToStream()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        var received = new List<PerceptionEvent>();

        using var sub = self.Perceptions.Subscribe(p => received.Add(p));

        // Act
        self.PublishVisualPerception("A person at a desk", confidence: 0.9);

        // Small delay for Rx
        await Task.Delay(50);

        // Assert
        Assert.Single(received);
        var visualPerception = Assert.IsType<VisualPerception>(received[0]);
        Assert.Equal("A person at a desk", visualPerception.Description);
    }

    [Fact]
    public void VirtualSelf_ActivateSensor_UpdatesState()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");

        // Act
        var result = self.ActivateSensor(SensorModality.Audio);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains(SensorModality.Audio, self.CurrentState.ActiveSensors);
    }

    [Fact]
    public void VirtualSelf_DeactivateSensor_UpdatesState()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");
        self.ActivateSensor(SensorModality.Audio);

        // Act
        var result = self.DeactivateSensor(SensorModality.Audio);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(SensorModality.Audio, self.CurrentState.ActiveSensors);
    }

    [Fact]
    public void VirtualSelf_FocusAttention_UpdatesAttention()
    {
        // Arrange
        using var self = new VirtualSelf("test-agent");

        // Act
        self.FocusAttention(SensorModality.Visual, "user_face", 0.9);

        // Assert
        Assert.NotNull(self.CurrentState.AttentionFocus);
        Assert.Equal("user_face", self.CurrentState.AttentionFocus.Target);
        Assert.Equal(0.9, self.CurrentState.AttentionFocus.Intensity);
    }

    [Fact]
    public void VirtualSelf_Dispose_CompletesStreams()
    {
        // Arrange
        var self = new VirtualSelf("test-agent");
        var completed = false;

        using var sub = self.Perceptions.Subscribe(
            _ => { },
            () => completed = true);

        // Act
        self.Dispose();

        // Assert
        Assert.True(completed);
    }
}

/// <summary>
/// Tests for BodySchema.
/// </summary>
[Trait("Category", "Unit")]
public class BodySchemaTests
{
    [Fact]
    public void BodySchema_EmptyByDefault()
    {
        // Arrange & Act
        var schema = new BodySchema();

        // Assert
        Assert.Empty(schema.Sensors);
        Assert.Empty(schema.Actuators);
        Assert.Empty(schema.Capabilities);
        Assert.Empty(schema.Limitations);
    }

    [Fact]
    public void BodySchema_WithSensor_AddsImmutably()
    {
        // Arrange
        var schema = new BodySchema();
        var sensor = SensorDescriptor.Audio("mic1", "Microphone");

        // Act
        var newSchema = schema.WithSensor(sensor);

        // Assert
        Assert.Empty(schema.Sensors); // Original unchanged
        Assert.Single(newSchema.Sensors);
        Assert.Contains(Capability.Hearing, newSchema.Capabilities);
    }

    [Fact]
    public void BodySchema_WithActuator_AddsImmutably()
    {
        // Arrange
        var schema = new BodySchema();
        var actuator = ActuatorDescriptor.Voice("voice1", "Voice Output");

        // Act
        var newSchema = schema.WithActuator(actuator);

        // Assert
        Assert.Empty(schema.Actuators); // Original unchanged
        Assert.Single(newSchema.Actuators);
        Assert.Contains(Capability.Speaking, newSchema.Capabilities);
    }

    [Fact]
    public void BodySchema_WithLimitation_AddsImmutably()
    {
        // Arrange
        var schema = new BodySchema();
        var limitation = new Limitation(
            LimitationType.MemoryBounded,
            "Limited context window",
            0.7);

        // Act
        var newSchema = schema.WithLimitation(limitation);

        // Assert
        Assert.Empty(schema.Limitations); // Original unchanged
        Assert.Single(newSchema.Limitations);
        Assert.Equal("Limited context window", newSchema.Limitations[0].Description);
    }

    [Fact]
    public void BodySchema_CreateConversational_HasCorrectDefaults()
    {
        // Arrange & Act
        var schema = BodySchema.CreateConversational();

        // Assert
        Assert.Contains(Capability.Reading, schema.Capabilities);
        Assert.Contains(Capability.Writing, schema.Capabilities);
        Assert.Contains(Capability.Reasoning, schema.Capabilities);
        Assert.NotEmpty(schema.Limitations);
    }

    [Fact]
    public void BodySchema_CreateMultimodal_HasAllModalities()
    {
        // Arrange & Act
        var schema = BodySchema.CreateMultimodal();

        // Assert
        Assert.Contains(Capability.Hearing, schema.Capabilities);
        Assert.Contains(Capability.Seeing, schema.Capabilities);
        Assert.Contains(Capability.Speaking, schema.Capabilities);
        Assert.True(schema.GetSensorsByModality(SensorModality.Audio).Any());
        Assert.True(schema.GetSensorsByModality(SensorModality.Visual).Any());
        Assert.True(schema.GetActuatorsByModality(ActuatorModality.Voice).Any());
    }

    [Fact]
    public void BodySchema_DescribeSelf_GeneratesDescription()
    {
        // Arrange
        var schema = BodySchema.CreateMultimodal();

        // Act
        var description = schema.DescribeSelf();

        // Assert
        Assert.Contains("Sensors", description);
        Assert.Contains("Actuators", description);
        Assert.Contains("Capabilities", description);
        Assert.Contains("Limitations", description);
    }

    [Fact]
    public void BodySchema_WithoutSensor_RemovesImmutably()
    {
        // Arrange
        var schema = new BodySchema()
            .WithSensor(SensorDescriptor.Audio("mic1", "Microphone"))
            .WithSensor(SensorDescriptor.Visual("cam1", "Camera"));

        // Act
        var newSchema = schema.WithoutSensor("mic1");

        // Assert
        Assert.Equal(2, schema.Sensors.Count); // Original unchanged
        Assert.Single(newSchema.Sensors);
        Assert.False(newSchema.Sensors.ContainsKey("mic1"));
    }

    [Fact]
    public void BodySchema_GetSensor_ReturnsOption()
    {
        // Arrange
        var schema = new BodySchema()
            .WithSensor(SensorDescriptor.Audio("mic1", "Microphone"));

        // Act
        var found = schema.GetSensor("mic1");
        var notFound = schema.GetSensor("nonexistent");

        // Assert
        Assert.True(found.HasValue);
        Assert.False(notFound.HasValue);
    }

    [Fact]
    public void BodySchema_HasCapability_ReturnsCorrectly()
    {
        // Arrange
        var schema = BodySchema.CreateMultimodal();

        // Act & Assert
        Assert.True(schema.HasCapability(Capability.Hearing));
        Assert.True(schema.HasCapability(Capability.Seeing));
        Assert.True(schema.HasCapability(Capability.Speaking));
    }
}

/// <summary>
/// Tests for Affordance.
/// </summary>
[Trait("Category", "Unit")]
public class AffordanceTests
{
    [Fact]
    public void Affordance_Traversable_HasCorrectType()
    {
        // Arrange & Act
        var affordance = Affordance.Traversable("floor", 0.95);

        // Assert
        Assert.Equal(AffordanceType.Traversable, affordance.Type);
        Assert.Equal("floor", affordance.TargetObjectId);
        Assert.Equal("walk", affordance.ActionVerb);
    }

    [Fact]
    public void Affordance_Graspable_HasCorrectProperties()
    {
        // Arrange & Act
        var affordance = Affordance.Graspable("cup", 0.92);

        // Assert
        Assert.Equal(AffordanceType.Graspable, affordance.Type);
        Assert.Equal("cup", affordance.TargetObjectId);
        Assert.Equal("grasp", affordance.ActionVerb);
        Assert.Contains("manipulator", affordance.RequiredCapabilities);
    }

    [Fact]
    public void Affordance_Create_WithDefaults()
    {
        // Arrange & Act
        var affordance = Affordance.Create(
            AffordanceType.Activatable,
            "button1",
            "press");

        // Assert
        Assert.Equal(AffordanceType.Activatable, affordance.Type);
        Assert.Equal("button1", affordance.TargetObjectId);
        Assert.Equal("press", affordance.ActionVerb);
        Assert.NotEqual(Guid.Empty, affordance.Id);
    }

    [Fact]
    public void AffordanceMap_AddAndGetByType()
    {
        // Arrange
        var map = new AffordanceMap();
        map.Add(Affordance.Traversable("floor"));
        map.Add(Affordance.Graspable("cup"));
        map.Add(Affordance.Traversable("ramp"));

        // Act
        var traversable = map.GetByType(AffordanceType.Traversable);

        // Assert
        Assert.Equal(2, traversable.Count);
        Assert.All(traversable, a => Assert.Equal(AffordanceType.Traversable, a.Type));
    }

    [Fact]
    public void AffordanceMap_GetForObject_ReturnsOption()
    {
        // Arrange
        var map = new AffordanceMap();
        map.Add(Affordance.Graspable("cup"));
        map.Add(AffordanceTestHelpers.Pushable("cup"));

        // Act
        var cupAffordances = map.GetForObject("cup");
        var notFound = map.GetForObject("nonexistent");

        // Assert
        Assert.True(cupAffordances.HasValue);
        Assert.Equal(2, cupAffordances.Value.Count);
        Assert.False(notFound.HasValue);
    }

    [Fact]
    public void Affordance_CanBeUsedBy_ChecksCapabilities()
    {
        // Arrange
        var affordance = Affordance.Graspable("cup");
        var agentWithCapabilities = new HashSet<string> { "manipulator", "gripper" };
        var agentWithoutCapabilities = new HashSet<string> { "vision" };

        // Act & Assert
        Assert.True(affordance.CanBeUsedBy(agentWithCapabilities));
        Assert.False(affordance.CanBeUsedBy(agentWithoutCapabilities));
    }

    [Fact]
    public void Affordance_RiskAdjustedConfidence_CalculatesCorrectly()
    {
        // Arrange
        var affordance = Affordance.Create(
            AffordanceType.Traversable,
            "ledge",
            "walk",
            confidence: 0.8,
            riskLevel: 0.3);

        // Act
        var riskAdjusted = affordance.RiskAdjustedConfidence;

        // Assert
        Assert.Equal(0.8 * 0.7, riskAdjusted, 3); // 0.8 * (1 - 0.3) = 0.56
    }
}

/// <summary>
/// Helper methods for test affordances.
/// </summary>
public static class AffordanceTestHelpers
{
    public static Affordance Pushable(string objectId) =>
        Affordance.Create(AffordanceType.Pushable, objectId, "push");
}

/// <summary>
/// Tests for perception types.
/// </summary>
[Trait("Category", "Unit")]
public class PerceptionTests
{
    [Fact]
    public void TextPerception_CreatesCorrectly()
    {
        // Arrange & Act
        var perception = new TextPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            1.0,
            "Hello, world!",
            "user");

        // Assert
        Assert.Equal("Hello, world!", perception.Text);
        Assert.Equal("user", perception.Source);
        Assert.Equal(SensorModality.Text, perception.Modality);
    }

    [Fact]
    public void AudioPerception_CreatesCorrectly()
    {
        // Arrange & Act
        var perception = new AudioPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            0.92,
            "Testing microphone",
            "en-US",
            null,
            TimeSpan.FromSeconds(2.5),
            true);

        // Assert
        Assert.Equal("Testing microphone", perception.TranscribedText);
        Assert.Equal(TimeSpan.FromSeconds(2.5), perception.Duration);
        Assert.Equal(0.92, perception.Confidence);
        Assert.Equal(SensorModality.Audio, perception.Modality);
    }

    [Fact]
    public void VisualPerception_WithDetections()
    {
        // Arrange
        var objects = new List<DetectedObject>
        {
            new("cup", 0.95, (0.1, 0.2, 0.3, 0.3), null),
            new("laptop", 0.88, (0.5, 0.1, 0.4, 0.4), null)
        };

        var faces = new List<DetectedFace>
        {
            new("face-1", 0.9, (0.3, 0.2, 0.2, 0.2), "Happy", 30, false, null)
        };

        // Act
        var perception = new VisualPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            0.9,
            "A person at a desk with a cup",
            objects,
            faces,
            "office",
            "Happy",
            null);

        // Assert
        Assert.Equal("A person at a desk with a cup", perception.Description);
        Assert.Equal(2, perception.Objects.Count);
        Assert.Single(perception.Faces);
        Assert.Contains("cup", perception.Objects.Select(o => o.Label));
        Assert.Equal(SensorModality.Visual, perception.Modality);
    }

    [Fact]
    public void FusedPerception_CombinesModalities()
    {
        // Arrange
        var audio = new List<AudioPerception>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, 0.9, "Hello", "en", null, TimeSpan.FromSeconds(1), true)
        };
        var visual = new List<VisualPerception>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, 0.8, "Person waving", [], [], null, null, null)
        };
        var text = new List<TextPerception>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, 1.0, "User greeting", "system")
        };

        // Act
        var fused = new FusedPerception(
            Guid.NewGuid(),
            DateTime.UtcNow,
            audio,
            visual,
            text,
            "[Heard] Hello | [Saw] Person waving | [Read] User greeting",
            0.9);

        // Assert
        Assert.Single(fused.AudioPerceptions);
        Assert.Single(fused.VisualPerceptions);
        Assert.Single(fused.TextPerceptions);
        Assert.True(fused.HasAudio);
        Assert.True(fused.HasVisual);
        Assert.Contains("Hello", fused.CombinedTranscript);
    }
}

/// <summary>
/// Tests for sensor configurations.
/// </summary>
[Trait("Category", "Unit")]
public class SensorConfigTests
{
    [Fact]
    public void AudioSensorConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new AudioSensorConfig();

        // Assert
        Assert.Equal(16000, config.SampleRate);
        Assert.Equal(1, config.Channels);
        Assert.True(config.EnableVAD);
        Assert.True(config.EnableInterimResults);
    }

    [Fact]
    public void VisualSensorConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new VisualSensorConfig();

        // Assert
        Assert.Equal(640, config.Width);
        Assert.Equal(480, config.Height);
        Assert.Equal(30, config.FrameRate);
        Assert.True(config.EnableObjectDetection);
        Assert.True(config.EnableFaceDetection);
    }

    [Fact]
    public void VoiceConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new VoiceConfig();

        // Assert
        Assert.Equal("default", config.Voice);
        Assert.Equal(1.0, config.Speed);
        Assert.Equal(1.0, config.Pitch);
        Assert.Equal(1.0, config.Volume);
        Assert.Equal("en-US", config.Language);
    }
}
