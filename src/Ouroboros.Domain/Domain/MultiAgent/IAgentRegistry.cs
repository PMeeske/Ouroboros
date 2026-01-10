// <copyright file="IAgentRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// Registry for tracking and managing agent capabilities and availability.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Registers an agent with its capabilities.
    /// </summary>
    /// <param name="capabilities">The agent's capabilities.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, string>> RegisterAgentAsync(AgentCapabilities capabilities);

    /// <summary>
    /// Unregisters an agent from the registry.
    /// </summary>
    /// <param name="agentId">The agent to unregister.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, string>> UnregisterAgentAsync(AgentId agentId);

    /// <summary>
    /// Gets the capabilities of a specific agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns>A Result containing the agent's capabilities or an error.</returns>
    Task<Result<AgentCapabilities, string>> GetAgentCapabilitiesAsync(AgentId agentId);

    /// <summary>
    /// Gets all registered agents.
    /// </summary>
    /// <returns>A list of all registered agents.</returns>
    Task<List<AgentCapabilities>> GetAllAgentsAsync();

    /// <summary>
    /// Updates the current load for an agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="load">The new load value (0.0 to 1.0).</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, string>> UpdateAgentLoadAsync(AgentId agentId, double load);

    /// <summary>
    /// Updates the availability status of an agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="isAvailable">The new availability status.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, string>> UpdateAgentAvailabilityAsync(AgentId agentId, bool isAvailable);
}
