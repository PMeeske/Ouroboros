#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Metrics Store Interface
// Defines contract for persistent storage of performance metrics
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for persistent storage and retrieval of performance metrics.
/// Enables long-term learning across orchestrator sessions.
/// </summary>
public interface IMetricsStore
{
    /// <summary>
    /// Stores or updates metrics for a resource.
    /// </summary>
    /// <param name="metrics">The metrics to store</param>
    /// <param name="ct">Cancellation token</param>
    Task StoreMetricsAsync(PerformanceMetrics metrics, CancellationToken ct = default);

    /// <summary>
    /// Retrieves metrics for a specific resource.
    /// </summary>
    /// <param name="resourceName">Name of the resource</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Metrics if found, null otherwise</returns>
    Task<PerformanceMetrics?> GetMetricsAsync(string resourceName, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all stored metrics.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary of all metrics by resource name</returns>
    Task<IReadOnlyDictionary<string, PerformanceMetrics>> GetAllMetricsAsync(CancellationToken ct = default);

    /// <summary>
    /// Removes metrics for a specific resource.
    /// </summary>
    /// <param name="resourceName">Name of the resource</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if removed, false if not found</returns>
    Task<bool> RemoveMetricsAsync(string resourceName, CancellationToken ct = default);

    /// <summary>
    /// Clears all stored metrics.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task ClearAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets statistics about the metrics store.
    /// </summary>
    /// <returns>Metrics store statistics</returns>
    Task<MetricsStoreStatistics> GetStatisticsAsync();
}

/// <summary>
/// Statistics about the metrics store.
/// </summary>
public sealed record MetricsStoreStatistics(
    int TotalResources,
    int TotalExecutions,
    double OverallSuccessRate,
    double AverageLatencyMs,
    DateTime? OldestMetric,
    DateTime? NewestMetric);

/// <summary>
/// In-memory metrics store for testing and development.
/// Does not persist across restarts.
/// </summary>
public sealed class InMemoryMetricsStore : IMetricsStore
{
    private readonly Dictionary<string, PerformanceMetrics> _metrics = new();
    private readonly object _lock = new();

    public Task StoreMetricsAsync(PerformanceMetrics metrics, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        lock (_lock)
        {
            _metrics[metrics.ResourceName] = metrics;
        }

        return Task.CompletedTask;
    }

    public Task<PerformanceMetrics?> GetMetricsAsync(string resourceName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_metrics.TryGetValue(resourceName, out var metrics) ? metrics : null);
        }
    }

    public Task<IReadOnlyDictionary<string, PerformanceMetrics>> GetAllMetricsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyDictionary<string, PerformanceMetrics>>(
                new Dictionary<string, PerformanceMetrics>(_metrics));
        }
    }

    public Task<bool> RemoveMetricsAsync(string resourceName, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_metrics.Remove(resourceName));
        }
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _metrics.Clear();
        }

        return Task.CompletedTask;
    }

    public Task<MetricsStoreStatistics> GetStatisticsAsync()
    {
        lock (_lock)
        {
            if (_metrics.Count == 0)
            {
                return Task.FromResult(new MetricsStoreStatistics(
                    TotalResources: 0,
                    TotalExecutions: 0,
                    OverallSuccessRate: 0,
                    AverageLatencyMs: 0,
                    OldestMetric: null,
                    NewestMetric: null));
            }

            var allMetrics = _metrics.Values.ToList();

            return Task.FromResult(new MetricsStoreStatistics(
                TotalResources: allMetrics.Count,
                TotalExecutions: allMetrics.Sum(m => m.ExecutionCount),
                OverallSuccessRate: allMetrics.Average(m => m.SuccessRate),
                AverageLatencyMs: allMetrics.Average(m => m.AverageLatencyMs),
                OldestMetric: allMetrics.Min(m => m.LastUsed),
                NewestMetric: allMetrics.Max(m => m.LastUsed)));
        }
    }
}
