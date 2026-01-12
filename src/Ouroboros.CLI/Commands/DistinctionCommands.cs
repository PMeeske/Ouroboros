// <copyright file="DistinctionCommands.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.CLI.Options;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using Ouroboros.Core.Learning;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Commands for managing distinction learning from consciousness dream cycles.
/// </summary>
public static class DistinctionCommands
{
    /// <summary>
    /// Shows distinction learning status and statistics.
    /// </summary>
    public static async Task RunStatusAsync(DistinctionStatusOptions options)
    {
        try
        {
            var storage = GetStorage();
            
            PrintHeader("Distinction Learning Status");
            
            var listResult = await storage.ListWeightsAsync();
            if (listResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {listResult.Error}");
                Console.ResetColor();
                return;
            }

            var allWeights = listResult.Value;
            var activeWeights = allWeights.Where(w => !w.IsDissolved).ToList();
            var dissolvedWeights = allWeights.Where(w => w.IsDissolved).ToList();

            var sizeResult = await storage.GetTotalStorageSizeAsync();
            var totalSize = sizeResult.IsSuccess ? sizeResult.Value : 0L;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    Distinction Statistics                     ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
            Console.ResetColor();

            Console.WriteLine($"║  Active Distinctions:    {activeWeights.Count,7}                            ║");
            Console.WriteLine($"║  Dissolved Distinctions: {dissolvedWeights.Count,7}                            ║");
            Console.WriteLine($"║  Total Storage Size:     {FormatBytes(totalSize),10}                        ║");

            if (activeWeights.Count > 0)
            {
                var avgFitness = activeWeights.Average(w => w.Fitness);
                var maxFitness = activeWeights.Max(w => w.Fitness);
                var minFitness = activeWeights.Min(w => w.Fitness);

                Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
                Console.WriteLine($"║  Average Fitness:        {avgFitness,7:F3}                            ║");
                Console.WriteLine($"║  Max Fitness:            {maxFitness,7:F3}                            ║");
                Console.WriteLine($"║  Min Fitness:            {minFitness,7:F3}                            ║");

                // Stage distribution
                var stageGroups = activeWeights.GroupBy(w => w.LearnedAtStage).ToList();
                if (stageGroups.Count > 0)
                {
                    Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
                    Console.WriteLine("║  Learning Stages:                                             ║");
                    foreach (var group in stageGroups.OrderByDescending(g => g.Count()))
                    {
                        var stageName = string.IsNullOrEmpty(group.Key) ? "Unknown" : group.Key;
                        Console.WriteLine($"║    • {stageName,-20} {group.Count(),5}                       ║");
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            if (options.Verbose)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Storage Path: {DistinctionStorageConfig.Default.StoragePath}");
                Console.WriteLine($"Max Storage:  {FormatBytes(DistinctionStorageConfig.Default.MaxTotalStorageBytes)}");
                Console.WriteLine($"Retention:    {DistinctionStorageConfig.Default.DissolvedRetentionPeriod.Days} days");
                Console.ResetColor();
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Lists all learned distinctions with optional filtering.
    /// </summary>
    public static async Task RunListAsync(DistinctionListOptions options)
    {
        try
        {
            var storage = GetStorage();

            PrintHeader("Learned Distinctions");

            var listResult = await storage.ListWeightsAsync();
            if (listResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {listResult.Error}");
                Console.ResetColor();
                return;
            }

            var weights = listResult.Value;

            // Apply filters
            if (!options.ShowDissolved)
            {
                weights = weights.Where(w => !w.IsDissolved).ToList();
            }

            if (!string.IsNullOrWhiteSpace(options.Stage))
            {
                weights = weights.Where(w => w.LearnedAtStage.Equals(options.Stage, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (options.MinFitness.HasValue)
            {
                weights = weights.Where(w => w.Fitness >= options.MinFitness.Value).ToList();
            }

            // Sort by fitness descending
            weights = weights.OrderByDescending(w => w.Fitness).Take(options.Limit).ToList();

            if (weights.Count == 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No distinctions found matching the criteria.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"{"ID",-38} {"Fitness",8} {"Stage",-20} {"Size",10} {"Created",-20} {"Status",-10}");
            Console.WriteLine(new string('─', 120));

            foreach (var weight in weights)
            {
                var idShort = weight.Id.Length > 36 ? weight.Id[..36] : weight.Id;
                var status = weight.IsDissolved ? "Dissolved" : "Active";
                var statusColor = weight.IsDissolved ? ConsoleColor.DarkGray : ConsoleColor.Green;

                Console.Write($"{idShort,-38} ");
                
                // Color-code fitness
                var fitnessColor = weight.Fitness >= 0.7 ? ConsoleColor.Green
                                 : weight.Fitness >= 0.4 ? ConsoleColor.Yellow
                                 : ConsoleColor.Red;
                Console.ForegroundColor = fitnessColor;
                Console.Write($"{weight.Fitness,8:F3}");
                Console.ResetColor();

                Console.Write($" {weight.LearnedAtStage,-20} ");
                Console.Write($"{FormatBytes(weight.SizeBytes),10} ");
                Console.Write($"{weight.CreatedAt:yyyy-MM-dd HH:mm:ss}  ");
                
                Console.ForegroundColor = statusColor;
                Console.Write($"{status,-10}");
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Showing {weights.Count} distinction(s)");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Dissolves low-fitness distinctions to free storage.
    /// </summary>
    public static async Task RunDissolveAsync(DistinctionDissolveOptions options)
    {
        try
        {
            var storage = GetStorage();
            var learner = GetLearner(storage);

            PrintHeader(options.DryRun ? "Dissolution Preview (Dry Run)" : "Dissolving Distinctions");

            var strategy = options.Strategy.ToLowerInvariant() switch
            {
                "fitness" => DissolutionStrategy.FitnessThreshold,
                "oldest" => DissolutionStrategy.OldestFirst,
                "lru" => DissolutionStrategy.LeastRecentlyUsed,
                _ => DissolutionStrategy.FitnessThreshold
            };

            // Get current state
            var listResult = await storage.ListWeightsAsync();
            if (listResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {listResult.Error}");
                Console.ResetColor();
                return;
            }

            var weights = listResult.Value;
            var toDissolve = strategy switch
            {
                DissolutionStrategy.FitnessThreshold => weights.Where(w => !w.IsDissolved && w.Fitness < options.Threshold).ToList(),
                DissolutionStrategy.OldestFirst => weights.Where(w => !w.IsDissolved).OrderBy(w => w.CreatedAt).Take(10).ToList(),
                DissolutionStrategy.LeastRecentlyUsed => weights.Where(w => !w.IsDissolved).OrderBy(w => w.CreatedAt).Take(10).ToList(),
                _ => new List<DistinctionWeightMetadata>()
            };

            if (toDissolve.Count == 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ No distinctions need dissolution.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Found {toDissolve.Count} distinction(s) to dissolve:");
            Console.WriteLine();

            foreach (var weight in toDissolve)
            {
                var idShort = weight.Id.Length > 36 ? weight.Id[..36] : weight.Id;
                Console.WriteLine($"  • {idShort} (fitness: {weight.Fitness:F3}, stage: {weight.LearnedAtStage})");
            }

            if (options.DryRun)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Dry run mode - no changes made. Remove --dry-run to actually dissolve.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("Proceed with dissolution? [y/N]: ");
            Console.ResetColor();
            
            var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirmation != "y" && confirmation != "yes")
            {
                Console.WriteLine("Cancelled.");
                return;
            }

            // Perform dissolution
            var currentState = DistinctionState.Initial();
            var dissolveResult = await learner.DissolveAsync(currentState, strategy);

            if (dissolveResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {dissolveResult.Error}");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Successfully dissolved {toDissolve.Count} distinction(s).");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Manually triggers distinction learning on provided text.
    /// </summary>
    public static async Task RunLearnAsync(DistinctionLearnOptions options)
    {
        try
        {
            var storage = GetStorage();
            var learner = GetLearner(storage);

            PrintHeader("Manual Distinction Learning");

            var observation = new Observation(
                Content: options.Text,
                Timestamp: DateTime.UtcNow,
                PriorCertainty: 0.5,
                Context: new Dictionary<string, object>
                {
                    { "source", "manual-cli" },
                    { "stage", options.Stage }
                });

            var currentState = DistinctionState.Initial();

            if (options.ShowStages)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Learning through consciousness stages...");
                Console.ResetColor();
                Console.WriteLine();
            }

            var result = await learner.UpdateFromDistinctionAsync(currentState, observation, options.Stage);

            if (result.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {result.Error}");
                Console.ResetColor();
                return;
            }

            var newState = result.Value;
            var learnedDistinction = newState.ActiveDistinctions.Last();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Successfully learned new distinction");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine($"  ID:               {learnedDistinction.Id}");
            Console.WriteLine($"  Fitness:          {learnedDistinction.Fitness:F3}");
            Console.WriteLine($"  Stage:            {learnedDistinction.LearnedAtStage}");
            Console.WriteLine($"  Learned At:       {learnedDistinction.LearnedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Content Preview:  {GetPreview(learnedDistinction.Content, 60)}");
            Console.WriteLine();
            Console.WriteLine($"  Epistemic Certainty: {newState.EpistemicCertainty:F3}");
            Console.WriteLine($"  Total Distinctions:  {newState.ActiveDistinctions.Count}");
            Console.WriteLine();

            if (options.ShowStages)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Note: In production, distinctions are learned through the full consciousness dream cycle.");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Exports distinctions to JSON format.
    /// </summary>
    public static async Task RunExportAsync(DistinctionExportOptions options)
    {
        try
        {
            var storage = GetStorage();

            var outputPath = options.Output ?? "distinctions-export.json";

            PrintHeader($"Exporting to {outputPath}");

            var listResult = await storage.ListWeightsAsync();
            if (listResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {listResult.Error}");
                Console.ResetColor();
                return;
            }

            var weights = listResult.Value;

            if (!options.IncludeDissolved)
            {
                weights = weights.Where(w => !w.IsDissolved).ToList();
            }

            var exportData = new
            {
                ExportedAt = DateTime.UtcNow,
                TotalCount = weights.Count,
                ActiveCount = weights.Count(w => !w.IsDissolved),
                DissolvedCount = weights.Count(w => w.IsDissolved),
                Distinctions = weights.Select(w => new
                {
                    w.Id,
                    w.Fitness,
                    w.LearnedAtStage,
                    w.CreatedAt,
                    w.IsDissolved,
                    SizeBytes = w.SizeBytes,
                    SizeFormatted = FormatBytes(w.SizeBytes)
                }).ToList()
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(outputPath, json);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Successfully exported {weights.Count} distinction(s) to {outputPath}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"  Active:     {exportData.ActiveCount}");
            Console.WriteLine($"  Dissolved:  {exportData.DissolvedCount}");
            Console.WriteLine($"  File size:  {FormatBytes(new FileInfo(outputPath).Length)}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Clears all distinctions with confirmation.
    /// </summary>
    public static async Task RunClearAsync(DistinctionClearOptions options)
    {
        try
        {
            var storage = GetStorage();

            PrintHeader("Clear All Distinctions");

            var listResult = await storage.ListWeightsAsync();
            if (listResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {listResult.Error}");
                Console.ResetColor();
                return;
            }

            var weights = listResult.Value;
            var activeCount = weights.Count(w => !w.IsDissolved);

            if (activeCount == 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No active distinctions to clear.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("⚠ WARNING: This will dissolve ALL active distinctions!");
            Console.ResetColor();
            Console.WriteLine($"  {activeCount} distinction(s) will be dissolved.");
            Console.WriteLine();

            if (!options.Confirm)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Type 'DELETE' to confirm: ");
                Console.ResetColor();
                
                var confirmation = Console.ReadLine()?.Trim();
                if (confirmation != "DELETE")
                {
                    Console.WriteLine("Cancelled.");
                    return;
                }
            }

            // Dissolve all active distinctions
            var learner = GetLearner(storage);
            var currentState = DistinctionState.Initial();
            var dissolveResult = await learner.DissolveAsync(currentState, DissolutionStrategy.All);

            if (dissolveResult.IsFailure)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {dissolveResult.Error}");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Successfully cleared {activeCount} distinction(s).");
            Console.ResetColor();
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static IDistinctionWeightStorage GetStorage()
    {
        var config = DistinctionStorageConfig.Default;
        return new FileSystemDistinctionWeightStorage(config);
    }

    private static IDistinctionLearner GetLearner(IDistinctionWeightStorage storage)
    {
        return new DistinctionLearner(storage);
    }

    private static void PrintHeader(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"╔═══ {title} ═══");
        Console.ResetColor();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        var size = (double)bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:F2} {sizes[order]}";
    }

    private static string GetPreview(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        if (content.Length <= maxLength) return content;
        return content[..maxLength] + "...";
    }
}
