// <copyright file="CollaborativePlan.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Represents a collaborative plan with task assignments and dependencies.
/// </summary>
/// <param name="Goal">The overall goal of the collaborative effort.</param>
/// <param name="Assignments">Task assignments for each agent.</param>
/// <param name="Dependencies">Dependencies between tasks.</param>
/// <param name="EstimatedDuration">Estimated time to complete the plan.</param>
public sealed record CollaborativePlan(
    string Goal,
    List<TaskAssignment> Assignments,
    List<Dependency> Dependencies,
    TimeSpan EstimatedDuration);
