#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Orchestration Tracing
// OpenTelemetry-compatible tracing for AI orchestration layer
// ==========================================================

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Ouroboros.Diagnostics;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Provides OpenTelemetry-compatible tracing and metrics for the orchestration layer.
/// Enables observability into model selection, routing decisions, and planning operations.
/// </summary>
public static class OrchestrationTracing
{
    /// <summary>
    /// Activity source for orchestration operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("Ouroboros.Orchestration", "1.0.0");

    /// <summary>
    /// Meter for orchestration metrics.
    /// </summary>
    public static readonly Meter Meter = new("Ouroboros.Orchestration", "1.0.0");

    // Counters
    private static readonly Counter<long> ModelSelectionsCounter = Meter.CreateCounter<long>(
        "orchestrator.model_selections",
        "count",
        "Number of model selection operations");

    private static readonly Counter<long> RoutingDecisionsCounter = Meter.CreateCounter<long>(
        "orchestrator.routing_decisions",
        "count",
        "Number of routing decisions made");

    private static readonly Counter<long> PlanCreationsCounter = Meter.CreateCounter<long>(
        "orchestrator.plan_creations",
        "count",
        "Number of plans created");

    private static readonly Counter<long> RoutingFallbacksCounter = Meter.CreateCounter<long>(
        "orchestrator.routing_fallbacks",
        "count",
        "Number of routing fallbacks triggered");

    // Histograms
    private static readonly Histogram<double> ModelSelectionLatency = Meter.CreateHistogram<double>(
        "orchestrator.model_selection_latency_ms",
        "ms",
        "Model selection latency in milliseconds");

    private static readonly Histogram<double> RoutingLatency = Meter.CreateHistogram<double>(
        "orchestrator.routing_latency_ms",
        "ms",
        "Routing decision latency in milliseconds");

    private static readonly Histogram<double> PlanExecutionLatency = Meter.CreateHistogram<double>(
        "orchestrator.plan_execution_latency_ms",
        "ms",
        "Plan execution latency in milliseconds");

    private static readonly Histogram<double> ConfidenceScores = Meter.CreateHistogram<double>(
        "orchestrator.confidence_score",
        "score",
        "Confidence scores of orchestration decisions");

    /// <summary>
    /// Starts tracing a model selection operation.
    /// </summary>
    public static Activity? StartModelSelection(string? prompt, string? contextInfo = null)
    {
        var tags = new Dictionary<string, object?>
        {
            ["orchestrator.operation"] = "model_selection",
            ["orchestrator.prompt_length"] = (prompt?.Length ?? 0).ToString(),
            ["orchestrator.has_context"] = (contextInfo != null).ToString(),
        };

        return ActivitySource.StartActivity("orchestrator.select_model", ActivityKind.Internal, default(ActivityContext), tags!);
    }

    /// <summary>
    /// Completes a model selection trace with result information.
    /// </summary>
    public static void CompleteModelSelection(
        Activity? activity,
        string selectedModel,
        UseCaseType useCase,
        double confidenceScore,
        TimeSpan duration,
        bool success = true)
    {
        if (activity != null)
        {
            activity.SetTag("orchestrator.selected_model", selectedModel);
            activity.SetTag("orchestrator.use_case", useCase.ToString());
            activity.SetTag("orchestrator.confidence", confidenceScore);
            activity.SetTag("orchestrator.success", success);
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }

        // Record metrics
        ModelSelectionsCounter.Add(1, new KeyValuePair<string, object?>("model", selectedModel),
            new KeyValuePair<string, object?>("use_case", useCase.ToString()),
            new KeyValuePair<string, object?>("success", success));

        ModelSelectionLatency.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("model", selectedModel));

        ConfidenceScores.Record(confidenceScore,
            new KeyValuePair<string, object?>("operation", "model_selection"),
            new KeyValuePair<string, object?>("model", selectedModel));
    }

    /// <summary>
    /// Starts tracing a routing decision.
    /// </summary>
    public static Activity? StartRouting(string? task)
    {
        var tags = new Dictionary<string, object?>
        {
            ["orchestrator.operation"] = "routing",
            ["orchestrator.task_length"] = (task?.Length ?? 0).ToString(),
        };

        return ActivitySource.StartActivity("orchestrator.route", ActivityKind.Internal, default(ActivityContext), tags!);
    }

    /// <summary>
    /// Completes a routing trace with decision information.
    /// </summary>
    public static void CompleteRouting(
        Activity? activity,
        string routeType,
        double confidence,
        bool usedFallback,
        TimeSpan duration,
        bool success = true)
    {
        if (activity != null)
        {
            activity.SetTag("orchestrator.route_type", routeType);
            activity.SetTag("orchestrator.confidence", confidence.ToString());
            activity.SetTag("orchestrator.used_fallback", usedFallback.ToString());
            activity.SetTag("orchestrator.success", success.ToString());
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }

        // Record metrics
        RoutingDecisionsCounter.Add(1,
            new KeyValuePair<string, object?>("route_type", routeType),
            new KeyValuePair<string, object?>("success", success));

        if (usedFallback)
        {
            RoutingFallbacksCounter.Add(1,
                new KeyValuePair<string, object?>("route_type", routeType));
        }

        RoutingLatency.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("route_type", routeType));

        ConfidenceScores.Record(confidence,
            new KeyValuePair<string, object?>("operation", "routing"),
            new KeyValuePair<string, object?>("route_type", routeType));
    }

    /// <summary>
    /// Starts tracing a plan creation.
    /// </summary>
    public static Activity? StartPlanCreation(string? goal, int maxDepth)
    {
        var tags = new Dictionary<string, object?>
        {
            ["orchestrator.operation"] = "plan_creation",
            ["orchestrator.goal_length"] = (goal?.Length ?? 0).ToString(),
            ["orchestrator.max_depth"] = maxDepth.ToString(),
        };

        return ActivitySource.StartActivity("orchestrator.create_plan", ActivityKind.Internal, default(ActivityContext), tags!);
    }

    /// <summary>
    /// Completes a plan creation trace.
    /// </summary>
    public static void CompletePlanCreation(
        Activity? activity,
        int stepCount,
        int depth,
        TimeSpan duration,
        bool success = true)
    {
        if (activity != null)
        {
            activity.SetTag("orchestrator.step_count", stepCount.ToString());
            activity.SetTag("orchestrator.depth", depth.ToString());
            activity.SetTag("orchestrator.success", success.ToString());
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }

        // Record metrics
        PlanCreationsCounter.Add(1,
            new KeyValuePair<string, object?>("success", success));
    }

    /// <summary>
    /// Starts tracing a plan execution.
    /// </summary>
    public static Activity? StartPlanExecution(Guid planId, int stepCount)
    {
        var tags = new Dictionary<string, object?>
        {
            ["orchestrator.operation"] = "plan_execution",
            ["orchestrator.plan_id"] = planId.ToString(),
            ["orchestrator.step_count"] = stepCount.ToString(),
        };

        return ActivitySource.StartActivity("orchestrator.execute_plan", ActivityKind.Internal, default(ActivityContext), tags!);
    }

    /// <summary>
    /// Completes a plan execution trace.
    /// </summary>
    public static void CompletePlanExecution(
        Activity? activity,
        int stepsCompleted,
        int stepsFailed,
        TimeSpan duration,
        bool success = true)
    {
        if (activity != null)
        {
            activity.SetTag("orchestrator.steps_completed", stepsCompleted.ToString());
            activity.SetTag("orchestrator.steps_failed", stepsFailed.ToString());
            activity.SetTag("orchestrator.success", success.ToString());
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }

        // Record metrics
        PlanExecutionLatency.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("success", success));
    }

    /// <summary>
    /// Records an orchestration error.
    /// </summary>
    public static void RecordError(Activity? activity, string operation, Exception exception)
    {
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag("error.type", exception.GetType().FullName);
            activity.SetTag("error.message", exception.Message);
            activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection(new[]
            {
                new KeyValuePair<string, object?>("exception.type", exception.GetType().FullName),
                new KeyValuePair<string, object?>("exception.message", exception.Message),
                new KeyValuePair<string, object?>("exception.stacktrace", exception.StackTrace),
            })));
        }
    }

    /// <summary>
    /// Records a custom orchestration event.
    /// </summary>
    public static void RecordEvent(string eventName, Dictionary<string, object?>? attributes = null)
    {
        Activity? activity = Activity.Current;
        if (activity == null) return;

        var tags = new ActivityTagsCollection(
            attributes?.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))
            ?? Array.Empty<KeyValuePair<string, object?>>());

        activity.AddEvent(new ActivityEvent(eventName, tags: tags));
    }
}

/// <summary>
/// Disposable scope for tracing orchestration operations.
/// </summary>
public sealed class OrchestrationScope : IDisposable
{
    private readonly Activity? _activity;
    private readonly Stopwatch _stopwatch;
    private readonly Action<Activity?, TimeSpan> _onComplete;
    private bool _completed;

    private OrchestrationScope(Activity? activity, Action<Activity?, TimeSpan> onComplete)
    {
        _activity = activity;
        _stopwatch = Stopwatch.StartNew();
        _onComplete = onComplete;
    }

    /// <summary>
    /// Creates a model selection scope.
    /// </summary>
    public static OrchestrationScope ModelSelection(string prompt, string? contextInfo = null)
    {
        var activity = OrchestrationTracing.StartModelSelection(prompt, contextInfo);
        return new OrchestrationScope(activity, (a, d) =>
        {
            // Default completion - actual completion should be called explicitly
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Creates a routing scope.
    /// </summary>
    public static OrchestrationScope Routing(string task)
    {
        var activity = OrchestrationTracing.StartRouting(task);
        return new OrchestrationScope(activity, (a, d) =>
        {
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Creates a plan creation scope.
    /// </summary>
    public static OrchestrationScope PlanCreation(string goal, int maxDepth)
    {
        var activity = OrchestrationTracing.StartPlanCreation(goal, maxDepth);
        return new OrchestrationScope(activity, (a, d) =>
        {
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Creates a plan execution scope.
    /// </summary>
    public static OrchestrationScope PlanExecution(Guid planId, int stepCount)
    {
        var activity = OrchestrationTracing.StartPlanExecution(planId, stepCount);
        return new OrchestrationScope(activity, (a, d) =>
        {
            a?.SetStatus(ActivityStatusCode.Unset);
        });
    }

    /// <summary>
    /// Gets the elapsed time.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Gets the activity.
    /// </summary>
    public Activity? Activity => _activity;

    /// <summary>
    /// Marks the operation as successful with model selection details.
    /// </summary>
    public void CompleteModelSelection(string selectedModel, UseCaseType useCase, double confidenceScore)
    {
        _completed = true;
        _stopwatch.Stop();
        OrchestrationTracing.CompleteModelSelection(_activity, selectedModel, useCase, confidenceScore, _stopwatch.Elapsed);
    }

    /// <summary>
    /// Marks the operation as failed.
    /// </summary>
    public void Fail(string error)
    {
        _completed = true;
        _stopwatch.Stop();
        _activity?.SetStatus(ActivityStatusCode.Error, error);
    }

    /// <summary>
    /// Records an exception.
    /// </summary>
    public void RecordException(Exception exception)
    {
        OrchestrationTracing.RecordError(_activity, _activity?.OperationName ?? "unknown", exception);
    }

    public void Dispose()
    {
        if (!_completed)
        {
            _stopwatch.Stop();
            _onComplete(_activity, _stopwatch.Elapsed);
        }

        _activity?.Dispose();
    }
}

/// <summary>
/// Configuration for orchestration observability.
/// </summary>
public sealed record OrchestrationObservabilityConfig(
    bool EnableTracing = true,
    bool EnableMetrics = true,
    bool EnableDetailedTags = false,
    double SamplingRate = 1.0);

/// <summary>
/// Extension methods for instrumenting orchestration components.
/// </summary>
public static class OrchestrationInstrumentationExtensions
{
    /// <summary>
    /// Wraps an async operation with tracing.
    /// </summary>
    public static async Task<Result<T, string>> WithTracing<T>(
        this Task<Result<T, string>> operation,
        string operationName,
        Dictionary<string, object?>? tags = null)
    {
        using var activity = OrchestrationTracing.ActivitySource.StartActivity(
            operationName,
            ActivityKind.Internal,
            default(ActivityContext),
            tags!);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation;
            stopwatch.Stop();

            result.Match(
                success =>
                {
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    activity?.SetTag("orchestrator.success", true);
                    return success;
                },
                error =>
                {
                    activity?.SetStatus(ActivityStatusCode.Error, error);
                    activity?.SetTag("orchestrator.success", false);
                    activity?.SetTag("orchestrator.error", error);
                    return default!;
                });

            activity?.SetTag("orchestrator.duration_ms", stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            OrchestrationTracing.RecordError(activity, operationName, ex);
            throw;
        }
    }
}
