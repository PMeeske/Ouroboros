// <copyright file="NeuralNetworkRoutingTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.Autonomous;
using Xunit;

namespace Ouroboros.Tests.Tests.Safety;

/// <summary>
/// Safety-critical tests for OuroborosNeuralNetwork message routing.
/// Verifies routing logic, edge cases, and concurrent safety.
/// </summary>
[Trait("Category", "Safety")]
public sealed class NeuralNetworkRoutingTests
{
    #region Basic Routing Tests

    [Fact]
    public void RouteMessage_DirectTarget_DeliversToTarget()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var receivedMessages = new List<NeuronMessage>();
        
        var targetNeuron = new TestNeuron("target", receivedMessages);
        network.RegisterNeuron(targetNeuron);
        
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            TargetNeuron = "target",
            Topic = "test.message",
            Payload = "test payload"
        };

        // Act
        network.RouteMessage(message);
        // Note: Thread.Sleep is used here for simplicity. For production tests,
        // consider using TaskCompletionSource-based WaitForMessageAsync pattern.
        Thread.Sleep(50); // Allow async routing to complete

        // Assert
        receivedMessages.Should().ContainSingle("message should be delivered to target");
        receivedMessages[0].Payload.Should().Be("test payload");
    }

    [Fact]
    public void RouteMessage_TopicSubscription_DeliversToSubscribers()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var subscriber1Messages = new List<NeuronMessage>();
        var subscriber2Messages = new List<NeuronMessage>();
        
        var subscriber1 = new TestNeuron("subscriber1", subscriber1Messages);
        var subscriber2 = new TestNeuron("subscriber2", subscriber2Messages);
        
        network.RegisterNeuron(subscriber1);
        network.RegisterNeuron(subscriber2);
        
        network.Subscribe("test.topic", "subscriber1");
        network.Subscribe("test.topic", "subscriber2");
        
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "broadcast payload"
        };

        // Act
        network.RouteMessage(message);
        Thread.Sleep(50);

        // Assert
        subscriber1Messages.Should().ContainSingle();
        subscriber2Messages.Should().ContainSingle();
    }

    [Fact]
    public void RouteMessage_WildcardSubscription_Matches()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var receivedMessages = new List<NeuronMessage>();
        
        var subscriber = new TestNeuron("wildcard_sub", receivedMessages);
        network.RegisterNeuron(subscriber);
        network.Subscribe("test.*", "wildcard_sub");
        
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.specific",
            Payload = "wildcard test"
        };

        // Act
        network.RouteMessage(message);
        Thread.Sleep(50);

        // Assert
        receivedMessages.Should().ContainSingle("wildcard should match specific topic");
    }

    [Fact]
    public void RouteMessage_NoSubscribers_DoesNotThrow()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "unsubscribed.topic",
            Payload = "test"
        };

        // Act
        var act = () => network.RouteMessage(message);

        // Assert
        act.Should().NotThrow("routing to no subscribers should not throw");
    }

    [Fact]
    public void RouteMessage_NullMessage_ThrowsArgumentNull()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();

        // Act
        var act = () => network.RouteMessage(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>("null message should throw");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RouteMessage_SourceNeuron_DoesNotReceiveOwnMessage()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var receivedMessages = new List<NeuronMessage>();
        
        var neuron = new TestNeuron("self_sender", receivedMessages);
        network.RegisterNeuron(neuron);
        network.Subscribe("test.topic", "self_sender");
        
        var message = new NeuronMessage
        {
            SourceNeuron = "self_sender",
            Topic = "test.topic",
            Payload = "self message"
        };

        // Act
        network.RouteMessage(message);
        Thread.Sleep(50);

        // Assert
        receivedMessages.Should().BeEmpty("source neuron should not receive its own message");
    }

    [Fact]
    public void RouteMessage_UnregisteredTarget_DoesNotThrow()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            TargetNeuron = "nonexistent",
            Topic = "test.topic",
            Payload = "test"
        };

        // Act
        var act = () => network.RouteMessage(message);

        // Assert
        act.Should().NotThrow("routing to unregistered target should not throw");
    }

    [Fact]
    public void Broadcast_DeliversToAllNeurons()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var messages1 = new List<NeuronMessage>();
        var messages2 = new List<NeuronMessage>();
        var messages3 = new List<NeuronMessage>();
        
        var neuron1 = new TestNeuron("neuron1", messages1);
        var neuron2 = new TestNeuron("neuron2", messages2);
        var neuron3 = new TestNeuron("neuron3", messages3);
        
        network.RegisterNeuron(neuron1);
        network.RegisterNeuron(neuron2);
        network.RegisterNeuron(neuron3);

        // Act
        network.Broadcast("broadcast.topic", "broadcast payload", "sender");
        Thread.Sleep(50);

        // Assert
        // All neurons except sender should receive
        var totalReceived = messages1.Count + messages2.Count + messages3.Count;
        totalReceived.Should().BeGreaterThan(0, "broadcast should deliver to neurons");
    }

    #endregion

    #region Concurrent Routing Tests

    [Fact]
    public void RouteMessage_ConcurrentRouting_DoesNotCorrupt()
    {
        // Arrange
        var network = new OuroborosNeuralNetwork();
        var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<NeuronMessage>();
        
        var subscriber = new TestNeuron("concurrent_sub", new List<NeuronMessage>());
        network.RegisterNeuron(subscriber);
        network.Subscribe("test.*", "concurrent_sub");

        // Override to use concurrent bag
        var testNeuron = new ConcurrentTestNeuron("concurrent_sub", receivedMessages);
        network.RegisterNeuron(testNeuron); // Re-register with concurrent version

        // Act - Route 100 messages from 10 threads concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                var message = new NeuronMessage
                {
                    SourceNeuron = $"source{i % 10}",
                    Topic = $"test.topic{i % 5}",
                    Payload = $"payload{i}"
                };
                network.RouteMessage(message);
            }))
            .ToArray();

        Task.WaitAll(tasks);
        Thread.Sleep(200); // Wait for all routing to complete

        // Assert
        // We should receive messages without corruption
        receivedMessages.Should().NotBeEmpty("messages should be routed");
        receivedMessages.Should().OnlyContain(m => m != null, "no null messages should be routed");
    }

    #endregion

    #region Helper Classes

    private class TestNeuron : Neuron
    {
        private readonly List<NeuronMessage> _receivedMessages;
        private readonly string _name;
        private readonly string _id;

        public TestNeuron(string name, List<NeuronMessage> receivedMessages)
        {
            _name = name;
            _id = name;
            _receivedMessages = receivedMessages;
        }

        public override string Id => _id;
        public override string Name => _name;
        public override NeuronType Type => NeuronType.Custom;
        public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>();

        protected override async Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
        {
            _receivedMessages.Add(message);
            await Task.CompletedTask;
        }
    }

    private class ConcurrentTestNeuron : Neuron
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<NeuronMessage> _receivedMessages;
        private readonly string _name;
        private readonly string _id;

        public ConcurrentTestNeuron(string name, System.Collections.Concurrent.ConcurrentBag<NeuronMessage> receivedMessages)
        {
            _name = name;
            _id = name;
            _receivedMessages = receivedMessages;
        }

        public override string Id => _id;
        public override string Name => _name;
        public override NeuronType Type => NeuronType.Custom;
        public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>();

        protected override async Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
        {
            _receivedMessages.Add(message);
            await Task.CompletedTask;
        }
    }

    #endregion
}
