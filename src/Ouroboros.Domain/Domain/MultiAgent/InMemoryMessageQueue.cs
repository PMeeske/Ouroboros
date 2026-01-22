// <copyright file="InMemoryMessageQueue.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

using System.Collections.Concurrent;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// In-memory implementation of message queue for multi-agent communication.
/// </summary>
public sealed class InMemoryMessageQueue : IMessageQueue
{
    private readonly ConcurrentDictionary<AgentId, ConcurrentQueue<Message>> queues = new();

    /// <inheritdoc/>
    public Task<Result<Unit, string>> EnqueueAsync(AgentId agentId, Message message, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromResult(Result<Unit, string>.Failure("Operation was cancelled"));
        }

        try
        {
            var queue = this.queues.GetOrAdd(agentId, _ => new ConcurrentQueue<Message>());
            queue.Enqueue(message);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to enqueue message: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Message, string>> DequeueAsync(AgentId agentId, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromResult(Result<Message, string>.Failure("Operation was cancelled"));
        }

        if (!this.queues.TryGetValue(agentId, out var queue))
        {
            return Task.FromResult(Result<Message, string>.Failure("No messages available for agent"));
        }

        if (queue.TryDequeue(out var message))
        {
            return Task.FromResult(Result<Message, string>.Success(message));
        }

        return Task.FromResult(Result<Message, string>.Failure("No messages available for agent"));
    }

    /// <inheritdoc/>
    public Task<bool> HasPendingMessagesAsync(AgentId agentId)
    {
        if (this.queues.TryGetValue(agentId, out var queue))
        {
            return Task.FromResult(!queue.IsEmpty);
        }

        return Task.FromResult(false);
    }
}
