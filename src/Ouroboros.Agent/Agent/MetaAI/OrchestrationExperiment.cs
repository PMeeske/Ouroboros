#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// A/B Testing Framework for Orchestration
// Enables empirical comparison of orchestration strategies
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Interface for orchestration A/B testing experiments.
/// </summary>
public interface IOrchestrationExperiment
{
    /// <summary>
    /// Runs an A/B test comparing multiple orchestration strategies.
    /// </summary>
    /// <param name="experimentId">Unique identifier for the experiment.</param>
    /// <param name="variants">List of orchestrator variants to compare.</param>
    /// <param name="testPrompts">Test prompts to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing experiment results or error message.</returns>
    Task<Result<ExperimentResult, string>> RunExperimentAsync(
        string experimentId,
        List<IModelOrchestrator> variants,
        List<string> testPrompts,
        CancellationToken ct = default);
}

/// <summary>
/// Implements A/B testing for orchestration strategies with statistical analysis.
/// </summary>
public sealed class OrchestrationExperiment : IOrchestrationExperiment
{
    private readonly ConcurrentDictionary<string, ExperimentResult> _experimentResults = new();
    private readonly ConcurrentDictionary<string, ExperimentState> _runningExperiments = new();

    /// <summary>
    /// Gets all completed experiment results.
    /// </summary>
    public IReadOnlyDictionary<string, ExperimentResult> CompletedExperiments =>
        _experimentResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// Gets currently running experiments.
    /// </summary>
    public IEnumerable<string> RunningExperiments => _runningExperiments.Keys;

    /// <inheritdoc/>
    public async Task<Result<ExperimentResult, string>> RunExperimentAsync(
        string experimentId,
        List<IModelOrchestrator> variants,
        List<string> testPrompts,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
        {
            return Result<ExperimentResult, string>.Failure("Experiment ID cannot be empty");
        }

        if (variants == null || variants.Count < 2)
        {
            return Result<ExperimentResult, string>.Failure("At least 2 variants required for A/B testing");
        }

        if (testPrompts == null || testPrompts.Count == 0)
        {
            return Result<ExperimentResult, string>.Failure("At least 1 test prompt required");
        }

        // Check if experiment is already running
        if (_runningExperiments.ContainsKey(experimentId))
        {
            return Result<ExperimentResult, string>.Failure($"Experiment '{experimentId}' is already running");
        }

        // Track running state
        var state = new ExperimentState(experimentId, DateTime.UtcNow);
        _runningExperiments[experimentId] = state;

        try
        {
            var variantResults = new List<VariantResult>();

            for (int i = 0; i < variants.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var variantResult = await EvaluateVariantAsync(
                    $"variant_{i}",
                    variants[i],
                    testPrompts,
                    ct);

                variantResults.Add(variantResult);
            }

            // Calculate statistical analysis
            var analysis = CalculateStatisticalAnalysis(variantResults);

            var result = new ExperimentResult(
                ExperimentId: experimentId,
                StartedAt: state.StartedAt,
                CompletedAt: DateTime.UtcNow,
                VariantResults: variantResults,
                Analysis: analysis,
                Winner: DetermineWinner(variantResults, analysis),
                Status: ExperimentStatus.Completed);

            _experimentResults[experimentId] = result;
            return Result<ExperimentResult, string>.Success(result);
        }
        catch (OperationCanceledException)
        {
            var cancelledResult = new ExperimentResult(
                ExperimentId: experimentId,
                StartedAt: state.StartedAt,
                CompletedAt: DateTime.UtcNow,
                VariantResults: new List<VariantResult>(),
                Analysis: null,
                Winner: null,
                Status: ExperimentStatus.Cancelled);

            _experimentResults[experimentId] = cancelledResult;
            return Result<ExperimentResult, string>.Failure("Experiment was cancelled");
        }
        catch (Exception ex)
        {
            var failedResult = new ExperimentResult(
                ExperimentId: experimentId,
                StartedAt: state.StartedAt,
                CompletedAt: DateTime.UtcNow,
                VariantResults: new List<VariantResult>(),
                Analysis: null,
                Winner: null,
                Status: ExperimentStatus.Failed);

            _experimentResults[experimentId] = failedResult;
            return Result<ExperimentResult, string>.Failure($"Experiment failed: {ex.Message}");
        }
        finally
        {
            _runningExperiments.TryRemove(experimentId, out _);
        }
    }

    /// <summary>
    /// Gets the result of a completed experiment.
    /// </summary>
    public Option<ExperimentResult> GetExperimentResult(string experimentId)
    {
        return _experimentResults.TryGetValue(experimentId, out var result)
            ? Option<ExperimentResult>.Some(result)
            : Option<ExperimentResult>.None();
    }

    /// <summary>
    /// Checks if an experiment is currently running.
    /// </summary>
    public bool IsExperimentRunning(string experimentId)
    {
        return _runningExperiments.ContainsKey(experimentId);
    }

    private async Task<VariantResult> EvaluateVariantAsync(
        string variantId,
        IModelOrchestrator orchestrator,
        List<string> testPrompts,
        CancellationToken ct)
    {
        var promptResults = new List<PromptResult>();
        var latencies = new List<double>();
        int successCount = 0;
        int totalCount = 0;

        foreach (var prompt in testPrompts)
        {
            ct.ThrowIfCancellationRequested();

            var sw = Stopwatch.StartNew();
            var result = await orchestrator.SelectModelAsync(prompt, ct: ct);
            sw.Stop();

            var latencyMs = sw.Elapsed.TotalMilliseconds;
            latencies.Add(latencyMs);
            totalCount++;

            var promptResult = result.Match(
                success =>
                {
                    successCount++;
                    return new PromptResult(
                        Prompt: prompt,
                        Success: true,
                        LatencyMs: latencyMs,
                        ConfidenceScore: success.ConfidenceScore,
                        SelectedModel: success.ModelName,
                        Error: null);
                },
                error => new PromptResult(
                    Prompt: prompt,
                    Success: false,
                    LatencyMs: latencyMs,
                    ConfidenceScore: 0,
                    SelectedModel: null,
                    Error: error));

            promptResults.Add(promptResult);
        }

        double avgLatency = latencies.Count > 0 ? latencies.Average() : 0;
        double p95Latency = CalculatePercentile(latencies, 95);
        double p99Latency = CalculatePercentile(latencies, 99);
        double successRate = totalCount > 0 ? (double)successCount / totalCount : 0;
        double avgConfidence = promptResults.Where(p => p.Success).Select(p => p.ConfidenceScore).DefaultIfEmpty(0).Average();

        return new VariantResult(
            VariantId: variantId,
            PromptResults: promptResults,
            Metrics: new VariantMetrics(
                SuccessRate: successRate,
                AverageLatencyMs: avgLatency,
                P95LatencyMs: p95Latency,
                P99LatencyMs: p99Latency,
                AverageConfidence: avgConfidence,
                TotalPrompts: totalCount,
                SuccessfulPrompts: successCount));
    }

    private static double CalculatePercentile(List<double> values, int percentile)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling((percentile / 100.0) * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    private StatisticalAnalysis CalculateStatisticalAnalysis(List<VariantResult> variants)
    {
        if (variants.Count < 2) return new StatisticalAnalysis(0, false, "Insufficient variants");

        var latencies = variants.Select(v => v.Metrics.AverageLatencyMs).ToList();
        var successRates = variants.Select(v => v.Metrics.SuccessRate).ToList();

        // Calculate effect size (Cohen's d) for latency
        double effectSize = CalculateEffectSize(latencies);

        // Statistical significance (simplified - assumes normal distribution)
        bool isSignificant = effectSize > 0.5; // Medium effect size threshold

        string interpretation = effectSize switch
        {
            < 0.2 => "Negligible difference between variants",
            < 0.5 => "Small difference between variants",
            < 0.8 => "Medium difference between variants",
            _ => "Large difference between variants"
        };

        return new StatisticalAnalysis(effectSize, isSignificant, interpretation);
    }

    private static double CalculateEffectSize(List<double> values)
    {
        if (values.Count < 2) return 0;

        double mean = values.Average();
        double variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        double stdDev = Math.Sqrt(variance);

        if (stdDev == 0) return 0;

        // Return normalized effect size
        double maxDiff = values.Max() - values.Min();
        return maxDiff / stdDev;
    }

    private static string? DetermineWinner(List<VariantResult> variants, StatisticalAnalysis? analysis)
    {
        if (variants.Count == 0 || analysis == null) return null;

        // Score each variant based on multiple factors
        var scores = variants.Select(v => new
        {
            v.VariantId,
            Score = CalculateVariantScore(v.Metrics)
        }).ToList();

        var winner = scores.OrderByDescending(s => s.Score).First();

        // Only declare winner if statistically significant
        return analysis.IsSignificant ? winner.VariantId : null;
    }

    private static double CalculateVariantScore(VariantMetrics metrics)
    {
        // Weighted scoring: success rate (40%), latency (30%), confidence (30%)
        double latencyScore = 1.0 / (1.0 + metrics.AverageLatencyMs / 1000); // Normalize latency
        return (metrics.SuccessRate * 0.4) +
               (latencyScore * 0.3) +
               (metrics.AverageConfidence * 0.3);
    }
}

/// <summary>
/// Result of an A/B testing experiment.
/// </summary>
public sealed record ExperimentResult(
    string ExperimentId,
    DateTime StartedAt,
    DateTime CompletedAt,
    List<VariantResult> VariantResults,
    StatisticalAnalysis? Analysis,
    string? Winner,
    ExperimentStatus Status)
{
    /// <summary>Duration of the experiment.</summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>Whether the experiment completed successfully.</summary>
    public bool IsCompleted => Status == ExperimentStatus.Completed;
}

/// <summary>
/// Result for a single variant in the experiment.
/// </summary>
public sealed record VariantResult(
    string VariantId,
    List<PromptResult> PromptResults,
    VariantMetrics Metrics);

/// <summary>
/// Aggregated metrics for a variant.
/// </summary>
public sealed record VariantMetrics(
    double SuccessRate,
    double AverageLatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double AverageConfidence,
    int TotalPrompts,
    int SuccessfulPrompts);

/// <summary>
/// Result for a single prompt evaluation.
/// </summary>
public sealed record PromptResult(
    string Prompt,
    bool Success,
    double LatencyMs,
    double ConfidenceScore,
    string? SelectedModel,
    string? Error);

/// <summary>
/// Statistical analysis of experiment results.
/// </summary>
public sealed record StatisticalAnalysis(
    double EffectSize,
    bool IsSignificant,
    string Interpretation);

/// <summary>
/// Status of an experiment.
/// </summary>
public enum ExperimentStatus
{
    /// <summary>Experiment is currently running.</summary>
    Running,

    /// <summary>Experiment completed successfully.</summary>
    Completed,

    /// <summary>Experiment was cancelled.</summary>
    Cancelled,

    /// <summary>Experiment failed due to an error.</summary>
    Failed
}

/// <summary>
/// Internal state tracking for running experiments.
/// </summary>
internal sealed record ExperimentState(
    string ExperimentId,
    DateTime StartedAt);
