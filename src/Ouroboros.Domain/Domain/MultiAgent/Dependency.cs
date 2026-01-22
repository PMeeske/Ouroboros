// <copyright file="Dependency.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Represents a dependency relationship between two tasks.
/// </summary>
/// <param name="TaskA">The first task identifier.</param>
/// <param name="TaskB">The second task identifier.</param>
/// <param name="Type">The type of dependency relationship.</param>
public sealed record Dependency(
    string TaskA,
    string TaskB,
    DependencyType Type);
