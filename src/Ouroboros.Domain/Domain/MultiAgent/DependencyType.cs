// <copyright file="DependencyType.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Defines the type of dependency between tasks.
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// TaskA is blocked by TaskB and cannot start until TaskB completes.
    /// </summary>
    BlockedBy,

    /// <summary>
    /// TaskA requires the output of TaskB.
    /// </summary>
    Requires,

    /// <summary>
    /// Tasks must synchronize at a specific point.
    /// </summary>
    Synchronize,
}
