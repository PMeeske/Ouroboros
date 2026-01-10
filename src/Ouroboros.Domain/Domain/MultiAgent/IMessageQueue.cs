// <copyright file="IMessageQueue.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// Abstraction for message queue operations supporting multi-agent communication.
/// </summary>
public interface IMessageQueue
{
    /// <summary>
    /// Enqueues a message for delivery to an agent.
    /// </summary>
    /// <param name="agentId">The recipient agent identifier.</param>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, string>> EnqueueAsync(AgentId agentId, Message message, CancellationToken ct = default);

    /// <summary>
    /// Dequeues a message for the specified agent.
    /// </summary>
    /// <param name="agentId">The agent to receive the message.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A Result containing the message or error if queue is empty.</returns>
    Task<Result<Message, string>> DequeueAsync(AgentId agentId, CancellationToken ct = default);

    /// <summary>
    /// Checks if there are pending messages for an agent.
    /// </summary>
    /// <param name="agentId">The agent to check.</param>
    /// <returns>True if there are pending messages, false otherwise.</returns>
    Task<bool> HasPendingMessagesAsync(AgentId agentId);
}
