#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Self-Evaluator Interface
// Metacognitive monitoring and self-improvement
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a self-assessment of agent performance.
/// </summary>
public sealed record SelfAssessment(
    double OverallPerformance,
    double ConfidenceCalibration,
    double SkillAcquisitionRate,
    Dictionary<string, double> CapabilityScores,
    List<string> Strengths,
    List<string> Weaknesses,
    DateTime AssessmentTime,
    string Summary);

/// <summary>
/// Represents an insight gained from self-reflection.
/// </summary>
public sealed record Insight(
    string Category,
    string Description,
    double Confidence,
    List<string> SupportingEvidence,
    DateTime DiscoveredAt);

/// <summary>
/// Represents a plan for self-improvement.
/// </summary>
public sealed record ImprovementPlan(
    string Goal,
    List<string> Actions,
    Dictionary<string, double> ExpectedImprovements,
    TimeSpan EstimatedDuration,
    double Priority,
    DateTime CreatedAt);

/// <summary>
/// Interface for agent self-evaluation and metacognition.
/// Enables autonomous performance assessment and improvement planning.
/// </summary>
public interface ISelfEvaluator
{
    /// <summary>
    /// Evaluates current performance across all capabilities.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Comprehensive self-assessment</returns>
    Task<Result<SelfAssessment, string>> EvaluatePerformanceAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Generates insights from recent experiences and performance data.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of insights discovered</returns>
    Task<List<Insight>> GenerateInsightsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Suggests improvement strategies based on weaknesses.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Prioritized improvement plan</returns>
    Task<Result<ImprovementPlan, string>> SuggestImprovementsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Tracks confidence calibration over time.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Calibration score (1.0 = perfect calibration)</returns>
    Task<double> GetConfidenceCalibrationAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Records a prediction and its actual outcome for calibration.
    /// </summary>
    /// <param name="predictedConfidence">Predicted confidence (0-1)</param>
    /// <param name="actualSuccess">Actual outcome</param>
    void RecordPrediction(double predictedConfidence, bool actualSuccess);

    /// <summary>
    /// Gets performance trends over time.
    /// </summary>
    /// <param name="metric">The metric to analyze (e.g., "success_rate", "skill_count")</param>
    /// <param name="timeWindow">Time window for trend analysis</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Trend data points</returns>
    Task<List<(DateTime Time, double Value)>> GetPerformanceTrendAsync(
        string metric,
        TimeSpan timeWindow,
        CancellationToken ct = default);
}
