// <copyright file="EnvironmentState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Environment;

/// <summary>
/// Represents the state of an environment at a specific point in time.
/// Immutable record that can be serialized for replay.
/// </summary>
/// <param name="StateData">Dictionary containing the state data</param>
/// <param name="IsTerminal">Whether this state is terminal (episode ended)</param>
public sealed record EnvironmentState(
    IReadOnlyDictionary<string, object> StateData,
    bool IsTerminal = false);
