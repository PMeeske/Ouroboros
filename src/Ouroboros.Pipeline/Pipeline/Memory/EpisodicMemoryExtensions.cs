// <copyright file="EpisodicMemoryExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Memory;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Events;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Pipeline.Memory;

/// <summary>
/// Kleisli extensions for integrating episodic memory with pipeline branches.
/// Enables memory-aware processing while maintaining mathematical purity.
/// </summary>
public static class EpisodicMemoryExtensions
{
    private static readonly ILogger? _logger = null;

    /// <summary>
    /// Creates a step that retrieves relevant episodes before executing the main step.
    /// This enables experience-based reasoning by providing historical context.
    /// </summary>
    /// <param name="step">The main pipeline step to execute.</param>
    /// <param name="memory">The episodic memory engine.</param>
    /// <param name="queryExtractor">Function to extract search query from branch.</param>
    /// <param name="topK">Number of episodes to retrieve.</param>
    /// <param name="minSimilarity">Minimum similarity threshold.</param>
    /// <returns>A memory-aware Kleisli arrow.</returns>
    public static Step<PipelineBranch, PipelineBranch> WithEpisodicRetrieval(
        this Step<PipelineBranch, PipelineBranch> step,
        IEpisodicMemoryEngine memory,
        Func<PipelineBranch, string> queryExtractor,
        int topK = 5,
        double minSimilarity = 0.7)
    {
        return async branch =>
        {
            try
            {
                // Extract query for semantic search
                var query = queryExtractor(branch);

                // Retrieve relevant entries
                var entriesResult = await memory.RetrieveSimilarEntriesAsync(
                    query, topK, minSimilarity);

                if (entriesResult.IsFailure)
                {
                    _logger?.LogWarning("Failed to retrieve entries: {Error}", entriesResult.Error);
                    // Continue without entries
                    return await step(branch);
                }

                var entries = entriesResult.Value;

                if (entries.Any())
                {
                    // Add memory retrieval event to the branch
                    var contextBranch = branch.WithEvent(new MemoryRetrievalEvent(
                        Guid.NewGuid(), query, entries.Count, DateTime.UtcNow));

                    // Execute original step with episodic context
                    var result = await step(contextBranch);

                    // Store the execution as a new episode for future learning
                    await StoreExecutionEpisode(memory, branch, result, query);

                    return result;
                }
                else
                {
                    // Execute without episodes
                    return await step(branch);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Episodic retrieval pipeline failed");
                throw;
            }
        };
    }

    /// <summary>
    /// Creates a step that automatically consolidates memories after execution.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> WithMemoryConsolidation(
        this Step<PipelineBranch, PipelineBranch> step,
        IEpisodicMemoryEngine memory,
        MemoryConsolidationStrategy strategy,
        TimeSpan consolidationInterval)
    {
        return async branch =>
        {
            // Execute original step
            var result = await step(branch);

            // Perform consolidation in background if interval has passed
            _ = Task.Run(async () =>
            {
                try
                {
                    await memory.ConsolidateMemoriesAsync(consolidationInterval, strategy);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Background memory consolidation failed");
                }
            });

            return result;
        };
    }

    /// <summary>
    /// Creates a Kleisli composition that wraps pipeline execution with full episodic memory lifecycle.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> WithEpisodicLifecycle(
        this Step<PipelineBranch, PipelineBranch> step,
        IEpisodicMemoryEngine memory,
        Func<PipelineBranch, string> queryExtractor,
        MemoryConsolidationStrategy consolidationStrategy,
        TimeSpan consolidationInterval)
    {
        return step
            .WithEpisodicRetrieval(memory, queryExtractor)
            .WithMemoryConsolidation(memory, consolidationStrategy, consolidationInterval);
    }

    /// <summary>
    /// Extracts goal from pipeline branch using reasoning events.
    /// </summary>
    public static string ExtractGoalFromReasoning(this PipelineBranch branch)
    {
        var latestReasoning = branch.Events
            .OfType<ReasoningStep>()
            .LastOrDefault();

        return latestReasoning?.Prompt ?? "Unspecified goal";
    }

    /// <summary>
    /// Extracts goal from pipeline branch using branch name and events.
    /// </summary>
    public static string ExtractGoalFromBranchInfo(this PipelineBranch branch)
    {
        if (!string.IsNullOrEmpty(branch.Name) && branch.Name != "test")
        {
            return branch.Name;
        }

        return ExtractGoalFromReasoning(branch);
    }

    #region Private Implementation

    private static async Task StoreExecutionEpisode(
        IEpisodicMemoryEngine memory,
        PipelineBranch originalBranch,
        PipelineBranch resultBranch,
        string query)
    {
        try
        {
            // Extract lessons from the execution
            var lessons = resultBranch.Events
                .OfType<ReasoningStep>()
                .Select(r => $"Reasoning: {r.Prompt[..Math.Min(50, r.Prompt.Length)]}...")
                .ToList();

            // Create and store the entry
            var entry = EpisodicMemoryEntry.Create(
                goal: query,
                content: $"Pipeline execution on branch {originalBranch.Name}",
                successScore: 1.0, // Assume success since we completed execution
                lessonsLearned: lessons,
                metadata: new Dictionary<string, object>
                {
                    ["branch_name"] = originalBranch.Name,
                    ["event_count"] = resultBranch.Events.Count,
                    ["execution_timestamp"] = DateTime.UtcNow
                });

            await memory.StoreEntryAsync(entry);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to store execution episode");
        }
    }

    #endregion
}

/// <summary>
/// Event representing memory retrieval operation.
/// </summary>
public sealed record MemoryRetrievalEvent(
    Guid Id,
    string Query,
    int RetrievedCount,
    DateTime Timestamp) : PipelineEvent(Id, "MemoryRetrieval", Timestamp);

/// <summary>
/// Event representing planning based on experience.
/// </summary>
public sealed record PlanningEvent(
    Guid Id,
    string Goal,
    double Confidence,
    DateTime Timestamp) : PipelineEvent(Id, "Planning", Timestamp);
