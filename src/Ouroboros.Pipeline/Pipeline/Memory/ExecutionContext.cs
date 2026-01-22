// <copyright file="ExecutionContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;

/// <summary>
/// Represents the execution context for a pipeline run.
/// Contains metadata and environmental information about the execution.
/// </summary>
/// <param name="Goal">The goal or objective being pursued.</param>
/// <param name="Metadata">Additional metadata about the execution.</param>
public sealed record ExecutionContext(
    string Goal,
    ImmutableDictionary<string, object> Metadata)
{
    /// <summary>
    /// Creates an execution context with a goal and no metadata.
    /// </summary>
    /// <param name="goal">The goal of the execution.</param>
    /// <returns>A new execution context.</returns>
    public static ExecutionContext WithGoal(string goal) =>
        new(goal, ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Returns a new context with additional metadata.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new execution context with the added metadata.</returns>
    public ExecutionContext WithMetadata(string key, object value) =>
        this with { Metadata = Metadata.Add(key, value) };
}
