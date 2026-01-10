// <copyright file="EnvironmentInfo.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Information about an available environment.
/// Used for discovery and selection of environments.
/// </summary>
/// <param name="Name">Environment name</param>
/// <param name="Description">Human-readable description</param>
/// <param name="AvailableActions">List of actions supported by this environment</param>
/// <param name="Observations">List of observation types provided</param>
/// <param name="Type">Type of environment</param>
public sealed record EnvironmentInfo(
    string Name,
    string Description,
    IReadOnlyList<string> AvailableActions,
    IReadOnlyList<string> Observations,
    EnvironmentType Type);
