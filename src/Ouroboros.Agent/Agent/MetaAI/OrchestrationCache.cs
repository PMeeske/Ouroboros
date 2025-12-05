#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Orchestration Cache
// Caching layer for orchestration decisions to improve performance
// ==========================================================

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using LangChainPipeline.Core.Monads;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Interface for orchestration decision caching.
/// </summary>
public interface IOrchestrationCache
{
    /// <summary>
    /// Gets a cached orchestration decision for the given prompt hash.
    /// </summary>
    /// <param name="promptHash">Hash of the prompt.</param>
    /// <returns>Cached decision if found, None otherwise.</returns>
    Task<Option<OrchestratorDecision>> GetCachedDecisionAsync(string promptHash);

    /// <summary>
    /// Caches an orchestration decision.
    /// </summary>
    /// <param name="promptHash">Hash of the prompt.</param>
    /// <param name="decision">The decision to cache.</param>
    /// <param name="ttl">Time to live for the cache entry.</param>
    Task CacheDecisionAsync(string promptHash, OrchestratorDecision decision, TimeSpan ttl);

    /// <summary>
    /// Invalidates a specific cache entry.
    /// </summary>
    /// <param name="promptHash">Hash of the prompt to invalidate.</param>
    Task InvalidateAsync(string promptHash);

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// In-memory implementation of orchestration cache.
/// Thread-safe and suitable for single-instance deployments.
/// </summary>
public sealed class InMemoryOrchestrationCache : IOrchestrationCache, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _cleanupInterval;
    private readonly int _maxEntries;
    private long _hits;
    private long _misses;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the cache with default settings.
    /// </summary>
    public InMemoryOrchestrationCache()
        : this(maxEntries: 10000, cleanupIntervalSeconds: 60)
    {
    }

    /// <summary>
    /// Initializes a new instance of the cache with custom settings.
    /// </summary>
    /// <param name="maxEntries">Maximum number of cache entries.</param>
    /// <param name="cleanupIntervalSeconds">Interval for cleanup operations.</param>
    public InMemoryOrchestrationCache(int maxEntries, int cleanupIntervalSeconds = 60)
    {
        _maxEntries = maxEntries;
        _cleanupInterval = TimeSpan.FromSeconds(cleanupIntervalSeconds);
        _cleanupTimer = new Timer(
            _ => CleanupExpiredEntries(),
            null,
            _cleanupInterval,
            _cleanupInterval);
    }

    /// <inheritdoc/>
    public Task<Option<OrchestratorDecision>> GetCachedDecisionAsync(string promptHash)
    {
        if (string.IsNullOrEmpty(promptHash))
        {
            Interlocked.Increment(ref _misses);
            return Task.FromResult(Option<OrchestratorDecision>.None());
        }

        if (_cache.TryGetValue(promptHash, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                entry.IncrementAccessCount();
                Interlocked.Increment(ref _hits);
                return Task.FromResult(Option<OrchestratorDecision>.Some(entry.Decision));
            }
            else
            {
                // Entry expired, remove it
                _cache.TryRemove(promptHash, out _);
            }
        }

        Interlocked.Increment(ref _misses);
        return Task.FromResult(Option<OrchestratorDecision>.None());
    }

    /// <inheritdoc/>
    public Task CacheDecisionAsync(string promptHash, OrchestratorDecision decision, TimeSpan ttl)
    {
        if (string.IsNullOrEmpty(promptHash) || decision == null)
        {
            return Task.CompletedTask;
        }

        // Check if we need to evict entries
        if (_cache.Count >= _maxEntries)
        {
            EvictLeastRecentlyUsed();
        }

        var entry = new CacheEntry(decision, DateTime.UtcNow.Add(ttl));
        _cache.AddOrUpdate(promptHash, entry, (_, _) => entry);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task InvalidateAsync(string promptHash)
    {
        if (!string.IsNullOrEmpty(promptHash))
        {
            _cache.TryRemove(promptHash, out _);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public CacheStatistics GetStatistics()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        var total = hits + misses;

        return new CacheStatistics(
            TotalEntries: _cache.Count,
            MaxEntries: _maxEntries,
            HitCount: hits,
            MissCount: misses,
            HitRate: total > 0 ? (double)hits / total : 0,
            MemoryEstimateBytes: EstimateMemoryUsage());
    }

    /// <summary>
    /// Generates a hash for a prompt.
    /// </summary>
    public static string GeneratePromptHash(string prompt, Dictionary<string, object>? context = null)
    {
        var sb = new StringBuilder(prompt);

        if (context != null)
        {
            foreach (var kvp in context.OrderBy(k => k.Key))
            {
                sb.Append($"|{kvp.Key}:{kvp.Value}");
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            _cache.Clear();
            _disposed = true;
        }
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        // Evict 10% of entries based on last access time
        var toEvict = _cache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(_maxEntries / 10)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toEvict)
        {
            _cache.TryRemove(key, out _);
        }
    }

    private long EstimateMemoryUsage()
    {
        // Rough estimate: ~500 bytes per entry on average
        return _cache.Count * 500L;
    }

    private sealed class CacheEntry
    {
        private int _accessCount;

        public OrchestratorDecision Decision { get; }
        public DateTime ExpiresAt { get; }
        public DateTime LastAccessedAt { get; private set; }
        public int AccessCount => _accessCount;

        public CacheEntry(OrchestratorDecision decision, DateTime expiresAt)
        {
            Decision = decision;
            ExpiresAt = expiresAt;
            LastAccessedAt = DateTime.UtcNow;
            _accessCount = 1;
        }

        public void IncrementAccessCount()
        {
            LastAccessedAt = DateTime.UtcNow;
            Interlocked.Increment(ref _accessCount);
        }
    }
}

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public sealed record CacheStatistics(
    int TotalEntries,
    int MaxEntries,
    long HitCount,
    long MissCount,
    double HitRate,
    long MemoryEstimateBytes)
{
    /// <summary>Percentage of cache capacity used.</summary>
    public double UtilizationPercent => MaxEntries > 0 ? (double)TotalEntries / MaxEntries * 100 : 0;

    /// <summary>Whether the cache is healthy (hit rate > 50% or still warming up).</summary>
    public bool IsHealthy => HitRate > 0.5 || (HitCount + MissCount) < 100;
}

/// <summary>
/// Extension methods for orchestrator caching.
/// </summary>
public static class OrchestrationCacheExtensions
{
    /// <summary>
    /// Default TTL for cached decisions (5 minutes).
    /// </summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Wraps an orchestrator with caching capability.
    /// </summary>
    public static CachingModelOrchestrator WithCaching(
        this IModelOrchestrator orchestrator,
        IOrchestrationCache cache,
        TimeSpan? ttl = null)
    {
        return new CachingModelOrchestrator(orchestrator, cache, ttl ?? DefaultTtl);
    }
}

/// <summary>
/// Decorator that adds caching to an existing orchestrator.
/// </summary>
public sealed class CachingModelOrchestrator : IModelOrchestrator
{
    private readonly IModelOrchestrator _inner;
    private readonly IOrchestrationCache _cache;
    private readonly TimeSpan _ttl;

    /// <summary>
    /// Creates a new caching orchestrator wrapper.
    /// </summary>
    public CachingModelOrchestrator(
        IModelOrchestrator inner,
        IOrchestrationCache cache,
        TimeSpan ttl)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _ttl = ttl;
    }

    /// <inheritdoc/>
    public async Task<Result<OrchestratorDecision, string>> SelectModelAsync(
        string prompt,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        // Generate cache key
        var hash = InMemoryOrchestrationCache.GeneratePromptHash(prompt, context);

        // Check cache first
        var cached = await _cache.GetCachedDecisionAsync(hash);
        if (cached.HasValue)
        {
            return Result<OrchestratorDecision, string>.Success(cached.Value!);
        }

        // Cache miss - call inner orchestrator
        var result = await _inner.SelectModelAsync(prompt, context, ct);

        // Cache successful results
        if (result.IsSuccess)
        {
            await _cache.CacheDecisionAsync(hash, result.Value, _ttl);
        }

        return result;
    }

    /// <inheritdoc/>
    public UseCase ClassifyUseCase(string prompt) => _inner.ClassifyUseCase(prompt);

    /// <inheritdoc/>
    public void RegisterModel(ModelCapability capability) => _inner.RegisterModel(capability);

    /// <inheritdoc/>
    public void RecordMetric(string resourceName, double latencyMs, bool success) =>
        _inner.RecordMetric(resourceName, latencyMs, success);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics() => _inner.GetMetrics();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetCacheStatistics() => _cache.GetStatistics();

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public Task ClearCacheAsync() => _cache.ClearAsync();
}
