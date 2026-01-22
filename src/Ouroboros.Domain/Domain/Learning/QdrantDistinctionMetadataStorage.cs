// <copyright file="QdrantDistinctionMetadataStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Ouroboros.Domain.Learning;

/// <summary>
/// Qdrant-based storage for distinction metadata with semantic search capabilities.
/// </summary>
public sealed class QdrantDistinctionMetadataStorage
{
    private const string CollectionName = "distinction_states";
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantDistinctionMetadataStorage> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantDistinctionMetadataStorage"/> class.
    /// </summary>
    /// <param name="connectionString">Qdrant connection string.</param>
    /// <param name="logger">Logger instance.</param>
    public QdrantDistinctionMetadataStorage(
        string connectionString,
        ILogger<QdrantDistinctionMetadataStorage> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Parse connection string
        var uri = new Uri(connectionString);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6333; // Default Qdrant port
        var useHttps = uri.Scheme == "https";

        _client = new QdrantClient(host, port, useHttps);
        _logger.LogInformation("Initialized Qdrant metadata storage: {Host}:{Port}", host, port);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantDistinctionMetadataStorage"/> class with existing client.
    /// </summary>
    /// <param name="client">Existing Qdrant client.</param>
    /// <param name="logger">Logger instance.</param>
    public QdrantDistinctionMetadataStorage(
        QdrantClient client,
        ILogger<QdrantDistinctionMetadataStorage> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stores distinction metadata with semantic embedding for retrieval.
    /// </summary>
    /// <param name="weights">The distinction weights containing metadata.</param>
    /// <param name="storagePath">The file system path where weights are stored.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> StoreMetadataAsync(
        DistinctionWeights weights,
        string storagePath,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionExistsAsync(weights.Embedding.Length, ct);

            var pointId = BitConverter.ToUInt64(weights.Id.Value.ToByteArray(), 0);

            var payload = new Dictionary<string, Value>
            {
                ["id"] = weights.Id.ToString(),
                ["circumstance"] = weights.Circumstance,
                ["storage_path"] = storagePath,
                ["learned_at_stage"] = (int)weights.LearnedAtStage,
                ["fitness"] = weights.Fitness,
                ["is_dissolved"] = false,
                ["created_at"] = weights.CreatedAt.ToString("O"),
                ["dissolved_at"] = string.Empty
            };

            var point = new PointStruct
            {
                Id = new PointId { Num = pointId },
                Vectors = weights.Embedding,
                Payload = { payload }
            };

            await _client.UpsertAsync(CollectionName, new[] { point }, cancellationToken: ct);

            _logger.LogInformation("Stored metadata for distinction {Id}", weights.Id);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store metadata for distinction {Id}", weights.Id);
            return Result<Unit, string>.Failure($"Metadata storage failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves similar distinctions by semantic search.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing similar distinctions or an error.</returns>
    public async Task<Result<IReadOnlyList<DistinctionMetadata>, string>> SearchSimilarAsync(
        string query,
        int topK = 5,
        CancellationToken ct = default)
    {
        try
        {
            // For semantic search, we'd need an embedding model here
            // For now, we'll return an empty list as this requires additional infrastructure
            _logger.LogWarning("Semantic search not yet implemented - requires embedding model");
            return await Task.FromResult(Result<IReadOnlyList<DistinctionMetadata>, string>.Success(
                (IReadOnlyList<DistinctionMetadata>)Array.Empty<DistinctionMetadata>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search similar distinctions");
            return Result<IReadOnlyList<DistinctionMetadata>, string>.Failure($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks a distinction as dissolved.
    /// </summary>
    /// <param name="id">The distinction ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> MarkDissolvedAsync(
        DistinctionId id,
        CancellationToken ct = default)
    {
        try
        {
            // For simplicity, we'll delete and note this in logs
            // In a production system, you might want to update the payload
            var pointId = BitConverter.ToUInt64(id.Value.ToByteArray(), 0);

            await _client.DeleteAsync(
                CollectionName,
                new[] { new PointId { Num = pointId } },
                cancellationToken: ct);

            _logger.LogInformation("Marked distinction {Id} as dissolved (deleted from Qdrant)", id);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark distinction {Id} as dissolved", id);
            return Result<Unit, string>.Failure($"Update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets metadata by ID.
    /// </summary>
    /// <param name="id">The distinction ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the metadata or an error.</returns>
    public async Task<Result<DistinctionMetadata, string>> GetByIdAsync(
        DistinctionId id,
        CancellationToken ct = default)
    {
        try
        {
            var pointId = BitConverter.ToUInt64(id.Value.ToByteArray(), 0);

            var points = await _client.RetrieveAsync(
                CollectionName,
                new[] { new PointId { Num = pointId } },
                withPayload: true,
                cancellationToken: ct);

            if (points.Count == 0)
            {
                return Result<DistinctionMetadata, string>.Failure($"Distinction {id} not found");
            }

            var point = points[0];
            var metadata = PayloadToMetadata(id, point.Payload);

            _logger.LogInformation("Retrieved metadata for distinction {Id}", id);
            return Result<DistinctionMetadata, string>.Success(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for distinction {Id}", id);
            return Result<DistinctionMetadata, string>.Failure($"Retrieval failed: {ex.Message}");
        }
    }

    private async Task EnsureCollectionExistsAsync(int vectorDimension, CancellationToken ct)
    {
        try
        {
            var exists = await _client.CollectionExistsAsync(CollectionName, ct);

            if (!exists)
            {
                await _client.CreateCollectionAsync(
                    CollectionName,
                    new VectorParams { Size = (ulong)vectorDimension, Distance = Distance.Cosine },
                    cancellationToken: ct);

                _logger.LogInformation("Created collection {Collection} with dimension {Dimension}",
                    CollectionName, vectorDimension);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure collection exists");
        }
    }

    private DistinctionMetadata PayloadToMetadata(DistinctionId id, IDictionary<string, Value> payload)
    {
        var circumstance = payload.TryGetValue("circumstance", out var c) ? c.StringValue : string.Empty;
        var storagePath = payload.TryGetValue("storage_path", out var sp) ? sp.StringValue : string.Empty;
        var learnedAtStage = payload.TryGetValue("learned_at_stage", out var stage)
            ? (int)stage.IntegerValue
            : 1; // Default to Distinction stage
        var fitness = payload.TryGetValue("fitness", out var f) ? f.DoubleValue : 0.0;
        var isDissolved = payload.TryGetValue("is_dissolved", out var dissolved) && dissolved.BoolValue;
        var createdAt = payload.TryGetValue("created_at", out var ca)
            ? DateTime.Parse(ca.StringValue)
            : DateTime.UtcNow;
        var dissolvedAt = payload.TryGetValue("dissolved_at", out var da) && !string.IsNullOrEmpty(da.StringValue)
            ? DateTime.Parse(da.StringValue)
            : (DateTime?)null;

        return new DistinctionMetadata(
            Id: id,
            Circumstance: circumstance,
            StoragePath: storagePath,
            LearnedAtStage: learnedAtStage,
            Fitness: fitness,
            IsDissolved: isDissolved,
            CreatedAt: createdAt,
            DissolvedAt: dissolvedAt);
    }
}

/// <summary>
/// Metadata about a stored distinction.
/// </summary>
public sealed record DistinctionMetadata(
    DistinctionId Id,
    string Circumstance,
    string StoragePath,
    int LearnedAtStage,
    double Fitness,
    bool IsDissolved,
    DateTime CreatedAt,
    DateTime? DissolvedAt);
