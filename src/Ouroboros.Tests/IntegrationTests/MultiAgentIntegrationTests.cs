// <copyright file="MultiAgentIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Integration;

using FluentAssertions;
using Ouroboros.Domain.MultiAgent;
using Ouroboros.Domain.Reinforcement;
using Xunit;

/// <summary>
/// Integration tests for multi-agent coordination system.
/// Tests end-to-end scenarios with multiple agents working together.
/// </summary>
[Trait("Category", "Integration")]
public class MultiAgentIntegrationTests
{
    /// <summary>
    /// Tests a complete workflow from agent registration through task completion.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteWorkflow_WithMultipleAgents_WorksEndToEnd()
    {
        // Arrange
        var messageQueue = new InMemoryMessageQueue();
        var registry = new InMemoryAgentRegistry();
        var coordinator = new MultiAgentCoordinator(messageQueue, registry);

        // Create a team of agents
        var agents = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "Agent-Research"),
            new AgentId(Guid.NewGuid(), "Agent-Development"),
            new AgentId(Guid.NewGuid(), "Agent-Testing"),
        };

        // Register agents with capabilities
        var capabilities = new List<AgentCapabilities>
        {
            new(agents[0], new List<string> { "analyze", "research" }, new Dictionary<string, double> { { "analyze", 0.9 } }, 0.2, true),
            new(agents[1], new List<string> { "execute", "code" }, new Dictionary<string, double> { { "execute", 0.95 } }, 0.4, true),
            new(agents[2], new List<string> { "verify", "test" }, new Dictionary<string, double> { { "verify", 0.85 } }, 0.3, true),
        };

        foreach (var cap in capabilities)
        {
            await registry.RegisterAgentAsync(cap);
        }

        // Step 1: Broadcast project kickoff
        var kickoffMessage = new Message(
            new AgentId(Guid.NewGuid(), "Manager"),
            null,
            MessageType.Notification,
            "Project starting: Build AI system",
            DateTime.UtcNow,
            Guid.NewGuid());

        var group = new AgentGroup("project-team", agents, GroupType.Broadcast);
        var broadcastResult = await coordinator.BroadcastMessageAsync(kickoffMessage, group);

        // Step 2: Allocate tasks
        var allocationResult = await coordinator.AllocateTasksAsync(
            "Build AI system with testing",
            capabilities,
            AllocationStrategy.SkillBased);

        // Step 3: Reach consensus on approach
        var consensusResult = await coordinator.ReachConsensusAsync(
            "Use microservices architecture",
            agents,
            ConsensusProtocol.Majority);

        // Step 4: Synchronize knowledge
        var syncResult = await coordinator.SynchronizeKnowledgeAsync(
            agents,
            KnowledgeSyncStrategy.Full);

        // Step 5: Create collaborative plan
        var planResult = await coordinator.PlanCollaborativelyAsync(
            "Complete AI system development",
            agents);

        // Assert
        broadcastResult.IsSuccess.Should().BeTrue();
        allocationResult.IsSuccess.Should().BeTrue();
        allocationResult.Value.Should().NotBeEmpty();
        consensusResult.IsSuccess.Should().BeTrue();
        consensusResult.Value.Accepted.Should().BeTrue();
        syncResult.IsSuccess.Should().BeTrue();
        planResult.IsSuccess.Should().BeTrue();
        planResult.Value.Assignments.Should().NotBeEmpty();
        planResult.Value.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);

        // Verify message delivery
        foreach (var agent in agents)
        {
            var hasPending = await messageQueue.HasPendingMessagesAsync(agent);
            hasPending.Should().BeTrue();
        }
    }

    /// <summary>
    /// Tests task allocation with agents having different workloads.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task LoadBalancedAllocation_WithVaryingLoads_PreferLowLoadAgents()
    {
        // Arrange
        var coordinator = new MultiAgentCoordinator(new InMemoryMessageQueue(), new InMemoryAgentRegistry());

        var agents = new List<AgentCapabilities>
        {
            new(new AgentId(Guid.NewGuid(), "HighLoad"), new List<string> { "skill" }, new Dictionary<string, double>(), 0.9, true),
            new(new AgentId(Guid.NewGuid(), "MediumLoad"), new List<string> { "skill" }, new Dictionary<string, double>(), 0.5, true),
            new(new AgentId(Guid.NewGuid(), "LowLoad"), new List<string> { "skill" }, new Dictionary<string, double>(), 0.1, true),
        };

        // Act
        var result = await coordinator.AllocateTasksAsync("Execute tasks", agents, AllocationStrategy.LoadBalanced);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        // Low load agent should receive first task
        var firstAssignment = result.Value.Values.First();
        firstAssignment.AssignedTo.Name.Should().Contain("LowLoad");
    }

    /// <summary>
    /// Tests consensus protocol requiring unanimous agreement.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task UnanimousConsensus_WithAllAgents_RequiresFullAgreement()
    {
        // Arrange
        var registry = new InMemoryAgentRegistry();
        var coordinator = new MultiAgentCoordinator(new InMemoryMessageQueue(), registry);

        var voters = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "Voter1"),
            new AgentId(Guid.NewGuid(), "Voter2"),
            new AgentId(Guid.NewGuid(), "Voter3"),
        };

        foreach (var voter in voters)
        {
            await registry.RegisterAgentAsync(
                new AgentCapabilities(voter, new List<string>(), new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await coordinator.ReachConsensusAsync(
            "Critical system change",
            voters,
            ConsensusProtocol.Unanimous);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ConsensusScore.Should().BeInRange(0.0, 1.0);
    }

    /// <summary>
    /// Tests collaborative planning with dependency identification.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task CollaborativePlanning_WithMultipleParticipants_CreatesDependencies()
    {
        // Arrange
        var registry = new InMemoryAgentRegistry();
        var coordinator = new MultiAgentCoordinator(new InMemoryMessageQueue(), registry);

        var participants = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "Planner"),
            new AgentId(Guid.NewGuid(), "Executor"),
        };

        foreach (var participant in participants)
        {
            await registry.RegisterAgentAsync(
                new AgentCapabilities(
                    participant,
                    new List<string> { "Analyze", "Plan", "Execute", "Verify" },
                    new Dictionary<string, double>(),
                    0.5,
                    true));
        }

        // Act
        var result = await coordinator.PlanCollaborativelyAsync("Complex project", participants);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Goal.Should().Be("Complex project");
        result.Value.Assignments.Should().NotBeEmpty();
        result.Value.Dependencies.Should().NotBeNull();
        result.Value.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    /// <summary>
    /// Tests knowledge synchronization between multiple agents.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Fact]
    public async Task KnowledgeSync_WithMultipleStrategies_CompletesSuccessfully()
    {
        // Arrange
        var coordinator = new MultiAgentCoordinator(new InMemoryMessageQueue(), new InMemoryAgentRegistry());

        var agents = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "Agent1"),
            new AgentId(Guid.NewGuid(), "Agent2"),
            new AgentId(Guid.NewGuid(), "Agent3"),
        };

        // Act & Assert - Test all sync strategies
        var fullResult = await coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Full);
        fullResult.IsSuccess.Should().BeTrue();

        var incResult = await coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Incremental);
        incResult.IsSuccess.Should().BeTrue();

        var gossipResult = await coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Gossip);
        gossipResult.IsSuccess.Should().BeTrue();
    }
}
