// <copyright file="IReflectionEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Persistence;

/// <summary>
/// Interface for the reflection engine providing meta-cognitive analysis capabilities.
/// Follows functional programming principles with Result monad for error handling.
/// </summary>
public interface IReflectionEngine
{
    /// <summary>
    /// Analyzes performance across recent episodes within a specified time period.
    /// </summary>
    /// <param name="recentEpisodes">List of episodes to analyze</param>
    /// <param name="period">Time period to consider for analysis</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing performance report or error message</returns>
    Task<Result<PerformanceReport, string>> AnalyzePerformanceAsync(
        IReadOnlyList<Episode> recentEpisodes,
        TimeSpan period,
        CancellationToken ct = default);

    /// <summary>
    /// Detects recurring error patterns using clustering and pattern matching.
    /// </summary>
    /// <param name="failures">List of failed episodes to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing list of detected error patterns or error message</returns>
    Task<Result<IReadOnlyList<ErrorPattern>, string>> DetectErrorPatternsAsync(
        IReadOnlyList<FailedEpisode> failures,
        CancellationToken ct = default);

    /// <summary>
    /// Assesses capabilities across all cognitive dimensions using benchmark tasks.
    /// </summary>
    /// <param name="tasks">Benchmark tasks to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing capability map or error message</returns>
    Task<Result<CapabilityMap, string>> AssessCapabilitiesAsync(
        IReadOnlyList<BenchmarkTask> tasks,
        CancellationToken ct = default);

    /// <summary>
    /// Generates actionable improvement suggestions based on performance analysis.
    /// </summary>
    /// <param name="report">Performance report to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing list of improvement suggestions or error message</returns>
    Task<Result<IReadOnlyList<ImprovementSuggestion>, string>> SuggestImprovementsAsync(
        PerformanceReport report,
        CancellationToken ct = default);

    /// <summary>
    /// Assesses certainty of a claim using Laws of Form three-valued logic.
    /// Integrates epistemic uncertainty modeling with evidence evaluation.
    /// </summary>
    /// <param name="claim">The claim to assess</param>
    /// <param name="evidence">Supporting or contradicting evidence</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing Form (Mark/Void/Imaginary) representing certainty or error message</returns>
    Task<Result<Form, string>> AssessCertaintyAsync(
        string claim,
        IReadOnlyList<Fact> evidence,
        CancellationToken ct = default);
}
