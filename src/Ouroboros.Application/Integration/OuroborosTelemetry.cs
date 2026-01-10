// <copyright file="OuroborosTelemetry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// OpenTelemetry instrumentation for Ouroboros system.
/// Provides metrics, traces, and activity tracking for all operations.
/// </summary>
public sealed class OuroborosTelemetry : IDisposable
{
    /// <summary>
    /// Activity source name for distributed tracing.
    /// </summary>
    public const string ActivitySourceName = "Ouroboros";

    /// <summary>
    /// Meter name for metrics collection.
    /// </summary>
    public const string MeterName = "Ouroboros";

    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _goalsExecutedCounter;
    private readonly Counter<long> _reasoningQueriesCounter;
    private readonly Counter<long> _learningIterationsCounter;
    private readonly Counter<long> _episodesStoredCounter;
    private readonly Counter<long> _errorsCounter;

    // Histograms
    private readonly Histogram<double> _executionDurationHistogram;
    private readonly Histogram<double> _reasoningDurationHistogram;
    private readonly Histogram<double> _learningDurationHistogram;
    private readonly Histogram<long> _workspaceItemsHistogram;
    private readonly Histogram<long> _planningDepthHistogram;

    // Gauges (via ObservableGauge)
    private long _activeGoals;
    private long _workspaceItems;
    private long _episodesInMemory;
    private long _cognitiveLoopCycles;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosTelemetry"/> class.
    /// </summary>
    public OuroborosTelemetry()
    {
        _activitySource = new ActivitySource(ActivitySourceName, "1.0.0");
        _meter = new Meter(MeterName, "1.0.0");

        // Initialize counters
        _goalsExecutedCounter = _meter.CreateCounter<long>(
            "ouroboros.goals.executed",
            description: "Total number of goals executed");

        _reasoningQueriesCounter = _meter.CreateCounter<long>(
            "ouroboros.reasoning.queries",
            description: "Total number of reasoning queries");

        _learningIterationsCounter = _meter.CreateCounter<long>(
            "ouroboros.learning.iterations",
            description: "Total number of learning iterations");

        _episodesStoredCounter = _meter.CreateCounter<long>(
            "ouroboros.episodes.stored",
            description: "Total number of episodes stored");

        _errorsCounter = _meter.CreateCounter<long>(
            "ouroboros.errors",
            description: "Total number of errors encountered");

        // Initialize histograms
        _executionDurationHistogram = _meter.CreateHistogram<double>(
            "ouroboros.execution.duration",
            unit: "ms",
            description: "Duration of goal execution");

        _reasoningDurationHistogram = _meter.CreateHistogram<double>(
            "ouroboros.reasoning.duration",
            unit: "ms",
            description: "Duration of reasoning operations");

        _learningDurationHistogram = _meter.CreateHistogram<double>(
            "ouroboros.learning.duration",
            unit: "ms",
            description: "Duration of learning operations");

        _workspaceItemsHistogram = _meter.CreateHistogram<long>(
            "ouroboros.workspace.items",
            description: "Number of items in consciousness workspace");

        _planningDepthHistogram = _meter.CreateHistogram<long>(
            "ouroboros.planning.depth",
            description: "Depth of hierarchical plans");

        // Initialize observable gauges
        _meter.CreateObservableGauge(
            "ouroboros.goals.active",
            () => _activeGoals,
            description: "Number of active goals");

        _meter.CreateObservableGauge(
            "ouroboros.workspace.current_items",
            () => _workspaceItems,
            description: "Current number of workspace items");

        _meter.CreateObservableGauge(
            "ouroboros.memory.episodes",
            () => _episodesInMemory,
            description: "Number of episodes in memory");

        _meter.CreateObservableGauge(
            "ouroboros.cognitive_loop.cycles",
            () => _cognitiveLoopCycles,
            description: "Total cognitive loop cycles");
    }

    /// <summary>
    /// Creates a new activity for distributed tracing.
    /// </summary>
    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
    {
        return _activitySource.StartActivity(name, kind);
    }

    /// <summary>
    /// Records a goal execution event.
    /// </summary>
    public void RecordGoalExecution(bool success, TimeSpan duration, Dictionary<string, object>? tags = null)
    {
        var tagList = CreateTagList(tags);
        tagList.Add("success", success);

        _goalsExecutedCounter.Add(1, tagList);
        _executionDurationHistogram.Record(duration.TotalMilliseconds, tagList);

        if (!success)
        {
            _errorsCounter.Add(1, new KeyValuePair<string, object?>("operation", "goal_execution"));
        }
    }

    /// <summary>
    /// Records a reasoning query event.
    /// </summary>
    public void RecordReasoningQuery(TimeSpan duration, bool useSymbolic, bool useCausal, bool useAbductive)
    {
        var tags = new TagList
        {
            { "symbolic", useSymbolic },
            { "causal", useCausal },
            { "abductive", useAbductive }
        };

        _reasoningQueriesCounter.Add(1, tags);
        _reasoningDurationHistogram.Record(duration.TotalMilliseconds, tags);
    }

    /// <summary>
    /// Records a learning iteration event.
    /// </summary>
    public void RecordLearningIteration(TimeSpan duration, int episodesProcessed, int rulesLearned)
    {
        var tags = new TagList
        {
            { "episodes_processed", episodesProcessed },
            { "rules_learned", rulesLearned }
        };

        _learningIterationsCounter.Add(1, tags);
        _learningDurationHistogram.Record(duration.TotalMilliseconds, tags);
    }

    /// <summary>
    /// Records episode storage event.
    /// </summary>
    public void RecordEpisodeStored(int count = 1)
    {
        _episodesStoredCounter.Add(count);
        Interlocked.Add(ref _episodesInMemory, count);
    }

    /// <summary>
    /// Records workspace item count.
    /// </summary>
    public void RecordWorkspaceItems(int count)
    {
        _workspaceItemsHistogram.Record(count);
        Interlocked.Exchange(ref _workspaceItems, count);
    }

    /// <summary>
    /// Records planning depth.
    /// </summary>
    public void RecordPlanningDepth(int depth)
    {
        _planningDepthHistogram.Record(depth);
    }

    /// <summary>
    /// Updates active goals count.
    /// </summary>
    public void UpdateActiveGoals(int count)
    {
        Interlocked.Exchange(ref _activeGoals, count);
    }

    /// <summary>
    /// Increments cognitive loop cycle count.
    /// </summary>
    public void IncrementCognitiveLoopCycle()
    {
        Interlocked.Increment(ref _cognitiveLoopCycles);
    }

    /// <summary>
    /// Records an error event.
    /// </summary>
    public void RecordError(string operation, string errorType)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "error_type", errorType }
        };

        _errorsCounter.Add(1, tags);
    }

    private static TagList CreateTagList(Dictionary<string, object>? tags)
    {
        var tagList = new TagList();
        if (tags != null)
        {
            foreach (var kvp in tags)
            {
                tagList.Add(kvp.Key, kvp.Value);
            }
        }
        return tagList;
    }

    /// <summary>
    /// Disposes telemetry resources.
    /// </summary>
    public void Dispose()
    {
        _activitySource?.Dispose();
        _meter?.Dispose();
    }
}

/// <summary>
/// Extension methods for telemetry integration.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Executes an operation with telemetry tracking.
    /// </summary>
    public static async Task<T> WithTelemetryAsync<T>(
        this OuroborosTelemetry telemetry,
        string activityName,
        Func<Activity?, Task<T>> operation,
        Action<Activity?, T>? onSuccess = null,
        Action<Activity?, Exception>? onError = null)
    {
        using var activity = telemetry.StartActivity(activityName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation(activity);
            onSuccess?.Invoke(activity, result);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.message", ex.Message);
            onError?.Invoke(activity, ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
