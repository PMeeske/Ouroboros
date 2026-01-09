// <copyright file="MaintenanceScheduler.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Ouroboros.Core.Monads;

namespace Ouroboros.Domain.Governance;

/// <summary>
/// Scheduled maintenance system for DAG compaction, archiving, and anomaly detection.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
public sealed class MaintenanceScheduler : IMaintenanceScheduler
{
    private readonly ConcurrentBag<MaintenanceTask> _tasks = new();
    private readonly ConcurrentBag<MaintenanceExecution> _history = new();
    private readonly ConcurrentBag<AnomalyAlert> _alerts = new();
    private CancellationTokenSource? _schedulerCts;
    private Task? _schedulerTask;

    /// <summary>
    /// Schedules a maintenance task.
    /// </summary>
    public Result<MaintenanceTask> ScheduleTask(MaintenanceTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        _tasks.Add(task);
        return Result<MaintenanceTask>.Success(task);
    }

    /// <summary>
    /// Starts the maintenance scheduler.
    /// </summary>
    public Result<bool> Start()
    {
        if (_schedulerTask != null && !_schedulerTask.IsCompleted)
        {
            return Result<bool>.Failure("Scheduler is already running");
        }

        _schedulerCts?.Dispose(); // Dispose any previous instance
        _schedulerCts = new CancellationTokenSource();
        _schedulerTask = RunSchedulerAsync(_schedulerCts.Token);
        
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Stops the maintenance scheduler.
    /// </summary>
    public async Task<Result<bool>> StopAsync()
    {
        if (_schedulerCts == null || _schedulerTask == null)
        {
            return Result<bool>.Failure("Scheduler is not running");
        }

        _schedulerCts.Cancel();
        
        try
        {
            await _schedulerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _schedulerCts.Dispose();
        _schedulerCts = null;
        _schedulerTask = null;

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Executes a maintenance task immediately.
    /// </summary>
    public async Task<Result<MaintenanceExecution>> ExecuteTaskAsync(
        MaintenanceTask task,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        var execution = new MaintenanceExecution
        {
            Task = task,
            StartedAt = DateTime.UtcNow,
            Status = MaintenanceStatus.Running
        };

        try
        {
            var result = await task.Execute(ct);
            
            execution = execution with
            {
                CompletedAt = DateTime.UtcNow,
                Status = result.IsSuccess ? MaintenanceStatus.Completed : MaintenanceStatus.Failed,
                ResultMessage = result.IsSuccess ? "Success" : result.Error,
                Metadata = result.IsSuccess ? new Dictionary<string, object> { ["result"] = result.Value } : new Dictionary<string, object>()
            };
        }
        catch (Exception ex)
        {
            execution = execution with
            {
                CompletedAt = DateTime.UtcNow,
                Status = MaintenanceStatus.Failed,
                ResultMessage = ex.Message
            };
        }

        _history.Add(execution);
        return Result<MaintenanceExecution>.Success(execution);
    }

    /// <summary>
    /// Gets maintenance task execution history.
    /// </summary>
    public IReadOnlyList<MaintenanceExecution> GetHistory(int limit = 50)
    {
        return _history
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Gets anomaly alerts.
    /// </summary>
    public IReadOnlyList<AnomalyAlert> GetAlerts(bool unresolvedOnly = true)
    {
        var alerts = _alerts.AsEnumerable();
        if (unresolvedOnly)
        {
            alerts = alerts.Where(a => !a.IsResolved);
        }
        return alerts.OrderByDescending(a => a.DetectedAt).ToList();
    }

    /// <summary>
    /// Creates an anomaly alert.
    /// </summary>
    public Result<AnomalyAlert> CreateAlert(AnomalyAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _alerts.Add(alert);
        return Result<AnomalyAlert>.Success(alert);
    }

    /// <summary>
    /// Resolves an anomaly alert.
    /// </summary>
    public Result<AnomalyAlert> ResolveAlert(Guid alertId, string resolution)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
        if (alert == null)
        {
            return Result<AnomalyAlert>.Failure($"Alert {alertId} not found");
        }

        // Since ConcurrentBag doesn't support updates, we add a resolved copy
        var resolved = alert with
        {
            IsResolved = true,
            ResolvedAt = DateTime.UtcNow,
            Resolution = resolution
        };

        _alerts.Add(resolved);
        return Result<AnomalyAlert>.Success(resolved);
    }

    private async Task RunSchedulerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                foreach (var task in _tasks.Where(t => t.IsEnabled))
                {
                    if (ShouldExecute(task, now))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ExecuteTaskAsync(task, ct);
                            }
                            catch (Exception)
                            {
                                // Logged in ExecuteTaskAsync
                            }
                        }, ct);
                    }
                }

                // Check every minute
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private bool ShouldExecute(MaintenanceTask task, DateTime now)
    {
        var lastExecution = _history
            .Where(e => e.Task.Id == task.Id)
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefault();

        if (lastExecution == null)
        {
            return true; // Never executed
        }

        var timeSinceLastExecution = now - lastExecution.StartedAt;
        return timeSinceLastExecution >= task.Schedule;
    }

    /// <summary>
    /// Creates a compaction task for DAG snapshots.
    /// </summary>
    public static MaintenanceTask CreateCompactionTask(
        string name,
        TimeSpan schedule,
        Func<CancellationToken, Task<Result<CompactionResult>>> compactor)
    {
        return new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Compacts DAG snapshots to reduce storage",
            TaskType = MaintenanceTaskType.Compaction,
            Schedule = schedule,
            IsEnabled = true,
            Execute = async ct =>
            {
                var result = await compactor(ct);
                return result.IsSuccess
                    ? Result<object>.Success(result.Value)
                    : Result<object>.Failure(result.Error);
            }
        };
    }

    /// <summary>
    /// Creates an archiving task for old snapshots.
    /// </summary>
    public static MaintenanceTask CreateArchivingTask(
        string name,
        TimeSpan schedule,
        TimeSpan archiveAge,
        Func<TimeSpan, CancellationToken, Task<Result<ArchiveResult>>> archiver)
    {
        return new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Archives snapshots older than {archiveAge.TotalDays} days",
            TaskType = MaintenanceTaskType.Archiving,
            Schedule = schedule,
            IsEnabled = true,
            Execute = async ct =>
            {
                var result = await archiver(archiveAge, ct);
                return result.IsSuccess
                    ? Result<object>.Success(result.Value)
                    : Result<object>.Failure(result.Error);
            }
        };
    }

    /// <summary>
    /// Creates an anomaly detection task.
    /// </summary>
    public static MaintenanceTask CreateAnomalyDetectionTask(
        string name,
        TimeSpan schedule,
        Func<CancellationToken, Task<Result<AnomalyDetectionResult>>> detector)
    {
        return new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Detects anomalies in system metrics",
            TaskType = MaintenanceTaskType.AnomalyDetection,
            Schedule = schedule,
            IsEnabled = true,
            Execute = async ct =>
            {
                var result = await detector(ct);
                return result.IsSuccess
                    ? Result<object>.Success(result.Value)
                    : Result<object>.Failure(result.Error);
            }
        };
    }
}

/// <summary>
/// Interface for maintenance scheduler.
/// </summary>
public interface IMaintenanceScheduler
{
    /// <summary>
    /// Schedules a maintenance task.
    /// </summary>
    Result<MaintenanceTask> ScheduleTask(MaintenanceTask task);

    /// <summary>
    /// Starts the scheduler.
    /// </summary>
    Result<bool> Start();

    /// <summary>
    /// Stops the scheduler.
    /// </summary>
    Task<Result<bool>> StopAsync();

    /// <summary>
    /// Executes a task immediately.
    /// </summary>
    Task<Result<MaintenanceExecution>> ExecuteTaskAsync(MaintenanceTask task, CancellationToken ct = default);

    /// <summary>
    /// Gets execution history.
    /// </summary>
    IReadOnlyList<MaintenanceExecution> GetHistory(int limit = 50);

    /// <summary>
    /// Gets anomaly alerts.
    /// </summary>
    IReadOnlyList<AnomalyAlert> GetAlerts(bool unresolvedOnly = true);

    /// <summary>
    /// Creates an anomaly alert.
    /// </summary>
    Result<AnomalyAlert> CreateAlert(AnomalyAlert alert);

    /// <summary>
    /// Resolves an anomaly alert.
    /// </summary>
    Result<AnomalyAlert> ResolveAlert(Guid alertId, string resolution);
}

/// <summary>
/// Represents a scheduled maintenance task.
/// </summary>
public sealed record MaintenanceTask
{
    /// <summary>
    /// Gets the task identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the task name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the task description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the task type.
    /// </summary>
    public required MaintenanceTaskType TaskType { get; init; }

    /// <summary>
    /// Gets the schedule (how often to run).
    /// </summary>
    public required TimeSpan Schedule { get; init; }

    /// <summary>
    /// Gets a value indicating whether the task is enabled.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets the task execution function.
    /// </summary>
    public required Func<CancellationToken, Task<Result<object>>> Execute { get; init; }
}

/// <summary>
/// Maintenance task types.
/// </summary>
public enum MaintenanceTaskType
{
    /// <summary>
    /// Compaction task.
    /// </summary>
    Compaction = 0,

    /// <summary>
    /// Archiving task.
    /// </summary>
    Archiving = 1,

    /// <summary>
    /// Anomaly detection task.
    /// </summary>
    AnomalyDetection = 2,

    /// <summary>
    /// Custom task.
    /// </summary>
    Custom = 99
}

/// <summary>
/// Represents a maintenance task execution.
/// </summary>
public sealed record MaintenanceExecution
{
    /// <summary>
    /// Gets the task that was executed.
    /// </summary>
    public required MaintenanceTask Task { get; init; }

    /// <summary>
    /// Gets when the execution started.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets when the execution completed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Gets the execution status.
    /// </summary>
    public MaintenanceStatus Status { get; init; }

    /// <summary>
    /// Gets the result message.
    /// </summary>
    public string? ResultMessage { get; init; }

    /// <summary>
    /// Gets additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Maintenance execution status.
/// </summary>
public enum MaintenanceStatus
{
    /// <summary>
    /// Task is running.
    /// </summary>
    Running = 0,

    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Task failed.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Task was cancelled.
    /// </summary>
    Cancelled = 3
}

/// <summary>
/// Represents an anomaly alert.
/// </summary>
public sealed record AnomalyAlert
{
    /// <summary>
    /// Gets the alert identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the metric name where the anomaly was detected.
    /// </summary>
    public required string MetricName { get; init; }

    /// <summary>
    /// Gets the anomaly description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the severity level.
    /// </summary>
    public AlertSeverity Severity { get; init; } = AlertSeverity.Warning;

    /// <summary>
    /// Gets the expected value or range.
    /// </summary>
    public object? ExpectedValue { get; init; }

    /// <summary>
    /// Gets the actual observed value.
    /// </summary>
    public object? ObservedValue { get; init; }

    /// <summary>
    /// Gets when the anomaly was detected.
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets a value indicating whether the alert is resolved.
    /// </summary>
    public bool IsResolved { get; init; }

    /// <summary>
    /// Gets when the alert was resolved.
    /// </summary>
    public DateTime? ResolvedAt { get; init; }

    /// <summary>
    /// Gets the resolution description.
    /// </summary>
    public string? Resolution { get; init; }
}

/// <summary>
/// Alert severity levels.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning level.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error level.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical level.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Result of a compaction operation.
/// </summary>
public sealed record CompactionResult
{
    /// <summary>
    /// Gets the number of snapshots compacted.
    /// </summary>
    public int SnapshotsCompacted { get; init; }

    /// <summary>
    /// Gets the space saved in bytes.
    /// </summary>
    public long BytesSaved { get; init; }

    /// <summary>
    /// Gets the timestamp of the compaction.
    /// </summary>
    public DateTime CompactedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of an archiving operation.
/// </summary>
public sealed record ArchiveResult
{
    /// <summary>
    /// Gets the number of snapshots archived.
    /// </summary>
    public int SnapshotsArchived { get; init; }

    /// <summary>
    /// Gets the archive location.
    /// </summary>
    public required string ArchiveLocation { get; init; }

    /// <summary>
    /// Gets the timestamp of the archiving.
    /// </summary>
    public DateTime ArchivedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of anomaly detection.
/// </summary>
public sealed record AnomalyDetectionResult
{
    /// <summary>
    /// Gets the anomalies detected.
    /// </summary>
    public IReadOnlyList<AnomalyAlert> Anomalies { get; init; } = Array.Empty<AnomalyAlert>();

    /// <summary>
    /// Gets the timestamp of the detection.
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}
