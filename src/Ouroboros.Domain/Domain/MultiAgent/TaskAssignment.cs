// <copyright file="TaskAssignment.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Represents a task assigned to a specific agent.
/// </summary>
/// <param name="TaskDescription">Description of what needs to be done.</param>
/// <param name="AssignedTo">The agent assigned to perform this task.</param>
/// <param name="Deadline">When the task should be completed.</param>
/// <param name="Dependencies">Other agents this task depends on.</param>
/// <param name="Priority">The priority level of this task.</param>
public sealed record TaskAssignment(
    string TaskDescription,
    AgentId AssignedTo,
    DateTime Deadline,
    List<AgentId> Dependencies,
    Priority Priority);
