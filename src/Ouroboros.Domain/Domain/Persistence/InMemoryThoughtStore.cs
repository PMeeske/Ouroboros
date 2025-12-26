// <copyright file="InMemoryThoughtStore.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Domain.Persistence;

/// <summary>
/// In-memory implementation of thought storage for development and testing.
/// </summary>
public class InMemoryThoughtStore : IThoughtStore
{
    private readonly ConcurrentDictionary<string, List<PersistedThought>> _sessions = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public Task SaveThoughtAsync(string sessionId, PersistedThought thought, CancellationToken ct = default)
    {
        return SaveThoughtsAsync(sessionId, new[] { thought }, ct);
    }

    /// <inheritdoc/>
    public Task SaveThoughtsAsync(string sessionId, IEnumerable<PersistedThought> thoughts, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var list = _sessions.GetOrAdd(sessionId, _ => new List<PersistedThought>());
            list.AddRange(thoughts);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PersistedThought>> GetThoughtsAsync(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var thoughts))
        {
            return Task.FromResult<IReadOnlyList<PersistedThought>>(thoughts.OrderBy(t => t.Timestamp).ToList());
        }
        return Task.FromResult<IReadOnlyList<PersistedThought>>(Array.Empty<PersistedThought>());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetThoughtsInRangeAsync(
        string sessionId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var thoughts = await GetThoughtsAsync(sessionId, ct);
        return thoughts.Where(t => t.Timestamp >= from && t.Timestamp <= to).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetThoughtsByTypeAsync(
        string sessionId,
        string thoughtType,
        int limit = 100,
        CancellationToken ct = default)
    {
        var thoughts = await GetThoughtsAsync(sessionId, ct);
        return thoughts
            .Where(t => t.Type.Equals(thoughtType, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> SearchThoughtsAsync(
        string sessionId,
        string query,
        int limit = 20,
        CancellationToken ct = default)
    {
        var thoughts = await GetThoughtsAsync(sessionId, ct);
        var queryLower = query.ToLowerInvariant();

        return thoughts
            .Where(t => t.Content.ToLowerInvariant().Contains(queryLower))
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetRecentThoughtsAsync(
        string sessionId,
        int count = 10,
        CancellationToken ct = default)
    {
        var thoughts = await GetThoughtsAsync(sessionId, ct);
        return thoughts
            .OrderByDescending(t => t.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetChainedThoughtsAsync(
        string sessionId,
        Guid parentThoughtId,
        CancellationToken ct = default)
    {
        var thoughts = await GetThoughtsAsync(sessionId, ct);
        return thoughts.Where(t => t.ParentThoughtId == parentThoughtId).ToList();
    }

    /// <inheritdoc/>
    public Task ClearSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ThoughtStatistics> GetStatisticsAsync(string sessionId, CancellationToken ct = default)
    {
        var thoughts = await GetThoughtsAsync(sessionId, ct);

        if (!thoughts.Any())
        {
            return new ThoughtStatistics { TotalCount = 0 };
        }

        return new ThoughtStatistics
        {
            TotalCount = thoughts.Count,
            CountByType = thoughts.GroupBy(t => t.Type).ToDictionary(g => g.Key, g => g.Count()),
            CountByOrigin = thoughts.GroupBy(t => t.Origin).ToDictionary(g => g.Key, g => g.Count()),
            AverageConfidence = thoughts.Average(t => t.Confidence),
            AverageRelevance = thoughts.Average(t => t.Relevance),
            EarliestThought = thoughts.Min(t => t.Timestamp),
            LatestThought = thoughts.Max(t => t.Timestamp),
            ChainCount = thoughts.Count(t => t.ParentThoughtId.HasValue),
        };
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListSessionsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_sessions.Keys.ToList());
    }
}
