// <copyright file="AllocationStrategy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Strategy for allocating tasks to agents.
/// </summary>
public enum AllocationStrategy
{
    /// <summary>
    /// Distribute tasks in round-robin fashion.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Allocate tasks based on agent skills and capabilities.
    /// </summary>
    SkillBased,

    /// <summary>
    /// Distribute tasks to balance load across agents.
    /// </summary>
    LoadBalanced,

    /// <summary>
    /// Use an auction mechanism where agents bid for tasks.
    /// </summary>
    Auction,
}
