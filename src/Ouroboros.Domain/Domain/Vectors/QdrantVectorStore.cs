// <copyright file="QdrantVectorStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using LCVector = LangChain.Databases.Vector;
using LCDocument = LangChain.DocumentLoaders.Document;

namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// Qdrant vector store implementation for production use.
/// Provides persistent vector storage with similarity search capabilities.
/// </summary>
public sealed class QdrantVectorStore : IVectorStore, IAsyncDisposable
{
    private readonly QdrantClient _client;
    private readonly ILogger? _logger;
    private readonly string _collectionName;
    private readonly bool _disposeClient;

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
    /// Gets all vectors currently stored in the collection.
    /// Note: This can be expensive for large collections and is not recommended for production use with Qdrant.
    /// </summary>
    public IEnumerable<LCVector> GetAll()
    {
        // Note: Qdrant doesn't have a built-in "get all" operation
        // This is a simplified implementation that returns empty for Qdrant
        // In a real implementation, you would need to use pagination/scrolling
        _logger?.LogWarning("GetAll() called on Qdrant store - this operation is not efficient and returns empty. Use search instead.");
        return Enumerable.Empty<LCVector>();
    }

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
