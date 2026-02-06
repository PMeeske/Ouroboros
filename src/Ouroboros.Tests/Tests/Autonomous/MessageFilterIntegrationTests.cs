// <copyright file="MessageFilterIntegrationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Application.Autonomous;
using Ouroboros.Core.Ethics;
using Ouroboros.Domain.Autonomous;
using Ouroboros.Tests.Mocks;
using Xunit;

namespace Ouroboros.Tests.Tests.Autonomous;

/// <summary>
/// Integration tests for message filtering in the neural network.
/// </summary>
public sealed class MessageFilterIntegrationTests
{
    [Fact]
    public async Task CompositeFilter_WithMultipleFilters_AllPass_ShouldRouteMessage()
    {
        // Arrange
        var filter1 = new TestMessageFilter(shouldRoute: true);
        var filter2 = new TestMessageFilter(shouldRoute: true);
        var filter3 = new TestMessageFilter(shouldRoute: true);

        var compositeFilter = new CompositeMessageFilter(new[] { filter1, filter2, filter3 });
        var message = CreateTestMessage("test.topic");

        // Act
        var result = await compositeFilter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeTrue();
        filter1.CallCount.Should().Be(1);
        filter2.CallCount.Should().Be(1);
        filter3.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task CompositeFilter_WithMultipleFilters_OneBlocks_ShouldNotRouteMessage()
    {
        // Arrange
        var filter1 = new TestMessageFilter(shouldRoute: true);
        var filter2 = new TestMessageFilter(shouldRoute: false); // This one blocks
        var filter3 = new TestMessageFilter(shouldRoute: true);

        var compositeFilter = new CompositeMessageFilter(new[] { filter1, filter2, filter3 });
        var message = CreateTestMessage("test.topic");

        // Act
        var result = await compositeFilter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeFalse();
        filter1.CallCount.Should().Be(1);
        filter2.CallCount.Should().Be(1);
        filter3.CallCount.Should().Be(0, "should short-circuit after first failure");
    }

    [Fact]
    public async Task CompositeFilter_WithNoFilters_ShouldRouteMessage()
    {
        // Arrange
        var compositeFilter = new CompositeMessageFilter(Array.Empty<IMessageFilter>());
        var message = CreateTestMessage("test.topic");

        // Act
        var result = await compositeFilter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeTrue("empty filter list should allow all messages");
    }

    [Fact]
    public void CompositeFilter_WithNullFilters_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CompositeMessageFilter(null!));
    }

    [Fact]
    public async Task NeuralNetwork_WithNoFilters_ShouldRouteAllMessages()
    {
        // Arrange
        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        var testNeuron = new TestNeuron("test-neuron");

        network.RegisterNeuron(testNeuron);
        network.Start();

        // Act
        var message = CreateTestMessage("test.topic", "test-neuron");
        var waitTask = testNeuron.WaitForMessageAsync(500);
        network.RouteMessage(message);

        // Wait for message to be processed
        await waitTask;

        // Assert
        testNeuron.ReceivedMessages.Should().ContainSingle();

        // Cleanup
        await network.StopAsync();
        network.Dispose();
    }

    [Fact]
    public async Task NeuralNetwork_WithEthicsFilter_SafeTopic_ShouldRouteWithoutEthicsEvaluation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EthicsMessageFilter>>();
        var mockFramework = new MockEthicsFramework();
        var ethicsFilter = new EthicsMessageFilter(mockFramework, mockLogger.Object);

        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        network.SetMessageFilters(new[] { ethicsFilter });

        var testNeuron = new TestNeuron("test-neuron");
        network.RegisterNeuron(testNeuron);
        network.Start();

        // Act
        var message = CreateTestMessage("reflection.request", "test-neuron");
        var waitTask = testNeuron.WaitForMessageAsync(500);
        network.RouteMessage(message);

        // Wait for message to be processed
        await waitTask;

        // Assert
        testNeuron.ReceivedMessages.Should().ContainSingle();
        mockFramework.EvaluateActionCallCount.Should().Be(0, "safe topics should bypass ethics");

        // Cleanup
        await network.StopAsync();
        network.Dispose();
    }

    [Fact]
    public async Task NeuralNetwork_WithEthicsFilter_PermittedTopic_ShouldRoute()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EthicsMessageFilter>>();
        var mockFramework = new MockEthicsFramework((action, context) =>
            EthicalClearance.Permitted("Action is permitted"));
        var ethicsFilter = new EthicsMessageFilter(mockFramework, mockLogger.Object);

        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        network.SetMessageFilters(new[] { ethicsFilter });

        var testNeuron = new TestNeuron("test-neuron");
        network.RegisterNeuron(testNeuron);
        network.Start();

        // Act
        var message = CreateTestMessage("custom.action", "test-neuron");
        var waitTask = testNeuron.WaitForMessageAsync(1000);
        network.RouteMessage(message);

        // Wait for message to be processed
        await waitTask;

        // Assert
        testNeuron.ReceivedMessages.Should().ContainSingle();
        mockFramework.EvaluateActionCallCount.Should().Be(1);

        // Cleanup
        await network.StopAsync();
        network.Dispose();
    }

    [Fact]
    public async Task NeuralNetwork_WithEthicsFilter_BlockedTopic_ShouldNotRoute()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EthicsMessageFilter>>();
        var violation = new EthicalViolation
        {
            ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
            Description = "Action would cause harm",
            Severity = ViolationSeverity.High,
            Evidence = "Test evidence",
            AffectedParties = new[] { "Users" }
        };

        var mockFramework = new MockEthicsFramework((action, context) =>
            EthicalClearance.Denied("Action is denied", new[] { violation }));
        var ethicsFilter = new EthicsMessageFilter(mockFramework, mockLogger.Object);

        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        network.SetMessageFilters(new[] { ethicsFilter });

        var testNeuron = new TestNeuron("test-neuron");
        network.RegisterNeuron(testNeuron);
        network.Start();

        // Act
        var message = CreateTestMessage("dangerous.action", "test-neuron");
        network.RouteMessage(message);

        // Wait a bit to ensure the message would have been delivered if it wasn't blocked
        await Task.Delay(500);

        // Assert
        testNeuron.ReceivedMessages.Should().BeEmpty("blocked messages should not be delivered");
        mockFramework.EvaluateActionCallCount.Should().Be(1);

        // Cleanup
        await network.StopAsync();
        network.Dispose();
    }

    [Fact]
    public async Task NeuralNetwork_WithMultipleFilters_AllPass_ShouldRoute()
    {
        // Arrange
        var filter1 = new TestMessageFilter(shouldRoute: true);
        var filter2 = new TestMessageFilter(shouldRoute: true);

        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        network.SetMessageFilters(new[] { filter1, filter2 });

        var testNeuron = new TestNeuron("test-neuron");
        network.RegisterNeuron(testNeuron);
        network.Start();

        // Act
        var message = CreateTestMessage("test.topic", "test-neuron");
        var waitTask = testNeuron.WaitForMessageAsync(1000);
        network.RouteMessage(message);

        // Wait for message to be processed
        await waitTask;

        // Assert
        testNeuron.ReceivedMessages.Should().ContainSingle();
        filter1.CallCount.Should().Be(1);
        filter2.CallCount.Should().Be(1);

        // Cleanup
        await network.StopAsync();
        network.Dispose();
    }

    [Fact]
    public async Task NeuralNetwork_WithMultipleFilters_OneBlocks_ShouldNotRoute()
    {
        // Arrange
        var filter1 = new TestMessageFilter(shouldRoute: true);
        var filter2 = new TestMessageFilter(shouldRoute: false); // This one blocks

        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        network.SetMessageFilters(new[] { filter1, filter2 });

        var testNeuron = new TestNeuron("test-neuron");
        network.RegisterNeuron(testNeuron);
        network.Start();

        // Act
        var message = CreateTestMessage("test.topic", "test-neuron");
        network.RouteMessage(message);

        // Wait a bit to ensure the message would have been delivered if it wasn't blocked
        await Task.Delay(500);

        // Assert
        testNeuron.ReceivedMessages.Should().BeEmpty();

        // Cleanup
        await network.StopAsync();
        network.Dispose();
    }

    [Fact]
    public async Task NeuralNetwork_ClearFilters_ShouldRouteAllMessages()
    {
        // Arrange
        var blockingFilter = new TestMessageFilter(shouldRoute: false);

        var intentionBus = new IntentionBus();
        var network = new OuroborosNeuralNetwork(intentionBus);
        network.SetMessageFilters(new[] { blockingFilter });

        var testNeuron = new TestNeuron("test-neuron");
        network.RegisterNeuron(testNeuron);
        network.Start();

        // Act - First message should be blocked
        var message1 = CreateTestMessage("test.topic", "test-neuron");
        network.RouteMessage(message1);
        await Task.Delay(500);

        testNeuron.ReceivedMessages.Should().BeEmpty();

        // Clear filters
        network.SetMessageFilters(null);

        // Second message should go through
        var message2 = CreateTestMessage("test.topic", "test-neuron");
        var waitTask = testNeuron.WaitForMessageAsync(500);
        network.RouteMessage(message2);

        // Wait for message to be processed
        await waitTask;

        // Assert
        testNeuron.ReceivedMessages.Should().ContainSingle();

        // Cleanup
        await network.StopAsync();
        network.Dispose();
    }

    private static NeuronMessage CreateTestMessage(string topic, string? targetNeuron = null)
    {
        return new NeuronMessage
        {
            Id = Guid.NewGuid(),
            SourceNeuron = "source-neuron",
            TargetNeuron = targetNeuron,
            Topic = topic,
            Payload = new { test = "data" },
            Priority = IntentionPriority.Normal,
            CreatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Test message filter for testing composite filtering.
    /// </summary>
    private sealed class TestMessageFilter : IMessageFilter
    {
        private readonly bool _shouldRoute;

        public TestMessageFilter(bool shouldRoute)
        {
            _shouldRoute = shouldRoute;
        }

        public int CallCount { get; private set; }

        public Task<bool> ShouldRouteAsync(NeuronMessage message, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_shouldRoute);
        }
    }

    /// <summary>
    /// Test neuron for testing message delivery.
    /// </summary>
    private sealed class TestNeuron : Neuron
    {
        private static readonly IReadOnlySet<string> EmptyTopics = new HashSet<string>();
        private readonly List<NeuronMessage> _receivedMessages = new();
        private TaskCompletionSource<bool>? _messageReceivedSignal;

        public TestNeuron(string id)
        {
            Id = id;
        }

        public override string Id { get; }

        public override string Name => "Test Neuron";

        public override NeuronType Type => NeuronType.Custom;

        public override IReadOnlySet<string> SubscribedTopics => EmptyTopics;

        public IReadOnlyList<NeuronMessage> ReceivedMessages => _receivedMessages;

        /// <summary>
        /// Sets up a signal that will be completed when a message is received.
        /// </summary>
        public Task WaitForMessageAsync(int timeoutMs = 1000)
        {
            _messageReceivedSignal = new TaskCompletionSource<bool>();
            return Task.WhenAny(_messageReceivedSignal.Task, Task.Delay(timeoutMs));
        }

        protected override Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
        {
            _receivedMessages.Add(message);
            _messageReceivedSignal?.TrySetResult(true);
            return Task.CompletedTask;
        }
    }
}
