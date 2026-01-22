// <copyright file="EnvironmentHandle.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Handle to an active environment instance.
/// Used to reference and manage environment lifecycle.
/// </summary>
/// <param name="Id">Unique identifier for the environment instance</param>
/// <param name="SceneName">Name of the loaded scene</param>
/// <param name="Type">Type of environment</param>
/// <param name="IsRunning">Whether the environment is currently running</param>
public sealed record EnvironmentHandle(
    Guid Id,
    string SceneName,
    EnvironmentType Type,
    bool IsRunning);
