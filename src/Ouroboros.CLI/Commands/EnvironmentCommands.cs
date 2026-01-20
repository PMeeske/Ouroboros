// <copyright file="EnvironmentCommands.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Reinforcement;
using Ouroboros.Options;
using Ouroboros.Application.Services.Reinforcement;
using Ouroboros.Examples.Environments;
using Unit = Ouroboros.Core.Monads.Unit;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// CLI commands for environment interaction and RL training.
/// </summary>
public static class EnvironmentCommands
{
    /// <summary>
    /// Executes environment commands based on options.
    /// </summary>
    public static async Task RunEnvironmentCommandAsync(EnvironmentOptions options)
    {
        var command = options.Command?.ToLowerInvariant();

        switch (command)
        {
            case "step":
                await RunSingleStepAsync(options);
                break;
            case "run":
                await RunEpisodesAsync(options);
                break;
            case "replay":
                await ReplayEpisodeAsync(options);
                break;
            default:
                Console.WriteLine($"Unknown command: {options.Command}");
                Console.WriteLine("Available commands: step, run, replay");
                break;
        }
    }

    private static async Task RunSingleStepAsync(EnvironmentOptions options)
    {
        Console.WriteLine("=== Environment Single Step ===\n");

        var (environment, policy) = CreateEnvironmentAndPolicy(options);

        // Get initial state
        var stateResult = await environment.GetStateAsync();
        if (stateResult.IsFailure)
        {
            Console.WriteLine($"Error getting state: {stateResult.Error}");
            return;
        }

        Console.WriteLine($"Current State: {FormatState(stateResult.Value)}");

        // Get available actions
        var actionsResult = await environment.GetAvailableActionsAsync();
        if (actionsResult.IsFailure)
        {
            Console.WriteLine($"Error getting actions: {actionsResult.Error}");
            return;
        }

        Console.WriteLine($"Available Actions: {string.Join(", ", actionsResult.Value.Select(a => a.ActionType))}");

        // Select action
        var actionResult = await policy.SelectActionAsync(stateResult.Value, actionsResult.Value);
        if (actionResult.IsFailure)
        {
            Console.WriteLine($"Error selecting action: {actionResult.Error}");
            return;
        }

        Console.WriteLine($"Selected Action: {actionResult.Value.ActionType}");

        // Execute action
        var observationResult = await environment.ExecuteActionAsync(actionResult.Value);
        if (observationResult.IsFailure)
        {
            Console.WriteLine($"Error executing action: {observationResult.Error}");
            return;
        }

        var observation = observationResult.Value;
        Console.WriteLine($"\nObservation:");
        Console.WriteLine($"  New State: {FormatState(observation.State)}");
        Console.WriteLine($"  Reward: {observation.Reward:F2}");
        Console.WriteLine($"  Terminal: {observation.IsTerminal}");

        if (observation.Info != null && observation.Info.Count > 0)
        {
            Console.WriteLine($"  Info: {string.Join(", ", observation.Info.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
    }

    private static async Task RunEpisodesAsync(EnvironmentOptions options)
    {
        Console.WriteLine("=== Running Episodes ===\n");
        Console.WriteLine($"Environment: {options.Environment}");
        Console.WriteLine($"Policy: {options.Policy}");
        Console.WriteLine($"Episodes: {options.Episodes}");
        Console.WriteLine($"Max Steps: {options.MaxSteps}\n");

        var (environment, policy) = CreateEnvironmentAndPolicy(options);
        var runner = new EpisodeRunner(environment, policy, options.Environment);

        var episodes = new List<Episode>();
        var metrics = new List<EpisodeMetrics>();

        for (var i = 0; i < options.Episodes; i++)
        {
            var result = await runner.RunEpisodeAsync(options.MaxSteps);

            if (result.IsFailure)
            {
                Console.WriteLine($"Episode {i + 1} failed: {result.Error}");
                continue;
            }

            var episode = result.Value;
            episodes.Add(episode);

            var episodeMetrics = EpisodeMetrics.FromEpisode(episode);
            metrics.Add(episodeMetrics);

            Console.WriteLine($"Episode {i + 1}/{options.Episodes}:");
            Console.WriteLine($"  Steps: {episode.StepCount}");
            Console.WriteLine($"  Reward: {episode.TotalReward:F2}");
            Console.WriteLine($"  Success: {episode.Success}");
            Console.WriteLine($"  Duration: {episode.Duration?.TotalSeconds:F2}s");

            if (options.Verbose)
            {
                Console.WriteLine($"  Steps detail:");
                foreach (var step in episode.Steps)
                {
                    Console.WriteLine($"    Step {step.StepNumber}: {step.Action.ActionType} → Reward: {step.Observation.Reward:F2}");
                }
            }

            Console.WriteLine();
        }

        // Print summary statistics
        PrintSummaryStatistics(metrics);

        // Save to file if requested
        if (!string.IsNullOrWhiteSpace(options.OutputFile))
        {
            await SaveEpisodesToFileAsync(episodes, options.OutputFile);
            Console.WriteLine($"\nEpisodes saved to {options.OutputFile}");
        }
    }

    private static async Task ReplayEpisodeAsync(EnvironmentOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputFile))
        {
            Console.WriteLine("Error: Input file required for replay. Use -i or --input option.");
            return;
        }

        Console.WriteLine("=== Replaying Episode ===\n");

        try
        {
            var json = await File.ReadAllTextAsync(options.InputFile);
            var episodes = JsonSerializer.Deserialize<List<Episode>>(json);

            if (episodes == null || episodes.Count == 0)
            {
                Console.WriteLine("No episodes found in file.");
                return;
            }

            foreach (var episode in episodes)
            {
                Console.WriteLine($"Episode {episode.Id}:");
                Console.WriteLine($"  Environment: {episode.EnvironmentName}");
                Console.WriteLine($"  Steps: {episode.StepCount}");
                Console.WriteLine($"  Total Reward: {episode.TotalReward:F2}");
                Console.WriteLine($"  Success: {episode.Success}");
                Console.WriteLine($"  Duration: {episode.Duration?.TotalSeconds:F2}s");
                Console.WriteLine();

                foreach (var step in episode.Steps)
                {
                    Console.WriteLine($"  Step {step.StepNumber}:");
                    Console.WriteLine($"    State: {FormatState(step.State)}");
                    Console.WriteLine($"    Action: {step.Action.ActionType}");
                    Console.WriteLine($"    Reward: {step.Observation.Reward:F2}");
                    Console.WriteLine($"    Next State: {FormatState(step.Observation.State)}");
                    Console.WriteLine($"    Terminal: {step.Observation.IsTerminal}");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error replaying episode: {ex.Message}");
        }
    }

    private static (IEnvironmentActor Environment, IPolicy Policy) CreateEnvironmentAndPolicy(
        EnvironmentOptions options)
    {
        // Create environment
        IEnvironmentActor environment = options.Environment.ToLowerInvariant() switch
        {
            "gridworld" => new GridWorldEnvironment(options.Width, options.Height),
            _ => throw new ArgumentException($"Unknown environment: {options.Environment}")
        };

        // Create policy
        IPolicy policy = options.Policy.ToLowerInvariant() switch
        {
            "epsilon-greedy" => new EpsilonGreedyPolicy(options.Epsilon, options.Seed),
            "bandit" => new BanditPolicy(),
            "random" => new RandomPolicy(options.Seed),
            _ => throw new ArgumentException($"Unknown policy: {options.Policy}")
        };

        return (environment, policy);
    }

    private static string FormatState(EnvironmentState state)
    {
        var parts = state.StateData.Select(kv => $"{kv.Key}={kv.Value}");
        return $"[{string.Join(", ", parts)}]";
    }

    private static void PrintSummaryStatistics(List<EpisodeMetrics> metrics)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        Console.WriteLine("=== Summary Statistics ===");
        Console.WriteLine($"Total Episodes: {metrics.Count}");
        Console.WriteLine($"Success Rate: {metrics.Count(m => m.Success) / (double)metrics.Count:P1}");
        Console.WriteLine($"Average Reward: {metrics.Average(m => m.TotalReward):F2}");
        Console.WriteLine($"Average Steps: {metrics.Average(m => m.StepCount):F2}");
        Console.WriteLine($"Average Duration: {TimeSpan.FromTicks((long)metrics.Average(m => m.Duration.Ticks)).TotalSeconds:F2}s");

        var bestEpisode = metrics.OrderByDescending(m => m.TotalReward).First();
        Console.WriteLine($"\nBest Episode:");
        Console.WriteLine($"  ID: {bestEpisode.EpisodeId}");
        Console.WriteLine($"  Reward: {bestEpisode.TotalReward:F2}");
        Console.WriteLine($"  Steps: {bestEpisode.StepCount}");
    }

    private static async Task SaveEpisodesToFileAsync(List<Episode> episodes, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        var json = JsonSerializer.Serialize(episodes, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Random policy for baseline comparison.
    /// </summary>
    private sealed class RandomPolicy : IPolicy
    {
        private readonly Random random;

        public RandomPolicy(int? seed = null)
        {
            this.random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public ValueTask<Ouroboros.Core.Monads.Result<EnvironmentAction>> SelectActionAsync(
            EnvironmentState state,
            IReadOnlyList<EnvironmentAction> availableActions,
            CancellationToken cancellationToken = default)
        {
            if (availableActions == null || availableActions.Count == 0)
            {
                return ValueTask.FromResult(
                    Ouroboros.Core.Monads.Result<EnvironmentAction>.Failure("No available actions"));
            }

            var index = this.random.Next(availableActions.Count);
            return ValueTask.FromResult(
                Ouroboros.Core.Monads.Result<EnvironmentAction>.Success(availableActions[index]));
        }

        public ValueTask<Ouroboros.Core.Monads.Result<Unit>> UpdateAsync(
            EnvironmentState state,
            EnvironmentAction action,
            Observation observation,
            CancellationToken cancellationToken = default)
        {
            // Random policy doesn't learn
            return ValueTask.FromResult(Ouroboros.Core.Monads.Result<Unit>.Success(Unit.Value));
        }
    }
}
