// <copyright file="TaskResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Represents the result of a single benchmark task execution.
/// </summary>
/// <param name="TaskId">Unique identifier for the task.</param>
/// <param name="TaskName">Human-readable name of the task.</param>
/// <param name="Success">Whether the task completed successfully.</param>
/// <param name="Score">Normalized score (0.0 to 1.0) for the task.</param>
/// <param name="Duration">Time taken to complete the task.</param>
/// <param name="ErrorMessage">Error message if the task failed.</param>
/// <param name="Metadata">Additional metadata about the task execution.</param>
public sealed record TaskResult(
    string TaskId,
    string TaskName,
    bool Success,
    double Score,
    TimeSpan Duration,
    string? ErrorMessage,
    Dictionary<string, object> Metadata);
