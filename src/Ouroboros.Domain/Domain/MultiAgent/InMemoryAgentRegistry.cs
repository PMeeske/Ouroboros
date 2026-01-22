// <copyright file="InMemoryAgentRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

using System.Collections.Concurrent;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// In-memory implementation of agent registry for tracking agent capabilities.
/// </summary>
public sealed class InMemoryAgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<AgentId, AgentCapabilities> agents = new();

    /// <inheritdoc/>
    public Task<Result<Unit, string>> RegisterAgentAsync(AgentCapabilities capabilities)
    {
        try
        {
            this.agents[capabilities.Id] = capabilities;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to register agent: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UnregisterAgentAsync(AgentId agentId)
    {
        if (this.agents.TryRemove(agentId, out _))
        {
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        return Task.FromResult(Result<Unit, string>.Failure("Agent not found"));
    }

    /// <inheritdoc/>
    public Task<Result<AgentCapabilities, string>> GetAgentCapabilitiesAsync(AgentId agentId)
    {
        if (this.agents.TryGetValue(agentId, out var capabilities))
        {
            return Task.FromResult(Result<AgentCapabilities, string>.Success(capabilities));
        }

        return Task.FromResult(Result<AgentCapabilities, string>.Failure("Agent not found"));
    }

    /// <inheritdoc/>
    public Task<List<AgentCapabilities>> GetAllAgentsAsync()
    {
        return Task.FromResult(this.agents.Values.ToList());
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UpdateAgentLoadAsync(AgentId agentId, double load)
    {
        if (load < 0.0 || load > 1.0)
        {
            return Task.FromResult(Result<Unit, string>.Failure("Load must be between 0.0 and 1.0"));
        }

        if (this.agents.TryGetValue(agentId, out var capabilities))
        {
            var updated = capabilities with { CurrentLoad = load };
            this.agents[agentId] = updated;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        return Task.FromResult(Result<Unit, string>.Failure("Agent not found"));
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UpdateAgentAvailabilityAsync(AgentId agentId, bool isAvailable)
    {
        if (this.agents.TryGetValue(agentId, out var capabilities))
        {
            var updated = capabilities with { IsAvailable = isAvailable };
            this.agents[agentId] = updated;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        return Task.FromResult(Result<Unit, string>.Failure("Agent not found"));
    }
}
