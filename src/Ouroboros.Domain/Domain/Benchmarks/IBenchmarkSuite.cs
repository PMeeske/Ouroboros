// <copyright file="IBenchmarkSuite.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Interface for benchmark suite implementations that evaluate AI capabilities
/// across multiple dimensions and standard benchmarks.
/// </summary>
public interface IBenchmarkSuite
{
    /// <summary>
    /// Runs the ARC-AGI-2 benchmark for abstract reasoning and pattern recognition.
    /// </summary>
    /// <param name="taskCount">Number of tasks to include in the benchmark (default: 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the benchmark report or an error message.</returns>
    Task<Result<BenchmarkReport, string>> RunARCBenchmarkAsync(
        int taskCount = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Runs the MMLU (Massive Multitask Language Understanding) benchmark.
    /// </summary>
    /// <param name="subjects">List of subjects to test (e.g., "mathematics", "history").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the benchmark report or an error message.</returns>
    Task<Result<BenchmarkReport, string>> RunMMLUBenchmarkAsync(
        List<string> subjects,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a continual learning benchmark to measure catastrophic forgetting.
    /// </summary>
    /// <param name="sequences">Task sequences for continual learning evaluation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the benchmark report or an error message.</returns>
    Task<Result<BenchmarkReport, string>> RunContinualLearningBenchmarkAsync(
        List<TaskSequence> sequences,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a cognitive dimension-specific benchmark.
    /// </summary>
    /// <param name="dimension">The cognitive dimension to benchmark.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the benchmark report or an error message.</returns>
    Task<Result<BenchmarkReport, string>> RunCognitiveBenchmarkAsync(
        CognitiveDimension dimension,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a comprehensive evaluation across all available benchmarks.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the comprehensive report or an error message.</returns>
    Task<Result<ComprehensiveReport, string>> RunFullEvaluationAsync(
        CancellationToken ct = default);
}
