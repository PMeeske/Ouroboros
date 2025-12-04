#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Uncertainty Router Implementation
// Routes based on confidence with fallback strategies
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Implementation of uncertainty-aware routing with fallback strategies.
/// </summary>
public sealed class UncertaintyRouter : IUncertaintyRouter
{
    private readonly IModelOrchestrator _orchestrator;
    private readonly ConcurrentDictionary<string, List<(RoutingDecision decision, bool success)>> _routingHistory = new();

    /// <inheritdoc/>
    public double MinimumConfidenceThreshold { get; }

    public UncertaintyRouter(IModelOrchestrator orchestrator, double minConfidenceThreshold = 0.7)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        MinimumConfidenceThreshold = Math.Clamp(minConfidenceThreshold, 0.0, 1.0);
    }

    /// <summary>
    /// Routes a task based on confidence analysis.
    /// </summary>
    public async Task<Result<RoutingDecision, string>> RouteAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(task))
            return Result<RoutingDecision, string>.Failure("Task cannot be empty");

        try
        {
            // Use orchestrator to determine best route
            Result<OrchestratorDecision, string> orchestratorDecision = await _orchestrator.SelectModelAsync(task, context, ct);

            return orchestratorDecision.Match(
                decision =>
                {
                    double confidence = decision.ConfidenceScore;

                    // If confidence is below threshold, apply fallback strategy
                    if (confidence < MinimumConfidenceThreshold)
                    {
                        FallbackStrategy fallback = DetermineFallback(task, confidence);
                        Dictionary<string, object> metadata = new Dictionary<string, object>
                        {
                            ["original_route"] = decision.ModelName,
                            ["fallback_strategy"] = fallback.ToString(),
                            ["original_confidence"] = confidence
                        };

                        RoutingDecision routingDecision = new RoutingDecision(
                            ApplyFallbackRoute(decision.ModelName, fallback),
                            $"Low confidence ({confidence:P0}), using {fallback}",
                            confidence,
                            metadata);

                        return Result<RoutingDecision, string>.Success(routingDecision);
                    }

                    // High confidence - use direct route
                    RoutingDecision directDecision = new RoutingDecision(
                        decision.ModelName,
                        decision.Reason,
                        confidence,
                        new Dictionary<string, object>
                        {
                            ["strategy"] = "direct",
                            ["use_case"] = context?.GetValueOrDefault("use_case", "unknown")?.ToString() ?? "unknown"
                        });

                    return Result<RoutingDecision, string>.Success(directDecision);
                },
                error => Result<RoutingDecision, string>.Failure(error));
        }
        catch (Exception ex)
        {
            return Result<RoutingDecision, string>.Failure($"Routing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines fallback strategy based on confidence.
    /// </summary>
    public FallbackStrategy DetermineFallback(string task, double confidence)
    {
        // Very low confidence - need more information
        if (confidence < 0.3)
        {
            return task.Length < 50
                ? FallbackStrategy.RequestClarification
                : FallbackStrategy.GatherMoreContext;
        }

        // Low confidence - use safer approaches
        if (confidence < 0.5)
        {
            return task.Split(' ').Length > 20
                ? FallbackStrategy.DecomposeTask
                : FallbackStrategy.UseEnsemble;
        }

        // Moderate confidence - use ensemble for better results
        if (confidence < MinimumConfidenceThreshold)
        {
            return FallbackStrategy.UseEnsemble;
        }

        // Should not reach here, but provide safe default
        return FallbackStrategy.UseDefault;
    }

    /// <summary>
    /// Calculates confidence for a routing decision.
    /// </summary>
    public async Task<double> CalculateConfidenceAsync(
        string task,
        string route,
        Dictionary<string, object>? context = null)
    {
        if (string.IsNullOrWhiteSpace(task) || string.IsNullOrWhiteSpace(route))
            return 0.0;

        // Get historical performance for this route
        List<(RoutingDecision decision, bool success)> history = GetRouteHistory(route);
        double baseConfidence = history.Any()
            ? history.Count(h => h.success) / (double)history.Count
            : 0.5;

        // Adjust based on task complexity
        double complexityFactor = 1.0 - (task.Split(' ').Length / 100.0);
        complexityFactor = Math.Clamp(complexityFactor, 0.5, 1.0);

        // Adjust based on context availability
        double contextFactor = context != null && context.Any() ? 1.1 : 0.9;

        double confidence = baseConfidence * complexityFactor * contextFactor;
        return await Task.FromResult(Math.Clamp(confidence, 0.0, 1.0));
    }

    /// <summary>
    /// Records routing outcome for learning.
    /// </summary>
    public void RecordRoutingOutcome(RoutingDecision decision, bool success)
    {
        ArgumentNullException.ThrowIfNull(decision);

        _routingHistory.AddOrUpdate(
            decision.Route,
            _ => new List<(RoutingDecision, bool)> { (decision, success) },
            (_, existing) =>
            {
                existing.Add((decision, success));
                // Keep only recent history (last 100 entries)
                return existing.TakeLast(100).ToList();
            });
    }

    private List<(RoutingDecision decision, bool success)> GetRouteHistory(string route)
    {
        return _routingHistory.TryGetValue(route, out List<(RoutingDecision decision, bool success)>? history)
            ? history
            : new List<(RoutingDecision, bool)>();
    }

    private string ApplyFallbackRoute(string originalRoute, FallbackStrategy fallback)
    {
        return fallback switch
        {
            FallbackStrategy.UseDefault => "default",
            FallbackStrategy.RequestClarification => "clarification_needed",
            FallbackStrategy.UseEnsemble => $"ensemble:{originalRoute}",
            FallbackStrategy.DecomposeTask => "task_decomposer",
            FallbackStrategy.GatherMoreContext => "context_gatherer",
            _ => originalRoute
        };
    }
}
