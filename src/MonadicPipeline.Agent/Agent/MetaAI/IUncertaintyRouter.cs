#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Uncertainty-Aware Router - Route based on confidence
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Routing decision with confidence information.
/// </summary>
public sealed record RoutingDecision(
    string Route,
    string Reason,
    double Confidence,
    Dictionary<string, object> Metadata);

/// <summary>
/// Fallback strategy when confidence is low.
/// </summary>
public enum FallbackStrategy
{
    /// <summary>
    /// Use default general-purpose model.
    /// </summary>
    UseDefault,

    /// <summary>
    /// Ask for human clarification.
    /// </summary>
    RequestClarification,

    /// <summary>
    /// Use ensemble of multiple models.
    /// </summary>
    UseEnsemble,

    /// <summary>
    /// Decompose into simpler sub-tasks.
    /// </summary>
    DecomposeTask,

    /// <summary>
    /// Retrieve more context before deciding.
    /// </summary>
    GatherMoreContext
}

/// <summary>
/// Interface for uncertainty-aware routing decisions.
/// Routes tasks based on confidence levels with fallback strategies.
/// </summary>
public interface IUncertaintyRouter
{
    /// <summary>
    /// Routes a task based on confidence analysis.
    /// </summary>
    /// <param name="task">The task to route</param>
    /// <param name="context">Optional context information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Routing decision with confidence score</returns>
    Task<Result<RoutingDecision, string>> RouteAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Determines fallback strategy for low-confidence scenarios.
    /// </summary>
    /// <param name="task">The task being routed</param>
    /// <param name="confidence">The confidence score (0-1)</param>
    /// <returns>Recommended fallback strategy</returns>
    FallbackStrategy DetermineFallback(string task, double confidence);

    /// <summary>
    /// Calculates confidence for a given decision.
    /// </summary>
    /// <param name="task">The task to assess</param>
    /// <param name="route">The proposed route</param>
    /// <param name="context">Optional context</param>
    /// <returns>Confidence score between 0 and 1</returns>
    Task<double> CalculateConfidenceAsync(
        string task,
        string route,
        Dictionary<string, object>? context = null);

    /// <summary>
    /// Gets the minimum confidence threshold for direct routing.
    /// Below this threshold, fallback strategies are triggered.
    /// </summary>
    double MinimumConfidenceThreshold { get; }

    /// <summary>
    /// Updates routing performance metrics.
    /// </summary>
    /// <param name="decision">The routing decision made</param>
    /// <param name="success">Whether the routing was successful</param>
    void RecordRoutingOutcome(RoutingDecision decision, bool success);
}
