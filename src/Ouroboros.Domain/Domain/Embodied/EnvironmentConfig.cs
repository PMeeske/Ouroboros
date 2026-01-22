// <copyright file="EnvironmentConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Configuration for creating an environment instance.
/// </summary>
/// <param name="SceneName">Name of the scene/environment to load</param>
/// <param name="Parameters">Configuration parameters specific to the environment</param>
/// <param name="AvailableActions">List of action names available in this environment</param>
/// <param name="Type">Type of environment</param>
public sealed record EnvironmentConfig(
    string SceneName,
    IReadOnlyDictionary<string, object> Parameters,
    IReadOnlyList<string> AvailableActions,
    EnvironmentType Type);
