// <copyright file="ComprehensiveReport.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Represents a comprehensive report aggregating results from multiple benchmarks.
/// </summary>
/// <param name="BenchmarkResults">Results from each individual benchmark keyed by benchmark name.</param>
/// <param name="OverallScore">Aggregated overall score across all benchmarks.</param>
/// <param name="Strengths">Identified areas of strength based on benchmark results.</param>
/// <param name="Weaknesses">Identified areas of weakness based on benchmark results.</param>
/// <param name="Recommendations">Actionable recommendations for improvement.</param>
/// <param name="GeneratedAt">Timestamp when the report was generated.</param>
public sealed record ComprehensiveReport(
    Dictionary<string, BenchmarkReport> BenchmarkResults,
    double OverallScore,
    List<string> Strengths,
    List<string> Weaknesses,
    List<string> Recommendations,
    DateTime GeneratedAt);
