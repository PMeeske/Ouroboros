using System.Text;
using System.Text.Json;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Agent.MetaAI.SelfModel;
using Ouroboros.CLI.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Self-model commands for agent introspection.
/// Provides state, forecast, commitments, and explanation operations.
/// </summary>
public static class SelfCommands
{
    private static IIdentityGraph? _identityGraph;
    private static IPredictiveMonitor? _predictiveMonitor;
    private static IGlobalWorkspace? _globalWorkspace;

    /// <summary>
    /// Initializes self-model components.
    /// </summary>
    public static void Initialize(
        IIdentityGraph identityGraph,
        IPredictiveMonitor predictiveMonitor,
        IGlobalWorkspace globalWorkspace)
    {
        _identityGraph = identityGraph;
        _predictiveMonitor = predictiveMonitor;
        _globalWorkspace = globalWorkspace;
    }

    /// <summary>
    /// Executes a self-model command based on the provided options.
    /// </summary>
    public static async Task RunSelfAsync(SelfOptions options)
    {
        try
        {
            // Initialize with default components if not already initialized
            if (_identityGraph == null)
            {
                InitializeDefaults();
            }

            string command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "state" => ExecuteStateAsync(options),
                "forecast" => ExecuteForecastAsync(options),
                "commitments" => ExecuteCommitmentsAsync(options),
                "explain" => ExecuteExplainAsync(options),
                _ => PrintErrorAsync($"Unknown self command: {options.Command}. Valid commands: state, forecast, commitments, explain")
            });
        }
        catch (Exception ex)
        {
            PrintError($"Self-model operation failed: {ex.Message}");
            if (options.Verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    private static async Task ExecuteStateAsync(SelfOptions options)
    {
        Console.WriteLine("=== Agent Identity State ===\n");

        AgentIdentityState state = await _identityGraph!.GetStateAsync();

        if (options.OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await File.WriteAllTextAsync(options.OutputPath, json);
                Console.WriteLine($"\n✓ Saved to: {options.OutputPath}");
            }
        }
        else if (options.OutputFormat.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            PrintStateTable(state);
        }
        else // summary
        {
            PrintStateSummary(state);
        }
    }

    private static async Task ExecuteForecastAsync(SelfOptions options)
    {
        Console.WriteLine("=== Agent Forecasts & Predictions ===\n");

        List<Forecast> pendingForecasts = _predictiveMonitor!.GetPendingForecasts();
        ForecastCalibration calibration = _predictiveMonitor.GetCalibration(TimeSpan.FromDays(30));
        List<AnomalyDetection> anomalies = _predictiveMonitor.GetRecentAnomalies(10);

        if (options.OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var output = new
            {
                PendingForecasts = pendingForecasts,
                Calibration = calibration,
                RecentAnomalies = anomalies
            };

            string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await File.WriteAllTextAsync(options.OutputPath, json);
                Console.WriteLine($"\n✓ Saved to: {options.OutputPath}");
            }
        }
        else
        {
            PrintForecastTable(pendingForecasts, calibration, anomalies);
        }
    }

    private static async Task ExecuteCommitmentsAsync(SelfOptions options)
    {
        Console.WriteLine("=== Active Commitments ===\n");

        List<AgentCommitment> commitments = _identityGraph!.GetActiveCommitments();

        if (options.OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            string json = JsonSerializer.Serialize(commitments, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await File.WriteAllTextAsync(options.OutputPath, json);
                Console.WriteLine($"\n✓ Saved to: {options.OutputPath}");
            }
        }
        else
        {
            PrintCommitmentsTable(commitments);
        }
    }

    private static async Task ExecuteExplainAsync(SelfOptions options)
    {
        Console.WriteLine("=== Self-Explanation ===\n");

        // Get workspace items for context
        List<WorkspaceItem> contextItems = _globalWorkspace!.GetItems(WorkspacePriority.Normal);
        AgentIdentityState state = await _identityGraph!.GetStateAsync();

        // Build explanation
        StringBuilder narrative = new StringBuilder();
        narrative.AppendLine($"Agent: {state.Name}");
        narrative.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

        narrative.AppendLine("Current State:");
        narrative.AppendLine($"  Capabilities: {state.Capabilities.Count}");
        narrative.AppendLine($"  Active Commitments: {state.Commitments.Count(c => c.Status == CommitmentStatus.InProgress)}");
        narrative.AppendLine($"  Success Rate: {state.Performance.OverallSuccessRate:P1}");
        narrative.AppendLine($"  Avg Response Time: {state.Performance.AverageResponseTime:F0}ms\n");

        if (contextItems.Any())
        {
            narrative.AppendLine("Recent High-Priority Items:");
            foreach (WorkspaceItem item in contextItems.Take(5))
            {
                narrative.AppendLine($"  [{item.Priority}] {item.Content}");
            }
            narrative.AppendLine();
        }

        List<AgentCommitment> activeCommitments = state.Commitments
            .Where(c => c.Status == CommitmentStatus.InProgress)
            .OrderByDescending(c => c.Priority)
            .Take(3)
            .ToList();

        if (activeCommitments.Any())
        {
            narrative.AppendLine("Active Commitments:");
            foreach (AgentCommitment commitment in activeCommitments)
            {
                string deadline = commitment.Deadline < DateTime.UtcNow.AddDays(1) ? "⚠️ URGENT" : "";
                narrative.AppendLine($"  {commitment.Description} ({commitment.ProgressPercent:F0}%) {deadline}");
            }
        }

        Console.WriteLine(narrative.ToString());

        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            await File.WriteAllTextAsync(options.OutputPath, narrative.ToString());
            Console.WriteLine($"\n✓ Saved to: {options.OutputPath}");
        }
    }

    private static void PrintStateTable(AgentIdentityState state)
    {
        Console.WriteLine($"Agent: {state.Name}");
        Console.WriteLine($"ID: {state.AgentId}");
        Console.WriteLine($"Timestamp: {state.StateTimestamp:yyyy-MM-dd HH:mm:ss} UTC\n");

        Console.WriteLine("Performance:");
        Console.WriteLine($"  Success Rate: {state.Performance.OverallSuccessRate:P1}");
        Console.WriteLine($"  Total Tasks: {state.Performance.TotalTasks}");
        Console.WriteLine($"  Successful: {state.Performance.SuccessfulTasks}");
        Console.WriteLine($"  Failed: {state.Performance.FailedTasks}");
        Console.WriteLine($"  Avg Response Time: {state.Performance.AverageResponseTime:F0}ms\n");

        if (state.Resources.Any())
        {
            Console.WriteLine("Resources:");
            foreach (AgentResource resource in state.Resources)
            {
                double utilization = resource.Total > 0 ? (resource.Total - resource.Available) / resource.Total * 100 : 0;
                Console.WriteLine($"  {resource.Name}: {resource.Available:F1}/{resource.Total:F1} {resource.Unit} ({utilization:F0}% used)");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Capabilities: {state.Capabilities.Count}");
        Console.WriteLine($"Active Commitments: {state.Commitments.Count(c => c.Status == CommitmentStatus.InProgress)}");
    }

    private static void PrintStateSummary(AgentIdentityState state)
    {
        Console.WriteLine($"Agent '{state.Name}' is {(state.Performance.OverallSuccessRate >= 0.7 ? "performing well" : "underperforming")} with {state.Capabilities.Count} capabilities.");
        Console.WriteLine($"Current success rate: {state.Performance.OverallSuccessRate:P0} across {state.Performance.TotalTasks} tasks.");
        Console.WriteLine($"{state.Commitments.Count(c => c.Status == CommitmentStatus.InProgress)} active commitment(s) in progress.");
    }

    private static void PrintForecastTable(
        List<Forecast> forecasts,
        ForecastCalibration calibration,
        List<AnomalyDetection> anomalies)
    {
        Console.WriteLine($"Pending Forecasts: {forecasts.Count}");
        if (forecasts.Any())
        {
            foreach (Forecast forecast in forecasts.Take(5))
            {
                TimeSpan timeUntil = forecast.TargetTime - DateTime.UtcNow;
                Console.WriteLine($"  {forecast.MetricName}: {forecast.PredictedValue:F2} (confidence: {forecast.Confidence:P0}, due in {timeUntil.TotalHours:F1}h)");
            }
        }
        Console.WriteLine();

        Console.WriteLine("Calibration (30 days):");
        Console.WriteLine($"  Total Forecasts: {calibration.TotalForecasts}");
        Console.WriteLine($"  Average Confidence: {calibration.AverageConfidence:P1}");
        Console.WriteLine($"  Average Accuracy: {calibration.AverageAccuracy:P1}");
        Console.WriteLine($"  Brier Score: {calibration.BrierScore:F3} (lower is better)");
        Console.WriteLine($"  Calibration Error: {calibration.CalibrationError:F3}\n");

        if (anomalies.Any())
        {
            Console.WriteLine($"Recent Anomalies: {anomalies.Count}");
            foreach (AnomalyDetection anomaly in anomalies.Take(5))
            {
                Console.WriteLine($"  [{anomaly.Severity}] {anomaly.MetricName}: {anomaly.ObservedValue:F2} (expected: {anomaly.ExpectedValue:F2}, deviation: {anomaly.Deviation:F2})");
            }
        }
    }

    private static void PrintCommitmentsTable(List<AgentCommitment> commitments)
    {
        if (!commitments.Any())
        {
            Console.WriteLine("No active commitments.");
            return;
        }

        Console.WriteLine($"Total: {commitments.Count}\n");
        foreach (AgentCommitment commitment in commitments)
        {
            TimeSpan timeUntilDeadline = commitment.Deadline - DateTime.UtcNow;
            string urgency = timeUntilDeadline.TotalHours < 24 ? "⚠️" : "";
            string statusIndicator = commitment.Status switch
            {
                CommitmentStatus.InProgress => "▶",
                CommitmentStatus.Completed => "✓",
                CommitmentStatus.Failed => "✗",
                CommitmentStatus.AtRisk => "⚠",
                _ => "-"
            };

            Console.WriteLine($"{statusIndicator} [{commitment.Priority:F1}] {commitment.Description}");
            Console.WriteLine($"   Status: {commitment.Status} | Progress: {commitment.ProgressPercent:F0}% | Deadline: {commitment.Deadline:yyyy-MM-dd HH:mm} {urgency}");
            Console.WriteLine();
        }
    }

    private static void InitializeDefaults()
    {
        // Create mock capability registry for CLI
        var mockRegistry = new CapabilityRegistry(
            new MockChatModel(),
            new ToolRegistry(),
            new CapabilityRegistryConfig());

        _identityGraph = new IdentityGraph(
            Guid.NewGuid(),
            "OuroborosCLI",
            mockRegistry,
            Path.Combine(Path.GetTempPath(), "ouroboros_cli_identity.json"));

        _predictiveMonitor = new PredictiveMonitor();
        _globalWorkspace = new GlobalWorkspace();
    }

    private static Task PrintErrorAsync(string message)
    {
        PrintError(message);
        return Task.CompletedTask;
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {message}");
        Console.ResetColor();
    }

    // Mock chat model for CLI
    private sealed class MockChatModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            return Task.FromResult("Mock LLM response");
        }
    }
}
