// <copyright file="MultiAgentCoordinationExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Domain.MultiAgent;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// Comprehensive demonstration of multi-agent coordination capabilities.
/// Shows message passing, task allocation, consensus protocols, knowledge synchronization, and collaborative planning.
/// </summary>
public static class MultiAgentCoordinationExample
{
    /// <summary>
    /// Runs all multi-agent coordination demonstrations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunAllDemonstrations()
    {
        Console.WriteLine("=== Multi-Agent Coordination Demonstration ===\n");

        await DemonstrateMessageBroadcasting();
        await DemonstrateTaskAllocation();
        await DemonstrateConsensusProtocols();
        await DemonstrateKnowledgeSynchronization();
        await DemonstrateCollaborativePlanning();
        await DemonstrateCompleteScenario();

        Console.WriteLine("\n=== All Demonstrations Complete ===");
    }

    /// <summary>
    /// Demonstrates message broadcasting with different group types.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task DemonstrateMessageBroadcasting()
    {
        Console.WriteLine("=== Message Broadcasting Demo ===");

        var messageQueue = new InMemoryMessageQueue();
        var registry = new InMemoryAgentRegistry();
        var coordinator = new MultiAgentCoordinator(messageQueue, registry);

        // Create test agents
        var agents = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "Agent-Alpha"),
            new AgentId(Guid.NewGuid(), "Agent-Beta"),
            new AgentId(Guid.NewGuid(), "Agent-Gamma"),
        };

        // Register agents with capabilities
        foreach (var agent in agents)
        {
            await registry.RegisterAgentAsync(new AgentCapabilities(
                agent,
                new List<string> { "communication", "analysis" },
                new Dictionary<string, double> { { "communication", 0.9 } },
                0.3,
                true));
        }

        // Broadcast message to all agents
        var broadcastGroup = new AgentGroup("research-team", agents, GroupType.Broadcast);
        var message = new Message(
            new AgentId(Guid.NewGuid(), "Coordinator"),
            null,
            MessageType.Query,
            "What is the status of the current project?",
            DateTime.UtcNow,
            Guid.NewGuid());

        var result = await coordinator.BroadcastMessageAsync(message, broadcastGroup);
        Console.WriteLine($"Broadcast result: {(result.IsSuccess ? "Success" : $"Failed: {result.Error}")}");

        // Verify all agents received the message
        foreach (var agent in agents)
        {
            var hasPending = await messageQueue.HasPendingMessagesAsync(agent);
            Console.WriteLine($"  {agent.Name} has pending messages: {hasPending}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates different task allocation strategies.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task DemonstrateTaskAllocation()
    {
        Console.WriteLine("=== Task Allocation Demo ===");

        var coordinator = CreateCoordinator();

        // Create agents with different capabilities
        var agents = new List<AgentCapabilities>
        {
            new(new AgentId(Guid.NewGuid(), "Analyzer"), new List<string> { "analyze", "research" }, new Dictionary<string, double>(), 0.2, true),
            new(new AgentId(Guid.NewGuid(), "Planner"), new List<string> { "plan", "strategy" }, new Dictionary<string, double>(), 0.4, true),
            new(new AgentId(Guid.NewGuid(), "Executor"), new List<string> { "execute", "implement" }, new Dictionary<string, double>(), 0.6, true),
            new(new AgentId(Guid.NewGuid(), "Verifier"), new List<string> { "verify", "test" }, new Dictionary<string, double>(), 0.1, true),
        };

        var goal = "Develop and deploy a new feature";

        // Demonstrate RoundRobin allocation
        Console.WriteLine("--- Round-Robin Allocation ---");
        var rrResult = await coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.RoundRobin);
        PrintAllocationResult(rrResult);

        // Demonstrate Skill-Based allocation
        Console.WriteLine("--- Skill-Based Allocation ---");
        var sbResult = await coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.SkillBased);
        PrintAllocationResult(sbResult);

        // Demonstrate Load-Balanced allocation
        Console.WriteLine("--- Load-Balanced Allocation ---");
        var lbResult = await coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.LoadBalanced);
        PrintAllocationResult(lbResult);

        // Demonstrate Auction allocation
        Console.WriteLine("--- Auction Allocation ---");
        var aResult = await coordinator.AllocateTasksAsync(goal, agents, AllocationStrategy.Auction);
        PrintAllocationResult(aResult);

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates different consensus protocols.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task DemonstrateConsensusProtocols()
    {
        Console.WriteLine("=== Consensus Protocols Demo ===");

        var coordinator = CreateCoordinator();
        var voters = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "Voter-1"),
            new AgentId(Guid.NewGuid(), "Voter-2"),
            new AgentId(Guid.NewGuid(), "Voter-3"),
            new AgentId(Guid.NewGuid(), "Voter-4"),
            new AgentId(Guid.NewGuid(), "Voter-5"),
        };

        // Register voters
        var registry = new InMemoryAgentRegistry();
        foreach (var voter in voters)
        {
            await registry.RegisterAgentAsync(new AgentCapabilities(
                voter,
                new List<string> { "decision-making" },
                new Dictionary<string, double>(),
                0.5,
                true));
        }

        var coordinator2 = new MultiAgentCoordinator(new InMemoryMessageQueue(), registry);
        var proposal = "Adopt microservices architecture";

        // Majority protocol
        Console.WriteLine("--- Majority Protocol ---");
        var majorityResult = await coordinator2.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Majority);
        PrintConsensusResult(majorityResult);

        // Unanimous protocol
        Console.WriteLine("--- Unanimous Protocol ---");
        var unanimousResult = await coordinator2.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Unanimous);
        PrintConsensusResult(unanimousResult);

        // Weighted protocol
        Console.WriteLine("--- Weighted Protocol ---");
        var weightedResult = await coordinator2.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Weighted);
        PrintConsensusResult(weightedResult);

        // Raft protocol
        Console.WriteLine("--- Raft Protocol ---");
        var raftResult = await coordinator2.ReachConsensusAsync(proposal, voters, ConsensusProtocol.Raft);
        PrintConsensusResult(raftResult);

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates knowledge synchronization strategies.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task DemonstrateKnowledgeSynchronization()
    {
        Console.WriteLine("=== Knowledge Synchronization Demo ===");

        var registry = new InMemoryAgentRegistry();
        var coordinator = new MultiAgentCoordinator(new InMemoryMessageQueue(), registry);

        var agents = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "KB-Agent-1"),
            new AgentId(Guid.NewGuid(), "KB-Agent-2"),
            new AgentId(Guid.NewGuid(), "KB-Agent-3"),
        };

        // Register agents with skills for selective sync
        foreach (var agent in agents)
        {
            await registry.RegisterAgentAsync(new AgentCapabilities(
                agent,
                new List<string> { "knowledge-base", "learning" },
                new Dictionary<string, double>(),
                0.5,
                true));
        }

        // Full synchronization
        Console.WriteLine("--- Full Knowledge Sync ---");
        var fullResult = await coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Full);
        Console.WriteLine($"Result: {(fullResult.IsSuccess ? "Success" : $"Failed: {fullResult.Error}")}");

        // Incremental synchronization
        Console.WriteLine("--- Incremental Knowledge Sync ---");
        var incResult = await coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Incremental);
        Console.WriteLine($"Result: {(incResult.IsSuccess ? "Success" : $"Failed: {incResult.Error}")}");

        // Selective synchronization
        Console.WriteLine("--- Selective Knowledge Sync ---");
        var selResult = await coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Selective);
        Console.WriteLine($"Result: {(selResult.IsSuccess ? "Success" : $"Failed: {selResult.Error}")}");

        // Gossip synchronization
        Console.WriteLine("--- Gossip Knowledge Sync ---");
        var gossipResult = await coordinator.SynchronizeKnowledgeAsync(agents, KnowledgeSyncStrategy.Gossip);
        Console.WriteLine($"Result: {(gossipResult.IsSuccess ? "Success" : $"Failed: {gossipResult.Error}")}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates collaborative planning among multiple agents.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task DemonstrateCollaborativePlanning()
    {
        Console.WriteLine("=== Collaborative Planning Demo ===");

        var registry = new InMemoryAgentRegistry();
        var coordinator = new MultiAgentCoordinator(new InMemoryMessageQueue(), registry);

        var participants = new List<AgentId>
        {
            new AgentId(Guid.NewGuid(), "Architect"),
            new AgentId(Guid.NewGuid(), "Developer"),
            new AgentId(Guid.NewGuid(), "Tester"),
        };

        // Register participants with relevant skills
        await registry.RegisterAgentAsync(new AgentCapabilities(
            participants[0],
            new List<string> { "analyze", "plan", "design" },
            new Dictionary<string, double>(),
            0.3,
            true));

        await registry.RegisterAgentAsync(new AgentCapabilities(
            participants[1],
            new List<string> { "execute", "implement", "code" },
            new Dictionary<string, double>(),
            0.4,
            true));

        await registry.RegisterAgentAsync(new AgentCapabilities(
            participants[2],
            new List<string> { "verify", "test", "quality" },
            new Dictionary<string, double>(),
            0.2,
            true));

        var goal = "Build a distributed microservices system";

        var result = await coordinator.PlanCollaborativelyAsync(goal, participants);

        if (result.IsSuccess)
        {
            var plan = result.Value;
            Console.WriteLine($"Goal: {plan.Goal}");
            Console.WriteLine($"Estimated Duration: {plan.EstimatedDuration.TotalHours:F2} hours");
            Console.WriteLine($"\nTask Assignments ({plan.Assignments.Count}):");

            foreach (var assignment in plan.Assignments)
            {
                Console.WriteLine($"  - {assignment.TaskDescription}");
                Console.WriteLine($"    Assigned to: {assignment.AssignedTo.Name}");
                Console.WriteLine($"    Priority: {assignment.Priority}");
                Console.WriteLine($"    Deadline: {assignment.Deadline:yyyy-MM-dd HH:mm}");
            }

            Console.WriteLine($"\nDependencies ({plan.Dependencies.Count}):");
            foreach (var dep in plan.Dependencies)
            {
                Console.WriteLine($"  - {dep.TaskA} {dep.Type} {dep.TaskB}");
            }
        }
        else
        {
            Console.WriteLine($"Planning failed: {result.Error}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates a complete multi-agent coordination scenario.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task DemonstrateCompleteScenario()
    {
        Console.WriteLine("=== Complete Multi-Agent Scenario ===");
        Console.WriteLine("Scenario: A team of agents collaboratively develops a new feature");
        Console.WriteLine();

        var registry = new InMemoryAgentRegistry();
        var messageQueue = new InMemoryMessageQueue();
        var coordinator = new MultiAgentCoordinator(messageQueue, registry);

        // Step 1: Initialize agents
        Console.WriteLine("Step 1: Initializing agent team...");
        var agents = new List<AgentCapabilities>
        {
            new(new AgentId(Guid.NewGuid(), "ProductOwner"), new List<string> { "analyze", "plan", "requirements" }, new Dictionary<string, double>(), 0.3, true),
            new(new AgentId(Guid.NewGuid(), "TechLead"), new List<string> { "plan", "design", "review" }, new Dictionary<string, double>(), 0.5, true),
            new(new AgentId(Guid.NewGuid(), "Developer1"), new List<string> { "execute", "code", "test" }, new Dictionary<string, double>(), 0.4, true),
            new(new AgentId(Guid.NewGuid(), "Developer2"), new List<string> { "execute", "code", "test" }, new Dictionary<string, double>(), 0.3, true),
            new(new AgentId(Guid.NewGuid(), "QAEngineer"), new List<string> { "verify", "test", "quality" }, new Dictionary<string, double>(), 0.2, true),
        };

        foreach (var agent in agents)
        {
            await registry.RegisterAgentAsync(agent);
        }

        Console.WriteLine($"  Initialized {agents.Count} agents");

        // Step 2: Broadcast project kickoff
        Console.WriteLine("\nStep 2: Broadcasting project kickoff...");
        var kickoffMessage = new Message(
            new AgentId(Guid.NewGuid(), "ProjectManager"),
            null,
            MessageType.Notification,
            "New feature development starting: User authentication system",
            DateTime.UtcNow,
            Guid.NewGuid());

        var group = new AgentGroup("development-team", agents.Select(a => a.Id).ToList(), GroupType.Broadcast);
        await coordinator.BroadcastMessageAsync(kickoffMessage, group);
        Console.WriteLine("  Kickoff message sent to all team members");

        // Step 3: Collaborative planning
        Console.WriteLine("\nStep 3: Creating collaborative plan...");
        var planResult = await coordinator.PlanCollaborativelyAsync(
            "Implement secure user authentication with OAuth2",
            agents.Select(a => a.Id).ToList());

        if (planResult.IsSuccess)
        {
            Console.WriteLine($"  Plan created with {planResult.Value.Assignments.Count} tasks");
            Console.WriteLine($"  Estimated completion: {planResult.Value.EstimatedDuration.TotalHours:F1} hours");
        }

        // Step 4: Allocate tasks
        Console.WriteLine("\nStep 4: Allocating tasks using skill-based strategy...");
        var allocationResult = await coordinator.AllocateTasksAsync(
            "Implement authentication feature",
            agents,
            AllocationStrategy.SkillBased);

        if (allocationResult.IsSuccess)
        {
            Console.WriteLine($"  Allocated {allocationResult.Value.Count} tasks to team members");
        }

        // Step 5: Reach consensus on architecture
        Console.WriteLine("\nStep 5: Reaching consensus on architecture decision...");
        var consensusResult = await coordinator.ReachConsensusAsync(
            "Use OAuth2 with JWT tokens for authentication",
            agents.Select(a => a.Id).ToList(),
            ConsensusProtocol.Majority);

        if (consensusResult.IsSuccess)
        {
            Console.WriteLine($"  Consensus {(consensusResult.Value.Accepted ? "REACHED" : "NOT REACHED")}");
            Console.WriteLine($"  Consensus score: {consensusResult.Value.ConsensusScore:P0}");
        }

        // Step 6: Synchronize knowledge
        Console.WriteLine("\nStep 6: Synchronizing team knowledge...");
        var syncResult = await coordinator.SynchronizeKnowledgeAsync(
            agents.Select(a => a.Id).ToList(),
            KnowledgeSyncStrategy.Selective);

        if (syncResult.IsSuccess)
        {
            Console.WriteLine("  Knowledge successfully synchronized across team");
        }

        Console.WriteLine("\n=== Scenario Complete ===");
        Console.WriteLine();
    }

    private static MultiAgentCoordinator CreateCoordinator()
    {
        return new MultiAgentCoordinator(
            new InMemoryMessageQueue(),
            new InMemoryAgentRegistry());
    }

    private static void PrintAllocationResult(Ouroboros.Core.Monads.Result<Dictionary<AgentId, TaskAssignment>, string> result)
    {
        if (result.IsSuccess)
        {
            Console.WriteLine($"  Allocated {result.Value.Count} tasks:");
            foreach (var kvp in result.Value)
            {
                Console.WriteLine($"    {kvp.Key.Name}: {kvp.Value.TaskDescription}");
            }
        }
        else
        {
            Console.WriteLine($"  Allocation failed: {result.Error}");
        }
    }

    private static void PrintConsensusResult(Ouroboros.Core.Monads.Result<Decision, string> result)
    {
        if (result.IsSuccess)
        {
            var decision = result.Value;
            Console.WriteLine($"  Decision: {(decision.Accepted ? "ACCEPTED" : "REJECTED")}");
            Console.WriteLine($"  Consensus Score: {decision.ConsensusScore:P1}");
            Console.WriteLine($"  Votes: {decision.Votes.Count(v => v.Value.InFavor)}/{decision.Votes.Count} in favor");
        }
        else
        {
            Console.WriteLine($"  Consensus failed: {result.Error}");
        }
    }
}
