// <copyright file="AgentGroup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Represents a logical grouping of agents with a specific distribution strategy.
/// </summary>
/// <param name="Name">The name of the group.</param>
/// <param name="Members">The agents that belong to this group.</param>
/// <param name="Type">The message distribution strategy for the group.</param>
public sealed record AgentGroup(
    string Name,
    List<AgentId> Members,
    GroupType Type);
