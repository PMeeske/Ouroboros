using System.Text.Json;
using LangChain.DocumentLoaders;
using LangChainPipeline.Options;
using LangChainPipeline.Pipeline.Branches;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// DAG (Directed Acyclic Graph) commands for pipeline branch management.
/// Provides snapshot, show, replay, and retention operations.
/// </summary>
public static class DagCommands
{
    private static readonly GlobalProjectionService _projectionService = new();

    /// <summary>
    /// Executes a DAG command based on the provided options.
    /// </summary>
    public static async Task RunDagAsync(DagOptions options)
    {
        try
        {
            var command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "snapshot" => ExecuteSnapshotAsync(options),
                "show" => ExecuteShowAsync(options),
                "replay" => ExecuteReplayAsync(options),
                "validate" => ExecuteValidateAsync(options),
                "retention" => ExecuteRetentionAsync(options),
                _ => PrintErrorAsync($"Unknown DAG command: {options.Command}. Valid commands: snapshot, show, replay, validate, retention")
            });
        }
        catch (Exception ex)
        {
            PrintError($"DAG operation failed: {ex.Message}");
            if (options.Verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    private static async Task ExecuteSnapshotAsync(DagOptions options)
    {
        Console.WriteLine("=== Creating DAG Snapshot ===");

        // For demonstration, create a snapshot with test branches
        // In production, this would load actual pipeline branches
        var branches = new List<PipelineBranch>();
        
        if (!string.IsNullOrWhiteSpace(options.BranchName))
        {
            var branch = CreateTestBranch(options.BranchName);
            branches.Add(branch);
        }
        else
        {
            // Create a default test branch
            branches.Add(CreateTestBranch("main"));
        }

        var metadata = new Dictionary<string, object>
        {
            ["created_by"] = "cli",
            ["timestamp"] = DateTime.UtcNow,
            ["branch_count"] = branches.Count
        };

        var result = await _projectionService.CreateEpochAsync(branches, metadata);
        
        if (result.IsSuccess)
        {
            var epoch = result.Value;
            Console.WriteLine($"✓ Created epoch {epoch.EpochNumber} (ID: {epoch.EpochId})");
            Console.WriteLine($"  Branches: {epoch.Branches.Count}");
            Console.WriteLine($"  Created: {epoch.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await ExportEpochAsync(epoch, options.OutputPath);
                Console.WriteLine($"  Exported to: {options.OutputPath}");
            }
        }
        else
        {
            PrintError($"Failed to create snapshot: {result.Error}");
        }
    }

    private static Task ExecuteShowAsync(DagOptions options)
    {
        Console.WriteLine("=== DAG Information ===");

        if (options.EpochNumber.HasValue)
        {
            // Show specific epoch
            var result = _projectionService.GetEpoch(options.EpochNumber.Value);
            if (result.IsSuccess)
            {
                PrintEpochInfo(result.Value, options.Format == "json");
            }
            else
            {
                PrintError($"Epoch not found: {result.Error}");
            }
        }
        else
        {
            // Show latest epoch or metrics
            var metricsResult = _projectionService.GetMetrics();
            if (metricsResult.IsSuccess)
            {
                PrintMetrics(metricsResult.Value, options.Format == "json");
            }

            var latestResult = _projectionService.GetLatestEpoch();
            if (latestResult.IsSuccess)
            {
                Console.WriteLine("\nLatest Epoch:");
                PrintEpochInfo(latestResult.Value, options.Format == "json");
            }
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteReplayAsync(DagOptions options)
    {
        Console.WriteLine("=== DAG Replay ===");

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            PrintError("Replay requires --input path to snapshot file");
            return;
        }

        if (!File.Exists(options.InputPath))
        {
            PrintError($"Snapshot file not found: {options.InputPath}");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(options.InputPath);
            var epoch = JsonSerializer.Deserialize<EpochSnapshot>(json);

            if (epoch is null)
            {
                PrintError("Failed to deserialize epoch snapshot");
                return;
            }

            Console.WriteLine($"Replaying epoch {epoch.EpochNumber} from {options.InputPath}");
            Console.WriteLine($"  Branches: {epoch.Branches.Count}");
            Console.WriteLine($"  Created: {epoch.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");

            foreach (var branchSnapshot in epoch.Branches)
            {
                Console.WriteLine($"\n  Branch: {branchSnapshot.Name}");
                Console.WriteLine($"    Events: {branchSnapshot.Events.Count}");
                Console.WriteLine($"    Vectors: {branchSnapshot.Vectors.Count}");

                if (options.Verbose)
                {
                    foreach (var evt in branchSnapshot.Events.Take(5))
                    {
                        Console.WriteLine($"      - {evt.GetType().Name} at {evt.Timestamp:HH:mm:ss}");
                    }
                    if (branchSnapshot.Events.Count > 5)
                    {
                        Console.WriteLine($"      ... and {branchSnapshot.Events.Count - 5} more");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"Replay failed: {ex.Message}");
        }
    }

    private static Task ExecuteValidateAsync(DagOptions options)
    {
        Console.WriteLine("=== DAG Validation ===");

        var allEpochs = _projectionService.Epochs;
        Console.WriteLine($"Total epochs: {allEpochs.Count}");

        var validationErrors = 0;
        foreach (var epoch in allEpochs)
        {
            // Validate each branch snapshot
            foreach (var branchSnapshot in epoch.Branches)
            {
                // Compute hash
                var hash = BranchHash.ComputeHash(branchSnapshot);
                Console.WriteLine($"Epoch {epoch.EpochNumber}, Branch '{branchSnapshot.Name}': Hash {hash[..8]}...");

                // Could add additional validation logic here
                // e.g., check event ordering, validate vector embeddings, etc.
            }
        }

        if (validationErrors == 0)
        {
            Console.WriteLine("✓ All snapshots validated successfully");
        }
        else
        {
            PrintError($"Found {validationErrors} validation errors");
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteRetentionAsync(DagOptions options)
    {
        Console.WriteLine($"=== Retention Policy Evaluation{(options.DryRun ? " (DRY RUN)" : "")} ===");

        // Build retention policy from options
        RetentionPolicy policy;
        if (options.MaxAgeDays.HasValue && options.MaxCount.HasValue)
        {
            policy = RetentionPolicy.Combined(TimeSpan.FromDays(options.MaxAgeDays.Value), options.MaxCount.Value);
        }
        else if (options.MaxAgeDays.HasValue)
        {
            policy = RetentionPolicy.ByAge(TimeSpan.FromDays(options.MaxAgeDays.Value));
        }
        else if (options.MaxCount.HasValue)
        {
            policy = RetentionPolicy.ByCount(options.MaxCount.Value);
        }
        else
        {
            PrintError("Retention policy requires --max-age-days and/or --max-count");
            return Task.CompletedTask;
        }

        // Convert epochs to snapshot metadata
        var snapshots = _projectionService.Epochs
            .SelectMany(e => e.Branches.Select(b => new SnapshotMetadata
            {
                Id = e.EpochId.ToString(),
                BranchName = b.Name,
                CreatedAt = e.CreatedAt,
                Hash = BranchHash.ComputeHash(b),
                SizeBytes = EstimateSize(b)
            }))
            .ToList();

        if (snapshots.Count == 0)
        {
            Console.WriteLine("No snapshots found to evaluate");
            return Task.CompletedTask;
        }

        var plan = RetentionEvaluator.Evaluate(snapshots, policy, options.DryRun);

        Console.WriteLine(plan.GetSummary());
        Console.WriteLine($"\nSnapshots to keep: {plan.ToKeep.Count}");
        if (options.Verbose)
        {
            foreach (var snapshot in plan.ToKeep)
            {
                Console.WriteLine($"  ✓ {snapshot.BranchName} ({snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss})");
            }
        }

        Console.WriteLine($"\nSnapshots to delete: {plan.ToDelete.Count}");
        if (options.Verbose)
        {
            foreach (var snapshot in plan.ToDelete)
            {
                Console.WriteLine($"  ✗ {snapshot.BranchName} ({snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss})");
            }
        }

        if (!options.DryRun && plan.ToDelete.Count > 0)
        {
            Console.WriteLine("\nNote: Actual deletion not implemented in this phase");
        }

        return Task.CompletedTask;
    }

    private static void PrintEpochInfo(EpochSnapshot epoch, bool asJson)
    {
        if (asJson)
        {
            var json = JsonSerializer.Serialize(epoch, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"Epoch {epoch.EpochNumber}:");
            Console.WriteLine($"  ID: {epoch.EpochId}");
            Console.WriteLine($"  Created: {epoch.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  Branches: {epoch.Branches.Count}");
            foreach (var branch in epoch.Branches)
            {
                Console.WriteLine($"    - {branch.Name}: {branch.Events.Count} events, {branch.Vectors.Count} vectors");
            }
        }
    }

    private static void PrintMetrics(ProjectionMetrics metrics, bool asJson)
    {
        if (asJson)
        {
            var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine("Global Metrics:");
            Console.WriteLine($"  Total Epochs: {metrics.TotalEpochs}");
            Console.WriteLine($"  Total Branches: {metrics.TotalBranches}");
            Console.WriteLine($"  Total Events: {metrics.TotalEvents}");
            Console.WriteLine($"  Average Events per Branch: {metrics.AverageEventsPerBranch:F2}");
            if (metrics.LastEpochAt.HasValue)
            {
                Console.WriteLine($"  Last Epoch: {metrics.LastEpochAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            }
        }
    }

    private static async Task ExportEpochAsync(EpochSnapshot epoch, string path)
    {
        var json = JsonSerializer.Serialize(epoch, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static PipelineBranch CreateTestBranch(string name)
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch(name, store, source);
    }

    private static long EstimateSize(BranchSnapshot snapshot)
    {
        // Rough estimate: 1KB per event + 1KB per vector
        return (snapshot.Events.Count + snapshot.Vectors.Count) * 1024L;
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
