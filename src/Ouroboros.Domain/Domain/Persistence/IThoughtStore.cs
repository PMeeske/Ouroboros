// <copyright file="IThoughtStore.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Persistence;

/// <summary>
/// Interface for persisting and retrieving agent thoughts.
/// Enables thought continuity across sessions and introspective analysis.
/// </summary>
public interface IThoughtStore
{
    /// <summary>
    /// Saves a thought to the store.
    /// </summary>
    /// <param name="sessionId">The session identifier (conversation/agent instance).</param>
    /// <param name="thought">The thought to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveThoughtAsync(string sessionId, PersistedThought thought, CancellationToken ct = default);

    /// <summary>
    /// Saves multiple thoughts in a batch.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="thoughts">The thoughts to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveThoughtsAsync(string sessionId, IEnumerable<PersistedThought> thoughts, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all thoughts for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of thoughts in chronological order.</returns>
    Task<IReadOnlyList<PersistedThought>> GetThoughtsAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves thoughts within a time range.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="from">Start time (inclusive).</param>
    /// <param name="to">End time (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PersistedThought>> GetThoughtsInRangeAsync(
        string sessionId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves thoughts by type.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="thoughtType">The type of thought to retrieve.</param>
    /// <param name="limit">Maximum number of thoughts to return.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PersistedThought>> GetThoughtsByTypeAsync(
        string sessionId,
        string thoughtType,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Searches thoughts by content similarity.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="query">The search query.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PersistedThought>> SearchThoughtsAsync(
        string sessionId,
        string query,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent thoughts.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="count">Number of recent thoughts to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PersistedThought>> GetRecentThoughtsAsync(
        string sessionId,
        int count = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Gets thoughts that are related (chained) to a parent thought.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="parentThoughtId">The parent thought ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PersistedThought>> GetChainedThoughtsAsync(
        string sessionId,
        Guid parentThoughtId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes all thoughts for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ClearSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Gets statistics about thoughts in a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ThoughtStatistics> GetStatisticsAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Lists all session IDs that have stored thoughts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<string>> ListSessionsAsync(CancellationToken ct = default);
}

/// <summary>
/// A thought record suitable for persistence.
/// </summary>
public sealed record PersistedThought
{
    /// <summary>Unique identifier for the thought.</summary>
    public required Guid Id { get; init; }

    /// <summary>Type of thought (Observation, Analytical, Curiosity, etc.).</summary>
    public required string Type { get; init; }

    /// <summary>The content/text of the thought.</summary>
    public required string Content { get; init; }

    /// <summary>Confidence level (0-1).</summary>
    public double Confidence { get; init; }

    /// <summary>Relevance to current context (0-1).</summary>
    public double Relevance { get; init; }

    /// <summary>When the thought occurred.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Origin of the thought (Reactive, Autonomous, Chained).</summary>
    public string Origin { get; init; } = "Reactive";

    /// <summary>Priority level.</summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>Parent thought ID for chained thoughts.</summary>
    public Guid? ParentThoughtId { get; init; }

    /// <summary>Personality trait that triggered this thought.</summary>
    public string? TriggeringTrait { get; init; }

    /// <summary>Associated topic/context.</summary>
    public string? Topic { get; init; }

    /// <summary>Tags for categorization.</summary>
    public string[]? Tags { get; init; }

    /// <summary>Additional metadata as JSON.</summary>
    public string? MetadataJson { get; init; }
}

/// <summary>
/// Statistics about thoughts in a session.
/// </summary>
public sealed record ThoughtStatistics
{
    /// <summary>Total number of thoughts.</summary>
    public int TotalCount { get; init; }

    /// <summary>Count by thought type.</summary>
    public Dictionary<string, int> CountByType { get; init; } = new();

    /// <summary>Count by origin.</summary>
    public Dictionary<string, int> CountByOrigin { get; init; } = new();

    /// <summary>Average confidence.</summary>
    public double AverageConfidence { get; init; }

    /// <summary>Average relevance.</summary>
    public double AverageRelevance { get; init; }

    /// <summary>Time range of thoughts.</summary>
    public DateTime? EarliestThought { get; init; }

    /// <summary>Time range of thoughts.</summary>
    public DateTime? LatestThought { get; init; }

    /// <summary>Number of thought chains.</summary>
    public int ChainCount { get; init; }
}
