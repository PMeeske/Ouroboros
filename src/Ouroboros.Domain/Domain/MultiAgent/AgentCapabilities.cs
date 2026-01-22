// <copyright file="AgentCapabilities.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Describes the capabilities and current state of an agent.
/// </summary>
/// <param name="Id">The unique identifier of the agent.</param>
/// <param name="Skills">List of skills this agent can perform.</param>
/// <param name="PerformanceScores">Performance metrics for each skill (0.0 to 1.0).</param>
/// <param name="CurrentLoad">Current computational load (0.0 to 1.0).</param>
/// <param name="IsAvailable">Whether the agent is available for new tasks.</param>
public sealed record AgentCapabilities(
    AgentId Id,
    List<string> Skills,
    Dictionary<string, double> PerformanceScores,
    double CurrentLoad,
    bool IsAvailable);
