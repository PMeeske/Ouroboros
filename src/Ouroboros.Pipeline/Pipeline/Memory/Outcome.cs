// <copyright file="Outcome.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;

/// <summary>
/// Represents the outcome of a pipeline execution.
/// Immutable record that captures success status, output, duration, and errors.
/// </summary>
/// <param name="Success">Whether the execution succeeded.</param>
/// <param name="Output">The output produced by the execution.</param>
/// <param name="Duration">How long the execution took.</param>
/// <param name="Errors">List of errors that occurred during execution.</param>
public sealed record Outcome(
    bool Success,
    string Output,
    TimeSpan Duration,
    ImmutableList<string> Errors)
{
    /// <summary>
    /// Creates a successful outcome.
    /// </summary>
    /// <param name="output">The output produced.</param>
    /// <param name="duration">The duration of execution.</param>
    /// <returns>A successful outcome.</returns>
    public static Outcome Successful(string output, TimeSpan duration) =>
        new(true, output, duration, ImmutableList<string>.Empty);

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="output">The output produced (may be partial).</param>
    /// <param name="duration">The duration of execution.</param>
    /// <param name="errors">The errors that occurred.</param>
    /// <returns>A failed outcome.</returns>
    public static Outcome Failed(string output, TimeSpan duration, IEnumerable<string> errors) =>
        new(false, output, duration, errors.ToImmutableList());
}
