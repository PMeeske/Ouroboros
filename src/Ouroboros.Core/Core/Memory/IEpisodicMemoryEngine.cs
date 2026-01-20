// <copyright file="IEpisodicMemoryEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Core.Memory;

/// <summary>
/// Interface for episodic memory engines that provide long-term memory with semantic retrieval.
/// Implementations combine experience-based learning with mathematical grounding in Kleisli composition.
/// </summary>
/// <remarks>
/// This interface defines the contract for episodic memory without depending on specific
/// pipeline or domain types. Implementations should be in higher-level assemblies
/// (e.g., Ouroboros.Application) that have access to pipeline and domain types.
/// </remarks>
public interface IEpisodicMemoryEngine
{
    /// <summary>
    /// Retrieves similar episodes using semantic similarity search.
    /// </summary>
    /// <param name="query">The semantic query for retrieval.</param>
    /// <param name="topK">Maximum number of episodes to retrieve.</param>
    /// <param name="minSimilarity">Minimum similarity threshold (0.0 to 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the list of similar episodes or an error message.</returns>
    Task<Result<IReadOnlyList<EpisodicMemoryEntry>, string>> RetrieveSimilarEntriesAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7,
        CancellationToken ct = default);

    /// <summary>
    /// Stores an episodic memory entry.
    /// </summary>
    /// <param name="entry">The memory entry to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the entry ID or an error message.</returns>
    Task<Result<Guid, string>> StoreEntryAsync(
        EpisodicMemoryEntry entry,
        CancellationToken ct = default);

    /// <summary>
    /// Consolidates memories according to the specified strategy.
    /// </summary>
    /// <param name="olderThan">Only consolidate memories older than this timespan.</param>
    /// <param name="strategy">The consolidation strategy to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or an error message.</returns>
    Task<Result<Unit, string>> ConsolidateMemoriesAsync(
        TimeSpan olderThan,
        MemoryConsolidationStrategy strategy,
        CancellationToken ct = default);
}

/// <summary>
/// A generic episodic memory entry that can be stored and retrieved.
/// </summary>
/// <param name="Id">Unique identifier for the entry.</param>
/// <param name="Timestamp">When the entry was created.</param>
/// <param name="Goal">The goal or intent of this episode.</param>
/// <param name="Content">The content/description of what happened.</param>
/// <param name="SuccessScore">How successful the episode was (0.0 to 1.0).</param>
/// <param name="LessonsLearned">Key takeaways from this episode.</param>
/// <param name="Metadata">Additional metadata.</param>
/// <param name="Embedding">Vector embedding for semantic search.</param>
public sealed record EpisodicMemoryEntry(
    Guid Id,
    DateTime Timestamp,
    string Goal,
    string Content,
    double SuccessScore,
    IReadOnlyList<string> LessonsLearned,
    IReadOnlyDictionary<string, object>? Metadata = null,
    float[]? Embedding = null)
{
    /// <summary>
    /// Creates a new entry with a generated ID.
    /// </summary>
    public static EpisodicMemoryEntry Create(
        string goal,
        string content,
        double successScore,
        IReadOnlyList<string>? lessonsLearned = null,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            goal,
            content,
            successScore,
            lessonsLearned ?? Array.Empty<string>(),
            metadata);
}

/// <summary>
/// Strategies for consolidating episodic memories.
/// </summary>
public enum MemoryConsolidationStrategy
{
    /// <summary>
    /// Compress similar episodes into summaries.
    /// </summary>
    Compress,

    /// <summary>
    /// Extract abstract patterns from concrete episodes.
    /// </summary>
    Abstract,

    /// <summary>
    /// Remove low-value or redundant memories.
    /// </summary>
    Prune,

    /// <summary>
    /// Organize memories into hierarchical structures.
    /// </summary>
    Hierarchical
}
