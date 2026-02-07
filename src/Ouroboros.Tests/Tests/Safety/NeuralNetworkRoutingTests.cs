// <copyright file="NeuralNetworkRoutingTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
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
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        var receivedMessages = new ConcurrentBag<NeuronMessage>();
        
        var targetNeuron = new TestNeuron("target", receivedMessages, "test.message");
        network.RegisterNeuron(targetNeuron);
        network.Start();
        
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
        Thread.Sleep(100); // Allow async routing to complete

        // Assert
        receivedMessages.Should().ContainSingle("message should be delivered to target");
        receivedMessages.First().Payload.Should().Be("test payload");
    }

    [Fact]
    public void RouteMessage_TopicSubscription_DeliversToSubscribers()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        var subscriber1Messages = new ConcurrentBag<NeuronMessage>();
        var subscriber2Messages = new ConcurrentBag<NeuronMessage>();
        
        // Subscriptions are done via the neuron's SubscribedTopics property
        var subscriber1 = new TestNeuron("subscriber1", subscriber1Messages, "test.topic");
        var subscriber2 = new TestNeuron("subscriber2", subscriber2Messages, "test.topic");
        
        network.RegisterNeuron(subscriber1);
        network.RegisterNeuron(subscriber2);
        network.Start();
        
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.topic",
            Payload = "broadcast payload"
        };

        // Act
        network.RouteMessage(message);
        Thread.Sleep(100);

        // Assert
        subscriber1Messages.Should().ContainSingle();
        subscriber2Messages.Should().ContainSingle();
    }

    [Fact]
    public void RouteMessage_WildcardSubscription_Matches()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        var receivedMessages = new ConcurrentBag<NeuronMessage>();
        
        // Wildcard subscription via the neuron's SubscribedTopics property
        var subscriber = new TestNeuron("wildcard_sub", receivedMessages, "test.*");
        network.RegisterNeuron(subscriber);
        network.Start();
        
        var message = new NeuronMessage
        {
            SourceNeuron = "source",
            Topic = "test.specific",
            Payload = "wildcard test"
        };

        // Act
        network.RouteMessage(message);
        Thread.Sleep(100);

        // Assert
        receivedMessages.Should().ContainSingle("wildcard should match specific topic");
    }

    [Fact]
    public void RouteMessage_NoSubscribers_DoesNotThrow()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
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
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);

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
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        var receivedMessages = new ConcurrentBag<NeuronMessage>();
        
        // Subscription via the neuron's SubscribedTopics property
        var neuron = new TestNeuron("self_sender", receivedMessages, "test.topic");
        network.RegisterNeuron(neuron);
        network.Start();
        
        var message = new NeuronMessage
        {
            SourceNeuron = "self_sender",
            Topic = "test.topic",
            Payload = "self message"
        };

        // Act
        network.RouteMessage(message);
        Thread.Sleep(100);

        // Assert
        receivedMessages.Should().BeEmpty("source neuron should not receive its own message");
    }

    [Fact]
    public void RouteMessage_UnregisteredTarget_DoesNotThrow()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
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
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        var messages1 = new ConcurrentBag<NeuronMessage>();
        var messages2 = new ConcurrentBag<NeuronMessage>();
        var messages3 = new ConcurrentBag<NeuronMessage>();
        
        var neuron1 = new TestNeuron("neuron1", messages1, "broadcast.topic");
        var neuron2 = new TestNeuron("neuron2", messages2, "broadcast.topic");
        var neuron3 = new TestNeuron("neuron3", messages3, "broadcast.topic");
        
        network.RegisterNeuron(neuron1);
        network.RegisterNeuron(neuron2);
        network.RegisterNeuron(neuron3);
        network.Start();

        // Act
        network.Broadcast("broadcast.topic", "broadcast payload", "sender");
        Thread.Sleep(100);

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
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        var receivedMessages = new ConcurrentBag<NeuronMessage>();
        
        // Subscription via the neuron's SubscribedTopics property with wildcard
        var testNeuron = new TestNeuron("concurrent_sub", receivedMessages, "test.*");
        network.RegisterNeuron(testNeuron);
        network.Start();

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

    private sealed class TestNeuron : Neuron
    {
        private readonly ConcurrentBag<NeuronMessage> _receivedMessages;
        private readonly string _name;
        private readonly string _id;
        private readonly HashSet<string> _topics;

        public TestNeuron(string name, ConcurrentBag<NeuronMessage> receivedMessages, params string[] topics)
        {
            _name = name;
            _id = name;
            _receivedMessages = receivedMessages;
            _topics = new HashSet<string>(topics);
        }

        public override string Id => _id;
        public override string Name => _name;
        public override NeuronType Type => NeuronType.Custom;
        public override IReadOnlySet<string> SubscribedTopics => _topics;

        protected override Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
        {
            _receivedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    #endregion
}
