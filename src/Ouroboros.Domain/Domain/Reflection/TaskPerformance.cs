// <copyright file="TaskPerformance.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Performance metrics for a specific task type.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="TaskType">The type/category of task</param>
/// <param name="TotalAttempts">Total number of attempts</param>
/// <param name="Successes">Number of successful attempts</param>
/// <param name="AverageTime">Average execution time in seconds</param>
/// <param name="CommonErrors">List of common error messages encountered</param>
public sealed record TaskPerformance(
    string TaskType,
    int TotalAttempts,
    int Successes,
    double AverageTime,
    IReadOnlyList<string> CommonErrors)
{
    /// <summary>
    /// Gets the success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate => TotalAttempts > 0 ? (double)Successes / TotalAttempts : 0.0;

    /// <summary>
    /// Gets the failure count.
    /// </summary>
    public int Failures => TotalAttempts - Successes;
}
