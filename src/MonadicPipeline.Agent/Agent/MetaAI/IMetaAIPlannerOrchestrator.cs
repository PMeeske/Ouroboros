#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-AI Layer v2 - Planner/Executor/Verifier Orchestrator
// Implements continual learning with plan-execute-verify loop
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a plan with steps and expected outcomes.
/// </summary>
public sealed record Plan(
    string Goal,
    List<PlanStep> Steps,
    Dictionary<string, double> ConfidenceScores,
    DateTime CreatedAt);

/// <summary>
/// Represents a single step in a plan.
/// </summary>
public sealed record PlanStep(
    string Action,
    Dictionary<string, object> Parameters,
    string ExpectedOutcome,
    double ConfidenceScore);

/// <summary>
/// Represents the result of executing a plan.
/// </summary>
public sealed record ExecutionResult(
    Plan Plan,
    List<StepResult> StepResults,
    bool Success,
    string FinalOutput,
    Dictionary<string, object> Metadata,
    TimeSpan Duration);

/// <summary>
/// Represents the result of executing a single plan step.
/// </summary>
public sealed record StepResult(
    PlanStep Step,
    bool Success,
    string Output,
    string? Error,
    TimeSpan Duration,
    Dictionary<string, object> ObservedState);

/// <summary>
/// Represents verification result with feedback for improvement.
/// </summary>
public sealed record VerificationResult(
    ExecutionResult Execution,
    bool Verified,
    double QualityScore,
    List<string> Issues,
    List<string> Improvements,
    string? RevisedPlan);

/// <summary>
/// Core interface for Meta-AI v2 planner/executor/verifier orchestrator.
/// Implements continual learning through plan-execute-verify loop.
/// </summary>
public interface IMetaAIPlannerOrchestrator
{
    /// <summary>
    /// Plans how to accomplish a goal based on available tools and past experience.
    /// </summary>
    /// <param name="goal">The goal to accomplish</param>
    /// <param name="context">Additional context information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A plan with steps and confidence scores</returns>
    Task<Result<Plan, string>> PlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a plan step by step with monitoring.
    /// </summary>
    /// <param name="plan">The plan to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result with outcomes for each step</returns>
    Task<Result<ExecutionResult, string>> ExecuteAsync(
        Plan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies execution results and provides feedback for improvement.
    /// </summary>
    /// <param name="execution">The execution result to verify</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Verification result with quality score and suggestions</returns>
    Task<Result<VerificationResult, string>> VerifyAsync(
        ExecutionResult execution,
        CancellationToken ct = default);

    /// <summary>
    /// Learns from execution experience to improve future planning.
    /// </summary>
    /// <param name="verification">The verification result to learn from</param>
    void LearnFromExecution(VerificationResult verification);

    /// <summary>
    /// Gets performance metrics for the orchestrator.
    /// </summary>
    IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics();
}
