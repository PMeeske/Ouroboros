// <copyright file="MemoryCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Persistence;
using Ouroboros.Domain.Vectors;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for Ouroboros memory administration.
/// Enables self-management of vector collections, health checks, and memory architecture.
/// </summary>
public static class MemoryCliSteps
{
    private static OuroborosMemoryManager? _memoryManager;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or initializes the memory manager singleton.
    /// </summary>
    private static async Task<OuroborosMemoryManager> GetMemoryManagerAsync(string endpoint = "http://localhost:6333")
    {
        if (_memoryManager == null)
        {
            lock (_lock)
            {
                _memoryManager ??= new OuroborosMemoryManager(endpoint);
            }
            await _memoryManager.InitializeAsync();
        }
        return _memoryManager;
    }

    /// <summary>
    /// Display the memory architecture map.
    /// Usage: MemoryMap()
    /// </summary>
    [PipelineToken("MemoryMap", "ShowMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemoryMap(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var map = await manager.GetMemoryMapAsync();
                Console.WriteLine(map);
                s.Context = map;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to generate memory map: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Perform a health check on all memory collections.
    /// Usage: MemoryHealth()
    /// Usage: MemoryHealth('heal') - auto-heal dimension mismatches
    /// </summary>
    [PipelineToken("MemoryHealth", "CheckMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemoryHealth(string? args = null)
        => async s =>
        {
            try
            {
                var autoHeal = args?.ToLowerInvariant().Contains("heal") == true;
                var manager = await GetMemoryManagerAsync();
                var report = await manager.PerformHealthCheckAsync(autoHeal);

                Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
                Console.WriteLine("║           OUROBOROS MEMORY HEALTH CHECK               ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine($"║  Healthy Collections:   {report.HealthyCollections,5}                        ║");
                Console.WriteLine($"║  Unhealthy Collections: {report.UnhealthyCollections,5}                        ║");
                Console.WriteLine($"║  Total Vectors:         {report.Statistics.TotalVectors,10}                 ║");
                Console.WriteLine($"║  Collection Links:      {report.Statistics.CollectionLinks,5}                        ║");

                if (report.HealedCollections.Any())
                {
                    Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                    Console.WriteLine("║  AUTO-HEALED:                                         ║");
                    foreach (var healed in report.HealedCollections)
                    {
                        Console.WriteLine($"║    ✓ {healed,-47} ║");
                    }
                }

                if (report.RemainingIssues.Any())
                {
                    Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                    Console.WriteLine("║  ISSUES REMAINING:                                    ║");
                    foreach (var issue in report.RemainingIssues)
                    {
                        Console.WriteLine($"║    ⚠ {issue,-47} ║");
                    }
                }

                Console.WriteLine("╚═══════════════════════════════════════════════════════╝");

                s.Context = $"Health check complete. Healthy: {report.HealthyCollections}, Unhealthy: {report.UnhealthyCollections}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Health check failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// List all memory collections with details.
    /// Usage: MemoryList()
    /// Usage: MemoryList('layer=procedural') - filter by memory layer
    /// </summary>
    [PipelineToken("MemoryList", "ListCollections")]
    public static Step<CliPipelineState, CliPipelineState> MemoryList(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var collections = await manager.Admin.GetAllCollectionsAsync();

                // Parse layer filter
                MemoryLayer? filterLayer = null;
                if (!string.IsNullOrEmpty(args))
                {
                    var layerArg = args.ToLowerInvariant().Replace("layer=", "");
                    filterLayer = layerArg switch
                    {
                        "working" => MemoryLayer.Working,
                        "episodic" => MemoryLayer.Episodic,
                        "semantic" => MemoryLayer.Semantic,
                        "procedural" => MemoryLayer.Procedural,
                        "autobiographical" or "self" => MemoryLayer.Autobiographical,
                        _ => null
                    };
                }

                Console.WriteLine("┌────────────────────────────────────┬────────┬──────────┬────────────┐");
                Console.WriteLine("│ Collection                         │ Dim    │ Vectors  │ Layer      │");
                Console.WriteLine("├────────────────────────────────────┼────────┼──────────┼────────────┤");

                foreach (var col in collections.OrderBy(c => c.Name))
                {
                    var layer = manager.GetLayerForCollection(col.Name);

                    // Apply filter
                    if (filterLayer.HasValue && layer != filterLayer.Value) continue;

                    var layerStr = layer?.ToString() ?? "Other";
                    var status = col.Status == Qdrant.Client.Grpc.CollectionStatus.Green ? " " : "⚠";
                    Console.WriteLine($"│{status}{col.Name,-35}│ {col.VectorSize,6} │ {col.PointsCount,8} │ {layerStr,-10} │");
                }

                Console.WriteLine("└────────────────────────────────────┴────────┴──────────┴────────────┘");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to list collections: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Create a new memory collection.
    /// Usage: MemoryCreate('collection_name')
    /// Usage: MemoryCreate('collection_name;dim=768')
    /// </summary>
    [PipelineToken("MemoryCreate", "CreateCollection")]
    public static Step<CliPipelineState, CliPipelineState> MemoryCreate(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryCreate('collection_name') or MemoryCreate('name;dim=768')");
                return s;
            }

            try
            {
                var parts = args.Split(';');
                var name = parts[0].Trim();
                var dim = 768;

                foreach (var part in parts.Skip(1))
                {
                    if (part.StartsWith("dim=") && int.TryParse(part[4..], out var d))
                    {
                        dim = d;
                    }
                }

                var manager = await GetMemoryManagerAsync();
                var success = await manager.Admin.CreateCollectionAsync(name, dim);

                if (success)
                {
                    Console.WriteLine($"[memory] ✓ Created collection '{name}' with {dim} dimensions");
                }
                else
                {
                    Console.WriteLine($"[memory] Collection '{name}' already exists or creation failed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to create collection: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Delete a memory collection.
    /// Usage: MemoryDelete('collection_name')
    /// </summary>
    [PipelineToken("MemoryDelete", "DeleteCollection")]
    public static Step<CliPipelineState, CliPipelineState> MemoryDelete(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryDelete('collection_name')");
                return s;
            }

            try
            {
                var name = args.Trim();
                var manager = await GetMemoryManagerAsync();
                var success = await manager.Admin.DeleteCollectionAsync(name);

                if (success)
                {
                    Console.WriteLine($"[memory] ✓ Deleted collection '{name}'");
                }
                else
                {
                    Console.WriteLine($"[memory] Collection '{name}' not found or deletion failed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to delete collection: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Auto-heal all collections with dimension mismatches.
    /// WARNING: This deletes data in mismatched collections!
    /// Usage: MemoryHeal()
    /// Usage: MemoryHeal('768') - heal to specific dimension
    /// </summary>
    [PipelineToken("MemoryHeal", "HealMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemoryHeal(string? args = null)
        => async s =>
        {
            try
            {
                var dim = 768;
                if (!string.IsNullOrEmpty(args) && int.TryParse(args.Trim(), out var d))
                {
                    dim = d;
                }

                var manager = await GetMemoryManagerAsync();
                var healed = await manager.Admin.AutoHealDimensionMismatchesAsync(dim);

                if (healed.Any())
                {
                    Console.WriteLine($"[memory] ✓ Auto-healed {healed.Count} collections to {dim} dimensions:");
                    foreach (var col in healed)
                    {
                        Console.WriteLine($"         - {col}");
                    }
                }
                else
                {
                    Console.WriteLine("[memory] No collections needed healing");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Auto-heal failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Link two collections in the memory graph.
    /// Usage: MemoryLink('source;target;relation_type')
    /// Example: MemoryLink('ouroboros_skills;tools;depends_on')
    /// </summary>
    [PipelineToken("MemoryLink", "LinkCollections")]
    public static Step<CliPipelineState, CliPipelineState> MemoryLink(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryLink('source;target;relation_type')");
                Console.WriteLine("[memory] Relation types: depends_on, indexes, extends, mirrors, aggregates, part_of, related_to");
                return s;
            }

            try
            {
                var parts = args.Split(';');
                if (parts.Length < 3)
                {
                    Console.WriteLine("[memory] Invalid format. Use: MemoryLink('source;target;relation_type')");
                    return s;
                }

                var source = parts[0].Trim();
                var target = parts[1].Trim();
                var relation = parts[2].Trim();
                var description = parts.Length > 3 ? parts[3].Trim() : null;

                var manager = await GetMemoryManagerAsync();
                manager.LinkCollections(source, target, relation, description);

                Console.WriteLine($"[memory] ✓ Linked {source} ─{relation}→ {target}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to link collections: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Show links for a collection.
    /// Usage: MemoryLinks('collection_name')
    /// </summary>
    [PipelineToken("MemoryLinks", "ShowLinks")]
    public static Step<CliPipelineState, CliPipelineState> MemoryLinks(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();

                if (string.IsNullOrWhiteSpace(args))
                {
                    // Show all links
                    var allLinks = manager.Admin.CollectionLinks;
                    Console.WriteLine($"[memory] All collection links ({allLinks.Count}):");
                    foreach (var link in allLinks)
                    {
                        Console.WriteLine($"         {link.SourceCollection} ─{link.RelationType}→ {link.TargetCollection}");
                    }
                }
                else
                {
                    // Show links for specific collection
                    var links = manager.GetRelatedCollections(args.Trim());
                    Console.WriteLine($"[memory] Links for '{args.Trim()}' ({links.Count}):");
                    foreach (var link in links)
                    {
                        var direction = link.SourceCollection == args.Trim() ? "→" : "←";
                        var other = link.SourceCollection == args.Trim() ? link.TargetCollection : link.SourceCollection;
                        Console.WriteLine($"         {direction} {other} ({link.RelationType})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to get links: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Get memory statistics.
    /// Usage: MemoryStats()
    /// </summary>
    [PipelineToken("MemoryStats", "MemoryStatistics")]
    public static Step<CliPipelineState, CliPipelineState> MemoryStats(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var stats = await manager.Admin.GetMemoryStatisticsAsync();

                Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
                Console.WriteLine("║           OUROBOROS MEMORY STATISTICS                 ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine($"║  Total Collections:     {stats.TotalCollections,10}                   ║");
                Console.WriteLine($"║  Total Vectors:         {stats.TotalVectors,10}                   ║");
                Console.WriteLine($"║  Healthy Collections:   {stats.HealthyCollections,10}                   ║");
                Console.WriteLine($"║  Unhealthy Collections: {stats.UnhealthyCollections,10}                   ║");
                Console.WriteLine($"║  Collection Links:      {stats.CollectionLinks,10}                   ║");
                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine("║  VECTOR DIMENSIONS:                                   ║");

                foreach (var (dim, count) in stats.DimensionDistribution.OrderByDescending(d => d.Value))
                {
                    Console.WriteLine($"║    {dim,4}d: {count,3} collections                              ║");
                }

                Console.WriteLine("╠═══════════════════════════════════════════════════════╣");
                Console.WriteLine("║  MEMORY LAYERS:                                       ║");

                foreach (var layer in Enum.GetValues<MemoryLayer>())
                {
                    var count = await manager.GetLayerVectorCountAsync(layer);
                    Console.WriteLine($"║    {layer,-20}: {count,10} vectors          ║");
                }

                Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to get statistics: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Create a memory snapshot for backup/analysis.
    /// Usage: MemorySnapshot()
    /// </summary>
    [PipelineToken("MemorySnapshot", "SnapshotMemory")]
    public static Step<CliPipelineState, CliPipelineState> MemorySnapshot(string? args = null)
        => async s =>
        {
            try
            {
                var manager = await GetMemoryManagerAsync();
                var snapshot = await manager.CreateSnapshotAsync();

                Console.WriteLine($"[memory] ✓ Snapshot created at {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"         Collections: {snapshot.Collections.Count}");
                Console.WriteLine($"         Links: {snapshot.Links.Count}");
                Console.WriteLine($"         Total vectors: {snapshot.Statistics.TotalVectors}");

                // Store snapshot info in context
                s.Context = $"Memory snapshot: {snapshot.Collections.Count} collections, {snapshot.Statistics.TotalVectors} vectors";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to create snapshot: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Clear a specific memory layer (dangerous!).
    /// Usage: MemoryClear('layer_name;confirm')
    /// Example: MemoryClear('procedural;confirm')
    /// </summary>
    [PipelineToken("MemoryClear", "ClearMemoryLayer")]
    public static Step<CliPipelineState, CliPipelineState> MemoryClear(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[memory] Usage: MemoryClear('layer_name;confirm')");
                Console.WriteLine("[memory] Layers: working, episodic, semantic, procedural, autobiographical");
                return s;
            }

            try
            {
                var parts = args.Split(';');
                var layerName = parts[0].Trim().ToLowerInvariant();
                var confirmed = parts.Length > 1 && parts[1].Trim().ToLowerInvariant() == "confirm";

                var layer = layerName switch
                {
                    "working" => MemoryLayer.Working,
                    "episodic" => MemoryLayer.Episodic,
                    "semantic" => MemoryLayer.Semantic,
                    "procedural" => MemoryLayer.Procedural,
                    "autobiographical" or "self" => MemoryLayer.Autobiographical,
                    _ => (MemoryLayer?)null
                };

                if (!layer.HasValue)
                {
                    Console.WriteLine($"[memory] Unknown layer: {layerName}");
                    return s;
                }

                if (!confirmed)
                {
                    Console.WriteLine($"[memory] ⚠ This will DELETE ALL DATA in the {layer.Value} memory layer!");
                    Console.WriteLine($"[memory] To confirm, use: MemoryClear('{layerName};confirm')");
                    return s;
                }

                var manager = await GetMemoryManagerAsync();
                var success = await manager.ClearMemoryLayerAsync(layer.Value, confirmed: true);

                if (success)
                {
                    Console.WriteLine($"[memory] ✓ Cleared {layer.Value} memory layer");
                }
                else
                {
                    Console.WriteLine($"[memory] Failed to clear {layer.Value} memory layer");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[memory] Failed to clear layer: {ex.Message}");
            }

            return s;
        };
}
