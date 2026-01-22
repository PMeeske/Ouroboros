// <copyright file="Priority.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Priority levels for task assignments.
/// </summary>
public enum Priority
{
    /// <summary>
    /// Low priority task.
    /// </summary>
    Low,

    /// <summary>
    /// Medium priority task.
    /// </summary>
    Medium,

    /// <summary>
    /// High priority task.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority task requiring immediate attention.
    /// </summary>
    Critical,
}
