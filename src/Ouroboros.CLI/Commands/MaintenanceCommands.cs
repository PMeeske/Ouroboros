using System.Text.Json;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Domain.Governance;
using LangChainPipeline.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Maintenance commands for DAG compaction, archiving, and anomaly detection.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
public static class MaintenanceCommands
{
    private static readonly MaintenanceScheduler _scheduler = new();

    /// <summary>
    /// Executes a maintenance command based on the provided options.
    /// </summary>
    public static async Task RunMaintenanceAsync(MaintenanceOptions options)
    {
        try
        {
            var command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "compact" => ExecuteCompactAsync(options),
                "archive" => ExecuteArchiveAsync(options),
                "detect-anomalies" => ExecuteDetectAnomaliesAsync(options),
                "schedule" => ExecuteScheduleAsync(options),
                "history" => ExecuteHistoryAsync(options),
                "alerts" => ExecuteAlertsAsync(options),
                _ => PrintErrorAsync($"Unknown maintenance command: {options.Command}. Valid commands: compact, archive, detect-anomalies, schedule, history, alerts")
            });
        }
        catch (Exception ex)
        {
            PrintError($"Maintenance operation failed: {ex.Message}");
            if (options.Verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    private static async Task ExecuteCompactAsync(MaintenanceOptions options)
    {
        Console.WriteLine("=== DAG Compaction ===\n");

        // Create a compaction task
        var task = MaintenanceScheduler.CreateCompactionTask(
            "Manual Compaction",
            TimeSpan.FromHours(24),
            async ct =>
            {
                // Simulated compaction logic
                await Task.Delay(100, ct);
                var result = new CompactionResult
                {
                    SnapshotsCompacted = 5,
                    BytesSaved = 1024 * 1024 * 10, // 10 MB
                    CompactedAt = DateTime.UtcNow
                };
                return Result<CompactionResult>.Success(result);
            });

        var execution = await _scheduler.ExecuteTaskAsync(task);

        if (execution.IsSuccess)
        {
            var exec = execution.Value;
            Console.WriteLine($"✓ Compaction completed in {(exec.CompletedAt - exec.StartedAt)?.TotalSeconds:F2}s");
            
            if (exec.Metadata.TryGetValue("result", out var resultObj) && resultObj is CompactionResult compactionResult)
            {
                Console.WriteLine($"  Snapshots Compacted: {compactionResult.SnapshotsCompacted}");
                Console.WriteLine($"  Space Saved: {FormatBytes(compactionResult.BytesSaved)}");
            }
        }
        else
        {
            PrintError($"Compaction failed: {execution.Error}");
        }
    }

    private static async Task ExecuteArchiveAsync(MaintenanceOptions options)
    {
        Console.WriteLine("=== DAG Archiving ===\n");

        var archivePath = options.ArchivePath ?? Path.Combine(Path.GetTempPath(), "ouroboros_archive");
        var archiveAge = TimeSpan.FromDays(options.ArchiveAgeDays);

        Console.WriteLine($"Archive age: {options.ArchiveAgeDays} days");
        Console.WriteLine($"Archive path: {archivePath}\n");

        // Create an archiving task
        var task = MaintenanceScheduler.CreateArchivingTask(
            "Manual Archiving",
            TimeSpan.FromHours(24),
            archiveAge,
            async (age, ct) =>
            {
                // Simulated archiving logic
                await Task.Delay(100, ct);
                
                Directory.CreateDirectory(archivePath);
                
                var result = new ArchiveResult
                {
                    SnapshotsArchived = 3,
                    ArchiveLocation = archivePath,
                    ArchivedAt = DateTime.UtcNow
                };
                return Result<ArchiveResult>.Success(result);
            });

        var execution = await _scheduler.ExecuteTaskAsync(task);

        if (execution.IsSuccess)
        {
            var exec = execution.Value;
            Console.WriteLine($"✓ Archiving completed in {(exec.CompletedAt - exec.StartedAt)?.TotalSeconds:F2}s");
            
            if (exec.Metadata.TryGetValue("result", out var resultObj) && resultObj is ArchiveResult archiveResult)
            {
                Console.WriteLine($"  Snapshots Archived: {archiveResult.SnapshotsArchived}");
                Console.WriteLine($"  Archive Location: {archiveResult.ArchiveLocation}");
            }
        }
        else
        {
            PrintError($"Archiving failed: {execution.Error}");
        }
    }

    private static async Task ExecuteDetectAnomaliesAsync(MaintenanceOptions options)
    {
        Console.WriteLine("=== Anomaly Detection ===\n");

        // Create an anomaly detection task
        var task = MaintenanceScheduler.CreateAnomalyDetectionTask(
            "Manual Anomaly Detection",
            TimeSpan.FromHours(1),
            async ct =>
            {
                // Simulated anomaly detection logic
                await Task.Delay(100, ct);
                
                var anomalies = new List<AnomalyAlert>();

                // Simulate detecting an anomaly
                var random = new Random();
                if (random.Next(100) > 50)
                {
                    anomalies.Add(new AnomalyAlert
                    {
                        MetricName = "snapshot_size",
                        Description = "Snapshot size exceeded expected range",
                        Severity = AlertSeverity.Warning,
                        ExpectedValue = "< 10 MB",
                        ObservedValue = "15 MB"
                    });
                }

                var result = new AnomalyDetectionResult
                {
                    Anomalies = anomalies,
                    DetectedAt = DateTime.UtcNow
                };
                return Result<AnomalyDetectionResult>.Success(result);
            });

        var execution = await _scheduler.ExecuteTaskAsync(task);

        if (execution.IsSuccess)
        {
            var exec = execution.Value;
            Console.WriteLine($"✓ Anomaly detection completed in {(exec.CompletedAt - exec.StartedAt)?.TotalSeconds:F2}s");
            
            if (exec.Metadata.TryGetValue("result", out var resultObj) && resultObj is AnomalyDetectionResult detectionResult)
            {
                Console.WriteLine($"  Anomalies Detected: {detectionResult.Anomalies.Count}");
                
                if (detectionResult.Anomalies.Count > 0)
                {
                    Console.WriteLine("\n  Detected Anomalies:");
                    foreach (var anomaly in detectionResult.Anomalies)
                    {
                        Console.WriteLine($"    [{anomaly.Severity}] {anomaly.MetricName}: {anomaly.Description}");
                        Console.WriteLine($"      Expected: {anomaly.ExpectedValue}, Observed: {anomaly.ObservedValue}");
                        
                        // Add to scheduler's alert list
                        _scheduler.CreateAlert(anomaly);
                    }
                }
            }
        }
        else
        {
            PrintError($"Anomaly detection failed: {execution.Error}");
        }
    }

    private static Task ExecuteScheduleAsync(MaintenanceOptions options)
    {
        Console.WriteLine("=== Schedule Maintenance Task ===\n");

        if (string.IsNullOrWhiteSpace(options.TaskName))
        {
            PrintError("Task name is required (use --task-name)");
            return Task.CompletedTask;
        }

        var schedule = TimeSpan.FromHours(options.ScheduleHours);
        Console.WriteLine($"Task: {options.TaskName}");
        Console.WriteLine($"Schedule: Every {options.ScheduleHours} hours\n");

        // Create a generic scheduled task
        var task = new MaintenanceTask
        {
            Id = Guid.NewGuid(),
            Name = options.TaskName,
            Description = $"Scheduled task: {options.TaskName}",
            TaskType = MaintenanceTaskType.Custom,
            Schedule = schedule,
            IsEnabled = true,
            Execute = async ct =>
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Executing scheduled task: {options.TaskName}");
                await Task.Delay(100, ct);
                return Result<object>.Success("Task completed");
            }
        };

        var result = _scheduler.ScheduleTask(task);

        if (result.IsSuccess)
        {
            Console.WriteLine($"✓ Task '{options.TaskName}' scheduled successfully");
            Console.WriteLine($"  ID: {task.Id}");
            Console.WriteLine($"  Schedule: Every {schedule.TotalHours} hours");
            Console.WriteLine($"\nNote: Use 'maintenance start-scheduler' to begin scheduled execution");
        }
        else
        {
            PrintError($"Failed to schedule task: {result.Error}");
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteHistoryAsync(MaintenanceOptions options)
    {
        Console.WriteLine("=== Maintenance Execution History ===\n");

        var history = _scheduler.GetHistory(options.Limit);

        if (history.Count == 0)
        {
            Console.WriteLine("No maintenance executions found.");
            return Task.CompletedTask;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            PrintHistoryTable(history, options.Verbose);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteAlertsAsync(MaintenanceOptions options)
    {
        Console.WriteLine("=== Anomaly Alerts ===\n");

        if (!string.IsNullOrWhiteSpace(options.AlertId) && !string.IsNullOrWhiteSpace(options.Resolution))
        {
            // Resolve an alert
            if (!Guid.TryParse(options.AlertId, out var alertId))
            {
                PrintError($"Invalid alert ID: {options.AlertId}");
                return Task.CompletedTask;
            }

            var result = _scheduler.ResolveAlert(alertId, options.Resolution);
            
            if (result.IsSuccess)
            {
                Console.WriteLine($"✓ Alert {alertId} resolved");
                Console.WriteLine($"  Resolution: {options.Resolution}");
            }
            else
            {
                PrintError($"Failed to resolve alert: {result.Error}");
            }

            return Task.CompletedTask;
        }

        // List alerts
        var alerts = _scheduler.GetAlerts(options.UnresolvedOnly);

        if (alerts.Count == 0)
        {
            Console.WriteLine(options.UnresolvedOnly ? "No unresolved alerts." : "No alerts found.");
            return Task.CompletedTask;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(alerts, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            PrintAlertsTable(alerts, options.Verbose);
        }

        return Task.CompletedTask;
    }

    private static void PrintHistoryTable(IReadOnlyList<MaintenanceExecution> executions, bool verbose)
    {
        Console.WriteLine($"Total Executions: {executions.Count}\n");

        foreach (var exec in executions)
        {
            var duration = exec.CompletedAt.HasValue
                ? (exec.CompletedAt.Value - exec.StartedAt).TotalSeconds
                : 0;

            string statusSymbol = exec.Status switch
            {
                MaintenanceStatus.Completed => "✓",
                MaintenanceStatus.Failed => "✗",
                MaintenanceStatus.Running => "▶",
                MaintenanceStatus.Cancelled => "○",
                _ => "-"
            };

            Console.WriteLine($"{statusSymbol} [{exec.Status}] {exec.Task.Name}");
            Console.WriteLine($"  Type: {exec.Task.TaskType}");
            Console.WriteLine($"  Started: {exec.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
            
            if (exec.CompletedAt.HasValue)
            {
                Console.WriteLine($"  Completed: {exec.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Duration: {duration:F2}s");
            }

            if (!string.IsNullOrWhiteSpace(exec.ResultMessage))
            {
                Console.WriteLine($"  Result: {exec.ResultMessage}");
            }

            if (verbose && exec.Metadata.Count > 0)
            {
                Console.WriteLine("  Metadata:");
                foreach (var kvp in exec.Metadata)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                }
            }

            Console.WriteLine();
        }
    }

    private static void PrintAlertsTable(IReadOnlyList<AnomalyAlert> alerts, bool verbose)
    {
        Console.WriteLine($"Total Alerts: {alerts.Count}\n");

        foreach (var alert in alerts)
        {
            string severitySymbol = alert.Severity switch
            {
                AlertSeverity.Critical => "🔴",
                AlertSeverity.Error => "🟠",
                AlertSeverity.Warning => "🟡",
                AlertSeverity.Info => "🔵",
                _ => "⚪"
            };

            string resolvedStatus = alert.IsResolved ? "✓ RESOLVED" : "⚠ ACTIVE";

            Console.WriteLine($"{severitySymbol} [{alert.Severity}] {resolvedStatus} {alert.MetricName}");
            Console.WriteLine($"  ID: {alert.Id}");
            Console.WriteLine($"  Description: {alert.Description}");
            Console.WriteLine($"  Expected: {alert.ExpectedValue ?? "N/A"}");
            Console.WriteLine($"  Observed: {alert.ObservedValue ?? "N/A"}");
            Console.WriteLine($"  Detected: {alert.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC");

            if (alert.IsResolved)
            {
                Console.WriteLine($"  Resolved: {alert.ResolvedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Resolution: {alert.Resolution}");
            }

            Console.WriteLine();
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"✗ {message}");
        Console.ResetColor();
    }

    private static Task PrintErrorAsync(string message)
    {
        PrintError(message);
        return Task.CompletedTask;
    }
}
