// <copyright file="IMultiAgentCoordinator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// Coordinates multiple Ouroboros instances for collaborative intelligence and distributed problem-solving.
/// </summary>
public interface IMultiAgentCoordinator
{
    /// <summary>
    /// Broadcasts a message to a group of agents according to the group's distribution strategy.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="recipients">The group of agents to receive the message.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A Result indicating success or failure with error message.</returns>
    Task<Result<Unit, string>> BroadcastMessageAsync(
        Message message,
        AgentGroup recipients,
        CancellationToken ct = default);

    /// <summary>
    /// Allocates tasks to available agents using the specified allocation strategy.
    /// </summary>
    /// <param name="goal">The overall goal to decompose into tasks.</param>
    /// <param name="availableAgents">The agents available for task assignment.</param>
    /// <param name="strategy">The strategy to use for task allocation.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A Result containing task assignments per agent or an error message.</returns>
    Task<Result<Dictionary<AgentId, TaskAssignment>, string>> AllocateTasksAsync(
        string goal,
        List<AgentCapabilities> availableAgents,
        AllocationStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Facilitates reaching consensus among agents on a proposal using the specified protocol.
    /// </summary>
    /// <param name="proposal">The proposal to reach consensus on.</param>
    /// <param name="voters">The agents participating in the consensus.</param>
    /// <param name="protocol">The consensus protocol to use.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A Result containing the decision or an error message.</returns>
    Task<Result<Decision, string>> ReachConsensusAsync(
        string proposal,
        List<AgentId> voters,
        ConsensusProtocol protocol,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronizes knowledge across multiple agents using the specified strategy.
    /// </summary>
    /// <param name="agents">The agents to synchronize knowledge between.</param>
    /// <param name="strategy">The knowledge synchronization strategy to use.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A Result indicating success or failure with error message.</returns>
    Task<Result<Unit, string>> SynchronizeKnowledgeAsync(
        List<AgentId> agents,
        KnowledgeSyncStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a collaborative plan with task assignments and dependencies.
    /// </summary>
    /// <param name="goal">The goal to achieve collaboratively.</param>
    /// <param name="participants">The agents participating in the planning.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A Result containing the collaborative plan or an error message.</returns>
    Task<Result<CollaborativePlan, string>> PlanCollaborativelyAsync(
        string goal,
        List<AgentId> participants,
        CancellationToken ct = default);
}
