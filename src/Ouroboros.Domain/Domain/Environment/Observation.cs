// <copyright file="Observation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Environment;

/// <summary>
/// Represents an observation received from the environment after taking an action.
/// Contains the new state, reward, and terminal flag.
/// </summary>
/// <param name="State">The resulting environment state</param>
/// <param name="Reward">The reward received for the action</param>
/// <param name="IsTerminal">Whether the episode has ended</param>
/// <param name="Info">Additional information about the outcome</param>
public sealed record Observation(
    EnvironmentState State,
    double Reward,
    bool IsTerminal,
    IReadOnlyDictionary<string, object>? Info = null);
