// <copyright file="MultiAgentTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MultiAgent;

using System;
using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;
using Xunit;

/// <summary>
/// Comprehensive tests for the multi-agent collaboration system.
/// </summary>
[Trait("Category", "Unit")]
public class MultiAgentTests
{
    #region AgentCapability Tests

    /// <summary>
    /// Tests that AgentCapability.Create correctly sets all properties.
    /// </summary>
    [Fact]
    public void AgentCapability_Create_ShouldSetProperties()
    {
        // Arrange
        string name = "coding";
        string description = "Ability to write code";
        double proficiency = 0.85;
        string[] tools = new[] { "compiler", "debugger" };

        // Act
        AgentCapability capability = AgentCapability.Create(name, description, proficiency, tools);

        // Assert
        capability.Name.Should().Be(name);
        capability.Description.Should().Be(description);
        capability.Proficiency.Should().Be(proficiency);
        capability.RequiredTools.Should().HaveCount(2);
        capability.RequiredTools.Should().Contain("compiler");
        capability.RequiredTools.Should().Contain("debugger");
    }

    /// <summary>
    /// Tests that AgentCapability.Create throws for out-of-range proficiency values.
    /// </summary>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void AgentCapability_Create_ShouldThrowForInvalidProficiency(double proficiency)
    {
        // Arrange
        string name = "coding";
        string description = "Ability to write code";

        // Act
        Action act = () => AgentCapability.Create(name, description, proficiency);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("proficiency");
    }

    /// <summary>
    /// Tests that valid boundary proficiency values are accepted.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(0.5)]
    public void AgentCapability_Create_ShouldAcceptValidProficiency(double proficiency)
    {
        // Arrange
        string name = "coding";
        string description = "Ability to write code";

        // Act
        AgentCapability capability = AgentCapability.Create(name, description, proficiency);

        // Assert
        capability.Proficiency.Should().Be(proficiency);
    }

    #endregion

    #region AgentIdentity Tests

    /// <summary>
    /// Tests that AgentIdentity.Create generates an ID and timestamp.
    /// </summary>
    [Fact]
    public void AgentIdentity_Create_ShouldGenerateIdAndTimestamp()
    {
        // Arrange
        string name = "TestAgent";
        AgentRole role = AgentRole.Coder;
        DateTime beforeCreation = DateTime.UtcNow;

        // Act
        AgentIdentity identity = AgentIdentity.Create(name, role);
        DateTime afterCreation = DateTime.UtcNow;

        // Assert
        identity.Id.Should().NotBe(Guid.Empty);
        identity.Name.Should().Be(name);
        identity.Role.Should().Be(role);
        identity.CreatedAt.Should().BeOnOrAfter(beforeCreation);
        identity.CreatedAt.Should().BeOnOrBefore(afterCreation);
        identity.Capabilities.Should().BeEmpty();
        identity.Metadata.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that WithCapability correctly adds a capability.
    /// </summary>
    [Fact]
    public void AgentIdentity_WithCapability_ShouldAddCapability()
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Coder);
        AgentCapability capability = AgentCapability.Create("coding", "Writing code", 0.9);

        // Act
        AgentIdentity updatedIdentity = identity.WithCapability(capability);

        // Assert
        updatedIdentity.Capabilities.Should().HaveCount(1);
        updatedIdentity.Capabilities[0].Should().Be(capability);
        identity.Capabilities.Should().BeEmpty(); // Original unchanged
    }

    /// <summary>
    /// Tests that WithMetadata correctly adds metadata entries.
    /// </summary>
    [Fact]
    public void AgentIdentity_WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Analyst);

        // Act
        AgentIdentity updatedIdentity = identity
            .WithMetadata("version", "1.0")
            .WithMetadata("priority", 5);

        // Assert
        updatedIdentity.Metadata.Should().HaveCount(2);
        updatedIdentity.Metadata["version"].Should().Be("1.0");
        updatedIdentity.Metadata["priority"].Should().Be(5);
        identity.Metadata.Should().BeEmpty(); // Original unchanged
    }

    /// <summary>
    /// Tests that GetCapability returns Some when capability exists.
    /// </summary>
    [Fact]
    public void AgentIdentity_GetCapability_WhenExists_ShouldReturnSome()
    {
        // Arrange
        AgentCapability capability = AgentCapability.Create("coding", "Writing code", 0.9);
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Coder)
            .WithCapability(capability);

        // Act
        Option<AgentCapability> result = identity.GetCapability("coding");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Name.Should().Be("coding");
    }

    /// <summary>
    /// Tests that GetCapability returns None when capability does not exist.
    /// </summary>
    [Fact]
    public void AgentIdentity_GetCapability_WhenNotExists_ShouldReturnNone()
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Coder);

        // Act
        Option<AgentCapability> result = identity.GetCapability("nonexistent");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests that HasCapability returns correct results.
    /// </summary>
    [Fact]
    public void AgentIdentity_HasCapability_ShouldReturnCorrectResult()
    {
        // Arrange
        AgentCapability capability = AgentCapability.Create("coding", "Writing code", 0.9);
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Coder)
            .WithCapability(capability);

        // Act & Assert
        identity.HasCapability("coding").Should().BeTrue();
        identity.HasCapability("testing").Should().BeFalse();
    }

    /// <summary>
    /// Tests that GetProficiencyFor returns correct proficiency value.
    /// </summary>
    [Fact]
    public void AgentIdentity_GetProficiencyFor_ShouldReturnCorrectValue()
    {
        // Arrange
        AgentCapability capability1 = AgentCapability.Create("coding", "Writing code", 0.9);
        AgentCapability capability2 = AgentCapability.Create("testing", "Writing tests", 0.7);
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Coder)
            .WithCapability(capability1)
            .WithCapability(capability2);

        // Act & Assert
        identity.GetProficiencyFor("coding").Should().Be(0.9);
        identity.GetProficiencyFor("testing").Should().Be(0.7);
        identity.GetProficiencyFor("nonexistent").Should().Be(0.0);
    }

    /// <summary>
    /// Tests that GetCapabilitiesAbove correctly filters by proficiency.
    /// </summary>
    [Fact]
    public void AgentIdentity_GetCapabilitiesAbove_ShouldFilterByProficiency()
    {
        // Arrange
        AgentCapability capability1 = AgentCapability.Create("coding", "Writing code", 0.9);
        AgentCapability capability2 = AgentCapability.Create("testing", "Writing tests", 0.7);
        AgentCapability capability3 = AgentCapability.Create("design", "System design", 0.5);
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Coder)
            .WithCapability(capability1)
            .WithCapability(capability2)
            .WithCapability(capability3);

        // Act
        IReadOnlyList<AgentCapability> result = identity.GetCapabilitiesAbove(0.6);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "coding");
        result.Should().Contain(c => c.Name == "testing");
        result.Should().NotContain(c => c.Name == "design");
    }

    #endregion

    #region AgentState Tests

    /// <summary>
    /// Tests that AgentState.ForAgent creates an idle state.
    /// </summary>
    [Fact]
    public void AgentState_ForAgent_ShouldCreateIdleState()
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Executor);

        // Act
        AgentState state = AgentState.ForAgent(identity);

        // Assert
        state.Identity.Should().Be(identity);
        state.Status.Should().Be(AgentStatus.Idle);
        state.CurrentTaskId.HasValue.Should().BeFalse();
        state.CompletedTasks.Should().Be(0);
        state.FailedTasks.Should().Be(0);
        state.IsAvailable.Should().BeTrue();
    }

    /// <summary>
    /// Tests that StartTask updates the status correctly.
    /// </summary>
    [Fact]
    public void AgentState_StartTask_ShouldUpdateStatus()
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Executor);
        AgentState state = AgentState.ForAgent(identity);
        Guid taskId = Guid.NewGuid();

        // Act
        AgentState updatedState = state.StartTask(taskId);

        // Assert
        updatedState.Status.Should().Be(AgentStatus.Busy);
        updatedState.CurrentTaskId.HasValue.Should().BeTrue();
        updatedState.CurrentTaskId.Value!.Should().Be(taskId);
        updatedState.IsAvailable.Should().BeFalse();
    }

    /// <summary>
    /// Tests that CompleteTask increments the completed counter.
    /// </summary>
    [Fact]
    public void AgentState_CompleteTask_ShouldIncrementCounter()
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Executor);
        AgentState state = AgentState.ForAgent(identity)
            .StartTask(Guid.NewGuid());

        // Act
        AgentState completedState = state.CompleteTask();

        // Assert
        completedState.Status.Should().Be(AgentStatus.Idle);
        completedState.CurrentTaskId.HasValue.Should().BeFalse();
        completedState.CompletedTasks.Should().Be(1);
        completedState.FailedTasks.Should().Be(0);
        completedState.IsAvailable.Should().BeTrue();
    }

    /// <summary>
    /// Tests that FailTask increments the failed counter.
    /// </summary>
    [Fact]
    public void AgentState_FailTask_ShouldIncrementFailedCounter()
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Executor);
        AgentState state = AgentState.ForAgent(identity)
            .StartTask(Guid.NewGuid());

        // Act
        AgentState failedState = state.FailTask();

        // Assert
        failedState.Status.Should().Be(AgentStatus.Error);
        failedState.CurrentTaskId.HasValue.Should().BeFalse();
        failedState.CompletedTasks.Should().Be(0);
        failedState.FailedTasks.Should().Be(1);
    }

    /// <summary>
    /// Tests that SuccessRate calculates correctly.
    /// </summary>
    [Theory]
    [InlineData(10, 0, 1.0)]
    [InlineData(8, 2, 0.8)]
    [InlineData(5, 5, 0.5)]
    [InlineData(0, 10, 0.0)]
    [InlineData(0, 0, 1.0)]
    public void AgentState_SuccessRate_ShouldCalculateCorrectly(int completed, int failed, double expected)
    {
        // Arrange
        AgentIdentity identity = AgentIdentity.Create("TestAgent", AgentRole.Executor);
        AgentState state = new AgentState(
            identity,
            AgentStatus.Idle,
            Option<Guid>.None(),
            completed,
            failed,
            DateTime.UtcNow);

        // Act
        double successRate = state.SuccessRate;

        // Assert
        successRate.Should().BeApproximately(expected, 0.0001);
    }

    #endregion

    #region AgentMessage Tests

    /// <summary>
    /// Tests that CreateRequest sets properties correctly.
    /// </summary>
    [Fact]
    public void AgentMessage_CreateRequest_ShouldSetProperties()
    {
        // Arrange
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();
        string topic = "task.execute";
        object payload = "Execute this task";

        // Act
        AgentMessage message = AgentMessage.CreateRequest(senderId, receiverId, topic, payload);

        // Assert
        message.Id.Should().NotBe(Guid.Empty);
        message.SenderId.Should().Be(senderId);
        message.ReceiverId.Should().Be(receiverId);
        message.Type.Should().Be(MessageType.Request);
        message.Priority.Should().Be(MessagePriority.Normal);
        message.Topic.Should().Be(topic);
        message.Payload.Should().Be(payload);
        message.CorrelationId.Should().NotBeNull();
        message.CorrelationId.Should().Be(message.Id);
        message.IsRequest.Should().BeTrue();
    }

    /// <summary>
    /// Tests that CreateResponse links to the original request.
    /// </summary>
    [Fact]
    public void AgentMessage_CreateResponse_ShouldLinkToRequest()
    {
        // Arrange
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();
        AgentMessage request = AgentMessage.CreateRequest(senderId, receiverId, "topic", "request payload");
        object responsePayload = "response data";

        // Act
        AgentMessage response = AgentMessage.CreateResponse(request, responsePayload);

        // Assert
        response.Id.Should().NotBe(request.Id);
        response.SenderId.Should().Be(receiverId);
        response.ReceiverId.Should().Be(senderId);
        response.Type.Should().Be(MessageType.Response);
        response.Topic.Should().Be(request.Topic);
        response.Payload.Should().Be(responsePayload);
        response.CorrelationId.Should().Be(request.CorrelationId);
    }

    /// <summary>
    /// Tests that CreateBroadcast has null receiver.
    /// </summary>
    [Fact]
    public void AgentMessage_CreateBroadcast_ShouldHaveNullReceiver()
    {
        // Arrange
        Guid senderId = Guid.NewGuid();
        string topic = "global.announcement";
        object payload = "Broadcast message";

        // Act
        AgentMessage message = AgentMessage.CreateBroadcast(senderId, topic, payload);

        // Assert
        message.ReceiverId.Should().BeNull();
        message.Type.Should().Be(MessageType.Broadcast);
        message.IsBroadcast.Should().BeTrue();
        message.CorrelationId.Should().BeNull();
    }

    #endregion

    #region InMemoryMessageBus Tests

    /// <summary>
    /// Tests that PublishAsync delivers messages to subscribers.
    /// </summary>
    [Fact]
    public async Task InMemoryMessageBus_PublishAsync_ShouldDeliverToSubscribers()
    {
        // Arrange
        using InMemoryMessageBus bus = new InMemoryMessageBus();
        Guid agentId = Guid.NewGuid();
        AgentMessage? receivedMessage = null;
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

        Subscription subscription = bus.Subscribe(agentId, null, async msg =>
        {
            receivedMessage = msg;
            tcs.SetResult(true);
            await Task.CompletedTask;
        });

        AgentMessage message = AgentMessage.CreateBroadcast(Guid.NewGuid(), "test.topic", "payload");

        // Act
        await bus.PublishAsync(message);
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Payload.Should().Be("payload");
    }

    /// <summary>
    /// Tests that RequestAsync receives the response.
    /// </summary>
    [Fact]
    public async Task InMemoryMessageBus_RequestAsync_ShouldReceiveResponse()
    {
        // Arrange
        using InMemoryMessageBus bus = new InMemoryMessageBus();
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();

        // Subscribe receiver to respond
        Subscription subscription = bus.Subscribe(receiverId, null, async request =>
        {
            if (request.Type == MessageType.Request)
            {
                AgentMessage response = AgentMessage.CreateResponse(request, "response payload");
                await bus.PublishAsync(response);
            }
        });

        AgentMessage request = AgentMessage.CreateRequest(senderId, receiverId, "test", "request payload");

        // Act
        AgentMessage response = await bus.RequestAsync(request, TimeSpan.FromSeconds(5));

        // Assert
        response.Type.Should().Be(MessageType.Response);
        response.Payload.Should().Be("response payload");
        response.CorrelationId.Should().Be(request.CorrelationId);
    }

    /// <summary>
    /// Tests that Subscribe filters messages by topic.
    /// </summary>
    [Fact]
    public async Task InMemoryMessageBus_Subscribe_ShouldFilterByTopic()
    {
        // Arrange
        using InMemoryMessageBus bus = new InMemoryMessageBus();
        Guid agentId = Guid.NewGuid();
        List<AgentMessage> receivedMessages = new List<AgentMessage>();
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        int expectedCount = 0;

        Subscription subscription = bus.Subscribe(agentId, "specific.topic", async msg =>
        {
            receivedMessages.Add(msg);
            if (receivedMessages.Count >= 1)
            {
                tcs.TrySetResult(true);
            }

            await Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "other.topic", "should not receive"));
        await bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "specific.topic", "should receive"));
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        // Assert
        receivedMessages.Should().HaveCount(1);
        receivedMessages[0].Topic.Should().Be("specific.topic");
    }

    /// <summary>
    /// Tests that Unsubscribe stops message delivery.
    /// </summary>
    [Fact]
    public async Task InMemoryMessageBus_Unsubscribe_ShouldStopDelivery()
    {
        // Arrange
        using InMemoryMessageBus bus = new InMemoryMessageBus();
        Guid agentId = Guid.NewGuid();
        int messageCount = 0;

        Subscription subscription = bus.Subscribe(agentId, null, async msg =>
        {
            Interlocked.Increment(ref messageCount);
            await Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "first"));
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        bus.Unsubscribe(subscription.Id);

        await bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "topic", "second"));
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        // Assert
        messageCount.Should().Be(1);
    }

    /// <summary>
    /// Tests that GetPendingMessages returns undelivered messages.
    /// </summary>
    [Fact]
    public async Task InMemoryMessageBus_GetPendingMessages_ShouldReturnUndelivered()
    {
        // Arrange
        using InMemoryMessageBus bus = new InMemoryMessageBus();
        Guid agentId = Guid.NewGuid();

        // Send message to agent without subscriber
        AgentMessage message = new AgentMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            agentId,
            MessageType.Notification,
            MessagePriority.Normal,
            "test",
            "payload",
            DateTime.UtcNow,
            null);

        await bus.PublishAsync(message);
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        // Act
        IReadOnlyList<AgentMessage> pendingMessages = bus.GetPendingMessages(agentId);

        // Assert
        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Payload.Should().Be("payload");
    }

    /// <summary>
    /// Tests that broadcasts deliver to all subscribers.
    /// </summary>
    [Fact]
    public async Task InMemoryMessageBus_Broadcast_ShouldDeliverToAllSubscribers()
    {
        // Arrange
        using InMemoryMessageBus bus = new InMemoryMessageBus();
        int receiveCount = 0;
        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

        Subscription sub1 = bus.Subscribe(Guid.NewGuid(), "broadcast", async msg =>
        {
            Interlocked.Increment(ref receiveCount);
            if (receiveCount >= 3)
            {
                tcs.TrySetResult(true);
            }

            await Task.CompletedTask;
        });

        Subscription sub2 = bus.Subscribe(Guid.NewGuid(), "broadcast", async msg =>
        {
            Interlocked.Increment(ref receiveCount);
            if (receiveCount >= 3)
            {
                tcs.TrySetResult(true);
            }

            await Task.CompletedTask;
        });

        Subscription sub3 = bus.Subscribe(Guid.NewGuid(), "broadcast", async msg =>
        {
            Interlocked.Increment(ref receiveCount);
            if (receiveCount >= 3)
            {
                tcs.TrySetResult(true);
            }

            await Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(AgentMessage.CreateBroadcast(Guid.NewGuid(), "broadcast", "message"));
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        receiveCount.Should().Be(3);
    }

    /// <summary>
    /// Tests that RequestAsync times out when no response is received.
    /// </summary>
    [Fact]
    public async Task InMemoryMessageBus_RequestAsync_ShouldTimeout()
    {
        // Arrange
        using InMemoryMessageBus bus = new InMemoryMessageBus();
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();

        AgentMessage request = AgentMessage.CreateRequest(senderId, receiverId, "test", "payload");

        // Act
        Func<Task> act = async () => await bus.RequestAsync(request, TimeSpan.FromMilliseconds(100));

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    /// <summary>
    /// Tests that Dispose cleans up resources properly.
    /// </summary>
    [Fact]
    public void InMemoryMessageBus_Dispose_ShouldCleanupResources()
    {
        // Arrange
        InMemoryMessageBus bus = new InMemoryMessageBus();
        Subscription sub = bus.Subscribe(Guid.NewGuid(), null, _ => Task.CompletedTask);

        // Act
        bus.Dispose();

        // Assert
        Action publishAct = () => bus.PublishAsync(
            AgentMessage.CreateBroadcast(Guid.NewGuid(), "test", "payload"))
            .GetAwaiter().GetResult();
        publishAct.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region AgentTeam Tests

    /// <summary>
    /// Tests that Empty team has no agents.
    /// </summary>
    [Fact]
    public void AgentTeam_Empty_ShouldHaveNoAgents()
    {
        // Act
        AgentTeam team = AgentTeam.Empty;

        // Assert
        team.Count.Should().Be(0);
        team.GetAllAgents().Should().BeEmpty();
    }

    /// <summary>
    /// Tests that AddAgent increases the count.
    /// </summary>
    [Fact]
    public void AgentTeam_AddAgent_ShouldIncreaseCount()
    {
        // Arrange
        AgentTeam team = AgentTeam.Empty;
        AgentIdentity agent1 = AgentIdentity.Create("Agent1", AgentRole.Coder);
        AgentIdentity agent2 = AgentIdentity.Create("Agent2", AgentRole.Analyst);

        // Act
        AgentTeam updatedTeam = team
            .AddAgent(agent1)
            .AddAgent(agent2);

        // Assert
        updatedTeam.Count.Should().Be(2);
        team.Count.Should().Be(0); // Original unchanged
    }

    /// <summary>
    /// Tests that RemoveAgent decreases the count.
    /// </summary>
    [Fact]
    public void AgentTeam_RemoveAgent_ShouldDecreaseCount()
    {
        // Arrange
        AgentIdentity agent1 = AgentIdentity.Create("Agent1", AgentRole.Coder);
        AgentIdentity agent2 = AgentIdentity.Create("Agent2", AgentRole.Analyst);
        AgentTeam team = AgentTeam.Empty
            .AddAgent(agent1)
            .AddAgent(agent2);

        // Act
        AgentTeam updatedTeam = team.RemoveAgent(agent1.Id);

        // Assert
        updatedTeam.Count.Should().Be(1);
        team.Count.Should().Be(2); // Original unchanged
    }

    /// <summary>
    /// Tests that GetAgent returns Some when agent exists.
    /// </summary>
    [Fact]
    public void AgentTeam_GetAgent_WhenExists_ShouldReturnSome()
    {
        // Arrange
        AgentIdentity agent = AgentIdentity.Create("TestAgent", AgentRole.Coder);
        AgentTeam team = AgentTeam.Empty.AddAgent(agent);

        // Act
        Option<AgentState> result = team.GetAgent(agent.Id);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Identity.Should().Be(agent);
    }

    /// <summary>
    /// Tests that GetAvailableAgents filters by status.
    /// </summary>
    [Fact]
    public void AgentTeam_GetAvailableAgents_ShouldFilterByStatus()
    {
        // Arrange
        AgentIdentity agent1 = AgentIdentity.Create("Agent1", AgentRole.Executor);
        AgentIdentity agent2 = AgentIdentity.Create("Agent2", AgentRole.Executor);
        AgentTeam team = AgentTeam.Empty
            .AddAgent(agent1)
            .AddAgent(agent2);

        // All agents start as idle/available
        // Act
        IReadOnlyList<AgentState> availableAgents = team.GetAvailableAgents();

        // Assert
        availableAgents.Should().HaveCount(2);
        availableAgents.Should().OnlyContain(a => a.IsAvailable);
    }

    /// <summary>
    /// Tests that GetAgentsWithCapability filters correctly.
    /// </summary>
    [Fact]
    public void AgentTeam_GetAgentsWithCapability_ShouldFilterCorrectly()
    {
        // Arrange
        AgentCapability codingCapability = AgentCapability.Create("coding", "Writing code", 0.9);
        AgentCapability testingCapability = AgentCapability.Create("testing", "Writing tests", 0.8);

        AgentIdentity agent1 = AgentIdentity.Create("Coder", AgentRole.Coder)
            .WithCapability(codingCapability);
        AgentIdentity agent2 = AgentIdentity.Create("Tester", AgentRole.Reviewer)
            .WithCapability(testingCapability);
        AgentIdentity agent3 = AgentIdentity.Create("FullStack", AgentRole.Coder)
            .WithCapability(codingCapability)
            .WithCapability(testingCapability);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(agent1)
            .AddAgent(agent2)
            .AddAgent(agent3);

        // Act
        IReadOnlyList<AgentState> codingAgents = team.GetAgentsWithCapability("coding");
        IReadOnlyList<AgentState> testingAgents = team.GetAgentsWithCapability("testing");

        // Assert
        codingAgents.Should().HaveCount(2);
        testingAgents.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests that GetAgentsByRole filters correctly.
    /// </summary>
    [Fact]
    public void AgentTeam_GetAgentsByRole_ShouldFilterCorrectly()
    {
        // Arrange
        AgentIdentity coder1 = AgentIdentity.Create("Coder1", AgentRole.Coder);
        AgentIdentity coder2 = AgentIdentity.Create("Coder2", AgentRole.Coder);
        AgentIdentity analyst = AgentIdentity.Create("Analyst", AgentRole.Analyst);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(coder1)
            .AddAgent(coder2)
            .AddAgent(analyst);

        // Act
        IReadOnlyList<AgentState> coders = team.GetAgentsByRole(AgentRole.Coder);
        IReadOnlyList<AgentState> analysts = team.GetAgentsByRole(AgentRole.Analyst);

        // Assert
        coders.Should().HaveCount(2);
        analysts.Should().HaveCount(1);
    }

    #endregion

    #region ConsensusProtocol Tests

    /// <summary>
    /// Tests that AgentVote.Create validates confidence range.
    /// </summary>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void AgentVote_Create_ShouldValidateConfidence(double confidence)
    {
        // Arrange
        Guid agentId = Guid.NewGuid();

        // Act
        Action act = () => AgentVote.Create(agentId, "OptionA", confidence);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("confidence");
    }

    /// <summary>
    /// Tests that Majority protocol requires over fifty percent.
    /// </summary>
    [Fact]
    public void ConsensusProtocol_Majority_ShouldRequireOverFiftyPercent()
    {
        // Arrange
        ConsensusProtocol protocol = ConsensusProtocol.Majority;
        List<AgentVote> votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.9),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8),
            AgentVote.Create(Guid.NewGuid(), "OptionB", 0.7),
        };

        // Act
        ConsensusResult result = protocol.Evaluate(votes);

        // Assert
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("OptionA");
    }

    /// <summary>
    /// Tests that SuperMajority protocol requires over sixty percent.
    /// </summary>
    [Fact]
    public void ConsensusProtocol_SuperMajority_ShouldRequireOverSixtyPercent()
    {
        // Arrange - 66.67% threshold requires more than 2/3
        ConsensusProtocol protocol = ConsensusProtocol.SuperMajority;
        List<AgentVote> votesSuccess = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.9),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.7),
        };
        List<AgentVote> votesFail = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.9),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8),
            AgentVote.Create(Guid.NewGuid(), "OptionB", 0.7),
        };

        // Act
        ConsensusResult resultSuccess = protocol.Evaluate(votesSuccess);
        ConsensusResult resultFail = protocol.Evaluate(votesFail);

        // Assert
        resultSuccess.HasConsensus.Should().BeTrue();
        resultFail.HasConsensus.Should().BeFalse();
    }

    /// <summary>
    /// Tests that Unanimous protocol requires all votes to agree.
    /// </summary>
    [Fact]
    public void ConsensusProtocol_Unanimous_ShouldRequireAllAgree()
    {
        // Arrange
        ConsensusProtocol protocol = ConsensusProtocol.Unanimous;
        List<AgentVote> unanimousVotes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.9),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.7),
        };
        List<AgentVote> nonUnanimousVotes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.9),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8),
            AgentVote.Create(Guid.NewGuid(), "OptionB", 0.7),
        };

        // Act
        ConsensusResult unanimousResult = protocol.Evaluate(unanimousVotes);
        ConsensusResult nonUnanimousResult = protocol.Evaluate(nonUnanimousVotes);

        // Assert
        unanimousResult.HasConsensus.Should().BeTrue();
        unanimousResult.WinningOption.Should().Be("OptionA");
        nonUnanimousResult.HasConsensus.Should().BeFalse();
    }

    /// <summary>
    /// Tests that WeightedByConfidence protocol weights votes by confidence.
    /// </summary>
    [Fact]
    public void ConsensusProtocol_WeightedByConfidence_ShouldWeightVotes()
    {
        // Arrange
        ConsensusProtocol protocol = ConsensusProtocol.WeightedByConfidence;

        // OptionA: 0.9 total confidence
        // OptionB: 0.3 + 0.3 + 0.3 = 0.9 total confidence but from 3 votes
        // With equal confidence, the one found first or with higher individual might win
        List<AgentVote> votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.9),
            AgentVote.Create(Guid.NewGuid(), "OptionB", 0.05),
            AgentVote.Create(Guid.NewGuid(), "OptionB", 0.05),
        };

        // Act
        ConsensusResult result = protocol.Evaluate(votes);

        // Assert - OptionA should win because 0.9/(0.9+0.1) = 0.9 > 0.5 threshold
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("OptionA");
    }

    /// <summary>
    /// Tests that HighestConfidence protocol selects the vote with top confidence.
    /// </summary>
    [Fact]
    public void ConsensusProtocol_HighestConfidence_ShouldSelectTopConfidence()
    {
        // Arrange
        ConsensusProtocol protocol = ConsensusProtocol.HighestConfidence;
        List<AgentVote> votes = new List<AgentVote>
        {
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.7),
            AgentVote.Create(Guid.NewGuid(), "OptionB", 0.95),
            AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8),
        };

        // Act
        ConsensusResult result = protocol.Evaluate(votes);

        // Assert
        result.HasConsensus.Should().BeTrue();
        result.WinningOption.Should().Be("OptionB");
        result.AggregateConfidence.Should().Be(0.95);
    }

    /// <summary>
    /// Tests that ParticipationRate calculates correctly.
    /// </summary>
    [Theory]
    [InlineData(3, 10, 0.3)]
    [InlineData(5, 5, 1.0)]
    [InlineData(1, 4, 0.25)]
    public void ConsensusResult_ParticipationRate_ShouldCalculateCorrectly(int votes, int totalAgents, double expected)
    {
        // Arrange
        List<AgentVote> voteList = Enumerable.Range(0, votes)
            .Select(_ => AgentVote.Create(Guid.NewGuid(), "Option", 0.8))
            .ToList();
        ConsensusResult result = ConsensusProtocol.Majority.Evaluate(voteList);

        // Act
        double participationRate = result.ParticipationRate(totalAgents);

        // Assert
        participationRate.Should().BeApproximately(expected, 0.001);
    }

    #endregion

    #region VotingSession Tests

    /// <summary>
    /// Tests that CastVote records the vote.
    /// </summary>
    [Fact]
    public void VotingSession_CastVote_ShouldRecordVote()
    {
        // Arrange
        IReadOnlyList<string> options = new List<string> { "OptionA", "OptionB" };
        VotingSession session = VotingSession.Create("TestTopic", options, ConsensusProtocol.Majority);
        AgentVote vote = AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8);

        // Act
        session.CastVote(vote);

        // Assert
        session.VoteCount.Should().Be(1);
        session.GetVotes().Should().Contain(vote);
    }

    /// <summary>
    /// Tests that HasVoted tracks voters.
    /// </summary>
    [Fact]
    public void VotingSession_HasVoted_ShouldTrackVoters()
    {
        // Arrange
        IReadOnlyList<string> options = new List<string> { "OptionA", "OptionB" };
        VotingSession session = VotingSession.Create("TestTopic", options, ConsensusProtocol.Majority);
        Guid agentId = Guid.NewGuid();
        AgentVote vote = AgentVote.Create(agentId, "OptionA", 0.8);

        // Act
        bool beforeVote = session.HasVoted(agentId);
        session.CastVote(vote);
        bool afterVote = session.HasVoted(agentId);

        // Assert
        beforeVote.Should().BeFalse();
        afterVote.Should().BeTrue();
    }

    /// <summary>
    /// Tests that TryGetResult returns consensus when reached.
    /// </summary>
    [Fact]
    public void VotingSession_TryGetResult_ShouldReturnConsensus()
    {
        // Arrange
        IReadOnlyList<string> options = new List<string> { "OptionA", "OptionB" };
        VotingSession session = VotingSession.Create("TestTopic", options, ConsensusProtocol.Majority);
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "OptionA", 0.9));
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "OptionA", 0.8));
        session.CastVote(AgentVote.Create(Guid.NewGuid(), "OptionB", 0.7));

        // Act
        Option<ConsensusResult> result = session.TryGetResult();

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.HasConsensus.Should().BeTrue();
        result.Value!.WinningOption.Should().Be("OptionA");
    }

    /// <summary>
    /// Tests that CastVote rejects duplicate votes from the same agent.
    /// </summary>
    [Fact]
    public void VotingSession_CastVote_ShouldRejectDuplicates()
    {
        // Arrange
        IReadOnlyList<string> options = new List<string> { "OptionA", "OptionB" };
        VotingSession session = VotingSession.Create("TestTopic", options, ConsensusProtocol.Majority);
        Guid agentId = Guid.NewGuid();
        session.CastVote(AgentVote.Create(agentId, "OptionA", 0.8));

        // Act
        Action act = () => session.CastVote(AgentVote.Create(agentId, "OptionB", 0.9));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{agentId}*already voted*");
    }

    /// <summary>
    /// Tests that CastVote rejects invalid options.
    /// </summary>
    [Fact]
    public void VotingSession_CastVote_ShouldRejectInvalidOption()
    {
        // Arrange
        IReadOnlyList<string> options = new List<string> { "OptionA", "OptionB" };
        VotingSession session = VotingSession.Create("TestTopic", options, ConsensusProtocol.Majority);
        AgentVote invalidVote = AgentVote.Create(Guid.NewGuid(), "InvalidOption", 0.8);

        // Act
        Action act = () => session.CastVote(invalidVote);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*InvalidOption*not a valid option*");
    }

    #endregion

    #region DelegationCriteria Tests

    /// <summary>
    /// Tests that FromGoal sets default values.
    /// </summary>
    [Fact]
    public void DelegationCriteria_FromGoal_ShouldSetDefaults()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test goal");

        // Act
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal);

        // Assert
        criteria.Goal.Should().Be(goal);
        criteria.RequiredCapabilities.Should().BeEmpty();
        criteria.MinProficiency.Should().Be(0.0);
        criteria.PreferAvailable.Should().BeTrue();
        criteria.PreferredRole.Should().BeNull();
    }

    /// <summary>
    /// Tests that WithMinProficiency throws for out of range values.
    /// </summary>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void DelegationCriteria_WithMinProficiency_ShouldThrowForInvalidValues(double minProficiency)
    {
        // Arrange
        Goal goal = Goal.Atomic("Test goal");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal);

        // Act
        Action act = () => criteria.WithMinProficiency(minProficiency);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("minProficiency");
    }

    /// <summary>
    /// Tests that WithMinProficiency accepts valid values.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void DelegationCriteria_WithMinProficiency_ShouldAcceptValidValues(double minProficiency)
    {
        // Arrange
        Goal goal = Goal.Atomic("Test goal");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal);

        // Act
        DelegationCriteria updated = criteria.WithMinProficiency(minProficiency);

        // Assert
        updated.MinProficiency.Should().Be(minProficiency);
    }

    #endregion

    #region DelegationStrategy Tests

    /// <summary>
    /// Tests that CapabilityBasedStrategy selects the most proficient agent.
    /// </summary>
    [Fact]
    public void CapabilityBasedStrategy_SelectAgent_ShouldSelectMostProficient()
    {
        // Arrange
        CapabilityBasedStrategy strategy = new CapabilityBasedStrategy();
        AgentCapability highCapability = AgentCapability.Create("coding", "Writing code", 0.95);
        AgentCapability lowCapability = AgentCapability.Create("coding", "Writing code", 0.60);

        AgentIdentity expertAgent = AgentIdentity.Create("Expert", AgentRole.Coder)
            .WithCapability(highCapability);
        AgentIdentity noviceAgent = AgentIdentity.Create("Novice", AgentRole.Coder)
            .WithCapability(lowCapability);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(noviceAgent)
            .AddAgent(expertAgent);

        Goal goal = Goal.Atomic("Write code");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal)
            .RequireCapability("coding");

        // Act
        DelegationResult result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(expertAgent.Id);
    }

    /// <summary>
    /// Tests that CapabilityBasedStrategy returns NoMatch when no agents qualify.
    /// </summary>
    [Fact]
    public void CapabilityBasedStrategy_SelectAgent_WhenNoMatch_ShouldReturnNoMatch()
    {
        // Arrange
        CapabilityBasedStrategy strategy = new CapabilityBasedStrategy();
        AgentIdentity agent = AgentIdentity.Create("Agent", AgentRole.Coder);

        AgentTeam team = AgentTeam.Empty.AddAgent(agent);

        Goal goal = Goal.Atomic("Write code");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal)
            .RequireCapability("nonexistent")
            .WithMinProficiency(0.5);

        // Act
        DelegationResult result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeFalse();
    }

    /// <summary>
    /// Tests that RoleBasedStrategy prefers agents with matching role.
    /// </summary>
    [Fact]
    public void RoleBasedStrategy_SelectAgent_ShouldPreferMatchingRole()
    {
        // Arrange
        RoleBasedStrategy strategy = new RoleBasedStrategy();
        AgentIdentity coder = AgentIdentity.Create("Coder", AgentRole.Coder);
        AgentIdentity analyst = AgentIdentity.Create("Analyst", AgentRole.Analyst);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(analyst)
            .AddAgent(coder);

        Goal goal = Goal.Atomic("Write code");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal)
            .WithPreferredRole(AgentRole.Coder);

        // Act
        DelegationResult result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(coder.Id);
    }

    /// <summary>
    /// Tests that LoadBalancingStrategy selects the least busy agent.
    /// </summary>
    [Fact]
    public void LoadBalancingStrategy_SelectAgent_ShouldSelectLeastBusy()
    {
        // Arrange
        LoadBalancingStrategy strategy = new LoadBalancingStrategy();
        AgentIdentity busyIdentity = AgentIdentity.Create("BusyAgent", AgentRole.Executor);
        AgentIdentity idleIdentity = AgentIdentity.Create("IdleAgent", AgentRole.Executor);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(busyIdentity)
            .AddAgent(idleIdentity);

        Goal goal = Goal.Atomic("Execute task");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal);

        // Both are available so it should balance based on tasks completed
        // Act
        DelegationResult result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.MatchScore.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that RoundRobinStrategy cycles through agents.
    /// </summary>
    [Fact]
    public void RoundRobinStrategy_SelectAgent_ShouldCycleThroughAgents()
    {
        // Arrange
        RoundRobinStrategy strategy = new RoundRobinStrategy();
        AgentIdentity agent1 = AgentIdentity.Create("Agent1", AgentRole.Executor);
        AgentIdentity agent2 = AgentIdentity.Create("Agent2", AgentRole.Executor);
        AgentIdentity agent3 = AgentIdentity.Create("Agent3", AgentRole.Executor);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(agent1)
            .AddAgent(agent2)
            .AddAgent(agent3);

        Goal goal = Goal.Atomic("Execute task");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal);

        // Act
        List<Guid> selectedIds = new List<Guid>();
        for (int i = 0; i < 6; i++)
        {
            DelegationResult result = strategy.SelectAgent(criteria, team);
            if (result.HasMatch)
            {
                selectedIds.Add(result.SelectedAgentId!.Value);
            }
        }

        // Assert - should cycle through all agents
        selectedIds.Should().HaveCount(6);
        selectedIds.Distinct().Should().HaveCount(3);
    }

    /// <summary>
    /// Tests that BestFitStrategy weights all factors.
    /// </summary>
    [Fact]
    public void BestFitStrategy_SelectAgent_ShouldWeightAllFactors()
    {
        // Arrange
        BestFitStrategy strategy = new BestFitStrategy();
        AgentCapability capability = AgentCapability.Create("coding", "Writing code", 0.9);

        AgentIdentity goodFitAgent = AgentIdentity.Create("GoodFit", AgentRole.Coder)
            .WithCapability(capability);
        AgentIdentity poorFitAgent = AgentIdentity.Create("PoorFit", AgentRole.Analyst);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(poorFitAgent)
            .AddAgent(goodFitAgent);

        Goal goal = Goal.Atomic("Write code");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal)
            .RequireCapability("coding")
            .WithPreferredRole(AgentRole.Coder);

        // Act
        DelegationResult result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(goodFitAgent.Id);
        result.Reasoning.Should().Contain("best-fit");
    }

    /// <summary>
    /// Tests that CompositeStrategy combines multiple strategies.
    /// </summary>
    [Fact]
    public void CompositeStrategy_SelectAgent_ShouldCombineStrategies()
    {
        // Arrange
        CompositeStrategy strategy = CompositeStrategy.Create(
            (new CapabilityBasedStrategy(), 0.5),
            (new LoadBalancingStrategy(), 0.5));

        AgentCapability capability = AgentCapability.Create("coding", "Writing code", 0.85);
        AgentIdentity agent = AgentIdentity.Create("Agent", AgentRole.Coder)
            .WithCapability(capability);

        AgentTeam team = AgentTeam.Empty.AddAgent(agent);

        Goal goal = Goal.Atomic("Write code");
        DelegationCriteria criteria = DelegationCriteria.FromGoal(goal)
            .RequireCapability("coding");

        // Act
        DelegationResult result = strategy.SelectAgent(criteria, team);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.Reasoning.Should().Contain("Composite");
    }

    /// <summary>
    /// Tests that DelegationStrategyFactory.ByCapability returns correct strategy.
    /// </summary>
    [Fact]
    public void DelegationStrategyFactory_ByCapability_ShouldReturnCorrectStrategy()
    {
        // Act
        IDelegationStrategy strategy = DelegationStrategyFactory.ByCapability();

        // Assert
        strategy.Should().BeOfType<CapabilityBasedStrategy>();
        strategy.Name.Should().Be("CapabilityBased");
    }

    /// <summary>
    /// Tests that DelegationStrategyFactory.RoundRobin returns correct strategy.
    /// </summary>
    [Fact]
    public void DelegationStrategyFactory_RoundRobin_ShouldReturnCorrectStrategy()
    {
        // Act
        IDelegationStrategy strategy = DelegationStrategyFactory.RoundRobin();

        // Assert
        strategy.Should().BeOfType<RoundRobinStrategy>();
        strategy.Name.Should().Be("RoundRobin");
    }

    /// <summary>
    /// Tests that DelegationResult.Success has a match.
    /// </summary>
    [Fact]
    public void DelegationResult_Success_ShouldHaveMatch()
    {
        // Arrange
        Guid agentId = Guid.NewGuid();

        // Act
        DelegationResult result = DelegationResult.Success(agentId, "Selected agent", 0.85);

        // Assert
        result.HasMatch.Should().BeTrue();
        result.SelectedAgentId.Should().Be(agentId);
        result.MatchScore.Should().Be(0.85);
    }

    /// <summary>
    /// Tests that DelegationResult.NoMatch does not have a match.
    /// </summary>
    [Fact]
    public void DelegationResult_NoMatch_ShouldNotHaveMatch()
    {
        // Act
        DelegationResult result = DelegationResult.NoMatch("No suitable agent found");

        // Assert
        result.HasMatch.Should().BeFalse();
        result.SelectedAgentId.Should().BeNull();
        result.MatchScore.Should().Be(0.0);
    }

    #endregion

    #region AgentTask Tests

    /// <summary>
    /// Tests that AgentTask.Create sets pending status.
    /// </summary>
    [Fact]
    public void AgentTask_Create_ShouldSetPendingStatus()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test task");

        // Act
        AgentTask task = AgentTask.Create(goal);

        // Assert
        task.Id.Should().NotBe(Guid.Empty);
        task.Goal.Should().Be(goal);
        task.Status.Should().Be(TaskStatus.Pending);
        task.AssignedAgentId.Should().BeNull();
        task.Result.HasValue.Should().BeFalse();
        task.Error.HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests that AssignTo sets the agent ID.
    /// </summary>
    [Fact]
    public void AgentTask_AssignTo_ShouldSetAgentId()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test task");
        AgentTask task = AgentTask.Create(goal);
        Guid agentId = Guid.NewGuid();

        // Act
        AgentTask assignedTask = task.AssignTo(agentId);

        // Assert
        assignedTask.AssignedAgentId.Should().Be(agentId);
        assignedTask.Status.Should().Be(TaskStatus.Assigned);
        task.AssignedAgentId.Should().BeNull(); // Original unchanged
    }

    /// <summary>
    /// Tests that Complete sets completed status.
    /// </summary>
    [Fact]
    public void AgentTask_Complete_ShouldSetCompletedStatus()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test task");
        AgentTask task = AgentTask.Create(goal)
            .AssignTo(Guid.NewGuid())
            .Start();
        string resultValue = "Task completed successfully";

        // Act
        AgentTask completedTask = task.Complete(resultValue);

        // Assert
        completedTask.Status.Should().Be(TaskStatus.Completed);
        completedTask.Result.HasValue.Should().BeTrue();
        completedTask.Result.Value!.Should().Be(resultValue);
        completedTask.CompletedAt.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that Fail sets failed status.
    /// </summary>
    [Fact]
    public void AgentTask_Fail_ShouldSetFailedStatus()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test task");
        AgentTask task = AgentTask.Create(goal)
            .AssignTo(Guid.NewGuid())
            .Start();
        string errorMessage = "Task failed due to error";

        // Act
        AgentTask failedTask = task.Fail(errorMessage);

        // Assert
        failedTask.Status.Should().Be(TaskStatus.Failed);
        failedTask.Error.HasValue.Should().BeTrue();
        failedTask.Error.Value!.Should().Be(errorMessage);
        failedTask.CompletedAt.Should().NotBeNull();
    }

    #endregion

    #region CoordinationResult Tests

    /// <summary>
    /// Tests that CoordinationResult.Success calculates metrics correctly.
    /// </summary>
    [Fact]
    public void CoordinationResult_Success_ShouldCalculateMetrics()
    {
        // Arrange
        Goal goal = Goal.Atomic("Test coordination");
        AgentIdentity agent = AgentIdentity.Create("TestAgent", AgentRole.Executor);

        List<AgentTask> tasks = new List<AgentTask>
        {
            AgentTask.Create(Goal.Atomic("Task1")).Complete("result1"),
            AgentTask.Create(Goal.Atomic("Task2")).Complete("result2"),
            AgentTask.Create(Goal.Atomic("Task3")).Fail("error"),
        };

        Dictionary<Guid, AgentIdentity> agents = new Dictionary<Guid, AgentIdentity>
        {
            { agent.Id, agent },
        };

        TimeSpan duration = TimeSpan.FromSeconds(10);

        // Act
        CoordinationResult result = CoordinationResult.Success(goal, tasks, agents, duration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.CompletedTaskCount.Should().Be(2);
        result.FailedTaskCount.Should().Be(1);
        result.SuccessRate.Should().BeApproximately(0.6667, 0.001);
        result.TotalDuration.Should().Be(duration);
    }

    #endregion

    #region AgentCoordinator Tests

    /// <summary>
    /// Tests that ExecuteAsync completes with a single goal.
    /// </summary>
    [Fact]
    public async Task AgentCoordinator_ExecuteAsync_WithSingleGoal_ShouldComplete()
    {
        // Arrange
        using InMemoryMessageBus messageBus = new InMemoryMessageBus();
        AgentIdentity agent = AgentIdentity.Create("TestAgent", AgentRole.Executor);
        AgentTeam team = AgentTeam.Empty.AddAgent(agent);

        AgentCoordinator coordinator = new AgentCoordinator(team, messageBus);
        Goal goal = Goal.Atomic("Test goal");

        // Act
        Result<CoordinationResult, string> result = await coordinator.ExecuteAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tasks.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests that ExecuteAsync fails when no agents are available.
    /// </summary>
    [Fact]
    public async Task AgentCoordinator_ExecuteAsync_WithNoAgents_ShouldFail()
    {
        // Arrange
        using InMemoryMessageBus messageBus = new InMemoryMessageBus();
        AgentTeam emptyTeam = AgentTeam.Empty;

        AgentCoordinator coordinator = new AgentCoordinator(emptyTeam, messageBus);
        Goal goal = Goal.Atomic("Test goal");

        // Act
        Result<CoordinationResult, string> result = await coordinator.ExecuteAsync(goal);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No available agents");
    }

    /// <summary>
    /// Tests that ExecuteParallelAsync executes goals concurrently.
    /// </summary>
    [Fact]
    public async Task AgentCoordinator_ExecuteParallelAsync_ShouldExecuteConcurrently()
    {
        // Arrange
        using InMemoryMessageBus messageBus = new InMemoryMessageBus();
        AgentIdentity agent1 = AgentIdentity.Create("Agent1", AgentRole.Executor);
        AgentIdentity agent2 = AgentIdentity.Create("Agent2", AgentRole.Executor);
        AgentTeam team = AgentTeam.Empty
            .AddAgent(agent1)
            .AddAgent(agent2);

        AgentCoordinator coordinator = new AgentCoordinator(team, messageBus);
        List<Goal> goals = new List<Goal>
        {
            Goal.Atomic("Goal 1"),
            Goal.Atomic("Goal 2"),
        };

        // Act
        Result<CoordinationResult, string> result = await coordinator.ExecuteParallelAsync(goals);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tasks.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests that CreateExecutionStep returns a valid step.
    /// </summary>
    [Fact]
    public void AgentCoordinator_CreateExecutionStep_ShouldReturnStep()
    {
        // Arrange
        using InMemoryMessageBus messageBus = new InMemoryMessageBus();
        AgentIdentity agent = AgentIdentity.Create("TestAgent", AgentRole.Executor);
        AgentTeam team = AgentTeam.Empty.AddAgent(agent);
        AgentCoordinator coordinator = new AgentCoordinator(team, messageBus);

        // Act
        Step<Goal, Result<CoordinationResult, string>> step = coordinator.CreateExecutionStep();

        // Assert
        step.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that SetDelegationStrategy updates the strategy.
    /// </summary>
    [Fact]
    public async Task AgentCoordinator_SetDelegationStrategy_ShouldUpdateStrategy()
    {
        // Arrange
        using InMemoryMessageBus messageBus = new InMemoryMessageBus();
        AgentCapability capability = AgentCapability.Create("specialized", "Special skill", 0.95);
        AgentIdentity specializedAgent = AgentIdentity.Create("Specialist", AgentRole.Specialist)
            .WithCapability(capability);
        AgentIdentity genericAgent = AgentIdentity.Create("Generic", AgentRole.Executor);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(genericAgent)
            .AddAgent(specializedAgent);

        AgentCoordinator coordinator = new AgentCoordinator(team, messageBus);

        // Use capability-based strategy
        IDelegationStrategy capabilityStrategy = DelegationStrategyFactory.ByCapability();
        coordinator.SetDelegationStrategy(capabilityStrategy);

        Goal goal = Goal.Atomic("Specialized task");

        // Act
        Result<CoordinationResult, string> result = await coordinator.ExecuteAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests that a multi-agent collaboration workflow completes successfully.
    /// </summary>
    [Fact]
    public async Task Integration_MultiAgentCollaboration_ShouldCompleteWorkflow()
    {
        // Arrange
        using InMemoryMessageBus messageBus = new InMemoryMessageBus();

        AgentCapability analysisCapability = AgentCapability.Create("analysis", "Data analysis", 0.9);
        AgentCapability codingCapability = AgentCapability.Create("coding", "Software development", 0.85);
        AgentCapability reviewCapability = AgentCapability.Create("review", "Code review", 0.8);

        AgentIdentity analyst = AgentIdentity.Create("DataAnalyst", AgentRole.Analyst)
            .WithCapability(analysisCapability);
        AgentIdentity developer = AgentIdentity.Create("Developer", AgentRole.Coder)
            .WithCapability(codingCapability);
        AgentIdentity reviewer = AgentIdentity.Create("CodeReviewer", AgentRole.Reviewer)
            .WithCapability(reviewCapability);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(analyst)
            .AddAgent(developer)
            .AddAgent(reviewer);

        AgentCoordinator coordinator = new AgentCoordinator(
            team,
            messageBus,
            DelegationStrategyFactory.BestFit());

        Goal parentGoal = Goal.Atomic("Complete feature implementation");

        // Act
        Result<CoordinationResult, string> result = await coordinator.ExecuteAsync(parentGoal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ParticipatingAgents.Should().NotBeEmpty();
        result.Value.Summary.Should().Contain("completed");
    }

    /// <summary>
    /// Tests that consensus can be reached with multiple agents.
    /// </summary>
    [Fact]
    public void Integration_ConsensusWithMultipleAgents_ShouldReachDecision()
    {
        // Arrange
        IReadOnlyList<string> options = new List<string> { "Approach A", "Approach B", "Approach C" };
        VotingSession session = VotingSession.Create("Architecture Decision", options, ConsensusProtocol.Majority);

        AgentIdentity agent1 = AgentIdentity.Create("Architect1", AgentRole.Planner);
        AgentIdentity agent2 = AgentIdentity.Create("Architect2", AgentRole.Planner);
        AgentIdentity agent3 = AgentIdentity.Create("Architect3", AgentRole.Planner);
        AgentIdentity agent4 = AgentIdentity.Create("TechLead", AgentRole.Reviewer);

        // Act
        session.CastVote(AgentVote.Create(agent1.Id, "Approach A", 0.9, "Best for scalability"));
        session.CastVote(AgentVote.Create(agent2.Id, "Approach A", 0.85, "Proven pattern"));
        session.CastVote(AgentVote.Create(agent3.Id, "Approach B", 0.7, "Simpler implementation"));
        session.CastVote(AgentVote.Create(agent4.Id, "Approach A", 0.95, "Team has experience"));

        Option<ConsensusResult> result = session.TryGetResult();

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.HasConsensus.Should().BeTrue();
        result.Value!.WinningOption.Should().Be("Approach A");
        result.Value!.TotalVotes.Should().Be(4);
    }

    /// <summary>
    /// Tests that delegation with a team assigns tasks correctly.
    /// </summary>
    [Fact]
    public void Integration_DelegationWithTeam_ShouldAssignTasks()
    {
        // Arrange
        AgentCapability frontendCapability = AgentCapability.Create("frontend", "UI development", 0.9);
        AgentCapability backendCapability = AgentCapability.Create("backend", "API development", 0.85);
        AgentCapability dbCapability = AgentCapability.Create("database", "Database design", 0.8);

        AgentIdentity frontendDev = AgentIdentity.Create("FrontendDev", AgentRole.Coder)
            .WithCapability(frontendCapability);
        AgentIdentity backendDev = AgentIdentity.Create("BackendDev", AgentRole.Coder)
            .WithCapability(backendCapability);
        AgentIdentity dbAdmin = AgentIdentity.Create("DBAAdmin", AgentRole.Specialist)
            .WithCapability(dbCapability);

        AgentTeam team = AgentTeam.Empty
            .AddAgent(frontendDev)
            .AddAgent(backendDev)
            .AddAgent(dbAdmin);

        IDelegationStrategy strategy = DelegationStrategyFactory.ByCapability();

        // Frontend task
        DelegationCriteria frontendCriteria = DelegationCriteria
            .FromGoal(Goal.Atomic("Build UI"))
            .RequireCapability("frontend");

        // Backend task
        DelegationCriteria backendCriteria = DelegationCriteria
            .FromGoal(Goal.Atomic("Build API"))
            .RequireCapability("backend");

        // Database task
        DelegationCriteria dbCriteria = DelegationCriteria
            .FromGoal(Goal.Atomic("Design schema"))
            .RequireCapability("database");

        // Act
        DelegationResult frontendResult = strategy.SelectAgent(frontendCriteria, team);
        DelegationResult backendResult = strategy.SelectAgent(backendCriteria, team);
        DelegationResult dbResult = strategy.SelectAgent(dbCriteria, team);

        // Assert
        frontendResult.HasMatch.Should().BeTrue();
        frontendResult.SelectedAgentId.Should().Be(frontendDev.Id);

        backendResult.HasMatch.Should().BeTrue();
        backendResult.SelectedAgentId.Should().Be(backendDev.Id);

        dbResult.HasMatch.Should().BeTrue();
        dbResult.SelectedAgentId.Should().Be(dbAdmin.Id);
    }

    #endregion
}
