// <copyright file="EpisodicMemoryExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Core.Memory;

/// <summary>
/// Kleisli extensions for integrating episodic memory with pipeline branches.
/// Enables memory-aware processing while maintaining mathematical purity.
/// </summary>
public static class EpisodicMemoryExtensions
{
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
                
                // Retrieve relevant episodes
                var episodesResult = await memory.RetrieveSimilarEpisodesAsync(
                    query, topK, minSimilarity, branch.Source.CancellationToken);
                
                if (episodesResult.IsFailure)
                {
                    _logger?.LogWarning("Failed to retrieve episodes: {Error}", episodesResult.Error);
                    // Continue without episodes
                    return await step(branch);
                }
                
                var episodes = episodesResult.Value;
                
                if (episodes.Any())
                {
                    // Add episodes as context to the branch
                    var contextBranch = branch.WithEvent(new MemoryRetrievalEvent(
                        Guid.NewGuid(), query, episodes.Count, DateTime.UtcNow));
                    
                    // Execute original step with episodic context
                    var result = await step(contextBranch);
                    
                    // Store the episode for future learning
                    await StoreExecutionEpisode(memory, branch, result, episodes, query);
                    
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
    /// Creates a KleisliResult step that retrieves relevant episodes with proper error handling.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> WithEpisodicRetrieval(
        this KleisliResult<PipelineBranch, PipelineBranch, string> step,
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
                
                // Retrieve relevant episodes
                var episodesResult = await memory.RetrieveSimilarEpisodesAsync(
                    query, topK, minSimilarity, branch.Source.CancellationToken);
                
                if (episodesResult.IsFailure)
                {
                    // Return failure if retrieval fails
                    return episodesResult.MapError(e => $"Episode retrieval failed: {e}");
                }
                
                var episodes = episodesResult.Value;
                
                // Add episodes as context to the branch
                var contextBranch = episodes.Any()
                    ? branch.WithEvent(new MemoryRetrievalEvent(
                        Guid.NewGuid(), query, episodes.Count, DateTime.UtcNow))
                    : branch;
                
                // Execute original step
                var result = await step(contextBranch);
                
                // Store episode if successful
                if (result.IsSuccess)
                {
                    await StoreExecutionEpisode(memory, branch, result.Value, episodes, query);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Episodic pipeline failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Creates a step that automatically consolidates memories after execution.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> WithMemoryConsolidation(
        this Step<PipelineBranch, PipelineBranch> step,
        IEpisodicMemoryEngine memory,
        ConsolidationStrategy strategy,
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
                    await memory.ConsolidateMemoriesAsync(
                        consolidationInterval, strategy, branch.Source.CancellationToken);
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
    /// Creates a step that plans using experience before executing the main step.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> WithExperientialPlanning(
        this Step<PipelineBranch, PipelineBranch> step,
        IEpisodicMemoryEngine memory,
        Func<PipelineBranch, string> goalExtractor)
    {
        return async branch =>
        {
            try
            {
                // Extract goal for planning
                var goal = goalExtractor(branch);
                
                // Retrieve relevant episodes
                var episodesResult = await memory.RetrieveSimilarEpisodesAsync(
                    goal, topK: 10, minSimilarity: 0.7, branch.Source.CancellationToken);
                
                if (episodesResult.IsSuccess && episodesResult.Value.Any())
                {
                    // Create plan using experience
                    var planResult = await memory.PlanWithExperienceAsync(
                        goal, episodesResult.Value, branch.Source.CancellationToken);
                    
                    if (planResult.IsSuccess)
                    {
                        // Add planning event to branch
                        var planningBranch = branch.WithEvent(new PlanningEvent(
                            Guid.NewGuid(), goal, planResult.Value.Confidence, DateTime.UtcNow));
                        
                        // Execute with planned approach
                        return await step(planningBranch);
                    }
                }
                
                // Execute without planning
                return await step(branch);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Experiential planning pipeline failed");
                throw;
            }
        };
    }

    /// <summary>
    /// Creates a Kleisli composition that wraps pipeline execution with full episodic memory lifecycle.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> WithEpisodicLifecycle(
        this Step<PipelineBranch, PipelineBranch> step,
        IEpisodicMemoryEngine memory,
        Func<PipelineBranch, string> queryExtractor,
        ConsolidationStrategy consolidationStrategy,
        TimeSpan consolidationInterval)
    {
        return step
            .WithEpisodicRetrieval(memory, queryExtractor)
            .WithExperientialPlanning(memory, queryExtractor)
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

    private static readonly Microsoft.Extensions.Logging.ILogger? _logger;

    private static async Task StoreExecutionEpisode(
        IEpisodicMemoryEngine memory,
        PipelineBranch originalBranch,
        PipelineBranch resultBranch,
        List<Episode> retrievedEpisodes,
        string query)
    {
        try
        {
            // Create execution context
            var context = new ExecutionContext(
                Environment: "pipeline_execution",
                Parameters: new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["retrieved_episode_count"] = retrievedEpisodes.Count,
                    ["branch_name"] = originalBranch.Name
                });

            // Create outcome
            var outcome = new Outcome(
                Success: true, // Assume success since we're storing after execution
                Output: "Execution completed",
                Duration: TimeSpan.Zero, // Would calculate actual duration in real implementation
                Errors: new List<string>());

            // Store episode
            await memory.StoreEpisodeAsync(
                resultBranch,
                context,
                outcome,
                new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["execution_timestamp"] = DateTime.UtcNow,
                    ["has_retrieved_context"] = retrievedEpisodes.Any()
                });
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
    DateTime Timestamp) : PipelineEvent;

/// <summary>
/// Event representing planning based on experience.
/// </summary>
public sealed record PlanningEvent(
    Guid Id,
    string Goal,
    double Confidence,
    DateTime Timestamp) : PipelineEvent;

/// <summary>
/// Base class for all pipeline events.
/// </summary>
public abstract record PipelineEvent;

/// <summary>
/// Extension methods for PipelineEvent hierarchy.
/// </summary>
public static class PipelineEventExtensions
{
    /// <summary>
    /// Gets all events of specified type.
    /// </summary>
    public static IEnumerable<TEvent> OfType<TEvent>(this IEnumerable<PipelineEvent> events)
        where TEvent : PipelineEvent
    {
        return events.Where(e => e is TEvent).Cast<TEvent>();
    }
}

/// <summary>
/// Event representing a reasoning step in the pipeline.
/// </summary>
public sealed record ReasoningStep(
    Guid Id,
    string Kind,
    object State,
    DateTime Timestamp,
    string Prompt,
    List<ToolExecution>? Tools) : PipelineEvent;