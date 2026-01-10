// <copyright file="EnvironmentType.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Defines the types of environments supported for embodied simulation.
/// </summary>
public enum EnvironmentType
{
    /// <summary>
    /// Unity ML-Agents environment.
    /// </summary>
    Unity,

    /// <summary>
    /// OpenAI Gym compatible environment.
    /// </summary>
    Gym,

    /// <summary>
    /// Custom environment implementation.
    /// </summary>
    Custom,

    /// <summary>
    /// Physics simulation environment.
    /// </summary>
    Simulation,
}
