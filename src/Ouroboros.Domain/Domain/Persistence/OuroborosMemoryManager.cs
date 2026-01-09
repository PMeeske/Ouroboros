// <copyright file="OuroborosMemoryManager.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Vectors;

namespace Ouroboros.Domain.Persistence;

/// <summary>
/// Memory layer types in Ouroboros's cognitive architecture.
/// </summary>
public enum MemoryLayer
{
    /// <summary>Immediate working memory - active thoughts.</summary>
    Working,

    /// <summary>Short-term episodic memory - recent conversations.</summary>
    Episodic,

    /// <summary>Long-term semantic memory - knowledge and facts.</summary>
    Semantic,

    /// <summary>Procedural memory - skills and tool patterns.</summary>
    Procedural,

    /// <summary>Self-referential memory - identity and self-model.</summary>
    Autobiographical,
}

/// <summary>
/// Maps memory layers to their underlying collections.
/// </summary>
public sealed record MemoryLayerMapping(
    MemoryLayer Layer,
    IReadOnlyList<string> Collections,
    string Description,
    double RetentionPriority);

/// <summary>
/// Ouroboros's unified memory management system.
/// Provides a cognitive architecture over the underlying vector collections.
/// </summary>
public sealed class OuroborosMemoryManager : IAsyncDisposable
{
    private readonly QdrantCollectionAdmin _admin;
    private readonly Dictionary<MemoryLayer, MemoryLayerMapping> _layerMappings;
    private bool _initialized;

    /// <summary>
    /// Default memory layer mappings for Ouroboros.
    /// </summary>
    public static readonly IReadOnlyList<MemoryLayerMapping> DefaultLayerMappings = new List<MemoryLayerMapping>
    {
        new(MemoryLayer.Working, new[] { "ouroboros_neuro_thoughts" },
            "Active thought processes and immediate reasoning", 1.0),

        new(MemoryLayer.Episodic, new[] { "ouroboros_conversations", "ouroboros_thought_results" },
            "Recent interactions and their outcomes", 0.9),

        new(MemoryLayer.Semantic, new[] { "core", "fullcore", "codebase", "qdrant_documentation" },
            "Learned facts, concepts, and domain knowledge", 0.7),

        new(MemoryLayer.Procedural, new[] { "ouroboros_skills", "ouroboros_tool_patterns", "tools" },
            "Learned skills, tool usage patterns, and procedures", 0.8),

        new(MemoryLayer.Autobiographical, new[] { "ouroboros_personalities", "ouroboros_persons", "ouroboros_selfindex" },
            "Self-model, identity, and known entities", 0.95),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosMemoryManager"/> class.
    /// </summary>
    /// <param name="qdrantEndpoint">Qdrant server endpoint.</param>
    public OuroborosMemoryManager(string qdrantEndpoint = "http://localhost:6333")
    {
        _admin = new QdrantCollectionAdmin(qdrantEndpoint);
        _layerMappings = DefaultLayerMappings.ToDictionary(m => m.Layer);
    }

    /// <summary>
    /// Gets the underlying collection admin.
    /// </summary>
    public QdrantCollectionAdmin Admin => _admin;

    /// <summary>
    /// Initializes the memory system, ensuring all required collections exist.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _admin.InitializeAsync(ct);

        // Ensure all memory layer collections exist
        foreach (var mapping in _layerMappings.Values)
        {
            foreach (var collection in mapping.Collections)
            {
                var info = await _admin.GetCollectionInfoAsync(collection, ct);
                if (info == null)
                {
                    // Create missing collection
                    QdrantCollectionAdmin.KnownCollections.TryGetValue(collection, out var purpose);
                    await _admin.CreateCollectionAsync(collection, purpose: purpose ?? mapping.Description, ct: ct);
                }
            }
        }

        _initialized = true;
    }

    /// <summary>
    /// Performs a full memory health check and auto-heals issues.
    /// </summary>
    public async Task<MemoryHealthReport> PerformHealthCheckAsync(
        bool autoHeal = false,
        CancellationToken ct = default)
    {
        var healthReports = await _admin.HealthCheckAsync(ct: ct);
        var unhealthyCollections = healthReports.Where(r => !r.IsHealthy).ToList();
        var healedCollections = new List<string>();

        if (autoHeal && unhealthyCollections.Any())
        {
            healedCollections = (await _admin.AutoHealDimensionMismatchesAsync(ct: ct)).ToList();
        }

        var stats = await _admin.GetMemoryStatisticsAsync(ct);

        return new MemoryHealthReport(
            healthReports.Count(r => r.IsHealthy),
            unhealthyCollections.Count,
            healedCollections,
            unhealthyCollections.Select(r => r.CollectionName).ToList(),
            stats);
    }

    /// <summary>
    /// Gets collections for a specific memory layer.
    /// </summary>
    public IReadOnlyList<string> GetCollectionsForLayer(MemoryLayer layer)
    {
        return _layerMappings.TryGetValue(layer, out var mapping)
            ? mapping.Collections
            : Array.Empty<string>();
    }

    /// <summary>
    /// Determines which memory layer a collection belongs to.
    /// </summary>
    public MemoryLayer? GetLayerForCollection(string collectionName)
    {
        foreach (var (layer, mapping) in _layerMappings)
        {
            if (mapping.Collections.Contains(collectionName))
            {
                return layer;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the memory map visualization.
    /// </summary>
    public async Task<string> GetMemoryMapAsync(CancellationToken ct = default)
    {
        return await _admin.GenerateMemoryMapAsync(ct);
    }

    /// <summary>
    /// Gets total vector count across all memory layers.
    /// </summary>
    public async Task<long> GetTotalMemoryVectorsAsync(CancellationToken ct = default)
    {
        var stats = await _admin.GetMemoryStatisticsAsync(ct);
        return stats.TotalVectors;
    }

    /// <summary>
    /// Gets vector count for a specific memory layer.
    /// </summary>
    public async Task<long> GetLayerVectorCountAsync(MemoryLayer layer, CancellationToken ct = default)
    {
        var collections = GetCollectionsForLayer(layer);
        long total = 0;

        foreach (var collection in collections)
        {
            var info = await _admin.GetCollectionInfoAsync(collection, ct);
            if (info != null)
            {
                total += (long)info.PointsCount;
            }
        }

        return total;
    }

    /// <summary>
    /// Clears a specific memory layer (with confirmation).
    /// </summary>
    public async Task<bool> ClearMemoryLayerAsync(
        MemoryLayer layer,
        bool confirmed = false,
        CancellationToken ct = default)
    {
        if (!confirmed) return false;

        var collections = GetCollectionsForLayer(layer);
        var success = true;

        foreach (var collection in collections)
        {
            var deleted = await _admin.DeleteCollectionAsync(collection, ct);
            if (deleted)
            {
                // Recreate empty collection
                QdrantCollectionAdmin.KnownCollections.TryGetValue(collection, out var purpose);
                await _admin.CreateCollectionAsync(collection, purpose: purpose, ct: ct);
            }
            else
            {
                success = false;
            }
        }

        return success;
    }

    /// <summary>
    /// Creates a snapshot of the memory system state.
    /// </summary>
    public async Task<MemorySnapshot> CreateSnapshotAsync(CancellationToken ct = default)
    {
        var collections = await _admin.GetAllCollectionsAsync(ct);
        var stats = await _admin.GetMemoryStatisticsAsync(ct);
        var links = _admin.CollectionLinks;

        var layerStats = new Dictionary<MemoryLayer, long>();
        foreach (var layer in Enum.GetValues<MemoryLayer>())
        {
            layerStats[layer] = await GetLayerVectorCountAsync(layer, ct);
        }

        return new MemorySnapshot(
            DateTime.UtcNow,
            collections.ToList(),
            links.ToList(),
            layerStats,
            stats);
    }

    /// <summary>
    /// Links two collections in the memory graph.
    /// </summary>
    public void LinkCollections(string source, string target, string relationType, string? description = null)
    {
        _admin.AddCollectionLink(new CollectionLink(source, target, relationType, Description: description));
    }

    /// <summary>
    /// Gets all collections related to a given collection.
    /// </summary>
    public IReadOnlyList<CollectionLink> GetRelatedCollections(string collectionName)
    {
        return _admin.GetLinkedCollections(collectionName);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _admin.DisposeAsync();
    }
}

/// <summary>
/// Report of a memory health check operation.
/// </summary>
public sealed record MemoryHealthReport(
    int HealthyCollections,
    int UnhealthyCollections,
    IReadOnlyList<string> HealedCollections,
    IReadOnlyList<string> RemainingIssues,
    MemoryStatistics Statistics);

/// <summary>
/// A point-in-time snapshot of the memory system.
/// </summary>
public sealed record MemorySnapshot(
    DateTime Timestamp,
    IReadOnlyList<CollectionInfo> Collections,
    IReadOnlyList<CollectionLink> Links,
    IReadOnlyDictionary<MemoryLayer, long> VectorsByLayer,
    MemoryStatistics Statistics);
