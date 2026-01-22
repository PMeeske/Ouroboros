// <copyright file="QdrantVectorStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;
using LCVector = LangChain.Databases.Vector;
using LCDocument = LangChain.DocumentLoaders.Document;

namespace Ouroboros.Domain.Vectors;

/// <summary>
/// Qdrant vector store implementation for production use.
/// Provides persistent vector storage with similarity search capabilities.
/// Implements IAdvancedVectorStore for filtering, batch operations, and more.
/// </summary>
public sealed class QdrantVectorStore : IAdvancedVectorStore, IAsyncDisposable
{
    private readonly QdrantClient _client;
    private readonly ILogger? _logger;
    private readonly string _collectionName;
    private readonly bool _disposeClient;
    private int? _vectorDimension;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorStore"/> class.
    /// </summary>
    /// <param name="connectionString">Qdrant connection string (e.g., "http://localhost:6333").</param>
    /// <param name="collectionName">Name of the collection to use.</param>
    /// <param name="logger">Optional logger instance.</param>
    public QdrantVectorStore(string connectionString, string collectionName = "pipeline_vectors", ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentException("Collection name cannot be null or empty", nameof(collectionName));
        }

        _collectionName = collectionName;
        _logger = logger;

        // Parse connection string to extract host and port
        var uri = new Uri(connectionString);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6334; // Default to gRPC port
        var useHttps = uri.Scheme == "https";

        _logger?.LogInformation("Initializing Qdrant client: {Host}:{Port} (HTTPS: {UseHttps})", host, port, useHttps);

        _client = new QdrantClient(host, port, useHttps);
        _disposeClient = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantVectorStore"/> class with an existing client.
    /// </summary>
    /// <param name="client">Existing Qdrant client instance.</param>
    /// <param name="collectionName">Name of the collection to use.</param>
    /// <param name="logger">Optional logger instance.</param>
    public QdrantVectorStore(QdrantClient client, string collectionName = "pipeline_vectors", ILogger? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        _logger = logger;
        _disposeClient = false; // Don't dispose client we don't own
    }

    /// <summary>
    /// Adds vectors to the Qdrant store asynchronously.
    /// Creates the collection if it doesn't exist.
    /// </summary>
    public async Task AddAsync(IEnumerable<LCVector> vectors, CancellationToken cancellationToken = default)
    {
        var vectorList = vectors.ToList();
        if (!vectorList.Any())
        {
            _logger?.LogDebug("No vectors to add");
            return;
        }

        try
        {
            // Ensure collection exists
            await EnsureCollectionExistsAsync(vectorList.First(), cancellationToken);

            // Convert vectors to Qdrant points
            var points = vectorList.Select(v => new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = v.Embedding?.ToArray() ?? Array.Empty<float>(),
                Payload =
                {
                    ["text"] = v.Text,
                    ["id"] = v.Id,
                }
            }).ToList();

            // Add metadata to payload
            foreach (var (point, vector) in points.Zip(vectorList))
            {
                if (vector.Metadata != null)
                {
                    foreach (var kvp in vector.Metadata)
                    {
                        point.Payload[$"metadata_{kvp.Key}"] = kvp.Value?.ToString() ?? string.Empty;
                    }
                }
            }

            // Upsert points in batches
            const int batchSize = 100;
            for (int i = 0; i < points.Count; i += batchSize)
            {
                var batch = points.Skip(i).Take(batchSize).ToList();
                await _client.UpsertAsync(_collectionName, batch, cancellationToken: cancellationToken);
                _logger?.LogDebug("Upserted batch of {Count} vectors to collection {Collection}", batch.Count, _collectionName);
            }

            _logger?.LogInformation("Added {Count} vectors to Qdrant collection {Collection}", vectorList.Count, _collectionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add vectors to Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <summary>
    /// Performs similarity search and returns the most similar documents.
    /// </summary>
    public async Task<IReadOnlyCollection<LCDocument>> GetSimilarDocumentsAsync(
        float[] embedding,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if collection exists
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (!collectionExists)
            {
                _logger?.LogDebug("Collection {Collection} does not exist, returning empty results", _collectionName);
                return Array.Empty<LCDocument>();
            }

            // Perform similarity search
            var searchResult = await _client.SearchAsync(
                _collectionName,
                embedding,
                limit: (ulong)amount,
                cancellationToken: cancellationToken);

            // Convert results to documents
            var documents = searchResult.Select(scored =>
            {
                var text = scored.Payload.TryGetValue("text", out var textValue)
                    ? textValue.StringValue
                    : string.Empty;

                var metadata = new Dictionary<string, object>();
                foreach (var kvp in scored.Payload)
                {
                    if (kvp.Key.StartsWith("metadata_"))
                    {
                        var key = kvp.Key["metadata_".Length..];
                        metadata[key] = kvp.Value.StringValue;
                    }
                    else if (kvp.Key != "text")
                    {
                        metadata[kvp.Key] = kvp.Value.StringValue;
                    }
                }

                metadata["score"] = scored.Score;

                return new LCDocument(text, metadata);
            }).ToList();

            _logger?.LogDebug("Found {Count} similar documents in collection {Collection}", documents.Count, _collectionName);
            return documents.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search vectors in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <summary>
    /// Clears all vectors from the collection by deleting and recreating it.
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (collectionExists)
            {
                await _client.DeleteCollectionAsync(_collectionName, cancellationToken: cancellationToken);
                _logger?.LogInformation("Deleted Qdrant collection {Collection}", _collectionName);
            }
            else
            {
                _logger?.LogDebug("Collection {Collection} does not exist, nothing to clear", _collectionName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <summary>
    /// Gets all vectors currently stored in the collection using scroll pagination.
    /// </summary>
    public IEnumerable<LCVector> GetAll()
    {
        // Use synchronous wrapper for async scroll - for truly large collections use ScrollAsync
        return GetAllAsync().GetAwaiter().GetResult();
    }

    private async Task<IEnumerable<LCVector>> GetAllAsync()
    {
        var results = new List<LCVector>();
        PointId? offset = null;

        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName);
            if (!collectionExists)
            {
                return results;
            }

            do
            {
                var scrollResult = await _client.ScrollAsync(
                    _collectionName,
                    limit: 100,
                    offset: offset,
                    payloadSelector: new WithPayloadSelector { Enable = true },
                    vectorsSelector: new WithVectorsSelector { Enable = true });

                foreach (var point in scrollResult.Result)
                {
                    var text = point.Payload.TryGetValue("text", out var textValue)
                        ? textValue.StringValue
                        : string.Empty;
                    var id = point.Payload.TryGetValue("id", out var idValue)
                        ? idValue.StringValue
                        : point.Id.Uuid ?? point.Id.Num.ToString();

                    float[]? embedding = null;
#pragma warning disable CS0612 // VectorOutput.Data is obsolete but still functional
                    if (point.Vectors?.Vector?.Data != null)
                    {
                        embedding = point.Vectors.Vector.Data.ToArray();
                    }
#pragma warning restore CS0612

                    results.Add(new LCVector
                    {
                        Id = id,
                        Text = text,
                        Embedding = embedding
                    });
                }

                offset = scrollResult.Result.Count > 0 ? scrollResult.Result.Last().Id : null;
            }
            while (offset != null);

            _logger?.LogDebug("Retrieved {Count} vectors from collection {Collection}", results.Count, _collectionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get all vectors from Qdrant collection {Collection}", _collectionName);
        }

        return results;
    }

    // ============ IAdvancedVectorStore Implementation ============

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<LCDocument>> SearchWithFilterAsync(
        float[] embedding,
        IDictionary<string, object>? filter = null,
        int amount = 5,
        float? scoreThreshold = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (!collectionExists)
            {
                return Array.Empty<LCDocument>();
            }

            var qdrantFilter = BuildFilter(filter);

            var searchResult = await _client.SearchAsync(
                _collectionName,
                embedding,
                filter: qdrantFilter,
                limit: (ulong)amount,
                scoreThreshold: scoreThreshold,
                cancellationToken: cancellationToken);

            return ConvertToDocuments(searchResult);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search with filter in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ulong> CountAsync(IDictionary<string, object>? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (!collectionExists)
            {
                return 0;
            }

            var qdrantFilter = BuildFilter(filter);
            var count = await _client.CountAsync(_collectionName, filter: qdrantFilter, exact: true, cancellationToken: cancellationToken);

            _logger?.LogDebug("Count in collection {Collection}: {Count}", _collectionName, count);
            return count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to count vectors in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ScrollResult> ScrollAsync(
        int limit = 10,
        string? offset = null,
        IDictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (!collectionExists)
            {
                return new ScrollResult(Array.Empty<LCDocument>(), null);
            }

            PointId? pointOffset = null;
            if (!string.IsNullOrEmpty(offset))
            {
                pointOffset = Guid.TryParse(offset, out var guid)
                    ? new PointId { Uuid = guid.ToString() }
                    : new PointId { Num = ulong.Parse(offset) };
            }

            var qdrantFilter = BuildFilter(filter);

            var scrollResult = await _client.ScrollAsync(
                _collectionName,
                filter: qdrantFilter,
                limit: (uint)limit,
                offset: pointOffset,
                payloadSelector: new WithPayloadSelector { Enable = true },
                cancellationToken: cancellationToken);

            var documents = scrollResult.Result.Select(point =>
            {
                var text = point.Payload.TryGetValue("text", out var textValue)
                    ? textValue.StringValue
                    : string.Empty;

                var metadata = ExtractMetadata(point.Payload);
                metadata["id"] = point.Id.Uuid ?? point.Id.Num.ToString();

                return new LCDocument(text, metadata);
            }).ToList();

            var nextOffset = scrollResult.Result.Count > 0
                ? scrollResult.Result.Last().Id.Uuid ?? scrollResult.Result.Last().Id.Num.ToString()
                : null;

            _logger?.LogDebug("Scrolled {Count} documents from collection {Collection}", documents.Count, _collectionName);
            return new ScrollResult(documents, nextOffset);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to scroll in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReadOnlyCollection<LCDocument>>> BatchSearchAsync(
        IReadOnlyList<float[]> embeddings,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (!collectionExists)
            {
                return embeddings.Select(_ => (IReadOnlyCollection<LCDocument>)Array.Empty<LCDocument>()).ToList();
            }

            var searches = embeddings.Select(e => new SearchPoints
            {
                CollectionName = _collectionName,
                Vector = { e },
                Limit = (ulong)amount,
                WithPayload = new WithPayloadSelector { Enable = true }
            }).ToList();

            var batchResults = await _client.SearchBatchAsync(_collectionName, searches, cancellationToken: cancellationToken);

            var results = batchResults.Select(batch =>
                (IReadOnlyCollection<LCDocument>)ConvertToDocuments(batch.Result)).ToList();

            _logger?.LogDebug("Batch search completed: {Queries} queries, {TotalResults} total results",
                embeddings.Count, results.Sum(r => r.Count));
            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to batch search in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<LCDocument>> RecommendAsync(
        IReadOnlyList<string> positiveIds,
        IReadOnlyList<string>? negativeIds = null,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (!collectionExists)
            {
                return Array.Empty<LCDocument>();
            }

            var positive = positiveIds.Select(id => Guid.TryParse(id, out var guid)
                ? new PointId { Uuid = guid.ToString() }
                : new PointId { Num = ulong.Parse(id) }).ToList();

            var negative = negativeIds?.Select(id => Guid.TryParse(id, out var guid)
                ? new PointId { Uuid = guid.ToString() }
                : new PointId { Num = ulong.Parse(id) }).ToList();

            var results = await _client.RecommendAsync(
                _collectionName,
                positive,
                negative,
                limit: (ulong)amount,
                payloadSelector: new WithPayloadSelector { Enable = true },
                cancellationToken: cancellationToken);

            _logger?.LogDebug("Recommend returned {Count} results from collection {Collection}", results.Count, _collectionName);
            return ConvertToDocuments(results);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to recommend in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteByIdAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        try
        {
            var pointIds = ids.Select(id => Guid.TryParse(id, out var guid)
                ? new PointId { Uuid = guid.ToString() }
                : new PointId { Num = ulong.Parse(id) }).ToList();

            await _client.DeleteAsync(_collectionName, pointIds, cancellationToken: cancellationToken);

            _logger?.LogInformation("Deleted {Count} vectors by ID from collection {Collection}", pointIds.Count, _collectionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete vectors by ID in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteByFilterAsync(IDictionary<string, object> filter, CancellationToken cancellationToken = default)
    {
        try
        {
            var qdrantFilter = BuildFilter(filter);
            if (qdrantFilter == null)
            {
                throw new ArgumentException("Filter cannot be empty for delete operation", nameof(filter));
            }

            await _client.DeleteAsync(_collectionName, qdrantFilter, cancellationToken: cancellationToken);

            _logger?.LogInformation("Deleted vectors by filter from collection {Collection}", _collectionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete vectors by filter in Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<VectorStoreInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
            if (!collectionExists)
            {
                return new VectorStoreInfo(_collectionName, 0, 0, "NotFound");
            }

            var info = await _client.GetCollectionInfoAsync(_collectionName, cancellationToken);
            var count = await _client.CountAsync(_collectionName, exact: true, cancellationToken: cancellationToken);

            var vectorDim = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;
            var status = info.Status.ToString();

            var additionalInfo = new Dictionary<string, object>
            {
                ["segments_count"] = info.SegmentsCount,
                ["points_count"] = info.PointsCount,
                ["indexed_vectors_count"] = info.IndexedVectorsCount,
                ["optimizer_status"] = info.OptimizerStatus?.Ok ?? false ? "OK" : "Optimizing"
            };

            _logger?.LogDebug("Collection info for {Collection}: {Count} vectors, {Dim}D, status={Status}",
                _collectionName, count, vectorDim, status);

            return new VectorStoreInfo(_collectionName, count, (int)vectorDim, status, additionalInfo);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get info for Qdrant collection {Collection}", _collectionName);
            throw;
        }
    }

    // ============ Helper Methods ============

    /// <summary>
    /// Ensures the collection exists with proper vector dimensions.
    /// </summary>
    private async Task EnsureCollectionExistsAsync(LCVector sampleVector, CancellationToken cancellationToken)
    {
        var collectionExists = await _client.CollectionExistsAsync(_collectionName, cancellationToken);
        if (!collectionExists)
        {
            var vectorSize = sampleVector.Embedding?.Length ?? 0;
            if (vectorSize == 0)
            {
                throw new InvalidOperationException("Cannot create collection: sample vector has no embedding");
            }

            _vectorDimension = vectorSize;
            _logger?.LogInformation("Creating Qdrant collection {Collection} with vector size {Size}", _collectionName, vectorSize);

            await _client.CreateCollectionAsync(
                _collectionName,
                new VectorParams
                {
                    Size = (ulong)vectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);

            _logger?.LogInformation("Created Qdrant collection {Collection}", _collectionName);
        }
    }

    private static Filter? BuildFilter(IDictionary<string, object>? filter)
    {
        if (filter == null || filter.Count == 0)
        {
            return null;
        }

        var conditions = new List<Condition>();

        foreach (var (key, value) in filter)
        {
            var fieldName = key.StartsWith("metadata_") ? key : $"metadata_{key}";

            // Build conditions based on value type
            var condition = value switch
            {
                int i => MatchValue(fieldName, i),
                long l => MatchValue(fieldName, l),
                double d => Range(fieldName, new Qdrant.Client.Grpc.Range { Gte = d, Lte = d }),
                bool b => MatchValue(fieldName, b),
                _ => MatchKeyword(fieldName, value?.ToString() ?? string.Empty)
            };

            conditions.Add(condition);
        }

        return new Filter { Must = { conditions } };
    }

    private static Condition MatchKeyword(string field, string value) =>
        new() { Field = new FieldCondition { Key = field, Match = new Match { Keyword = value } } };

    private static Condition MatchValue(string field, long value) =>
        new() { Field = new FieldCondition { Key = field, Match = new Match { Integer = value } } };

    private static Condition MatchValue(string field, bool value) =>
        new() { Field = new FieldCondition { Key = field, Match = new Match { Boolean = value } } };

    private static IReadOnlyList<LCDocument> ConvertToDocuments(IEnumerable<ScoredPoint> scoredPoints)
    {
        return scoredPoints.Select(scored =>
        {
            var text = scored.Payload.TryGetValue("text", out var textValue)
                ? textValue.StringValue
                : string.Empty;

            var metadata = ExtractMetadata(scored.Payload);
            metadata["score"] = scored.Score;

            return new LCDocument(text, metadata);
        }).ToList();
    }

    private static Dictionary<string, object> ExtractMetadata(Google.Protobuf.Collections.MapField<string, Value> payload)
    {
        var metadata = new Dictionary<string, object>();

        foreach (var kvp in payload)
        {
            if (kvp.Key.StartsWith("metadata_"))
            {
                var key = kvp.Key["metadata_".Length..];
                metadata[key] = kvp.Value.StringValue;
            }
            else if (kvp.Key != "text")
            {
                metadata[kvp.Key] = kvp.Value.StringValue;
            }
        }

        return metadata;
    }

    /// <summary>
    /// Disposes the Qdrant client if owned by this instance.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposeClient && _client != null)
        {
            _client.Dispose();
        }

        await Task.CompletedTask;
    }
}
