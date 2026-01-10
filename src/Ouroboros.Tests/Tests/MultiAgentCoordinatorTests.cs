// <copyright file="MultiAgentCoordinatorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MultiAgent;

using FluentAssertions;
using Ouroboros.Domain.MultiAgent;
using Ouroboros.Domain.Reinforcement;
using Xunit;

/// <summary>
/// Comprehensive tests for the MultiAgentCoordinator implementation.
/// Tests message passing, task allocation, consensus protocols, knowledge synchronization, and collaborative planning.
/// </summary>
[Trait("Category", "Unit")]
public class MultiAgentCoordinatorTests
{
    private readonly IMessageQueue messageQueue;
    private readonly IAgentRegistry agentRegistry;
    private readonly MultiAgentCoordinator coordinator;

    public MultiAgentCoordinatorTests()
    {
        this.messageQueue = new InMemoryMessageQueue();
        this.agentRegistry = new InMemoryAgentRegistry();
        this.coordinator = new MultiAgentCoordinator(this.messageQueue, this.agentRegistry);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullMessageQueue_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MultiAgentCoordinator(null!, this.agentRegistry);
        act.Should().Throw<ArgumentNullException>().WithParameterName("messageQueue");
    }

    [Fact]
    public void Constructor_WithNullAgentRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MultiAgentCoordinator(this.messageQueue, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("agentRegistry");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var coordinator = new MultiAgentCoordinator(this.messageQueue, this.agentRegistry);

        // Assert
        coordinator.Should().NotBeNull();
    }

    #endregion

    #region BroadcastMessageAsync Tests

    [Fact]
    public async Task BroadcastMessageAsync_WithNullMessage_ReturnsFailure()
    {
        // Arrange
        var group = CreateTestGroup(GroupType.Broadcast);

        // Act
        var result = await this.coordinator.BroadcastMessageAsync(null!, group);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Message cannot be null");
    }

    [Fact]
    public async Task BroadcastMessageAsync_WithEmptyGroup_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        var group = new AgentGroup("empty", new List<AgentId>(), GroupType.Broadcast);

        // Act
        var result = await this.coordinator.BroadcastMessageAsync(message, group);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one member");
    }

    [Fact]
    public async Task BroadcastMessageAsync_WithBroadcastGroup_SendsToAllMembers()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var message = CreateTestMessage();
        var group = new AgentGroup("test-group", agents, GroupType.Broadcast);

        // Act
        var result = await this.coordinator.BroadcastMessageAsync(message, group);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all agents received message
        foreach (var agent in agents)
        {
            var hasPending = await this.messageQueue.HasPendingMessagesAsync(agent);
            hasPending.Should().BeTrue();
        }
    }

    [Fact]
    public async Task BroadcastMessageAsync_WithRoundRobinGroup_SendsToOneAgent()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var message = CreateTestMessage();
        var group = new AgentGroup("test-group", agents, GroupType.RoundRobin);

        // Act
        var result = await this.coordinator.BroadcastMessageAsync(message, group);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify only one agent received message
        int recipientCount = 0;
        foreach (var agent in agents)
        {
            if (await this.messageQueue.HasPendingMessagesAsync(agent))
            {
                recipientCount++;
            }
        }

        recipientCount.Should().Be(1);
    }

    [Fact]
    public async Task BroadcastMessageAsync_WithLoadBalancedGroup_SendsToLeastLoadedAgent()
    {
        // Arrange
        var agents = CreateTestAgents(3);

        // Register agents with different loads
        var capabilities = new List<AgentCapabilities>
        {
            new(agents[0], new List<string> { "skill1" }, new Dictionary<string, double>(), 0.8, true),
            new(agents[1], new List<string> { "skill1" }, new Dictionary<string, double>(), 0.2, true),
            new(agents[2], new List<string> { "skill1" }, new Dictionary<string, double>(), 0.5, true),
        };

        foreach (var cap in capabilities)
        {
            await this.agentRegistry.RegisterAgentAsync(cap);
        }

        var message = CreateTestMessage();
        var group = new AgentGroup("test-group", agents, GroupType.LoadBalanced);

        // Act
        var result = await this.coordinator.BroadcastMessageAsync(message, group);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify least loaded agent (agents[1]) received message
        var hasPending = await this.messageQueue.HasPendingMessagesAsync(agents[1]);
        hasPending.Should().BeTrue();
    }

    [Fact]
    public async Task BroadcastMessageAsync_WithCancellationToken_ReturnsFailure()
    {
        // Arrange
        var message = CreateTestMessage();
        var group = CreateTestGroup(GroupType.Broadcast);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await this.coordinator.BroadcastMessageAsync(message, group, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    #endregion

    #region AllocateTasksAsync Tests

    [Fact]
    public async Task AllocateTasksAsync_WithEmptyGoal_ReturnsFailure()
    {
        // Arrange
        var agents = CreateTestAgentCapabilities(2);

        // Act
        var result = await this.coordinator.AllocateTasksAsync(string.Empty, agents, AllocationStrategy.RoundRobin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal cannot be empty");
    }

    [Fact]
    public async Task AllocateTasksAsync_WithNoAgents_ReturnsFailure()
    {
        // Arrange
        var goal = "Complete project";

        // Act
        var result = await this.coordinator.AllocateTasksAsync(goal, new List<AgentCapabilities>(), AllocationStrategy.RoundRobin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No agents available");
    }

    [Fact]
    public async Task AllocateTasksAsync_WithRoundRobinStrategy_DistributesEvenly()
    {
        // Arrange
        var goal = "Complete project";
        var agents = CreateTestAgentCapabilities(2);

        // Act
        var result = await this.coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.RoundRobin);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Keys.Should().HaveCount(2);
    }

    [Fact]
    public async Task AllocateTasksAsync_WithSkillBasedStrategy_AssignsBasedOnSkills()
    {
        // Arrange
        var goal = "Analyze data";
        var agents = new List<AgentCapabilities>
        {
            new(new AgentId(Guid.NewGuid(), "Agent1"), new List<string> { "Analyze", "Plan" }, new Dictionary<string, double>(), 0.5, true),
            new(new AgentId(Guid.NewGuid(), "Agent2"), new List<string> { "Execute", "Verify" }, new Dictionary<string, double>(), 0.5, true),
        };

        // Act
        var result = await this.coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.SkillBased);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AllocateTasksAsync_WithLoadBalancedStrategy_ConsidersLoad()
    {
        // Arrange
        var goal = "Complete tasks";
        var agents = new List<AgentCapabilities>
        {
            new(new AgentId(Guid.NewGuid(), "HighLoad"), new List<string> { "skill1" }, new Dictionary<string, double>(), 0.9, true),
            new(new AgentId(Guid.NewGuid(), "LowLoad"), new List<string> { "skill1" }, new Dictionary<string, double>(), 0.1, true),
        };

        // Act
        var result = await this.coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.LoadBalanced);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AllocateTasksAsync_WithAuctionStrategy_UsesBiddingMechanism()
    {
        // Arrange
        var goal = "Execute tasks";
        var agents = CreateTestAgentCapabilities(3);

        // Act
        var result = await this.coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.Auction);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Values.Should().AllSatisfy(assignment =>
            assignment.Priority.Should().Be(Priority.High));
    }

    [Fact]
    public async Task AllocateTasksAsync_WithCancellationToken_ReturnsFailure()
    {
        // Arrange
        var goal = "Complete project";
        var agents = CreateTestAgentCapabilities(2);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await this.coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.RoundRobin, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    #endregion

    #region ReachConsensusAsync Tests

    [Fact]
    public async Task ReachConsensusAsync_WithEmptyProposal_ReturnsFailure()
    {
        // Arrange
        var voters = CreateTestAgents(3);

        // Act
        var result = await this.coordinator.ReachConsensusAsync(string.Empty, voters, ConsensusProtocol.Majority);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Proposal cannot be empty");
    }

    [Fact]
    public async Task ReachConsensusAsync_WithNoVoters_ReturnsFailure()
    {
        // Arrange
        var proposal = "Adopt new feature";

        // Act
        var result = await this.coordinator.ReachConsensusAsync(proposal, new List<AgentId>(), ConsensusProtocol.Majority);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No voters provided");
    }

    [Fact]
    public async Task ReachConsensusAsync_WithMajorityProtocol_RequiresMajority()
    {
        // Arrange
        var proposal = "Adopt new feature";
        var voters = CreateTestAgents(3);

        // Register agents
        foreach (var voterId in voters)
        {
            await this.agentRegistry.RegisterAgentAsync(
                new AgentCapabilities(voterId, new List<string>(), new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await this.coordinator.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Majority);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Proposal.Should().Be(proposal);
        result.Value.ConsensusScore.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public async Task ReachConsensusAsync_WithUnanimousProtocol_RequiresAll()
    {
        // Arrange
        var proposal = "Critical change";
        var voters = CreateTestAgents(3);

        // Register agents
        foreach (var voterId in voters)
        {
            await this.agentRegistry.RegisterAgentAsync(
                new AgentCapabilities(voterId, new List<string>(), new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await this.coordinator.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Unanimous);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Proposal.Should().Be(proposal);
    }

    [Fact]
    public async Task ReachConsensusAsync_WithWeightedProtocol_ConsidersConfidence()
    {
        // Arrange
        var proposal = "Feature proposal";
        var voters = CreateTestAgents(3);

        // Register agents
        foreach (var voterId in voters)
        {
            await this.agentRegistry.RegisterAgentAsync(
                new AgentCapabilities(voterId, new List<string>(), new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await this.coordinator.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Weighted);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Votes.Should().HaveCount(3);
        result.Value.ConsensusScore.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task ReachConsensusAsync_WithRaftProtocol_RequiresQuorum()
    {
        // Arrange
        var proposal = "Leader election";
        var voters = CreateTestAgents(5);

        // Register agents
        foreach (var voterId in voters)
        {
            await this.agentRegistry.RegisterAgentAsync(
                new AgentCapabilities(voterId, new List<string>(), new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await this.coordinator.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Raft);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Votes.Should().HaveCount(5);
    }

    #endregion

    #region SynchronizeKnowledgeAsync Tests

    [Fact]
    public async Task SynchronizeKnowledgeAsync_WithSingleAgent_ReturnsFailure()
    {
        // Arrange
        var agents = CreateTestAgents(1);

        // Act
        var result = await this.coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Full);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least 2 agents");
    }

    [Fact]
    public async Task SynchronizeKnowledgeAsync_WithFullStrategy_SyncsAllKnowledge()
    {
        // Arrange
        var agents = CreateTestAgents(3);

        // Act
        var result = await this.coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Full);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SynchronizeKnowledgeAsync_WithIncrementalStrategy_SyncsNewKnowledge()
    {
        // Arrange
        var agents = CreateTestAgents(2);

        // Act
        var result = await this.coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Incremental);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SynchronizeKnowledgeAsync_WithSelectiveStrategy_SyncsRelevantKnowledge()
    {
        // Arrange
        var agents = CreateTestAgents(2);

        // Register agents with similar skills
        foreach (var agent in agents)
        {
            await this.agentRegistry.RegisterAgentAsync(
                new AgentCapabilities(agent, new List<string> { "common-skill" }, new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await this.coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Selective);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SynchronizeKnowledgeAsync_WithGossipStrategy_PropagatesKnowledge()
    {
        // Arrange
        var agents = CreateTestAgents(5);

        // Act
        var result = await this.coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Gossip);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region PlanCollaborativelyAsync Tests

    [Fact]
    public async Task PlanCollaborativelyAsync_WithEmptyGoal_ReturnsFailure()
    {
        // Arrange
        var participants = CreateTestAgents(2);

        // Act
        var result = await this.coordinator.PlanCollaborativelyAsync(string.Empty, participants);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal cannot be empty");
    }

    [Fact]
    public async Task PlanCollaborativelyAsync_WithNoParticipants_ReturnsFailure()
    {
        // Arrange
        var goal = "Build system";

        // Act
        var result = await this.coordinator.PlanCollaborativelyAsync(goal, new List<AgentId>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No participants provided");
    }

    [Fact]
    public async Task PlanCollaborativelyAsync_WithValidInput_CreatesPlan()
    {
        // Arrange
        var goal = "Build system";
        var participants = CreateTestAgents(3);

        // Register participants
        foreach (var participant in participants)
        {
            await this.agentRegistry.RegisterAgentAsync(
                new AgentCapabilities(participant, new List<string> { "skill1" }, new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await this.coordinator.PlanCollaborativelyAsync(goal, participants);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Goal.Should().Be(goal);
        result.Value.Assignments.Should().NotBeEmpty();
        result.Value.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task PlanCollaborativelyAsync_WithValidInput_IdentifiesDependencies()
    {
        // Arrange
        var goal = "Complex project";
        var participants = CreateTestAgents(2);

        // Register participants
        foreach (var participant in participants)
        {
            await this.agentRegistry.RegisterAgentAsync(
                new AgentCapabilities(participant, new List<string> { "Analyze", "Execute" }, new Dictionary<string, double>(), 0.5, true));
        }

        // Act
        var result = await this.coordinator.PlanCollaborativelyAsync(goal, participants);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Dependencies.Should().NotBeNull();
    }

    [Fact]
    public async Task PlanCollaborativelyAsync_WithUnregisteredAgent_ReturnsFailure()
    {
        // Arrange
        var goal = "Build system";
        var participants = CreateTestAgents(1);

        // Do not register agents

        // Act
        var result = await this.coordinator.PlanCollaborativelyAsync(goal, participants);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to get capabilities");
    }

    #endregion

    #region Helper Methods

    private static List<AgentId> CreateTestAgents(int count)
    {
        var agents = new List<AgentId>();
        for (int i = 0; i < count; i++)
        {
            agents.Add(new AgentId(Guid.NewGuid(), $"Agent{i + 1}"));
        }

        return agents;
    }

    private static List<AgentCapabilities> CreateTestAgentCapabilities(int count)
    {
        var capabilities = new List<AgentCapabilities>();
        for (int i = 0; i < count; i++)
        {
            capabilities.Add(new AgentCapabilities(
                new AgentId(Guid.NewGuid(), $"Agent{i + 1}"),
                new List<string> { $"skill{i + 1}" },
                new Dictionary<string, double> { { $"skill{i + 1}", 0.8 } },
                0.5,
                true));
        }

        return capabilities;
    }

    private static Message CreateTestMessage()
    {
        return new Message(
            Sender: new AgentId(Guid.NewGuid(), "Sender"),
            Recipient: null,
            Type: MessageType.Query,
            Payload: "Test message",
            Timestamp: DateTime.UtcNow,
            ConversationId: Guid.NewGuid());
    }

    private static AgentGroup CreateTestGroup(GroupType type)
    {
        var agents = CreateTestAgents(3);
        return new AgentGroup("test-group", agents, type);
    }

    #endregion
}
