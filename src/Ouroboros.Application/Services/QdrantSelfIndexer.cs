// <copyright file="QdrantSelfIndexer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LangChain.Databases;
using LangChain.Splitters.Text;
using Ouroboros.Domain;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// Configuration for the Qdrant self-indexer.
/// </summary>
public sealed record QdrantIndexerConfig
{
    /// <summary>Qdrant gRPC endpoint.</summary>
    public string QdrantEndpoint { get; init; } = "http://localhost:6334";

    /// <summary>Collection name for indexed content.</summary>
    public string CollectionName { get; init; } = "ouroboros_selfindex";

    /// <summary>Collection for file hashes (for incremental updates).</summary>
    public string HashCollectionName { get; init; } = "ouroboros_filehashes";

    /// <summary>Root paths to index.</summary>
    public List<string> RootPaths { get; init; } = new();

    /// <summary>File extensions to index.</summary>
    public HashSet<string> Extensions { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".py", ".js", ".ts", ".json", ".md", ".txt", ".yaml", ".yml",
        ".xml", ".html", ".css", ".sql", ".sh", ".ps1", ".bat", ".cmd",
        ".config", ".csproj", ".sln", ".fsproj", ".vbproj", ".props", ".targets"
    };

    /// <summary>Directories to exclude.</summary>
    public HashSet<string> ExcludeDirectories { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".vscode", ".idea",
        "packages", "TestResults", "dist", "build", "out", ".next",
        "__pycache__", ".pytest_cache", "coverage", ".nyc_output"
    };

    /// <summary>Chunk size for text splitting.</summary>
    public int ChunkSize { get; init; } = 1000;

    /// <summary>Chunk overlap.</summary>
    public int ChunkOverlap { get; init; } = 200;

    /// <summary>Max file size to index in bytes.</summary>
    public long MaxFileSize { get; init; } = 1024 * 1024; // 1MB

    /// <summary>Batch size for upserts.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>Vector dimensions (nomic-embed-text default).</summary>
    public int VectorSize { get; init; } = 768;

    /// <summary>Enable file watcher for live incremental updates.</summary>
    public bool EnableFileWatcher { get; init; } = true;

    /// <summary>Debounce delay for file changes in milliseconds.</summary>
    public int FileWatcherDebounceMs { get; init; } = 1000;
}

/// <summary>
/// Progress information for indexing operations.
/// </summary>
public sealed record IndexingProgress
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int IndexedChunks { get; init; }
    public int SkippedFiles { get; init; }
    public int ErrorFiles { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public TimeSpan Elapsed { get; init; }
    public bool IsComplete { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Qdrant-based self-indexer for Ouroboros workspace content.
/// Supports full reindex, incremental updates, and live file watching.
/// </summary>
public sealed class QdrantSelfIndexer : IAsyncDisposable
{
    private readonly QdrantClient _client;
    private readonly IEmbeddingModel _embedding;
    private readonly QdrantIndexerConfig _config;
    private readonly RecursiveCharacterTextSplitter _splitter;
    private readonly ConcurrentDictionary<string, string> _fileHashes = new();
    private readonly ConcurrentDictionary<string, DateTime> _pendingChanges = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly CancellationTokenSource _watcherCts = new();
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private Task? _debounceTask;
    private bool _isInitialized;
    private bool _isDisposed;

    /// <summary>
    /// Event raised when indexing progress updates.
    /// </summary>
    public event Action<IndexingProgress>? OnProgress;

    /// <summary>
    /// Event raised when a file is indexed.
    /// </summary>
    public event Action<string, int>? OnFileIndexed;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantSelfIndexer"/> class.
    /// </summary>
    public QdrantSelfIndexer(IEmbeddingModel embedding, QdrantIndexerConfig? config = null)
    {
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        _config = config ?? new QdrantIndexerConfig();

        var uri = new Uri(_config.QdrantEndpoint);
        _client = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");

        _splitter = new RecursiveCharacterTextSplitter(
            chunkSize: _config.ChunkSize,
            chunkOverlap: _config.ChunkOverlap);
    }

    /// <summary>
    /// Initializes the indexer, creating collections if needed.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await EnsureCollectionExistsAsync(_config.CollectionName, ct);
        await EnsureCollectionExistsAsync(_config.HashCollectionName, ct, isHashCollection: true);
        await LoadFileHashesAsync(ct);

        if (_config.EnableFileWatcher)
        {
            StartFileWatchers();
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Performs a full reindex of all configured paths.
    /// </summary>
    public async Task<IndexingProgress> FullReindexAsync(
        bool clearExisting = true,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken ct = default)
    {
        await _indexLock.WaitAsync(ct);
        try
        {
            var startTime = DateTime.UtcNow;
            var stats = new IndexingStats();

            if (clearExisting)
            {
                await ClearCollectionAsync(_config.CollectionName, ct);
                await ClearCollectionAsync(_config.HashCollectionName, ct);
                _fileHashes.Clear();
            }

            var allFiles = DiscoverFiles();
            stats.TotalFiles = allFiles.Count;

            ReportProgress(progress, stats, string.Empty, startTime);

            foreach (var file in allFiles)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    stats.CurrentFile = file;
                    var chunks = await IndexFileAsync(file, forceReindex: true, ct);
                    if (chunks > 0)
                    {
                        stats.ProcessedFiles++;
                        stats.IndexedChunks += chunks;
                        OnFileIndexed?.Invoke(file, chunks);
                    }
                    else
                    {
                        stats.SkippedFiles++;
                    }
                }
                catch (Exception ex)
                {
                    stats.ErrorFiles++;
                    Console.WriteLine($"[IndexError] {file}: {ex.Message}");
                }

                ReportProgress(progress, stats, file, startTime);
            }

            var finalProgress = CreateProgress(stats, startTime, isComplete: true);
            OnProgress?.Invoke(finalProgress);
            return finalProgress;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Performs incremental indexing, only updating changed files.
    /// </summary>
    public async Task<IndexingProgress> IncrementalIndexAsync(
        IProgress<IndexingProgress>? progress = null,
        CancellationToken ct = default)
    {
        await _indexLock.WaitAsync(ct);
        try
        {
            var startTime = DateTime.UtcNow;
            var stats = new IndexingStats();

            var allFiles = DiscoverFiles();
            var changedFiles = new List<string>();

            // Identify changed files
            foreach (var file in allFiles)
            {
                var currentHash = ComputeFileHash(file);
                if (!_fileHashes.TryGetValue(file, out var storedHash) || storedHash != currentHash)
                {
                    changedFiles.Add(file);
                }
            }

            // Identify deleted files
            var deletedFiles = _fileHashes.Keys
                .Where(f => !allFiles.Contains(f))
                .ToList();

            stats.TotalFiles = changedFiles.Count + deletedFiles.Count;

            // Remove deleted files from index
            foreach (var file in deletedFiles)
            {
                if (ct.IsCancellationRequested) break;
                await RemoveFileFromIndexAsync(file, ct);
                _fileHashes.TryRemove(file, out _);
                stats.ProcessedFiles++;
            }

            // Index changed files
            foreach (var file in changedFiles)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    stats.CurrentFile = file;
                    await RemoveFileFromIndexAsync(file, ct);
                    var chunks = await IndexFileAsync(file, forceReindex: true, ct);
                    if (chunks > 0)
                    {
                        stats.ProcessedFiles++;
                        stats.IndexedChunks += chunks;
                        OnFileIndexed?.Invoke(file, chunks);
                    }
                    else
                    {
                        stats.SkippedFiles++;
                    }
                }
                catch (Exception ex)
                {
                    stats.ErrorFiles++;
                    Console.WriteLine($"[IndexError] {file}: {ex.Message}");
                }

                ReportProgress(progress, stats, file, startTime);
            }

            var finalProgress = CreateProgress(stats, startTime, isComplete: true);
            OnProgress?.Invoke(finalProgress);
            return finalProgress;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Indexes a specific file or directory.
    /// </summary>
    public async Task<int> IndexPathAsync(string path, CancellationToken ct = default)
    {
        if (Directory.Exists(path))
        {
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => ShouldIndexFile(f))
                .ToList();

            int totalChunks = 0;
            foreach (var file in files)
            {
                totalChunks += await IndexFileAsync(file, forceReindex: true, ct);
            }
            return totalChunks;
        }
        else if (File.Exists(path))
        {
            return await IndexFileAsync(path, forceReindex: true, ct);
        }

        return 0;
    }

    /// <summary>
    /// Searches the indexed content.
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(
        string query,
        int limit = 10,
        float scoreThreshold = 0.5f,
        CancellationToken ct = default)
    {
        var queryEmbedding = await _embedding.CreateEmbeddingsAsync(query, ct);

        var searchResults = await _client.SearchAsync(
            _config.CollectionName,
            queryEmbedding,
            limit: (ulong)limit,
            scoreThreshold: scoreThreshold,
            cancellationToken: ct);

        return searchResults.Select(r => new SearchResult
        {
            FilePath = r.Payload.TryGetValue("file_path", out var fp) ? fp.StringValue : string.Empty,
            ChunkIndex = r.Payload.TryGetValue("chunk_index", out var ci) ? (int)ci.IntegerValue : 0,
            Content = r.Payload.TryGetValue("content", out var c) ? c.StringValue : string.Empty,
            Score = r.Score
        }).ToList();
    }

    /// <summary>
    /// Gets statistics about the indexed content.
    /// </summary>
    public async Task<IndexStats> GetStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionInfo = await _client.GetCollectionInfoAsync(_config.CollectionName, ct);
            return new IndexStats
            {
                TotalVectors = (long)collectionInfo.PointsCount,
                IndexedFiles = _fileHashes.Count,
                CollectionName = _config.CollectionName,
                VectorSize = _config.VectorSize
            };
        }
        catch
        {
            return new IndexStats { CollectionName = _config.CollectionName };
        }
    }

    #region Self-Reorganization (Learning-Driven Knowledge Optimization)

    // Track access patterns for intelligent reorganization
    private readonly ConcurrentDictionary<string, AccessPattern> _accessPatterns = new();
    private readonly ConcurrentDictionary<string, List<string>> _coAccessLog = new();
    private string? _lastAccessedPointId;

    /// <summary>
    /// Records an access pattern when knowledge is retrieved.
    /// Call this after SearchAsync to enable learning-driven reorganization.
    /// </summary>
    public void RecordAccess(IEnumerable<SearchResult> results)
    {
        var pointIds = results.Select(r => GeneratePointId(r.FilePath, r.ChunkIndex)).ToList();
        var now = DateTime.UtcNow;

        foreach (var result in results)
        {
            var pointId = GeneratePointId(result.FilePath, result.ChunkIndex);

            _accessPatterns.AddOrUpdate(
                pointId,
                _ => new AccessPattern
                {
                    PointId = pointId,
                    FilePath = result.FilePath,
                    AccessCount = 1,
                    LastAccessed = now,
                    CoAccessedWith = pointIds.Where(p => p != pointId).ToList()
                },
                (_, existing) => existing with
                {
                    AccessCount = existing.AccessCount + 1,
                    LastAccessed = now,
                    CoAccessedWith = existing.CoAccessedWith
                        .Concat(pointIds.Where(p => p != pointId))
                        .Distinct()
                        .TakeLast(20)
                        .ToList()
                });

            // Track co-access for clustering
            if (_lastAccessedPointId != null && _lastAccessedPointId != pointId)
            {
                _coAccessLog.AddOrUpdate(
                    pointId,
                    _ => new List<string> { _lastAccessedPointId },
                    (_, list) =>
                    {
                        if (!list.Contains(_lastAccessedPointId)) list.Add(_lastAccessedPointId);
                        return list.TakeLast(50).ToList();
                    });
            }

            _lastAccessedPointId = pointId;
        }
    }

    /// <summary>
    /// Performs intelligent reorganization based on access patterns and content analysis.
    /// Should be called periodically during thinking/learning cycles.
    /// </summary>
    public async Task<ReorganizationResult> ReorganizeAsync(
        bool createSummaries = true,
        bool removeDuplicates = true,
        bool clusterRelated = true,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var insights = new List<string>();
        int clustersFound = 0, consolidated = 0, duplicatesRemoved = 0, summariesCreated = 0;

        await _indexLock.WaitAsync(ct);
        try
        {
            Console.WriteLine("[Reorganize] 🧠 Beginning knowledge reorganization...");

            // 1. Find and remove near-duplicates
            if (removeDuplicates)
            {
                var (removed, insight) = await RemoveNearDuplicatesAsync(ct);
                duplicatesRemoved = removed;
                if (!string.IsNullOrEmpty(insight)) insights.Add(insight);
            }

            // 2. Cluster frequently co-accessed content
            if (clusterRelated)
            {
                var (clusters, clusterInsight) = await ClusterRelatedContentAsync(ct);
                clustersFound = clusters;
                if (!string.IsNullOrEmpty(clusterInsight)) insights.Add(clusterInsight);
            }

            // 3. Create summary vectors for high-access clusters
            if (createSummaries)
            {
                var (summaries, summaryInsight) = await CreateClusterSummariesAsync(ct);
                summariesCreated = summaries;
                if (!string.IsNullOrEmpty(summaryInsight)) insights.Add(summaryInsight);
            }

            // 4. Update metadata with access patterns
            consolidated = await UpdateAccessMetadataAsync(ct);

            Console.WriteLine($"[Reorganize] ✅ Complete: {duplicatesRemoved} duplicates removed, {clustersFound} clusters, {summariesCreated} summaries");

            return new ReorganizationResult
            {
                ClustersFound = clustersFound,
                ConsolidatedChunks = consolidated,
                DuplicatesRemoved = duplicatesRemoved,
                SummariesCreated = summariesCreated,
                Duration = DateTime.UtcNow - startTime,
                Insights = insights
            };
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Lightweight reorganization suitable for calling during active thinking.
    /// Only performs quick optimizations without heavy operations.
    /// </summary>
    public async Task<int> QuickReorganizeAsync(CancellationToken ct = default)
    {
        // Only run if we have enough access patterns to be meaningful
        if (_accessPatterns.Count < 10) return 0;

        int optimizations = 0;

        // Update frequently accessed content with "hot" flag for faster retrieval
        var hotContent = _accessPatterns.Values
            .Where(p => p.AccessCount >= 3)
            .OrderByDescending(p => p.AccessCount)
            .Take(20)
            .ToList();

        foreach (var pattern in hotContent)
        {
            try
            {
                if (Guid.TryParse(pattern.PointId, out var guid))
                {
                    await _client.SetPayloadAsync(
                        _config.CollectionName,
                        new Dictionary<string, Value>
                        {
                            ["access_count"] = pattern.AccessCount,
                            ["last_accessed"] = pattern.LastAccessed.ToString("O"),
                            ["is_hot"] = true
                        },
                        guid,
                        cancellationToken: ct);
                    optimizations++;
                }
            }
            catch { /* Point may not exist */ }
        }

        return optimizations;
    }

    private async Task<(int removed, string? insight)> RemoveNearDuplicatesAsync(CancellationToken ct)
    {
        int removed = 0;
        var toRemove = new List<PointId>();

        try
        {
            // Scroll through points and find near-duplicates using vector similarity
            var offset = (PointId?)null;
            var seenContent = new Dictionary<string, (PointId Id, float[] Vector)>();

            while (true)
            {
                var scrollResult = await _client.ScrollAsync(
                    _config.CollectionName,
                    limit: 50,
                    offset: offset,
                    vectorsSelector: new WithVectorsSelector { Enable = true },
                    cancellationToken: ct);

                foreach (var point in scrollResult.Result)
                {
                    var content = point.Payload.TryGetValue("content", out var c) ? c.StringValue : "";
                    var contentHash = Convert.ToBase64String(
                        System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(content)).Take(8).ToArray());

                    // Check for exact duplicates (same content hash)
                    if (seenContent.TryGetValue(contentHash, out var existing))
                    {
                        toRemove.Add(point.Id);
                        removed++;
                    }
                    else
                    {
                        // Check for near-duplicates using vector similarity
#pragma warning disable CS0612 // VectorOutput.Data is obsolete
                        var vector = point.Vectors?.Vector?.Data?.ToArray();
#pragma warning restore CS0612
                        if (vector != null)
                        {
                            foreach (var (_, (existingId, existingVector)) in seenContent)
                            {
                                var similarity = CosineSimilarity(vector, existingVector);
                                if (similarity > 0.98f) // Very similar
                                {
                                    toRemove.Add(point.Id);
                                    removed++;
                                    break;
                                }
                            }

                            if (!toRemove.Contains(point.Id))
                            {
                                seenContent[contentHash] = (point.Id, vector);
                            }
                        }
                    }
                }

                if (scrollResult.Result.Count < 50) break;
                offset = scrollResult.Result.Last().Id;
            }

            // Remove duplicates
            if (toRemove.Count > 0)
            {
                await _client.DeleteAsync(_config.CollectionName, toRemove, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Reorganize] Duplicate removal error: {ex.Message}");
        }

        return (removed, removed > 0 ? $"Removed {removed} near-duplicate chunks" : null);
    }

    private async Task<(int clusters, string? insight)> ClusterRelatedContentAsync(CancellationToken ct)
    {
        if (_coAccessLog.Count < 5) return (0, null);

        var clusters = new List<HashSet<string>>();

        // Build clusters from co-access patterns
        foreach (var (pointId, coAccessed) in _coAccessLog)
        {
            var existingCluster = clusters.FirstOrDefault(c => c.Contains(pointId) || coAccessed.Any(ca => c.Contains(ca)));

            if (existingCluster != null)
            {
                existingCluster.Add(pointId);
                foreach (var ca in coAccessed) existingCluster.Add(ca);
            }
            else
            {
                var newCluster = new HashSet<string> { pointId };
                foreach (var ca in coAccessed) newCluster.Add(ca);
                if (newCluster.Count >= 2) clusters.Add(newCluster);
            }
        }

        // Update cluster metadata
        for (int i = 0; i < clusters.Count; i++)
        {
            foreach (var pointId in clusters[i])
            {
                try
                {
                    if (Guid.TryParse(pointId, out var guid))
                    {
                        await _client.SetPayloadAsync(
                            _config.CollectionName,
                            new Dictionary<string, Value>
                            {
                                ["cluster_id"] = i,
                                ["cluster_size"] = clusters[i].Count
                            },
                            guid,
                            cancellationToken: ct);
                    }
                }
                catch { /* Point may not exist */ }
            }
        }

        return (clusters.Count, clusters.Count > 0 ? $"Identified {clusters.Count} knowledge clusters from access patterns" : null);
    }

    private async Task<(int summaries, string? insight)> CreateClusterSummariesAsync(CancellationToken ct)
    {
        // Find high-access clusters and create summary vectors
        var hotClusters = _accessPatterns.Values
            .Where(p => p.AccessCount >= 5)
            .GroupBy(p => p.FilePath)
            .Where(g => g.Count() >= 2)
            .Take(5)
            .ToList();

        int summariesCreated = 0;

        foreach (var cluster in hotClusters)
        {
            try
            {
                // Aggregate content from cluster
                var contents = new List<string>();
                foreach (var pattern in cluster.Take(5))
                {
                    var results = await _client.RetrieveAsync(
                        _config.CollectionName,
                        new[] { new PointId { Uuid = pattern.PointId } },
                        withPayload: true,
                        cancellationToken: ct);

                    foreach (var point in results)
                    {
                        if (point.Payload.TryGetValue("content", out var c))
                        {
                            contents.Add(c.StringValue);
                        }
                    }
                }

                if (contents.Count < 2) continue;

                // Create a summary embedding from combined content
                var summaryText = $"[SUMMARY] Key concepts from {cluster.Key}: " +
                    string.Join(" | ", contents.Select(c => c.Length > 100 ? c.Substring(0, 100) : c));

                var summaryEmbedding = await _embedding.CreateEmbeddingsAsync(summaryText, ct);
                var summaryId = Guid.NewGuid().ToString();

                await _client.UpsertAsync(
                    _config.CollectionName,
                    new[]
                    {
                        new PointStruct
                        {
                            Id = new PointId { Uuid = summaryId },
                            Vectors = summaryEmbedding,
                            Payload =
                            {
                                ["file_path"] = cluster.Key,
                                ["content"] = summaryText,
                                ["is_summary"] = true,
                                ["summarizes_count"] = contents.Count,
                                ["created_at"] = DateTime.UtcNow.ToString("O")
                            }
                        }
                    },
                    cancellationToken: ct);

                summariesCreated++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Reorganize] Summary creation error: {ex.Message}");
            }
        }

        return (summariesCreated, summariesCreated > 0 ? $"Created {summariesCreated} summary vectors for frequently accessed content" : null);
    }

    private async Task<int> UpdateAccessMetadataAsync(CancellationToken ct)
    {
        int updated = 0;

        foreach (var pattern in _accessPatterns.Values.Take(100))
        {
            try
            {
                if (Guid.TryParse(pattern.PointId, out var guid))
                {
                    await _client.SetPayloadAsync(
                        _config.CollectionName,
                        new Dictionary<string, Value>
                        {
                            ["access_count"] = pattern.AccessCount,
                            ["last_accessed"] = pattern.LastAccessed.ToString("O")
                        },
                        guid,
                        cancellationToken: ct);
                    updated++;
                }
            }
            catch { /* Point may not exist */ }
        }

        return updated;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude > 0 ? (float)(dot / magnitude) : 0;
    }

    /// <summary>
    /// Gets reorganization statistics and insights.
    /// </summary>
    public ReorganizationStats GetReorganizationStats()
    {
        var hotContent = _accessPatterns.Values.Where(p => p.AccessCount >= 3).ToList();
        var clusters = _coAccessLog.Count;

        return new ReorganizationStats
        {
            TrackedPatterns = _accessPatterns.Count,
            HotContentCount = hotContent.Count,
            CoAccessClusters = clusters,
            TopAccessedFiles = _accessPatterns.Values
                .GroupBy(p => p.FilePath)
                .OrderByDescending(g => g.Sum(p => p.AccessCount))
                .Take(5)
                .Select(g => (g.Key, g.Sum(p => p.AccessCount)))
                .ToList()
        };
    }

    #endregion

    /// <summary>
    /// Adds a root path to monitor.
    /// </summary>
    public void AddRootPath(string path)
    {
        if (!Directory.Exists(path)) return;
        if (_config.RootPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) return;

        _config.RootPaths.Add(path);

        if (_config.EnableFileWatcher && _isInitialized)
        {
            StartWatcherForPath(path);
        }
    }

    private async Task<int> IndexFileAsync(string filePath, bool forceReindex, CancellationToken ct)
    {
        if (!ShouldIndexFile(filePath)) return 0;

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length > _config.MaxFileSize) return 0;

        var currentHash = ComputeFileHash(filePath);

        if (!forceReindex && _fileHashes.TryGetValue(filePath, out var storedHash) && storedHash == currentHash)
        {
            return 0; // File unchanged
        }

        var content = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(content)) return 0;

        var chunks = _splitter.SplitText(content);
        if (chunks.Count == 0) return 0;

        var points = new List<PointStruct>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            if (string.IsNullOrWhiteSpace(chunk)) continue;

            var embedding = await _embedding.CreateEmbeddingsAsync(chunk, ct);
            var pointId = GeneratePointId(filePath, i);

            points.Add(new PointStruct
            {
                Id = new PointId { Uuid = pointId },
                Vectors = embedding,
                Payload =
                {
                    ["file_path"] = filePath,
                    ["file_name"] = Path.GetFileName(filePath),
                    ["extension"] = Path.GetExtension(filePath),
                    ["chunk_index"] = i,
                    ["chunk_count"] = chunks.Count,
                    ["content"] = chunk,
                    ["indexed_at"] = DateTime.UtcNow.ToString("O"),
                    ["file_hash"] = currentHash
                }
            });
        }

        if (points.Count > 0)
        {
            // Upsert in batches
            for (int i = 0; i < points.Count; i += _config.BatchSize)
            {
                var batch = points.Skip(i).Take(_config.BatchSize).ToList();
                await _client.UpsertAsync(_config.CollectionName, batch, cancellationToken: ct);
            }

            // Store file hash
            await StoreFileHashAsync(filePath, currentHash, ct);
            _fileHashes[filePath] = currentHash;
        }

        return points.Count;
    }

    private async Task RemoveFileFromIndexAsync(string filePath, CancellationToken ct)
    {
        // Delete all points for this file
        await _client.DeleteAsync(
            _config.CollectionName,
            new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "file_path",
                            Match = new Match { Keyword = filePath }
                        }
                    }
                }
            },
            cancellationToken: ct);

        // Remove hash
        await _client.DeleteAsync(
            _config.HashCollectionName,
            new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "file_path",
                            Match = new Match { Keyword = filePath }
                        }
                    }
                }
            },
            cancellationToken: ct);
    }

    private List<string> DiscoverFiles()
    {
        var files = new List<string>();
        foreach (var root in _config.RootPaths)
        {
            if (!Directory.Exists(root)) continue;

            try
            {
                files.AddRange(
                    Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                        .Where(ShouldIndexFile));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DiscoverError] {root}: {ex.Message}");
            }
        }
        return files;
    }

    private bool ShouldIndexFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

        // Check excluded directories
        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => _config.ExcludeDirectories.Contains(p)))
        {
            return false;
        }

        // Check extension
        if (!_config.Extensions.Contains(extension))
        {
            return false;
        }

        // Skip hidden files
        if (fileName.StartsWith('.'))
        {
            return false;
        }

        return true;
    }

    private static string ComputeFileHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GeneratePointId(string filePath, int chunkIndex)
    {
        using var sha256 = SHA256.Create();
        var input = $"{filePath}::{chunkIndex}";
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.Take(16).ToArray()).ToString();
    }

    private async Task EnsureCollectionExistsAsync(string collectionName, CancellationToken ct, bool isHashCollection = false)
    {
        var expectedSize = isHashCollection ? 4u : (uint)_config.VectorSize;
        var exists = await _client.CollectionExistsAsync(collectionName, ct);

        if (exists)
        {
            // Validate dimensions match - if not, recreate the collection
            try
            {
                var info = await _client.GetCollectionInfoAsync(collectionName);
                var currentSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;

                if (currentSize != expectedSize)
                {
                    Console.WriteLine($"[CollectionDimensionMismatch] {collectionName}: expected {expectedSize}D, got {currentSize}D. Recreating...");
                    await _client.DeleteCollectionAsync(collectionName);
                    exists = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CollectionInfoError] {collectionName}: {ex.Message}");
            }
        }

        if (!exists)
        {
            await _client.CreateCollectionAsync(
                collectionName,
                new VectorParams
                {
                    Size = expectedSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: ct);
        }
    }

    private async Task ClearCollectionAsync(string collectionName, CancellationToken ct)
    {
        var exists = await _client.CollectionExistsAsync(collectionName, ct);
        if (exists)
        {
            await _client.DeleteCollectionAsync(collectionName);
            await EnsureCollectionExistsAsync(collectionName, ct, collectionName == _config.HashCollectionName);
        }
    }

    private async Task LoadFileHashesAsync(CancellationToken ct)
    {
        try
        {
            var exists = await _client.CollectionExistsAsync(_config.HashCollectionName, ct);
            if (!exists) return;

            // Scroll through all hash records
            var offset = (PointId?)null;
            while (true)
            {
                var scrollResult = await _client.ScrollAsync(
                    _config.HashCollectionName,
                    limit: 100,
                    offset: offset,
                    cancellationToken: ct);

                foreach (var point in scrollResult.Result)
                {
                    if (point.Payload.TryGetValue("file_path", out var fp) &&
                        point.Payload.TryGetValue("file_hash", out var fh))
                    {
                        _fileHashes[fp.StringValue] = fh.StringValue;
                    }
                }

                if (scrollResult.Result.Count < 100) break;
                offset = scrollResult.Result.Last().Id;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadHashesError] {ex.Message}");
        }
    }

    private async Task StoreFileHashAsync(string filePath, string hash, CancellationToken ct)
    {
        var pointId = GeneratePointId(filePath, -1); // -1 for hash entry
        var point = new PointStruct
        {
            Id = new PointId { Uuid = pointId },
            Vectors = new float[] { 0, 0, 0, 0 }, // Dummy vector for hash storage
            Payload =
            {
                ["file_path"] = filePath,
                ["file_hash"] = hash,
                ["indexed_at"] = DateTime.UtcNow.ToString("O")
            }
        };

        try
        {
            await _client.UpsertAsync(_config.HashCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Grpc.Core.RpcException ex) when (ex.Message.Contains("dimension error"))
        {
            // Collection has wrong dimensions - recreate it
            Console.WriteLine($"[HashCollectionFix] Dimension mismatch detected, recreating {_config.HashCollectionName}...");
            try
            {
                await _client.DeleteCollectionAsync(_config.HashCollectionName);
                await _client.CreateCollectionAsync(
                    _config.HashCollectionName,
                    new VectorParams { Size = 4, Distance = Distance.Cosine },
                    cancellationToken: ct);
                // Retry the upsert
                await _client.UpsertAsync(_config.HashCollectionName, new[] { point }, cancellationToken: ct);
                Console.WriteLine($"[HashCollectionFix] Successfully recreated and stored hash.");
            }
            catch (Exception innerEx)
            {
                Console.WriteLine($"[HashCollectionFixError] {innerEx.Message}");
            }
        }
    }

    private void StartFileWatchers()
    {
        foreach (var path in _config.RootPaths)
        {
            StartWatcherForPath(path);
        }

        // Start debounce processor
        _debounceTask = ProcessPendingChangesAsync(_watcherCts.Token);
    }

    private void StartWatcherForPath(string path)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;

            _watchers.Add(watcher);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WatcherError] {path}: {ex.Message}");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!ShouldIndexFile(e.FullPath)) return;
        _pendingChanges[e.FullPath] = DateTime.UtcNow;
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (!ShouldIndexFile(e.FullPath)) return;
        _pendingChanges[e.FullPath] = DateTime.MinValue; // Mark for deletion
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Mark old path for deletion
        if (ShouldIndexFile(e.OldFullPath))
        {
            _pendingChanges[e.OldFullPath] = DateTime.MinValue;
        }

        // Mark new path for indexing
        if (ShouldIndexFile(e.FullPath))
        {
            _pendingChanges[e.FullPath] = DateTime.UtcNow;
        }
    }

    private async Task ProcessPendingChangesAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.FileWatcherDebounceMs, ct);

                var cutoff = DateTime.UtcNow.AddMilliseconds(-_config.FileWatcherDebounceMs);
                var toProcess = _pendingChanges
                    .Where(kvp => kvp.Value <= cutoff || kvp.Value == DateTime.MinValue)
                    .ToList();

                foreach (var (path, timestamp) in toProcess)
                {
                    _pendingChanges.TryRemove(path, out _);

                    if (timestamp == DateTime.MinValue)
                    {
                        // File deleted
                        await RemoveFileFromIndexAsync(path, ct);
                        _fileHashes.TryRemove(path, out _);
                        Console.WriteLine($"[IndexRemoved] {path}");
                    }
                    else
                    {
                        // File changed/created
                        await RemoveFileFromIndexAsync(path, ct);
                        var chunks = await IndexFileAsync(path, forceReindex: true, ct);
                        if (chunks > 0)
                        {
                            Console.WriteLine($"[IndexUpdated] {path} ({chunks} chunks)");
                            OnFileIndexed?.Invoke(path, chunks);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PendingChangesError] {ex.Message}");
            }
        }
    }

    private void ReportProgress(IProgress<IndexingProgress>? progress, IndexingStats stats, string currentFile, DateTime startTime)
    {
        var p = CreateProgress(stats, startTime, isComplete: false, currentFile: currentFile);
        progress?.Report(p);
        OnProgress?.Invoke(p);
    }

    private static IndexingProgress CreateProgress(IndexingStats stats, DateTime startTime, bool isComplete, string? currentFile = null)
    {
        return new IndexingProgress
        {
            TotalFiles = stats.TotalFiles,
            ProcessedFiles = stats.ProcessedFiles,
            IndexedChunks = stats.IndexedChunks,
            SkippedFiles = stats.SkippedFiles,
            ErrorFiles = stats.ErrorFiles,
            CurrentFile = currentFile ?? stats.CurrentFile,
            Elapsed = DateTime.UtcNow - startTime,
            IsComplete = isComplete
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _watcherCts.Cancel();

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        if (_debounceTask != null)
        {
            try { await _debounceTask; } catch { }
        }

        _watcherCts.Dispose();
        _indexLock.Dispose();
        _client.Dispose();
    }

    private class IndexingStats
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int IndexedChunks { get; set; }
        public int SkippedFiles { get; set; }
        public int ErrorFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
    }
}

/// <summary>
/// Search result from the self-index.
/// </summary>
public sealed record SearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public int ChunkIndex { get; init; }
    public string Content { get; init; } = string.Empty;
    public float Score { get; init; }
}

/// <summary>
/// Statistics about the index.
/// </summary>
public sealed record IndexStats
{
    public long TotalVectors { get; init; }
    public int IndexedFiles { get; init; }
    public string CollectionName { get; init; } = string.Empty;
    public int VectorSize { get; init; }
}

/// <summary>
/// Result of a reorganization operation.
/// </summary>
public sealed record ReorganizationResult
{
    public int ClustersFound { get; init; }
    public int ConsolidatedChunks { get; init; }
    public int DuplicatesRemoved { get; init; }
    public int SummariesCreated { get; init; }
    public TimeSpan Duration { get; init; }
    public List<string> Insights { get; init; } = new();
}

/// <summary>
/// Access pattern tracking for knowledge reorganization.
/// </summary>
public sealed record AccessPattern
{
    public string PointId { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public int AccessCount { get; init; }
    public DateTime LastAccessed { get; init; }
    public List<string> CoAccessedWith { get; init; } = new();
}

/// <summary>
/// Statistics about reorganization state.
/// </summary>
public sealed record ReorganizationStats
{
    public int TrackedPatterns { get; init; }
    public int HotContentCount { get; init; }
    public int CoAccessClusters { get; init; }
    public List<(string FilePath, int AccessCount)> TopAccessedFiles { get; init; } = new();
}
