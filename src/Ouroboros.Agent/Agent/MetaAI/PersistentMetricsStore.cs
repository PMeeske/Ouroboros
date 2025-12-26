#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Persistent Metrics Store Implementation
// JSON file-based persistence for performance metrics
// Enables long-term learning across orchestrator restarts
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for persistent metrics storage.
/// </summary>
public sealed record PersistentMetricsConfig(
    string StoragePath = "metrics",
    string FileName = "performance_metrics.json",
    bool AutoSave = true,
    TimeSpan AutoSaveInterval = default,
    int MaxMetricsAge = 90);

/// <summary>
/// Persistent metrics store that saves performance data to disk.
/// Enables long-term learning by preserving metrics across restarts.
/// Thread-safe implementation with automatic periodic saving.
/// </summary>
public sealed class PersistentMetricsStore : IMetricsStore, IDisposable
{
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();
    private readonly PersistentMetricsConfig _config;
    private readonly string _filePath;
    private readonly Timer? _autoSaveTimer;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _disposed;
    private bool _isDirty;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public PersistentMetricsStore(PersistentMetricsConfig? config = null)
    {
        _config = config ?? new PersistentMetricsConfig(
            AutoSaveInterval: TimeSpan.FromMinutes(5));

        // Ensure storage directory exists
        string storagePath = Path.GetFullPath(_config.StoragePath);
        Directory.CreateDirectory(storagePath);

        _filePath = Path.Combine(storagePath, _config.FileName);

        // Load existing metrics
        LoadMetricsSync();

        // Setup auto-save timer if enabled
        if (_config.AutoSave && _config.AutoSaveInterval > TimeSpan.Zero)
        {
            _autoSaveTimer = new Timer(
                async _ => await SaveIfDirtyAsync(),
                null,
                _config.AutoSaveInterval,
                _config.AutoSaveInterval);
        }
    }

    /// <summary>
    /// Stores or updates metrics for a resource.
    /// </summary>
    public async Task StoreMetricsAsync(PerformanceMetrics metrics, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ThrowIfDisposed();

        _metrics[metrics.ResourceName] = metrics;
        _isDirty = true;

        // Immediate save if auto-save disabled
        if (!_config.AutoSave)
        {
            await SaveMetricsAsync(ct);
        }
    }

    /// <summary>
    /// Retrieves metrics for a specific resource.
    /// </summary>
    public Task<PerformanceMetrics?> GetMetricsAsync(string resourceName, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return Task.FromResult(_metrics.TryGetValue(resourceName, out var metrics) ? metrics : null);
    }

    /// <summary>
    /// Retrieves all stored metrics.
    /// </summary>
    public Task<IReadOnlyDictionary<string, PerformanceMetrics>> GetAllMetricsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return Task.FromResult<IReadOnlyDictionary<string, PerformanceMetrics>>(
            new Dictionary<string, PerformanceMetrics>(_metrics));
    }

    /// <summary>
    /// Removes metrics for a specific resource.
    /// </summary>
    public async Task<bool> RemoveMetricsAsync(string resourceName, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (_metrics.TryRemove(resourceName, out _))
        {
            _isDirty = true;

            if (!_config.AutoSave)
            {
                await SaveMetricsAsync(ct);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears all stored metrics.
    /// </summary>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        _metrics.Clear();
        _isDirty = true;

        await SaveMetricsAsync(ct);
    }

    /// <summary>
    /// Gets statistics about the metrics store.
    /// </summary>
    public Task<MetricsStoreStatistics> GetStatisticsAsync()
    {
        ThrowIfDisposed();

        if (_metrics.IsEmpty)
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

    /// <summary>
    /// Forces an immediate save of metrics to disk.
    /// </summary>
    public async Task SaveMetricsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        await _saveLock.WaitAsync(ct);
        try
        {
            var snapshot = new Dictionary<string, PerformanceMetrics>(_metrics);
            var wrapper = new MetricsFileWrapper(
                Version: 1,
                LastSaved: DateTime.UtcNow,
                Metrics: snapshot);

            string json = JsonSerializer.Serialize(wrapper, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);

            _isDirty = false;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Loads metrics from disk.
    /// </summary>
    public async Task LoadMetricsAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();

        if (!File.Exists(_filePath))
        {
            return;
        }

        await _saveLock.WaitAsync(ct);
        try
        {
            string json = await File.ReadAllTextAsync(_filePath, ct);
            var wrapper = JsonSerializer.Deserialize<MetricsFileWrapper>(json, JsonOptions);

            if (wrapper?.Metrics != null)
            {
                _metrics.Clear();
                foreach (var kvp in wrapper.Metrics)
                {
                    _metrics[kvp.Key] = kvp.Value;
                }

                // Remove old metrics if configured
                if (_config.MaxMetricsAge > 0)
                {
                    DateTime cutoff = DateTime.UtcNow.AddDays(-_config.MaxMetricsAge);
                    var expired = _metrics.Where(kvp => kvp.Value.LastUsed < cutoff)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expired)
                    {
                        _metrics.TryRemove(key, out _);
                    }

                    if (expired.Count > 0)
                    {
                        _isDirty = true;
                    }
                }
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Synchronous load for constructor use.
    /// </summary>
    private void LoadMetricsSync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            var wrapper = JsonSerializer.Deserialize<MetricsFileWrapper>(json, JsonOptions);

            if (wrapper?.Metrics != null)
            {
                foreach (var kvp in wrapper.Metrics)
                {
                    _metrics[kvp.Key] = kvp.Value;
                }

                // Remove old metrics if configured
                if (_config.MaxMetricsAge > 0)
                {
                    DateTime cutoff = DateTime.UtcNow.AddDays(-_config.MaxMetricsAge);
                    var expired = _metrics.Where(kvp => kvp.Value.LastUsed < cutoff)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expired)
                    {
                        _metrics.TryRemove(key, out _);
                    }

                    if (expired.Count > 0)
                    {
                        _isDirty = true;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Corrupted file, start fresh
            _metrics.Clear();
        }
    }

    private async Task SaveIfDirtyAsync()
    {
        if (_isDirty && !_disposed)
        {
            await SaveMetricsAsync();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PersistentMetricsStore));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _autoSaveTimer?.Dispose();

        // Final save
        if (_isDirty)
        {
            _saveLock.Wait();
            try
            {
                var snapshot = new Dictionary<string, PerformanceMetrics>(_metrics);
                var wrapper = new MetricsFileWrapper(
                    Version: 1,
                    LastSaved: DateTime.UtcNow,
                    Metrics: snapshot);

                string json = JsonSerializer.Serialize(wrapper, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        _saveLock.Dispose();
    }

    /// <summary>
    /// Wrapper for metrics file serialization.
    /// </summary>
    private sealed record MetricsFileWrapper(
        int Version,
        DateTime LastSaved,
        Dictionary<string, PerformanceMetrics> Metrics);
}
