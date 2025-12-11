using System.Text;
using System.Text.Json;
using LangChainPipeline.Agent.MetaAI.Affect;
using Ouroboros.CLI.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Affective dynamics commands for agent affect monitoring and policy management.
/// Provides show, policy, tune, signal, and reset operations.
/// </summary>
public static class AffectCommands
{
    private static IValenceMonitor? _valenceMonitor;
    private static IHomeostasisPolicy? _homeostasisPolicy;
    private static IPriorityModulator? _priorityModulator;

    /// <summary>
    /// Initializes affect components.
    /// </summary>
    public static void Initialize(
        IValenceMonitor valenceMonitor,
        IHomeostasisPolicy homeostasisPolicy,
        IPriorityModulator priorityModulator)
    {
        _valenceMonitor = valenceMonitor;
        _homeostasisPolicy = homeostasisPolicy;
        _priorityModulator = priorityModulator;
    }

    /// <summary>
    /// Executes an affect command based on the provided options.
    /// </summary>
    public static async Task RunAffectAsync(AffectOptions options)
    {
        try
        {
            // Initialize with default components if not already initialized
            if (_valenceMonitor == null)
            {
                InitializeDefaults();
            }

            string command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "show" => ExecuteShowAsync(options),
                "policy" => ExecutePolicyAsync(options),
                "tune" => ExecuteTuneAsync(options),
                "signal" => ExecuteSignalAsync(options),
                "reset" => ExecuteResetAsync(options),
                _ => PrintErrorAsync($"Unknown affect command: {options.Command}. Valid commands: show, policy, tune, signal, reset")
            });
        }
        catch (Exception ex)
        {
            PrintError($"Affect operation failed: {ex.Message}");
            if (options.Verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    private static async Task ExecuteShowAsync(AffectOptions options)
    {
        Console.WriteLine("=== Affective State ===\n");

        AffectiveState state = _valenceMonitor!.GetCurrentState();

        if (options.DetectStress)
        {
            Console.WriteLine("Running FFT stress detection...\n");
            StressDetectionResult stressResult = await _valenceMonitor.DetectStressAsync();
            PrintStressDetection(stressResult);
            Console.WriteLine();
        }

        if (options.OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var output = new
            {
                State = state,
                Policy = _homeostasisPolicy!.GetHealthSummary(),
                Queue = _priorityModulator!.GetStatistics()
            };
            string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
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
            Console.WriteLine("\n--- Homeostasis Policy ---");
            PrintPolicyHealth(_homeostasisPolicy!.GetHealthSummary());
            Console.WriteLine("\n--- Priority Queue ---");
            PrintQueueStats(_priorityModulator!.GetStatistics());
        }
        else // summary
        {
            PrintStateSummary(state);
        }
    }

    private static Task ExecutePolicyAsync(AffectOptions options)
    {
        Console.WriteLine("=== Homeostasis Policies ===\n");

        List<HomeostasisRule> rules = _homeostasisPolicy!.GetRules(activeOnly: false);

        if (options.OutputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            string json = JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"Total Rules: {rules.Count}\n");
            foreach (HomeostasisRule rule in rules)
            {
                string status = rule.IsActive ? "✓" : "✗";
                Console.WriteLine($"{status} [{rule.Priority:F1}] {rule.Name}");
                Console.WriteLine($"   Signal: {rule.TargetSignal}");
                Console.WriteLine($"   Bounds: [{rule.LowerBound:F2}, {rule.UpperBound:F2}]");
                Console.WriteLine($"   Target: {rule.TargetValue:F2}");
                Console.WriteLine($"   Action: {rule.Action}");
                Console.WriteLine();
            }
        }

        // Show recent violations
        List<PolicyViolation> violations = _homeostasisPolicy.GetViolationHistory(5);
        if (violations.Any())
        {
            Console.WriteLine("--- Recent Violations ---");
            foreach (PolicyViolation violation in violations)
            {
                Console.WriteLine($"  [{violation.Severity:P0}] {violation.RuleName}: {violation.ViolationType}");
                Console.WriteLine($"      Value: {violation.ObservedValue:F3} (bounds: [{violation.LowerBound:F2}, {violation.UpperBound:F2}])");
            }
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteTuneAsync(AffectOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RuleName))
        {
            PrintError("Tune command requires --rule parameter");
            return Task.CompletedTask;
        }

        List<HomeostasisRule> rules = _homeostasisPolicy!.GetRules(activeOnly: false);
        HomeostasisRule? rule = rules.FirstOrDefault(r =>
            r.Name.Equals(options.RuleName, StringComparison.OrdinalIgnoreCase));

        if (rule == null)
        {
            PrintError($"Rule not found: {options.RuleName}");
            Console.WriteLine("Available rules: " + string.Join(", ", rules.Select(r => r.Name)));
            return Task.CompletedTask;
        }

        Console.WriteLine($"=== Tuning Rule: {rule.Name} ===\n");

        Console.WriteLine($"Current bounds: [{rule.LowerBound:F2}, {rule.UpperBound:F2}]");
        Console.WriteLine($"Current target: {rule.TargetValue:F2}\n");

        _homeostasisPolicy.UpdateRule(
            rule.Id,
            options.LowerBound,
            options.UpperBound,
            options.TargetValue);

        // Get updated rule
        HomeostasisRule? updated = _homeostasisPolicy.GetRules(activeOnly: false)
            .FirstOrDefault(r => r.Id == rule.Id);

        if (updated != null)
        {
            Console.WriteLine($"Updated bounds: [{updated.LowerBound:F2}, {updated.UpperBound:F2}]");
            Console.WriteLine($"Updated target: {updated.TargetValue:F2}");
            Console.WriteLine("\n✓ Rule updated successfully");
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteSignalAsync(AffectOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SignalType))
        {
            PrintError("Signal command requires --type parameter");
            return Task.CompletedTask;
        }

        if (!options.SignalValue.HasValue)
        {
            PrintError("Signal command requires --signal parameter");
            return Task.CompletedTask;
        }

        if (!Enum.TryParse<SignalType>(options.SignalType, true, out SignalType signalType))
        {
            PrintError($"Invalid signal type: {options.SignalType}");
            Console.WriteLine("Valid types: stress, confidence, curiosity, valence, arousal");
            return Task.CompletedTask;
        }

        Console.WriteLine($"=== Recording Signal ===\n");

        AffectiveState before = _valenceMonitor!.GetCurrentState();
        _valenceMonitor.RecordSignal("cli", options.SignalValue.Value, signalType);
        AffectiveState after = _valenceMonitor.GetCurrentState();

        Console.WriteLine($"Signal: {signalType} = {options.SignalValue.Value:F3}");
        Console.WriteLine($"\nBefore:");
        PrintStateCompact(before);
        Console.WriteLine($"\nAfter:");
        PrintStateCompact(after);

        // Check for policy violations
        List<PolicyViolation> violations = _homeostasisPolicy!.EvaluatePolicies(after);
        if (violations.Any())
        {
            Console.WriteLine($"\n⚠️ Policy Violations Detected: {violations.Count}");
            foreach (PolicyViolation violation in violations)
            {
                Console.WriteLine($"   [{violation.Severity:P0}] {violation.RuleName}: {violation.ViolationType}");
            }
        }
        else
        {
            Console.WriteLine("\n✓ All policies within bounds");
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteResetAsync(AffectOptions options)
    {
        Console.WriteLine("=== Resetting Affective State ===\n");

        AffectiveState before = _valenceMonitor!.GetCurrentState();
        Console.WriteLine("Before reset:");
        PrintStateCompact(before);

        _valenceMonitor.Reset();

        AffectiveState after = _valenceMonitor.GetCurrentState();
        Console.WriteLine("\nAfter reset:");
        PrintStateCompact(after);

        Console.WriteLine("\n✓ Affective state reset to baseline");

        return Task.CompletedTask;
    }

    private static void PrintStateTable(AffectiveState state)
    {
        Console.WriteLine($"State ID: {state.Id}");
        Console.WriteLine($"Timestamp: {state.Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n");

        Console.WriteLine("Affective Dimensions:");
        PrintDimensionBar("Valence", state.Valence, -1.0, 1.0);
        PrintDimensionBar("Stress", state.Stress, 0.0, 1.0);
        PrintDimensionBar("Confidence", state.Confidence, 0.0, 1.0);
        PrintDimensionBar("Curiosity", state.Curiosity, 0.0, 1.0);
        PrintDimensionBar("Arousal", state.Arousal, 0.0, 1.0);
    }

    private static void PrintDimensionBar(string name, double value, double min, double max)
    {
        int barWidth = 30;
        double normalized = (value - min) / (max - min);
        int filled = (int)(normalized * barWidth);
        filled = Math.Clamp(filled, 0, barWidth);

        string bar = new string('█', filled) + new string('░', barWidth - filled);
        Console.WriteLine($"  {name,-12}: [{bar}] {value:F3}");
    }

    private static void PrintStateCompact(AffectiveState state)
    {
        Console.WriteLine($"  Valence: {state.Valence:F3} | Stress: {state.Stress:F3} | Confidence: {state.Confidence:F3} | Curiosity: {state.Curiosity:F3} | Arousal: {state.Arousal:F3}");
    }

    private static void PrintStateSummary(AffectiveState state)
    {
        string mood = state.Valence switch
        {
            > 0.5 => "very positive",
            > 0.2 => "positive",
            > -0.2 => "neutral",
            > -0.5 => "negative",
            _ => "very negative"
        };

        string stressLevel = state.Stress switch
        {
            > 0.7 => "high stress",
            > 0.4 => "moderate stress",
            _ => "low stress"
        };

        Console.WriteLine($"Agent affect is {mood} with {stressLevel}.");
        Console.WriteLine($"Confidence: {state.Confidence:P0}, Curiosity: {state.Curiosity:P0}");
    }

    private static void PrintPolicyHealth(PolicyHealthSummary health)
    {
        Console.WriteLine($"  Rules: {health.ActiveRules}/{health.TotalRules} active");
        Console.WriteLine($"  Violations (24h): {health.RecentViolations}");
        Console.WriteLine($"  Corrections: {health.SuccessfulCorrections}/{health.TotalCorrections} successful ({health.CorrectionSuccessRate:P0})");
    }

    private static void PrintQueueStats(QueueStatistics stats)
    {
        Console.WriteLine($"  Tasks: {stats.PendingTasks} pending, {stats.InProgressTasks} in progress");
        Console.WriteLine($"  Completed: {stats.CompletedTasks}, Failed: {stats.FailedTasks}");
        Console.WriteLine($"  Avg Priority: {stats.AverageModulatedPriority:F2}");
        Console.WriteLine($"  Max Threat: {stats.HighestThreat:F2}, Max Opportunity: {stats.HighestOpportunity:F2}");
    }

    private static void PrintStressDetection(StressDetectionResult result)
    {
        Console.WriteLine("FFT Stress Analysis:");
        Console.WriteLine($"  Stress Level: {result.StressLevel:F3}");
        Console.WriteLine($"  Dominant Frequency: {result.Frequency:F4} Hz");
        Console.WriteLine($"  Amplitude: {result.Amplitude:F4}");
        Console.WriteLine($"  Anomalous: {(result.IsAnomalous ? "⚠️ YES" : "✓ No")}");
        Console.WriteLine($"  Spectral Peaks: {result.SpectralPeaks.Count}");
        Console.WriteLine($"  Analysis: {result.Analysis}");
    }

    private static void InitializeDefaults()
    {
        _valenceMonitor = new ValenceMonitor();
        _homeostasisPolicy = new HomeostasisPolicy();
        _priorityModulator = new PriorityModulator();
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
}
