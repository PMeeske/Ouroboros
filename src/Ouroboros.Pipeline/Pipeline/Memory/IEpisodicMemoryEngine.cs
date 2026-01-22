// <copyright file="IEpisodicMemoryEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;
using Ouroboros.Domain.Reinforcement;
using Ouroboros.Pipeline.Verification;

/// <summary>
/// Interface for the episodic memory system.
/// Provides long-term memory storage with semantic retrieval and consolidation capabilities.
/// All operations return Result types for functional error handling without exceptions.
/// </summary>
public interface IEpisodicMemoryEngine
{
    /// <summary>
    /// Stores a new episode in the memory system with semantic embeddings.
    /// Serializes the complete PipelineBranch reasoning trace for replay capability.
    /// </summary>
    /// <param name="branch">The pipeline branch containing reasoning trace.</param>
    /// <param name="context">Execution context with goal and metadata.</param>
    /// <param name="result">The outcome of the execution.</param>
    /// <param name="metadata">Additional metadata to store with the episode.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Result containing the episode ID on success, or error message on failure.</returns>
    Task<Result<EpisodeId, string>> StoreEpisodeAsync(
        PipelineBranch branch,
        ExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves episodes similar to the query using semantic search.
    /// Performance target: &lt;100ms for 100K+ stored episodes.
    /// </summary>
    /// <param name="query">The query string to search for similar episodes.</param>
    /// <param name="topK">Maximum number of episodes to retrieve.</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0 to 1.0).</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Result containing list of similar episodes, or error message on failure.</returns>
    Task<Result<ImmutableList<Episode>, string>> RetrieveSimilarEpisodesAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7,
        CancellationToken ct = default);

    /// <summary>
    /// Consolidates old memories using the specified strategy.
    /// Helps manage memory growth and extract higher-level patterns.
    /// </summary>
    /// <param name="olderThan">Only consolidate memories older than this timespan.</param>
    /// <param name="strategy">The consolidation strategy to use.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Result indicating success (Unit) or error message on failure.</returns>
    Task<Result<Unit, string>> ConsolidateMemoriesAsync(
        TimeSpan olderThan,
        ConsolidationStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a plan informed by relevant past episodes.
    /// Uses experience-based learning to improve planning over time.
    /// Target: 20%+ improvement in planning success rate with retrieval.
    /// </summary>
    /// <param name="goal">The goal to plan for.</param>
    /// <param name="relevantEpisodes">Previously retrieved relevant episodes to inform planning.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Result containing the generated plan, or error message on failure.</returns>
    Task<Result<Plan, string>> PlanWithExperienceAsync(
        string goal,
        ImmutableList<Episode> relevantEpisodes,
        CancellationToken ct = default);
}
