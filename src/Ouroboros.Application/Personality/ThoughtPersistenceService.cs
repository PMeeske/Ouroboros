// <copyright file="ThoughtPersistenceService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using LangChainPipeline.Domain.Persistence;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Service for persisting and retrieving InnerThought objects.
/// Bridges the InnerDialogEngine with the thought storage layer.
/// </summary>
public class ThoughtPersistenceService
{
    private readonly IThoughtStore _store;
    private readonly string _sessionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThoughtPersistenceService"/> class.
    /// </summary>
    /// <param name="store">The thought store implementation.</param>
    /// <param name="sessionId">Session identifier for this conversation/agent instance.</param>
    public ThoughtPersistenceService(IThoughtStore store, string sessionId)
    {
        _store = store;
        _sessionId = sessionId;
    }

    /// <summary>
    /// Creates a service with file-based persistence.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="directory">Directory for thought files.</param>
    public static ThoughtPersistenceService CreateWithFilePersistence(string sessionId, string? directory = null)
    {
        return new ThoughtPersistenceService(new FileThoughtStore(directory), sessionId);
    }

    /// <summary>
    /// Creates a service with in-memory storage (for testing).
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    public static ThoughtPersistenceService CreateInMemory(string sessionId)
    {
        return new ThoughtPersistenceService(new InMemoryThoughtStore(), sessionId);
    }

    /// <summary>
    /// Saves a thought to persistent storage.
    /// </summary>
    /// <param name="thought">The thought to save.</param>
    /// <param name="topic">Optional topic/context for the thought.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveAsync(InnerThought thought, string? topic = null, CancellationToken ct = default)
    {
        var persisted = ToPersisted(thought, topic);
        await _store.SaveThoughtAsync(_sessionId, persisted, ct);
    }

    /// <summary>
    /// Saves multiple thoughts to persistent storage.
    /// </summary>
    /// <param name="thoughts">The thoughts to save.</param>
    /// <param name="topic">Optional topic/context.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveManyAsync(IEnumerable<InnerThought> thoughts, string? topic = null, CancellationToken ct = default)
    {
        var persisted = thoughts.Select(t => ToPersisted(t, topic));
        await _store.SaveThoughtsAsync(_sessionId, persisted, ct);
    }

    /// <summary>
    /// Retrieves all thoughts for this session.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of thoughts in chronological order.</returns>
    public async Task<IReadOnlyList<InnerThought>> GetAllAsync(CancellationToken ct = default)
    {
        var persisted = await _store.GetThoughtsAsync(_sessionId, ct);
        return persisted.Select(ToInnerThought).ToList();
    }

    /// <summary>
    /// Gets the most recent thoughts.
    /// </summary>
    /// <param name="count">Number of thoughts to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<InnerThought>> GetRecentAsync(int count = 10, CancellationToken ct = default)
    {
        var persisted = await _store.GetRecentThoughtsAsync(_sessionId, count, ct);
        return persisted.Select(ToInnerThought).ToList();
    }

    /// <summary>
    /// Gets thoughts of a specific type.
    /// </summary>
    /// <param name="type">The thought type to filter by.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<InnerThought>> GetByTypeAsync(
        InnerThoughtType type,
        int limit = 100,
        CancellationToken ct = default)
    {
        var persisted = await _store.GetThoughtsByTypeAsync(_sessionId, type.ToString(), limit, ct);
        return persisted.Select(ToInnerThought).ToList();
    }

    /// <summary>
    /// Searches thoughts by content.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="limit">Maximum results.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<InnerThought>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken ct = default)
    {
        var persisted = await _store.SearchThoughtsAsync(_sessionId, query, limit, ct);
        return persisted.Select(ToInnerThought).ToList();
    }

    /// <summary>
    /// Gets thoughts that form a chain from a parent thought.
    /// </summary>
    /// <param name="parentId">Parent thought ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IReadOnlyList<InnerThought>> GetChainAsync(Guid parentId, CancellationToken ct = default)
    {
        var persisted = await _store.GetChainedThoughtsAsync(_sessionId, parentId, ct);
        return persisted.Select(ToInnerThought).ToList();
    }

    /// <summary>
    /// Gets thought statistics for this session.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task<ThoughtStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        return _store.GetStatisticsAsync(_sessionId, ct);
    }

    /// <summary>
    /// Clears all thoughts for this session.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public Task ClearAsync(CancellationToken ct = default)
    {
        return _store.ClearSessionAsync(_sessionId, ct);
    }

    /// <summary>
    /// Gets a summary of the agent's thought patterns.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<string> GetThoughtSummaryAsync(CancellationToken ct = default)
    {
        var stats = await GetStatisticsAsync(ct);
        var recent = await GetRecentAsync(5, ct);

        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"=== Thought Summary for Session: {_sessionId} ===");
        summary.AppendLine($"Total Thoughts: {stats.TotalCount}");
        summary.AppendLine($"Average Confidence: {stats.AverageConfidence:P0}");
        summary.AppendLine($"Average Relevance: {stats.AverageRelevance:P0}");

        if (stats.EarliestThought.HasValue && stats.LatestThought.HasValue)
        {
            var duration = stats.LatestThought.Value - stats.EarliestThought.Value;
            summary.AppendLine($"Session Duration: {duration.TotalMinutes:F1} minutes");
        }

        summary.AppendLine();
        summary.AppendLine("Thought Distribution:");
        foreach (var (type, count) in stats.CountByType.OrderByDescending(kv => kv.Value).Take(5))
        {
            var percentage = stats.TotalCount > 0 ? (count * 100.0 / stats.TotalCount) : 0;
            summary.AppendLine($"  {type}: {count} ({percentage:F1}%)");
        }

        summary.AppendLine();
        summary.AppendLine("Recent Thoughts:");
        foreach (var thought in recent)
        {
            var preview = thought.Content.Length > 60 ? thought.Content[..60] + "..." : thought.Content;
            summary.AppendLine($"  [{thought.Type}] {preview}");
        }

        return summary.ToString();
    }

    /// <summary>
    /// Converts an InnerThought to a PersistedThought.
    /// </summary>
    private static PersistedThought ToPersisted(InnerThought thought, string? topic)
    {
        string? metadataJson = null;
        if (thought.Metadata != null && thought.Metadata.Count > 0)
        {
            try
            {
                metadataJson = JsonSerializer.Serialize(thought.Metadata);
            }
            catch
            {
                // Ignore serialization failures for metadata
            }
        }

        return new PersistedThought
        {
            Id = thought.Id,
            Type = thought.Type.ToString(),
            Content = thought.Content,
            Confidence = thought.Confidence,
            Relevance = thought.Relevance,
            Timestamp = thought.Timestamp,
            Origin = thought.Origin.ToString(),
            Priority = thought.Priority.ToString(),
            ParentThoughtId = thought.ParentThoughtId,
            TriggeringTrait = thought.TriggeringTrait,
            Topic = topic,
            Tags = thought.Tags,
            MetadataJson = metadataJson,
        };
    }

    /// <summary>
    /// Converts a PersistedThought back to an InnerThought.
    /// </summary>
    private static InnerThought ToInnerThought(PersistedThought persisted)
    {
        var type = Enum.TryParse<InnerThoughtType>(persisted.Type, out var t) ? t : InnerThoughtType.Observation;
        var origin = Enum.TryParse<ThoughtOrigin>(persisted.Origin, out var o) ? o : ThoughtOrigin.Reactive;
        var priority = Enum.TryParse<ThoughtPriority>(persisted.Priority, out var p) ? p : ThoughtPriority.Normal;

        Dictionary<string, object>? metadata = null;
        if (!string.IsNullOrEmpty(persisted.MetadataJson))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(persisted.MetadataJson);
            }
            catch
            {
                // Ignore deserialization failures
            }
        }

        return new InnerThought(
            persisted.Id,
            type,
            persisted.Content,
            persisted.Confidence,
            persisted.Relevance,
            persisted.TriggeringTrait,
            persisted.Timestamp,
            origin,
            priority,
            persisted.ParentThoughtId,
            persisted.Tags,
            metadata);
    }
}
