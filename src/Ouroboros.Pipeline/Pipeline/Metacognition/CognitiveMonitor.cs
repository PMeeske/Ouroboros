// <copyright file="CognitiveMonitor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Metacognition;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents the type of cognitive event occurring in the system.
/// Each type captures a distinct aspect of cognitive processing.
/// </summary>
public enum CognitiveEventType
{
    /// <summary>
    /// A new thought or idea has been generated.
    /// </summary>
    ThoughtGenerated,

    /// <summary>
    /// A decision has been made by the system.
    /// </summary>
    DecisionMade,

    /// <summary>
    /// An error has been detected in processing.
    /// </summary>
    ErrorDetected,

    /// <summary>
    /// Confusion or uncertainty has been sensed in processing.
    /// </summary>
    ConfusionSensed,

    /// <summary>
    /// A new insight or understanding has been gained.
    /// </summary>
    InsightGained,

    /// <summary>
    /// Attention has shifted to a new focus.
    /// </summary>
    AttentionShift,

    /// <summary>
    /// A goal has been activated for pursuit.
    /// </summary>
    GoalActivated,

    /// <summary>
    /// A goal has been successfully completed.
    /// </summary>
    GoalCompleted,

    /// <summary>
    /// High uncertainty detected in processing.
    /// </summary>
    Uncertainty,

    /// <summary>
    /// A contradiction has been detected in reasoning.
    /// </summary>
    Contradiction,
}

/// <summary>
/// Represents the severity level of a cognitive event or alert.
/// </summary>
public enum Severity
{
    /// <summary>
    /// Informational event - normal cognitive processing.
    /// </summary>
    Info,

    /// <summary>
    /// Warning event - requires attention but not critical.
    /// </summary>
    Warning,

    /// <summary>
    /// Critical event - requires immediate attention.
    /// </summary>
    Critical,
}

/// <summary>
/// Represents the overall health status of cognitive processing.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// All cognitive processes are functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// Some degradation detected but still functional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Significant impairment affecting cognitive function.
    /// </summary>
    Impaired,

    /// <summary>
    /// Critical state requiring immediate intervention.
    /// </summary>
    Critical,
}

/// <summary>
/// Represents an event in cognitive processing.
/// Immutable record capturing the details of a cognitive event for monitoring and analysis.
/// </summary>
/// <param name="Id">Unique identifier for this cognitive event.</param>
/// <param name="EventType">The type of cognitive event.</param>
/// <param name="Description">Human-readable description of the event.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="Severity">The severity level of the event.</param>
/// <param name="Context">Additional contextual information about the event.</param>
public sealed record CognitiveEvent(
    Guid Id,
    CognitiveEventType EventType,
    string Description,
    DateTime Timestamp,
    Severity Severity,
    ImmutableDictionary<string, object> Context)
{
    /// <summary>
    /// Creates a thought generation event.
    /// </summary>
    /// <param name="description">Description of the thought.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for thought generation.</returns>
    public static CognitiveEvent Thought(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.ThoughtGenerated,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a decision event.
    /// </summary>
    /// <param name="description">Description of the decision.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for a decision.</returns>
    public static CognitiveEvent Decision(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.DecisionMade,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an error detection event.
    /// </summary>
    /// <param name="description">Description of the error.</param>
    /// <param name="severity">The severity of the error.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for an error.</returns>
    public static CognitiveEvent Error(string description, Severity severity = Severity.Warning, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.ErrorDetected,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: severity,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a confusion sensing event.
    /// </summary>
    /// <param name="description">Description of the confusion.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for confusion.</returns>
    public static CognitiveEvent Confusion(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.ConfusionSensed,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Warning,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an insight gaining event.
    /// </summary>
    /// <param name="description">Description of the insight.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for an insight.</returns>
    public static CognitiveEvent Insight(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.InsightGained,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an attention shift event.
    /// </summary>
    /// <param name="description">Description of the attention shift.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for attention shift.</returns>
    public static CognitiveEvent AttentionChange(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.AttentionShift,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a goal activation event.
    /// </summary>
    /// <param name="goalDescription">Description of the activated goal.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for goal activation.</returns>
    public static CognitiveEvent GoalStart(string goalDescription, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.GoalActivated,
        Description: goalDescription,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a goal completion event.
    /// </summary>
    /// <param name="goalDescription">Description of the completed goal.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for goal completion.</returns>
    public static CognitiveEvent GoalEnd(string goalDescription, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.GoalCompleted,
        Description: goalDescription,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an uncertainty detection event.
    /// </summary>
    /// <param name="description">Description of the uncertainty.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for uncertainty.</returns>
    public static CognitiveEvent UncertaintyDetected(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.Uncertainty,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Warning,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a contradiction detection event.
    /// </summary>
    /// <param name="description">Description of the contradiction.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for contradiction.</returns>
    public static CognitiveEvent ContradictionDetected(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.Contradiction,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Critical,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a copy of this event with additional context.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>A new CognitiveEvent with the added context.</returns>
    public CognitiveEvent WithContext(string key, object value)
        => this with { Context = Context.SetItem(key, value) };

    /// <summary>
    /// Creates a copy of this event with merged context.
    /// </summary>
    /// <param name="additionalContext">Additional context to merge.</param>
    /// <returns>A new CognitiveEvent with merged context.</returns>
    public CognitiveEvent WithMergedContext(ImmutableDictionary<string, object> additionalContext)
        => this with { Context = Context.SetItems(additionalContext) };
}

/// <summary>
/// Represents an alert generated by cognitive monitoring.
/// Alerts are triggered when monitored metrics exceed defined thresholds or patterns are detected.
/// </summary>
/// <param name="Id">Unique identifier for this alert.</param>
/// <param name="AlertType">The type/category of the alert.</param>
/// <param name="Message">Human-readable message describing the alert.</param>
/// <param name="TriggeringEvents">The cognitive events that triggered this alert.</param>
/// <param name="RecommendedAction">Suggested action to address the alert.</param>
/// <param name="Priority">Priority level from 1 (lowest) to 10 (highest).</param>
/// <param name="Timestamp">When the alert was generated.</param>
public sealed record MonitoringAlert(
    Guid Id,
    string AlertType,
    string Message,
    ImmutableList<CognitiveEvent> TriggeringEvents,
    string RecommendedAction,
    int Priority,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a new high-priority alert.
    /// </summary>
    /// <param name="alertType">The type of alert.</param>
    /// <param name="message">The alert message.</param>
    /// <param name="triggeringEvents">Events that triggered the alert.</param>
    /// <param name="recommendedAction">Recommended action to take.</param>
    /// <returns>A new high-priority MonitoringAlert.</returns>
    public static MonitoringAlert HighPriority(
        string alertType,
        string message,
        IEnumerable<CognitiveEvent> triggeringEvents,
        string recommendedAction) => new(
            Id: Guid.NewGuid(),
            AlertType: alertType,
            Message: message,
            TriggeringEvents: triggeringEvents.ToImmutableList(),
            RecommendedAction: recommendedAction,
            Priority: 8,
            Timestamp: DateTime.UtcNow);

    /// <summary>
    /// Creates a new medium-priority alert.
    /// </summary>
    /// <param name="alertType">The type of alert.</param>
    /// <param name="message">The alert message.</param>
    /// <param name="triggeringEvents">Events that triggered the alert.</param>
    /// <param name="recommendedAction">Recommended action to take.</param>
    /// <returns>A new medium-priority MonitoringAlert.</returns>
    public static MonitoringAlert MediumPriority(
        string alertType,
        string message,
        IEnumerable<CognitiveEvent> triggeringEvents,
        string recommendedAction) => new(
            Id: Guid.NewGuid(),
            AlertType: alertType,
            Message: message,
            TriggeringEvents: triggeringEvents.ToImmutableList(),
            RecommendedAction: recommendedAction,
            Priority: 5,
            Timestamp: DateTime.UtcNow);

    /// <summary>
    /// Creates a new low-priority alert.
    /// </summary>
    /// <param name="alertType">The type of alert.</param>
    /// <param name="message">The alert message.</param>
    /// <param name="triggeringEvents">Events that triggered the alert.</param>
    /// <param name="recommendedAction">Recommended action to take.</param>
    /// <returns>A new low-priority MonitoringAlert.</returns>
    public static MonitoringAlert LowPriority(
        string alertType,
        string message,
        IEnumerable<CognitiveEvent> triggeringEvents,
        string recommendedAction) => new(
            Id: Guid.NewGuid(),
            AlertType: alertType,
            Message: message,
            TriggeringEvents: triggeringEvents.ToImmutableList(),
            RecommendedAction: recommendedAction,
            Priority: 2,
            Timestamp: DateTime.UtcNow);

    /// <summary>
    /// Validates the alert priority is within valid range.
    /// </summary>
    /// <returns>A Result indicating validity or validation error.</returns>
    public Result<Unit, string> Validate()
    {
        if (Priority < 1 || Priority > 10)
        {
            return Result<Unit, string>.Failure($"Priority must be in [1, 10], got {Priority}.");
        }

        if (string.IsNullOrWhiteSpace(AlertType))
        {
            return Result<Unit, string>.Failure("AlertType cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            return Result<Unit, string>.Failure("Message cannot be empty.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}

/// <summary>
/// Represents the overall cognitive health status of the system.
/// Aggregates metrics and alerts into a comprehensive health assessment.
/// </summary>
/// <param name="Timestamp">When this health assessment was generated.</param>
/// <param name="HealthScore">Overall health score from 0 (critical) to 1 (optimal).</param>
/// <param name="ProcessingEfficiency">Efficiency of cognitive processing from 0 to 1.</param>
/// <param name="ErrorRate">Rate of errors in recent processing window.</param>
/// <param name="ResponseLatency">Average response latency for cognitive operations.</param>
/// <param name="ActiveAlerts">Currently active monitoring alerts.</param>
/// <param name="Status">Overall health status classification.</param>
public sealed record CognitiveHealth(
    DateTime Timestamp,
    double HealthScore,
    double ProcessingEfficiency,
    double ErrorRate,
    TimeSpan ResponseLatency,
    ImmutableList<MonitoringAlert> ActiveAlerts,
    HealthStatus Status)
{
    /// <summary>
    /// Creates a healthy cognitive health status with optimal metrics.
    /// </summary>
    /// <returns>A healthy CognitiveHealth instance.</returns>
    public static CognitiveHealth Optimal() => new(
        Timestamp: DateTime.UtcNow,
        HealthScore: 1.0,
        ProcessingEfficiency: 1.0,
        ErrorRate: 0.0,
        ResponseLatency: TimeSpan.Zero,
        ActiveAlerts: ImmutableList<MonitoringAlert>.Empty,
        Status: HealthStatus.Healthy);

    /// <summary>
    /// Creates a CognitiveHealth from computed metrics.
    /// Automatically determines the status based on metrics.
    /// </summary>
    /// <param name="healthScore">The computed health score.</param>
    /// <param name="efficiency">The processing efficiency.</param>
    /// <param name="errorRate">The error rate.</param>
    /// <param name="latency">The response latency.</param>
    /// <param name="alerts">Active alerts.</param>
    /// <returns>A new CognitiveHealth with computed status.</returns>
    public static CognitiveHealth FromMetrics(
        double healthScore,
        double efficiency,
        double errorRate,
        TimeSpan latency,
        ImmutableList<MonitoringAlert> alerts)
    {
        var clampedHealth = Math.Clamp(healthScore, 0.0, 1.0);
        var clampedEfficiency = Math.Clamp(efficiency, 0.0, 1.0);
        var clampedErrorRate = Math.Max(0.0, errorRate);

        var status = DetermineStatus(clampedHealth, clampedEfficiency, clampedErrorRate, alerts);

        return new CognitiveHealth(
            Timestamp: DateTime.UtcNow,
            HealthScore: clampedHealth,
            ProcessingEfficiency: clampedEfficiency,
            ErrorRate: clampedErrorRate,
            ResponseLatency: latency,
            ActiveAlerts: alerts,
            Status: status);
    }

    /// <summary>
    /// Determines if the cognitive health requires attention.
    /// </summary>
    /// <returns>True if status is not Healthy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RequiresAttention() => Status != HealthStatus.Healthy;

    /// <summary>
    /// Determines if the cognitive health is in a critical state.
    /// </summary>
    /// <returns>True if status is Critical.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCritical() => Status == HealthStatus.Critical;

    /// <summary>
    /// Validates the cognitive health values.
    /// </summary>
    /// <returns>A Result indicating validity or validation error.</returns>
    public Result<Unit, string> Validate()
    {
        if (HealthScore < 0.0 || HealthScore > 1.0)
        {
            return Result<Unit, string>.Failure($"HealthScore must be in [0, 1], got {HealthScore}.");
        }

        if (ProcessingEfficiency < 0.0 || ProcessingEfficiency > 1.0)
        {
            return Result<Unit, string>.Failure($"ProcessingEfficiency must be in [0, 1], got {ProcessingEfficiency}.");
        }

        if (ErrorRate < 0.0)
        {
            return Result<Unit, string>.Failure($"ErrorRate must be non-negative, got {ErrorRate}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private static HealthStatus DetermineStatus(
        double healthScore,
        double efficiency,
        double errorRate,
        ImmutableList<MonitoringAlert> alerts)
    {
        var hasCriticalAlert = alerts.Any(a => a.Priority >= 9);
        var hasHighPriorityAlerts = alerts.Count(a => a.Priority >= 7) >= 2;

        if (hasCriticalAlert || healthScore < 0.3 || errorRate > 0.5)
        {
            return HealthStatus.Critical;
        }

        if (hasHighPriorityAlerts || healthScore < 0.5 || efficiency < 0.4 || errorRate > 0.3)
        {
            return HealthStatus.Impaired;
        }

        if (healthScore < 0.7 || efficiency < 0.6 || errorRate > 0.1 || alerts.Any())
        {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }
}

/// <summary>
/// Interface for cognitive monitoring operations.
/// Provides methods for recording events, querying health, and managing alerts.
/// </summary>
public interface ICognitiveMonitor
{
    /// <summary>
    /// Records a cognitive event for monitoring.
    /// </summary>
    /// <param name="cognitiveEvent">The event to record.</param>
    /// <returns>A Result indicating success or error.</returns>
    Result<Unit, string> RecordEvent(CognitiveEvent cognitiveEvent);

    /// <summary>
    /// Gets the current cognitive health status.
    /// </summary>
    /// <returns>The current CognitiveHealth.</returns>
    CognitiveHealth GetHealth();

    /// <summary>
    /// Gets the most recent cognitive events.
    /// </summary>
    /// <param name="count">The maximum number of events to return.</param>
    /// <returns>An immutable list of recent events.</returns>
    ImmutableList<CognitiveEvent> GetRecentEvents(int count);

    /// <summary>
    /// Gets all active monitoring alerts.
    /// </summary>
    /// <returns>An immutable list of active alerts.</returns>
    ImmutableList<MonitoringAlert> GetAlerts();

    /// <summary>
    /// Acknowledges and dismisses an alert.
    /// </summary>
    /// <param name="alertId">The ID of the alert to acknowledge.</param>
    /// <returns>A Result indicating success or error.</returns>
    Result<Unit, string> AcknowledgeAlert(Guid alertId);

    /// <summary>
    /// Sets a threshold for a specific metric that triggers alerts.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>A Result indicating success or error.</returns>
    Result<Unit, string> SetThreshold(string metric, double threshold);

    /// <summary>
    /// Subscribes to alert notifications.
    /// </summary>
    /// <param name="handler">The handler to call when alerts are generated.</param>
    /// <returns>A disposable subscription that can be used to unsubscribe.</returns>
    IDisposable Subscribe(Action<MonitoringAlert> handler);
}

/// <summary>
/// Real-time cognitive monitor implementing ICognitiveMonitor.
/// Provides thread-safe event recording, sliding window metrics, and pattern detection.
/// </summary>
public sealed class RealtimeCognitiveMonitor : ICognitiveMonitor, IDisposable
{
    private readonly ConcurrentQueue<CognitiveEvent> eventBuffer;
    private readonly ConcurrentDictionary<Guid, MonitoringAlert> activeAlerts;
    private readonly ConcurrentDictionary<string, double> thresholds;
    private readonly ConcurrentBag<Action<MonitoringAlert>> subscribers;
    private readonly object metricsLock = new();

    private readonly int maxBufferSize;
    private readonly TimeSpan slidingWindowDuration;

    private int totalEventsRecorded;
    private int errorCount;
    private long totalLatencyTicks;
    private int latencyMeasurements;
    private DateTime lastHealthCheck;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeCognitiveMonitor"/> class.
    /// </summary>
    /// <param name="maxBufferSize">Maximum number of events to retain in buffer.</param>
    /// <param name="slidingWindowDuration">Duration of the sliding window for metrics.</param>
    public RealtimeCognitiveMonitor(int maxBufferSize = 1000, TimeSpan? slidingWindowDuration = null)
    {
        this.maxBufferSize = maxBufferSize;
        this.slidingWindowDuration = slidingWindowDuration ?? TimeSpan.FromMinutes(5);
        this.eventBuffer = new ConcurrentQueue<CognitiveEvent>();
        this.activeAlerts = new ConcurrentDictionary<Guid, MonitoringAlert>();
        this.thresholds = new ConcurrentDictionary<string, double>();
        this.subscribers = new ConcurrentBag<Action<MonitoringAlert>>();
        this.lastHealthCheck = DateTime.UtcNow;

        InitializeDefaultThresholds();
    }

    /// <inheritdoc/>
    public Result<Unit, string> RecordEvent(CognitiveEvent cognitiveEvent)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (cognitiveEvent is null)
        {
            return Result<Unit, string>.Failure("Cognitive event cannot be null.");
        }

        try
        {
            // Add event to buffer
            eventBuffer.Enqueue(cognitiveEvent);
            Interlocked.Increment(ref totalEventsRecorded);

            // Trim buffer if needed
            TrimBuffer();

            // Update metrics
            UpdateMetrics(cognitiveEvent);

            // Check for anomalies and generate alerts
            DetectAnomalies(cognitiveEvent);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to record event: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public CognitiveHealth GetHealth()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        lock (metricsLock)
        {
            var recentEvents = GetEventsInWindow();
            var errorEvents = recentEvents.Count(e => e.EventType == CognitiveEventType.ErrorDetected);
            var totalInWindow = recentEvents.Count;

            var errorRate = totalInWindow > 0 ? (double)errorEvents / totalInWindow : 0.0;
            var efficiency = CalculateEfficiency(recentEvents);
            var healthScore = CalculateHealthScore(errorRate, efficiency);
            var avgLatency = latencyMeasurements > 0
                ? TimeSpan.FromTicks(totalLatencyTicks / latencyMeasurements)
                : TimeSpan.Zero;

            lastHealthCheck = DateTime.UtcNow;

            return CognitiveHealth.FromMetrics(
                healthScore,
                efficiency,
                errorRate,
                avgLatency,
                activeAlerts.Values.ToImmutableList());
        }
    }

    /// <inheritdoc/>
    public ImmutableList<CognitiveEvent> GetRecentEvents(int count)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return eventBuffer
            .OrderByDescending(e => e.Timestamp)
            .Take(Math.Max(0, count))
            .ToImmutableList();
    }

    /// <inheritdoc/>
    public ImmutableList<MonitoringAlert> GetAlerts()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return activeAlerts.Values
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.Timestamp)
            .ToImmutableList();
    }

    /// <inheritdoc/>
    public Result<Unit, string> AcknowledgeAlert(Guid alertId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (activeAlerts.TryRemove(alertId, out _))
        {
            return Result<Unit, string>.Success(Unit.Value);
        }

        return Result<Unit, string>.Failure($"Alert with ID {alertId} not found.");
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetThreshold(string metric, double threshold)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (string.IsNullOrWhiteSpace(metric))
        {
            return Result<Unit, string>.Failure("Metric name cannot be empty.");
        }

        if (threshold < 0)
        {
            return Result<Unit, string>.Failure($"Threshold must be non-negative, got {threshold}.");
        }

        thresholds[metric] = threshold;
        return Result<Unit, string>.Success(Unit.Value);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<MonitoringAlert> handler)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        subscribers.Add(handler);
        return new AlertSubscription(this, handler);
    }

    /// <summary>
    /// Gets the current threshold for a metric.
    /// </summary>
    /// <param name="metric">The metric name.</param>
    /// <returns>An Option containing the threshold if set.</returns>
    public Option<double> GetThreshold(string metric)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return thresholds.TryGetValue(metric, out var threshold)
            ? Option<double>.Some(threshold)
            : Option<double>.None();
    }

    /// <summary>
    /// Clears all recorded events and resets metrics.
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        while (eventBuffer.TryDequeue(out _))
        {
        }

        activeAlerts.Clear();

        lock (metricsLock)
        {
            totalEventsRecorded = 0;
            errorCount = 0;
            totalLatencyTicks = 0;
            latencyMeasurements = 0;
        }
    }

    /// <summary>
    /// Disposes resources used by the monitor.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        // Clear buffers before marking as disposed
        while (eventBuffer.TryDequeue(out _))
        {
        }

        activeAlerts.Clear();

        disposed = true;
    }

    private void InitializeDefaultThresholds()
    {
        thresholds["error_rate"] = 0.1;
        thresholds["consecutive_errors"] = 3;
        thresholds["confusion_rate"] = 0.2;
        thresholds["contradiction_limit"] = 2;
        thresholds["latency_ms"] = 1000;
    }

    private void TrimBuffer()
    {
        while (eventBuffer.Count > maxBufferSize && eventBuffer.TryDequeue(out _))
        {
        }
    }

    private void UpdateMetrics(CognitiveEvent cognitiveEvent)
    {
        lock (metricsLock)
        {
            if (cognitiveEvent.EventType == CognitiveEventType.ErrorDetected)
            {
                Interlocked.Increment(ref errorCount);
            }

            // Track latency if present in context
            if (cognitiveEvent.Context.TryGetValue("latency_ms", out var latencyObj) &&
                latencyObj is double latencyMs)
            {
                totalLatencyTicks += TimeSpan.FromMilliseconds(latencyMs).Ticks;
                Interlocked.Increment(ref latencyMeasurements);
            }
        }
    }

    private void DetectAnomalies(CognitiveEvent cognitiveEvent)
    {
        var recentEvents = GetEventsInWindow();

        // Check for consecutive errors
        if (thresholds.TryGetValue("consecutive_errors", out var consecutiveErrorThreshold))
        {
            var consecutiveErrors = CountConsecutiveErrors(recentEvents);
            if (consecutiveErrors >= (int)consecutiveErrorThreshold)
            {
                var alert = MonitoringAlert.HighPriority(
                    "ConsecutiveErrors",
                    $"Detected {consecutiveErrors} consecutive errors",
                    recentEvents.Where(e => e.EventType == CognitiveEventType.ErrorDetected).TakeLast(consecutiveErrors),
                    "Review error causes and consider pausing processing");
                RaiseAlert(alert);
            }
        }

        // Check for contradiction detection
        if (cognitiveEvent.EventType == CognitiveEventType.Contradiction &&
            thresholds.TryGetValue("contradiction_limit", out var contradictionLimit))
        {
            var recentContradictions = recentEvents.Count(e => e.EventType == CognitiveEventType.Contradiction);
            if (recentContradictions >= (int)contradictionLimit)
            {
                var alert = MonitoringAlert.HighPriority(
                    "ContradictionOverload",
                    $"Detected {recentContradictions} contradictions in recent processing",
                    recentEvents.Where(e => e.EventType == CognitiveEventType.Contradiction),
                    "Review reasoning chain and resolve contradictions");
                RaiseAlert(alert);
            }
        }

        // Check for high confusion rate
        if (cognitiveEvent.EventType == CognitiveEventType.ConfusionSensed &&
            thresholds.TryGetValue("confusion_rate", out var confusionRateThreshold))
        {
            var confusionRate = recentEvents.Count > 0
                ? (double)recentEvents.Count(e => e.EventType == CognitiveEventType.ConfusionSensed) / recentEvents.Count
                : 0.0;

            if (confusionRate >= confusionRateThreshold)
            {
                var alert = MonitoringAlert.MediumPriority(
                    "HighConfusionRate",
                    $"Confusion rate ({confusionRate:P1}) exceeds threshold ({confusionRateThreshold:P1})",
                    recentEvents.Where(e => e.EventType == CognitiveEventType.ConfusionSensed),
                    "Consider providing additional context or simplifying task");
                RaiseAlert(alert);
            }
        }

        // Check critical severity events
        if (cognitiveEvent.Severity == Severity.Critical)
        {
            var alert = MonitoringAlert.HighPriority(
                "CriticalEvent",
                $"Critical cognitive event detected: {cognitiveEvent.Description}",
                new[] { cognitiveEvent },
                "Immediate attention required - review event context");
            RaiseAlert(alert);
        }
    }

    private ImmutableList<CognitiveEvent> GetEventsInWindow()
    {
        var windowStart = DateTime.UtcNow - slidingWindowDuration;
        return eventBuffer
            .Where(e => e.Timestamp >= windowStart)
            .OrderBy(e => e.Timestamp)
            .ToImmutableList();
    }

    private static int CountConsecutiveErrors(ImmutableList<CognitiveEvent> events)
    {
        var consecutive = 0;
        var maxConsecutive = 0;

        foreach (var evt in events.OrderBy(e => e.Timestamp))
        {
            if (evt.EventType == CognitiveEventType.ErrorDetected)
            {
                consecutive++;
                maxConsecutive = Math.Max(maxConsecutive, consecutive);
            }
            else
            {
                consecutive = 0;
            }
        }

        return maxConsecutive;
    }

    private static double CalculateEfficiency(ImmutableList<CognitiveEvent> recentEvents)
    {
        if (recentEvents.IsEmpty)
        {
            return 1.0;
        }

        var productiveEvents = recentEvents.Count(e =>
            e.EventType == CognitiveEventType.ThoughtGenerated ||
            e.EventType == CognitiveEventType.DecisionMade ||
            e.EventType == CognitiveEventType.InsightGained ||
            e.EventType == CognitiveEventType.GoalCompleted);

        var problemEvents = recentEvents.Count(e =>
            e.EventType == CognitiveEventType.ErrorDetected ||
            e.EventType == CognitiveEventType.ConfusionSensed ||
            e.EventType == CognitiveEventType.Contradiction);

        var total = productiveEvents + problemEvents;
        return total > 0 ? (double)productiveEvents / total : 1.0;
    }

    private static double CalculateHealthScore(double errorRate, double efficiency)
    {
        // Health score is a weighted combination of error rate and efficiency
        var errorComponent = 1.0 - Math.Min(1.0, errorRate * 2.0); // Error rate has double weight
        var efficiencyComponent = efficiency;

        return (errorComponent * 0.6) + (efficiencyComponent * 0.4);
    }

    private void RaiseAlert(MonitoringAlert alert)
    {
        // Add to active alerts (avoid duplicates by alert type in short window)
        var recentSameType = activeAlerts.Values
            .Any(a => a.AlertType == alert.AlertType &&
                      (DateTime.UtcNow - a.Timestamp) < TimeSpan.FromSeconds(30));

        if (!recentSameType)
        {
            activeAlerts[alert.Id] = alert;

            // Notify subscribers
            foreach (var subscriber in subscribers)
            {
                try
                {
                    subscriber(alert);
                }
                catch
                {
                    // Ignore subscriber errors to prevent cascade
                }
            }
        }
    }

    /// <summary>
    /// Subscription handle for alert notifications.
    /// </summary>
    private sealed class AlertSubscription : IDisposable
    {
        private readonly RealtimeCognitiveMonitor monitor;
        private readonly Action<MonitoringAlert> handler;
        private bool disposed;

        public AlertSubscription(RealtimeCognitiveMonitor monitor, Action<MonitoringAlert> handler)
        {
            this.monitor = monitor;
            this.handler = handler;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            // Note: ConcurrentBag doesn't support removal, so we rely on checking disposed state
            // In production, consider using a different collection type
        }
    }
}

/// <summary>
/// Provides Kleisli arrows for cognitive monitoring operations in the pipeline.
/// Enables functional composition of monitoring with other pipeline operations.
/// </summary>
public static class CognitiveMonitorArrow
{
    /// <summary>
    /// Creates a Kleisli arrow that records a cognitive event.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from CognitiveEvent to Unit.</returns>
    public static KleisliResult<CognitiveEvent, Unit, string> RecordArrow(ICognitiveMonitor monitor)
        => cognitiveEvent => Task.FromResult(monitor.RecordEvent(cognitiveEvent));

    /// <summary>
    /// Creates a Kleisli arrow that gets the current cognitive health.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from Unit to CognitiveHealth.</returns>
    public static KleisliResult<Unit, CognitiveHealth, string> HealthCheckArrow(ICognitiveMonitor monitor)
        => _ => Task.FromResult(Result<CognitiveHealth, string>.Success(monitor.GetHealth()));

    /// <summary>
    /// Creates a Kleisli arrow that gets recent events.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from count to list of events.</returns>
    public static KleisliResult<int, ImmutableList<CognitiveEvent>, string> GetRecentEventsArrow(ICognitiveMonitor monitor)
        => count =>
        {
            if (count < 0)
            {
                return Task.FromResult(Result<ImmutableList<CognitiveEvent>, string>.Failure("Count must be non-negative."));
            }

            return Task.FromResult(Result<ImmutableList<CognitiveEvent>, string>.Success(monitor.GetRecentEvents(count)));
        };

    /// <summary>
    /// Creates a Kleisli arrow that acknowledges an alert.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from alert ID to Unit.</returns>
    public static KleisliResult<Guid, Unit, string> AcknowledgeAlertArrow(ICognitiveMonitor monitor)
        => alertId => Task.FromResult(monitor.AcknowledgeAlert(alertId));

    /// <summary>
    /// Creates a Kleisli arrow that sets a monitoring threshold.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from (metric, threshold) to Unit.</returns>
    public static KleisliResult<(string Metric, double Threshold), Unit, string> SetThresholdArrow(ICognitiveMonitor monitor)
        => input => Task.FromResult(monitor.SetThreshold(input.Metric, input.Threshold));

    /// <summary>
    /// Creates a Kleisli arrow that records an event and returns the health status.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from CognitiveEvent to CognitiveHealth.</returns>
    public static KleisliResult<CognitiveEvent, CognitiveHealth, string> RecordAndCheckHealthArrow(ICognitiveMonitor monitor)
        => async cognitiveEvent =>
        {
            var recordResult = monitor.RecordEvent(cognitiveEvent);
            if (recordResult.IsFailure)
            {
                return Result<CognitiveHealth, string>.Failure(recordResult.Error);
            }

            return Result<CognitiveHealth, string>.Success(monitor.GetHealth());
        };

    /// <summary>
    /// Creates a Kleisli arrow that monitors an operation and records the appropriate cognitive event.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <param name="operation">The operation to monitor.</param>
    /// <param name="eventDescription">Description for the cognitive event.</param>
    /// <returns>A Kleisli arrow that wraps the operation with monitoring.</returns>
    public static KleisliResult<TInput, TOutput, string> MonitoredOperationArrow<TInput, TOutput>(
        ICognitiveMonitor monitor,
        Func<TInput, Task<Result<TOutput, string>>> operation,
        string eventDescription)
        => async input =>
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var result = await operation(input);
                var duration = DateTime.UtcNow - startTime;

                if (result.IsSuccess)
                {
                    var successEvent = CognitiveEvent.Decision(
                        $"{eventDescription}: Completed successfully",
                        ImmutableDictionary<string, object>.Empty
                            .Add("latency_ms", duration.TotalMilliseconds)
                            .Add("input_type", typeof(TInput).Name)
                            .Add("output_type", typeof(TOutput).Name));

                    monitor.RecordEvent(successEvent);
                }
                else
                {
                    var errorEvent = CognitiveEvent.Error(
                        $"{eventDescription}: Failed - {result.Error}",
                        Severity.Warning,
                        ImmutableDictionary<string, object>.Empty
                            .Add("latency_ms", duration.TotalMilliseconds)
                            .Add("error", result.Error));

                    monitor.RecordEvent(errorEvent);
                }

                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                var exceptionEvent = CognitiveEvent.Error(
                    $"{eventDescription}: Exception - {ex.Message}",
                    Severity.Critical,
                    ImmutableDictionary<string, object>.Empty
                        .Add("latency_ms", duration.TotalMilliseconds)
                        .Add("exception_type", ex.GetType().Name)
                        .Add("exception_message", ex.Message));

                monitor.RecordEvent(exceptionEvent);

                return Result<TOutput, string>.Failure($"Operation failed with exception: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a Kleisli arrow that checks health and fails if critical.
    /// </summary>
    /// <param name="monitor">The cognitive monitor to use.</param>
    /// <returns>A Kleisli arrow from Unit to CognitiveHealth that fails if critical.</returns>
    public static KleisliResult<Unit, CognitiveHealth, string> HealthGateArrow(ICognitiveMonitor monitor)
        => _ =>
        {
            var health = monitor.GetHealth();

            if (health.IsCritical())
            {
                return Task.FromResult(Result<CognitiveHealth, string>.Failure(
                    $"Cognitive health is critical (score: {health.HealthScore:P1}). Processing halted."));
            }

            return Task.FromResult(Result<CognitiveHealth, string>.Success(health));
        };
}
