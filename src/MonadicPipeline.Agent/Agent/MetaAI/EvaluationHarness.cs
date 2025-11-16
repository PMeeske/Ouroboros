#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Evaluation Harness - Measure Meta-AI performance
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Evaluation metrics for a single test case.
/// </summary>
public sealed record EvaluationMetrics(
    string TestCase,
    bool Success,
    double QualityScore,
    TimeSpan ExecutionTime,
    int PlanSteps,
    double ConfidenceScore,
    Dictionary<string, double> CustomMetrics);

/// <summary>
/// Aggregated evaluation results.
/// </summary>
public sealed record EvaluationResults(
    int TotalTests,
    int SuccessfulTests,
    int FailedTests,
    double AverageQualityScore,
    double AverageConfidence,
    TimeSpan AverageExecutionTime,
    List<EvaluationMetrics> TestResults,
    Dictionary<string, double> AggregatedMetrics);

/// <summary>
/// A test case for evaluation.
/// </summary>
public sealed record TestCase(
    string Name,
    string Goal,
    Dictionary<string, object>? Context,
    Func<VerificationResult, bool>? CustomValidator);

/// <summary>
/// Evaluation harness for measuring Meta-AI orchestrator performance.
/// Provides benchmarking and quality assessment capabilities.
/// </summary>
public sealed class EvaluationHarness
{
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private readonly List<EvaluationMetrics> _results = new();

    public EvaluationHarness(IMetaAIPlannerOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <summary>
    /// Evaluates the orchestrator on a single test case.
    /// </summary>
    public async Task<EvaluationMetrics> EvaluateTestCaseAsync(
        TestCase testCase,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(testCase);

        DateTime startTime = DateTime.UtcNow;
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Plan
            Result<Plan, string> planResult = await _orchestrator.PlanAsync(testCase.Goal, testCase.Context, ct);

            Plan? plan = null;
            planResult.Match(
                p => plan = p,
                error => throw new Exception($"Planning failed: {error}"));

            if (plan == null)
                throw new Exception("Plan is null");

            // Execute
            Result<ExecutionResult, string> execResult = await _orchestrator.ExecuteAsync(plan, ct);

            ExecutionResult? execution = null;
            execResult.Match(
                e => execution = e,
                error => throw new Exception($"Execution failed: {error}"));

            if (execution == null)
                throw new Exception("Execution is null");

            // Verify
            Result<VerificationResult, string> verifyResult = await _orchestrator.VerifyAsync(execution, ct);

            VerificationResult? verification = null;
            verifyResult.Match(
                v => verification = v,
                error => throw new Exception($"Verification failed: {error}"));

            if (verification == null)
                throw new Exception("Verification is null");

            // Learn from experience
            _orchestrator.LearnFromExecution(verification);

            stopwatch.Stop();

            // Apply custom validator if provided
            bool success = verification.Verified;
            if (testCase.CustomValidator != null)
            {
                success = success && testCase.CustomValidator(verification);
            }

            EvaluationMetrics metrics = new EvaluationMetrics(
                testCase.Name,
                success,
                verification.QualityScore,
                stopwatch.Elapsed,
                plan.Steps.Count,
                plan.ConfidenceScores.GetValueOrDefault("overall", 0.5),
                new Dictionary<string, double>
                {
                    ["steps_completed"] = execution.StepResults.Count,
                    ["steps_successful"] = execution.StepResults.Count(r => r.Success),
                    ["verification_score"] = verification.QualityScore
                });

            _results.Add(metrics);
            return metrics;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            EvaluationMetrics failedMetrics = new EvaluationMetrics(
                testCase.Name,
                Success: false,
                QualityScore: 0.0,
                stopwatch.Elapsed,
                PlanSteps: 0,
                ConfidenceScore: 0.0,
                new Dictionary<string, double>
                {
                    ["error"] = 1.0,
                    ["error_message_length"] = ex.Message.Length
                });

            _results.Add(failedMetrics);
            return failedMetrics;
        }
    }

    /// <summary>
    /// Evaluates the orchestrator on multiple test cases.
    /// </summary>
    public async Task<EvaluationResults> EvaluateBatchAsync(
        IEnumerable<TestCase> testCases,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(testCases);

        List<TestCase> testList = testCases.ToList();
        List<EvaluationMetrics> batchResults = new List<EvaluationMetrics>();

        foreach (TestCase? testCase in testList)
        {
            if (ct.IsCancellationRequested)
                break;

            EvaluationMetrics result = await EvaluateTestCaseAsync(testCase, ct);
            batchResults.Add(result);
        }

        return AggregateResults(batchResults);
    }

    /// <summary>
    /// Runs a benchmark suite with predefined test cases.
    /// </summary>
    public async Task<EvaluationResults> RunBenchmarkAsync(CancellationToken ct = default)
    {
        List<TestCase> benchmarkCases = new List<TestCase>
        {
            new TestCase(
                "Simple Calculation",
                "Calculate 15 * 23 + 47",
                null,
                result => result.Verified && result.QualityScore > 0.8),

            new TestCase(
                "Multi-Step Reasoning",
                "Plan a simple web application with authentication and data storage",
                new Dictionary<string, object> { ["complexity"] = "medium" },
                result => result.Verified && result.Execution.StepResults.Count >= 3),

            new TestCase(
                "Creative Task",
                "Generate a short explanation of quantum computing",
                null,
                result => result.Verified),

            new TestCase(
                "Tool Usage",
                "Use available tools to solve a math problem and explain the result",
                null,
                result => result.Verified && result.Execution.StepResults.Any(r => r.ObservedState.ContainsKey("tool"))),

            new TestCase(
                "Error Handling",
                "Attempt to execute an impossible task: divide by zero and handle the error",
                null,
                result => true) // Success is handling the error gracefully
        };

        return await EvaluateBatchAsync(benchmarkCases, ct);
    }

    /// <summary>
    /// Gets all evaluation results.
    /// </summary>
    public IReadOnlyList<EvaluationMetrics> GetAllResults() => _results.AsReadOnly();

    /// <summary>
    /// Clears all evaluation results.
    /// </summary>
    public void ClearResults() => _results.Clear();

    private EvaluationResults AggregateResults(List<EvaluationMetrics> results)
    {
        int total = results.Count;
        int successful = results.Count(r => r.Success);
        int failed = total - successful;

        double avgQuality = results.Any() ? results.Average(r => r.QualityScore) : 0.0;
        double avgConfidence = results.Any() ? results.Average(r => r.ConfidenceScore) : 0.0;
        TimeSpan avgTime = results.Any()
            ? TimeSpan.FromMilliseconds(results.Average(r => r.ExecutionTime.TotalMilliseconds))
            : TimeSpan.Zero;

        Dictionary<string, double> aggregated = new Dictionary<string, double>
        {
            ["success_rate"] = total > 0 ? successful / (double)total : 0.0,
            ["avg_plan_steps"] = results.Any() ? results.Average(r => r.PlanSteps) : 0.0,
            ["avg_quality"] = avgQuality,
            ["avg_confidence"] = avgConfidence,
            ["avg_execution_ms"] = avgTime.TotalMilliseconds
        };

        // Add custom metric aggregations
        IEnumerable<string> allCustomMetrics = results.SelectMany(r => r.CustomMetrics.Keys).Distinct();
        foreach (string? metricKey in allCustomMetrics)
        {
            IEnumerable<double> values = results
                .Where(r => r.CustomMetrics.ContainsKey(metricKey))
                .Select(r => r.CustomMetrics[metricKey]);

            if (values.Any())
            {
                aggregated[$"avg_{metricKey}"] = values.Average();
            }
        }

        return new EvaluationResults(
            total,
            successful,
            failed,
            avgQuality,
            avgConfidence,
            avgTime,
            results,
            aggregated);
    }
}
