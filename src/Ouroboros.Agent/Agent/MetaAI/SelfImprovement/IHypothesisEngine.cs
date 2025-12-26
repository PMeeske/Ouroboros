#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Hypothesis Engine Interface
// Scientific reasoning and hypothesis testing
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a hypothesis about system behavior or domain knowledge.
/// </summary>
public sealed record Hypothesis(
    Guid Id,
    string Statement,
    string Domain,
    double Confidence,
    List<string> SupportingEvidence,
    List<string> CounterEvidence,
    DateTime CreatedAt,
    bool Tested,
    bool? Validated);

/// <summary>
/// Represents an experiment designed to test a hypothesis.
/// </summary>
public sealed record Experiment(
    Guid Id,
    Hypothesis Hypothesis,
    string Description,
    List<PlanStep> Steps,
    Dictionary<string, object> ExpectedOutcomes,
    DateTime DesignedAt);

/// <summary>
/// Result of hypothesis testing.
/// </summary>
public sealed record HypothesisTestResult(
    Hypothesis Hypothesis,
    Experiment Experiment,
    ExecutionResult Execution,
    bool HypothesisSupported,
    double ConfidenceAdjustment,
    string Explanation,
    DateTime TestedAt);

/// <summary>
/// Configuration for hypothesis generation and testing.
/// </summary>
public sealed record HypothesisEngineConfig(
    double MinConfidenceForTesting = 0.3,
    int MaxHypothesesPerDomain = 10,
    bool EnableAbductiveReasoning = true,
    bool AutoGenerateCounterExamples = true);

/// <summary>
/// Interface for hypothesis generation and scientific reasoning.
/// Enables the agent to form and test hypotheses about its environment.
/// </summary>
public interface IHypothesisEngine
{
    /// <summary>
    /// Generates a hypothesis to explain an observation or pattern.
    /// </summary>
    /// <param name="observation">The observation to explain</param>
    /// <param name="context">Optional context information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated hypothesis</returns>
    Task<Result<Hypothesis, string>> GenerateHypothesisAsync(
        string observation,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Designs an experiment to test a hypothesis.
    /// </summary>
    /// <param name="hypothesis">The hypothesis to test</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Designed experiment</returns>
    Task<Result<Experiment, string>> DesignExperimentAsync(
        Hypothesis hypothesis,
        CancellationToken ct = default);

    /// <summary>
    /// Tests a hypothesis by executing an experiment.
    /// </summary>
    /// <param name="hypothesis">The hypothesis to test</param>
    /// <param name="experiment">The experiment to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Test result with validation</returns>
    Task<Result<HypothesisTestResult, string>> TestHypothesisAsync(
        Hypothesis hypothesis,
        Experiment experiment,
        CancellationToken ct = default);

    /// <summary>
    /// Uses abductive reasoning to infer the best explanation for observations.
    /// </summary>
    /// <param name="observations">List of observations</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Best explanation hypothesis</returns>
    Task<Result<Hypothesis, string>> AbductiveReasoningAsync(
        List<string> observations,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all hypotheses for a specific domain.
    /// </summary>
    /// <param name="domain">The domain to filter by</param>
    /// <returns>List of hypotheses</returns>
    List<Hypothesis> GetHypothesesByDomain(string domain);

    /// <summary>
    /// Updates a hypothesis based on new evidence.
    /// </summary>
    /// <param name="hypothesisId">ID of the hypothesis to update</param>
    /// <param name="evidence">New evidence (supporting or counter)</param>
    /// <param name="supports">Whether evidence supports the hypothesis</param>
    void UpdateHypothesis(Guid hypothesisId, string evidence, bool supports);

    /// <summary>
    /// Gets the confidence trend for a hypothesis over time.
    /// </summary>
    /// <param name="hypothesisId">ID of the hypothesis</param>
    /// <returns>List of confidence values over time</returns>
    List<(DateTime time, double confidence)> GetConfidenceTrend(Guid hypothesisId);
}
