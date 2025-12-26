// <copyright file="ICouncilOrchestrator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Interface for orchestrating multi-agent council debates.
/// </summary>
public interface ICouncilOrchestrator
{
    /// <summary>
    /// Convenes a council debate on the given topic and returns a decision.
    /// </summary>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="config">Configuration for the debate session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the council's decision or an error.</returns>
    Task<Result<CouncilDecision, string>> ConveneCouncilAsync(
        CouncilTopic topic,
        CouncilConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Convenes a council debate with default configuration.
    /// </summary>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the council's decision or an error.</returns>
    Task<Result<CouncilDecision, string>> ConveneCouncilAsync(
        CouncilTopic topic,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the list of agent personas participating in the council.
    /// </summary>
    IReadOnlyList<IAgentPersona> Agents { get; }

    /// <summary>
    /// Adds an agent to the council.
    /// </summary>
    /// <param name="agent">The agent to add.</param>
    void AddAgent(IAgentPersona agent);

    /// <summary>
    /// Removes an agent from the council.
    /// </summary>
    /// <param name="agentName">The name of the agent to remove.</param>
    /// <returns>True if the agent was removed, false if not found.</returns>
    bool RemoveAgent(string agentName);
}
