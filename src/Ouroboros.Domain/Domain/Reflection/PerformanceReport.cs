// <copyright file="PerformanceReport.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Comprehensive performance report aggregating metrics across all task types.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="AverageSuccessRate">Overall average success rate (0.0 to 1.0)</param>
/// <param name="AverageExecutionTime">Average execution time across all tasks</param>
/// <param name="ByTaskType">Performance breakdown by task type</param>
/// <param name="Insights">Derived insights from analysis</param>
/// <param name="GeneratedAt">Timestamp when this report was generated</param>
public sealed record PerformanceReport(
    double AverageSuccessRate,
    TimeSpan AverageExecutionTime,
    IReadOnlyDictionary<string, TaskPerformance> ByTaskType,
    IReadOnlyList<Insight> Insights,
    DateTime GeneratedAt)
{
    /// <summary>
    /// Gets the total number of tasks analyzed.
    /// </summary>
    public int TotalTasks => this.ByTaskType.Count;

    /// <summary>
    /// Gets the task types sorted by success rate (descending).
    /// </summary>
    public IEnumerable<TaskPerformance> BestPerformingTasks =>
        this.ByTaskType.Values.OrderByDescending(t => t.SuccessRate);

    /// <summary>
    /// Gets the task types sorted by success rate (ascending).
    /// </summary>
    public IEnumerable<TaskPerformance> WorstPerformingTasks =>
        this.ByTaskType.Values.OrderBy(t => t.SuccessRate);
}
