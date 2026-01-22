// <copyright file="BenchmarkReport.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Represents a comprehensive report for a single benchmark execution.
/// </summary>
/// <param name="BenchmarkName">The name of the benchmark.</param>
/// <param name="OverallScore">Overall normalized score (0.0 to 1.0) for the benchmark.</param>
/// <param name="SubScores">Scores for different sub-categories or dimensions.</param>
/// <param name="DetailedResults">Detailed results for each task in the benchmark.</param>
/// <param name="TotalDuration">Total time taken to complete the benchmark.</param>
/// <param name="CompletedAt">Timestamp when the benchmark was completed.</param>
public sealed record BenchmarkReport(
    string BenchmarkName,
    double OverallScore,
    Dictionary<string, double> SubScores,
    List<TaskResult> DetailedResults,
    TimeSpan TotalDuration,
    DateTime CompletedAt);
