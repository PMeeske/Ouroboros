// <copyright file="WeightedRoutingTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Autonomous;

using System.Collections.Concurrent;
using FluentAssertions;
using Ouroboros.Domain.Autonomous;
using Xunit;

/// <summary>
/// Comprehensive tests for weighted message routing in OuroborosNeuralNetwork.
/// Tests inhibition, excitation, and backward compatibility.
/// </summary>
[Trait("Category", "Unit")]
public class WeightedRoutingTests
{
    /// <summary>
    /// Simple mock neuron for testing message routing.
    /// </summary>
    private sealed class MockNeuron : Neuron
    {
        private readonly string _id;
        private readonly string _name;
        private readonly HashSet<string> _topics;

        public MockNeuron(string id, string name, params string[] topics)
        {
            _id = id;
            _name = name;
            _topics = new HashSet<string>(topics);
            ReceivedMessages = new ConcurrentQueue<NeuronMessage>();
        }

        public override string Id => _id;

        public override string Name => _name;

        public override NeuronType Type => NeuronType.Custom;

        public override IReadOnlySet<string> SubscribedTopics => _topics;

        public ConcurrentQueue<NeuronMessage> ReceivedMessages { get; }

        protected override Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
        {
            ReceivedMessages.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void RouteMessage_StrongInhibition_BlocksDelivery()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target", -0.9); // Strong inhibition

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source", "test.topic");
        var target = new MockNeuron("target", "Target", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
        };
        network.RouteMessage(message);

        // Wait for async processing - use shorter delay since we expect no delivery
        Thread.Sleep(100);

        // Assert
        target.ReceivedMessages.Should().BeEmpty();

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_WeakInhibition_ReducesPriority()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target", -0.3); // Weak inhibition

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source", "test.topic");
        var target = new MockNeuron("target", "Target", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
            Priority = IntentionPriority.Normal,
        };
        network.RouteMessage(message);

        // Wait deterministically for async processing
        SpinWait.SpinUntil(() => !target.ReceivedMessages.IsEmpty, TimeSpan.FromSeconds(5));

        // Assert
        target.ReceivedMessages.Should().ContainSingle();
        var receivedMessage = target.ReceivedMessages.First();
        receivedMessage.Priority.Should().Be(IntentionPriority.Low);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_StrongExcitation_BoostsPriority()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target", 0.9); // Strong excitation

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source", "test.topic");
        var target = new MockNeuron("target", "Target", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
            Priority = IntentionPriority.Normal,
        };
        network.RouteMessage(message);

        // Wait deterministically for async processing
        SpinWait.SpinUntil(() => !target.ReceivedMessages.IsEmpty, TimeSpan.FromSeconds(5));

        // Assert
        target.ReceivedMessages.Should().ContainSingle();
        var receivedMessage = target.ReceivedMessages.First();
        receivedMessage.Priority.Should().Be(IntentionPriority.High);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_NormalExcitation_DeliversNormally()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target", 0.5); // Normal excitation

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source", "test.topic");
        var target = new MockNeuron("target", "Target", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
            Priority = IntentionPriority.Normal,
        };
        network.RouteMessage(message);

        // Wait deterministically for async processing
        SpinWait.SpinUntil(() => !target.ReceivedMessages.IsEmpty, TimeSpan.FromSeconds(5));

        // Assert
        target.ReceivedMessages.Should().ContainSingle();
        var receivedMessage = target.ReceivedMessages.First();
        receivedMessage.Priority.Should().Be(IntentionPriority.Normal);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_NullTopology_DefaultBehavior()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus, topology: null); // No topology

        var source = new MockNeuron("source", "Source", "test.topic");
        var target = new MockNeuron("target", "Target", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
            Priority = IntentionPriority.Normal,
        };
        network.RouteMessage(message);

        // Wait deterministically for async processing
        SpinWait.SpinUntil(() => !target.ReceivedMessages.IsEmpty, TimeSpan.FromSeconds(5));

        // Assert
        target.ReceivedMessages.Should().ContainSingle();
        var receivedMessage = target.ReceivedMessages.First();
        receivedMessage.Priority.Should().Be(IntentionPriority.Normal);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_WithTopology_RecordsActivation()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target", 0.7);

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source", "test.topic");
        var target = new MockNeuron("target", "Target", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
        };
        network.RouteMessage(message);

        // Wait deterministically for async processing
        SpinWait.SpinUntil(() => !target.ReceivedMessages.IsEmpty, TimeSpan.FromSeconds(5));

        // Assert
        var connection = topology.GetConnection("source", "target");
        connection.Should().NotBeNull();
        connection!.ActivationCount.Should().Be(1);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_WildcardTopic_RespectsWeights()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target", -0.9); // Strong inhibition

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source");
        var target = new MockNeuron("target", "Target", "test.*");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.subtopic",
            Payload = "test",
        };
        network.RouteMessage(message);

        // Wait for async processing - use shorter delay since we expect no delivery
        Thread.Sleep(100);

        // Assert
        target.ReceivedMessages.Should().BeEmpty();

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_MultipleTargets_AppliesWeightsSeparately()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target1", 0.9); // Strong excitation
        topology.SetConnection("source", "target2", -0.9); // Strong inhibition
        topology.SetConnection("source", "target3", 0.5); // Normal

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source", "test.topic");
        var target1 = new MockNeuron("target1", "Target1", "test.topic");
        var target2 = new MockNeuron("target2", "Target2", "test.topic");
        var target3 = new MockNeuron("target3", "Target3", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target1);
        network.RegisterNeuron(target2);
        network.RegisterNeuron(target3);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
            Priority = IntentionPriority.Normal,
        };
        network.RouteMessage(message);

        // Wait deterministically for async processing
        SpinWait.SpinUntil(() => target1.ReceivedMessages.Count > 0 && target3.ReceivedMessages.Count > 0, TimeSpan.FromSeconds(5));

        // Assert
        target1.ReceivedMessages.Should().ContainSingle();
        target1.ReceivedMessages.First().Priority.Should().Be(IntentionPriority.High);

        target2.ReceivedMessages.Should().BeEmpty(); // Blocked by strong inhibition

        target3.ReceivedMessages.Should().ContainSingle();
        target3.ReceivedMessages.First().Priority.Should().Be(IntentionPriority.Normal);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RouteMessage_ExactThreshold_WeakInhibition()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("source", "target", -0.79); // Just above strong inhibition threshold

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);
        var source = new MockNeuron("source", "Source", "test.topic");
        var target = new MockNeuron("target", "Target", "test.topic");

        network.RegisterNeuron(source);
        network.RegisterNeuron(target);
        network.Start();

        // Act
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "test",
            Priority = IntentionPriority.Normal,
        };
        network.RouteMessage(message);

        // Wait deterministically for async processing
        SpinWait.SpinUntil(() => !target.ReceivedMessages.IsEmpty, TimeSpan.FromSeconds(5));

        // Assert
        target.ReceivedMessages.Should().ContainSingle();
        target.ReceivedMessages.First().Priority.Should().Be(IntentionPriority.Low);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RegisterNeuron_WithTopology_CreatesDefaultConnections()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);

        var neuron1 = new MockNeuron("neuron1", "Neuron1", "topic1", "topic2");
        var neuron2 = new MockNeuron("neuron2", "Neuron2", "topic2", "topic3");

        // Act
        network.RegisterNeuron(neuron1);
        network.RegisterNeuron(neuron2);

        // Assert - should create bidirectional connections based on shared topic
        var connection1to2 = topology.GetConnection("neuron1", "neuron2");
        var connection2to1 = topology.GetConnection("neuron2", "neuron1");

        connection1to2.Should().NotBeNull();
        connection2to1.Should().NotBeNull();

        // Weight should be 0.5 + (1 shared topic * 0.1) = 0.6
        connection1to2!.Weight.Should().Be(0.6);
        connection2to1!.Weight.Should().Be(0.6);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void RegisterNeuron_WithoutTopology_NoConnections()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus, topology: null);

        var neuron1 = new MockNeuron("neuron1", "Neuron1", "topic1", "topic2");
        var neuron2 = new MockNeuron("neuron2", "Neuron2", "topic2", "topic3");

        // Act & Assert - should not throw
        network.RegisterNeuron(neuron1);
        network.RegisterNeuron(neuron2);

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void GetNetworkState_WithTopology_IncludesConnectionInfo()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron2", 0.7);
        topology.SetConnection("neuron2", "neuron3", -0.5);

        var network = new OuroborosNeuralNetwork(intentionBus, topology: topology);

        // Act
        var state = network.GetNetworkState();

        // Assert
        state.Should().Contain("Weighted Connections");
        state.Should().Contain("Excitatory: 1");
        state.Should().Contain("Inhibitory: 1");

        // Cleanup
        network.Dispose();
    }

    [Fact]
    public void GetNetworkState_WithoutTopology_NoConnectionInfo()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus, topology: null);

        // Act
        var state = network.GetNetworkState();

        // Assert
        state.Should().NotContain("Weighted Connections");

        // Cleanup
        network.Dispose();
    }
}
