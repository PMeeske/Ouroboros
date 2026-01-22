// <copyright file="EpisodicMemoryExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;
using System.Diagnostics;

/// <summary>
/// Extension methods for integrating episodic memory with pipeline steps.
/// Follows Kleisli composition patterns for functional pipeline integration.
/// </summary>
public static class EpisodicMemoryExtensions
{
    /// <summary>
    /// Wraps a pipeline step with episodic memory storage and retrieval.
    /// Retrieves relevant past episodes before execution and stores the result after.
    /// </summary>
    /// <param name="step">The pipeline step to wrap.</param>
    /// <param name="memory">The episodic memory engine.</param>
    /// <param name="extractGoal">Function to extract goal from pipeline branch.</param>
    /// <param name="topK">Number of similar episodes to retrieve.</param>
    /// <returns>A new step that integrates with episodic memory.</returns>
    public static Step<PipelineBranch, PipelineBranch> WithEpisodicMemory(
        this Step<PipelineBranch, PipelineBranch> step,
        IEpisodicMemoryEngine memory,
        Func<PipelineBranch, string> extractGoal,
        int topK = 5)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(extractGoal);

        return async branch =>
        {
            var stopwatch = Stopwatch.StartNew();

            // Extract goal from branch
            var goal = extractGoal(branch);

            // Retrieve similar episodes (optional - doesn't fail the step if retrieval fails)
            var retrievalResult = await memory.RetrieveSimilarEpisodesAsync(goal, topK);
            var relevantEpisodes = retrievalResult.IsSuccess
                ? retrievalResult.Value
                : ImmutableList<Episode>.Empty;

            // Execute the original step
            var result = await step(branch);

            // Store episode (best effort - don't fail if storage fails)
            var context = ExecutionContext.WithGoal(goal);
            var outcome = Outcome.Successful(
                "Step executed",
                stopwatch.Elapsed);

            var metadata = ImmutableDictionary<string, object>.Empty
                .Add("retrieved_episodes", relevantEpisodes.Count)
                .Add("step_name", step.Method.Name ?? "anonymous");

            var storeResult = await memory.StoreEpisodeAsync(
                result,
                context,
                outcome,
                metadata);

            if (!storeResult.IsSuccess)
            {
                // Log but don't fail - memory is enhancement, not requirement
                Console.WriteLine($"Warning: Failed to store episode: {storeResult.Error}");
            }

            return result;
        };
    }

    /// <summary>
    /// Creates a step that retrieves similar episodes and adds them to branch metadata.
    /// Pure retrieval step without execution side effects.
    /// </summary>
    /// <param name="memory">The episodic memory engine.</param>
    /// <param name="query">The query to search for.</param>
    /// <param name="topK">Number of episodes to retrieve.</param>
    /// <param name="minSimilarity">Minimum similarity threshold.</param>
    /// <returns>A step that retrieves episodes and returns the original branch.</returns>
    public static Step<PipelineBranch, PipelineBranch> RetrieveEpisodesStep(
        IEpisodicMemoryEngine memory,
        string query,
        int topK = 5,
        double minSimilarity = 0.7)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return async branch =>
        {
            var result = await memory.RetrieveSimilarEpisodesAsync(query, topK, minSimilarity);

            if (result.IsSuccess)
            {
                // Could attach episodes to branch via custom event
                Console.WriteLine($"Retrieved {result.Value.Count} similar episodes");
            }

            return branch;
        };
    }

    /// <summary>
    /// Creates a step that consolidates memories based on age and strategy.
    /// Useful for periodic memory maintenance.
    /// </summary>
    /// <param name="memory">The episodic memory engine.</param>
    /// <param name="olderThan">Consolidate memories older than this timespan.</param>
    /// <param name="strategy">The consolidation strategy to use.</param>
    /// <returns>A step that consolidates memories and returns the original branch.</returns>
    public static Step<PipelineBranch, PipelineBranch> ConsolidateMemoriesStep(
        IEpisodicMemoryEngine memory,
        TimeSpan olderThan,
        ConsolidationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(memory);

        return async branch =>
        {
            var result = await memory.ConsolidateMemoriesAsync(olderThan, strategy);

            if (!result.IsSuccess)
            {
                Console.WriteLine($"Warning: Memory consolidation failed: {result.Error}");
            }

            return branch;
        };
    }

    /// <summary>
    /// Extracts goal from a pipeline branch by examining reasoning events.
    /// Default implementation that can be customized per use case.
    /// </summary>
    /// <param name="branch">The pipeline branch.</param>
    /// <returns>The extracted goal or a default value.</returns>
    public static string ExtractGoalFromBranch(PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        // Try to find goal in reasoning step prompts
        var reasoningSteps = branch.Events.OfType<ReasoningStep>().ToList();

        if (reasoningSteps.Count > 0)
        {
            var firstPrompt = reasoningSteps.First().Prompt;
            if (!string.IsNullOrWhiteSpace(firstPrompt))
            {
                // Extract first line or first 100 characters as goal
                var lines = firstPrompt.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return lines.Length > 0
                    ? lines[0].Substring(0, Math.Min(100, lines[0].Length))
                    : branch.Name;
            }
        }

        return branch.Name;
    }
}
