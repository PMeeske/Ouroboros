// <copyright file="QdrantNeuroSymbolicThoughtStore.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Ouroboros.Domain.Persistence;

/// <summary>
/// Configuration for the neuro-symbolic thought store.
/// </summary>
/// <param name="Endpoint">Qdrant server endpoint.</param>
/// <param name="ThoughtsCollection">Collection for thought nodes.</param>
/// <param name="RelationsCollection">Collection for symbolic relations between thoughts.</param>
/// <param name="ResultsCollection">Collection for thought outcomes/results.</param>
/// <param name="VectorSize">Embedding dimension (default 768 for nomic-embed-text).</param>
public sealed record QdrantNeuroSymbolicConfig(
    string Endpoint = "http://localhost:6334",
    string ThoughtsCollection = "ouroboros_neuro_thoughts",
    string RelationsCollection = "ouroboros_thought_relations",
    string ResultsCollection = "ouroboros_thought_results",
    int VectorSize = 768);

/// <summary>
/// Represents a symbolic relation between thoughts in the neuro-symbolic map.
/// </summary>
public sealed record ThoughtRelation(
    Guid Id,
    Guid SourceThoughtId,
    Guid TargetThoughtId,
    string RelationType,
    double Strength,
    DateTime CreatedAt,
    Dictionary<string, object>? Metadata = null)
{
    /// <summary>Common relation types for the symbolic layer.</summary>
    public static class Types
    {
        public const string CausedBy = "caused_by";
        public const string LeadsTo = "leads_to";
        public const string Contradicts = "contradicts";
        public const string Supports = "supports";
        public const string Refines = "refines";
        public const string Abstracts = "abstracts";
        public const string Elaborates = "elaborates";
        public const string SimilarTo = "similar_to";
        public const string InstanceOf = "instance_of";
        public const string PartOf = "part_of";
        public const string Triggers = "triggers";
        public const string Resolves = "resolves";
    }
}

/// <summary>
/// Represents the outcome/result of a thought or thought chain.
/// </summary>
public sealed record ThoughtResult(
    Guid Id,
    Guid ThoughtId,
    string ResultType,
    string Content,
    bool Success,
    double Confidence,
    DateTime CreatedAt,
    TimeSpan? ExecutionTime = null,
    Dictionary<string, object>? Metadata = null)
{
    /// <summary>Common result types.</summary>
    public static class Types
    {
        public const string Action = "action";
        public const string Response = "response";
        public const string Insight = "insight";
        public const string Decision = "decision";
        public const string SkillLearned = "skill_learned";
        public const string FactDiscovered = "fact_discovered";
        public const string Error = "error";
        public const string Deferred = "deferred";
    }
}

/// <summary>
/// Statistics for the neuro-symbolic thought map.
/// </summary>
public sealed record NeuroSymbolicStats(
    int TotalThoughts,
    int TotalRelations,
    int TotalResults,
    Dictionary<string, int> ThoughtsByType,
    Dictionary<string, int> RelationsByType,
    Dictionary<string, int> ResultsByType,
    int CausalChainCount,
    double AverageChainLength,
    DateTime? OldestThought,
    DateTime? NewestThought);

/// <summary>
/// Qdrant-backed neuro-symbolic thought store.
/// Combines vector embeddings (neural) with typed relations (symbolic) for rich thought persistence.
/// Enables semantic search, causal reasoning, and symbolic graph queries.
/// </summary>
public sealed class QdrantNeuroSymbolicThoughtStore : IThoughtStore, IAsyncDisposable
{
    private readonly QdrantNeuroSymbolicConfig _config;
    private readonly QdrantClient _client;
    private readonly Func<string, Task<float[]>>? _embeddingFunc;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantNeuroSymbolicThoughtStore"/> class.
    /// </summary>
    /// <param name="config">Configuration for the store.</param>
    /// <param name="embeddingFunc">Function to generate embeddings for thoughts.</param>
    public QdrantNeuroSymbolicThoughtStore(
        QdrantNeuroSymbolicConfig config,
        Func<string, Task<float[]>>? embeddingFunc = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _embeddingFunc = embeddingFunc;

        var uri = new Uri(config.Endpoint);
        _client = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");
    }

    /// <summary>
    /// Gets whether semantic search is available.
    /// </summary>
    public bool SupportsSemanticSearch => _embeddingFunc != null;

    /// <summary>
    /// Initializes the Qdrant collections.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        try
        {
            // Create thoughts collection
            if (!await _client.CollectionExistsAsync(_config.ThoughtsCollection, ct))
            {
                await _client.CreateCollectionAsync(
                    _config.ThoughtsCollection,
                    new VectorParams { Size = (ulong)_config.VectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            // Create relations collection
            if (!await _client.CollectionExistsAsync(_config.RelationsCollection, ct))
            {
                await _client.CreateCollectionAsync(
                    _config.RelationsCollection,
                    new VectorParams { Size = (ulong)_config.VectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            // Create results collection
            if (!await _client.CollectionExistsAsync(_config.ResultsCollection, ct))
            {
                await _client.CreateCollectionAsync(
                    _config.ResultsCollection,
                    new VectorParams { Size = (ulong)_config.VectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize neuro-symbolic collections: {ex.Message}", ex);
        }
    }

    #region IThoughtStore Implementation

    /// <inheritdoc/>
    public async Task SaveThoughtAsync(string sessionId, PersistedThought thought, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var embedding = await GenerateEmbeddingAsync(thought.Content, ct);
        var point = CreateThoughtPoint(sessionId, thought, embedding);

        await _client.UpsertAsync(_config.ThoughtsCollection, new[] { point }, cancellationToken: ct);
    }

    /// <inheritdoc/>
    public async Task SaveThoughtsAsync(string sessionId, IEnumerable<PersistedThought> thoughts, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var points = new List<PointStruct>();
        foreach (var thought in thoughts)
        {
            var embedding = await GenerateEmbeddingAsync(thought.Content, ct);
            points.Add(CreateThoughtPoint(sessionId, thought, embedding));
        }

        if (points.Count > 0)
        {
            // Batch upsert
            const int batchSize = 100;
            for (int i = 0; i < points.Count; i += batchSize)
            {
                var batch = points.Skip(i).Take(batchSize).ToList();
                await _client.UpsertAsync(_config.ThoughtsCollection, batch, cancellationToken: ct);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetThoughtsAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var filter = CreateSessionFilter(sessionId);
        var results = await _client.ScrollAsync(_config.ThoughtsCollection, filter: filter, limit: 1000, cancellationToken: ct);

        return results.Result
            .Select(DeserializeThought)
            .Where(t => t != null)
            .Cast<PersistedThought>()
            .OrderBy(t => t.Timestamp)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetThoughtsInRangeAsync(
        string sessionId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var all = await GetThoughtsAsync(sessionId, ct);
        return all.Where(t => t.Timestamp >= from && t.Timestamp <= to).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetThoughtsByTypeAsync(
        string sessionId, string thoughtType, int limit = 100, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var filter = new Filter
        {
            Must =
            {
                new Condition { Field = new FieldCondition { Key = "session_id", Match = new Match { Keyword = sessionId } } },
                new Condition { Field = new FieldCondition { Key = "type", Match = new Match { Keyword = thoughtType } } }
            }
        };

        var results = await _client.ScrollAsync(_config.ThoughtsCollection, filter: filter, limit: (uint)limit, cancellationToken: ct);

        return results.Result
            .Select(DeserializeThought)
            .Where(t => t != null)
            .Cast<PersistedThought>()
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> SearchThoughtsAsync(
        string sessionId, string query, int limit = 20, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        if (_embeddingFunc == null)
        {
            // Fallback to text search
            var all = await GetThoughtsAsync(sessionId, ct);
            return all
                .Where(t => t.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();
        }

        var queryEmbedding = await _embeddingFunc(query);
        var filter = CreateSessionFilter(sessionId);

        var results = await _client.SearchAsync(
            _config.ThoughtsCollection,
            queryEmbedding,
            filter: filter,
            limit: (ulong)limit,
            cancellationToken: ct);

        return results
            .Select(r => DeserializeThought(r))
            .Where(t => t != null)
            .Cast<PersistedThought>()
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetRecentThoughtsAsync(
        string sessionId, int count = 10, CancellationToken ct = default)
    {
        var all = await GetThoughtsAsync(sessionId, ct);
        return all.OrderByDescending(t => t.Timestamp).Take(count).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersistedThought>> GetChainedThoughtsAsync(
        string sessionId, Guid parentThoughtId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Find all relations where source is the parent
        var relFilter = new Filter
        {
            Must =
            {
                new Condition { Field = new FieldCondition { Key = "source_thought_id", Match = new Match { Keyword = parentThoughtId.ToString() } } }
            }
        };

        var relations = await _client.ScrollAsync(_config.RelationsCollection, filter: relFilter, limit: 100, cancellationToken: ct);
        var targetIds = relations.Result
            .Select(r => r.Payload.TryGetValue("target_thought_id", out var v) ? v.StringValue : null)
            .Where(id => id != null)
            .ToHashSet();

        if (targetIds.Count == 0) return Array.Empty<PersistedThought>();

        // Get the target thoughts
        var all = await GetThoughtsAsync(sessionId, ct);
        return all.Where(t => targetIds.Contains(t.Id.ToString())).ToList();
    }

    /// <inheritdoc/>
    public async Task ClearSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var filter = CreateSessionFilter(sessionId);

        // Delete from all collections
        await _client.DeleteAsync(_config.ThoughtsCollection, filter, cancellationToken: ct);
        await _client.DeleteAsync(_config.RelationsCollection, filter, cancellationToken: ct);
        await _client.DeleteAsync(_config.ResultsCollection, filter, cancellationToken: ct);
    }

    /// <inheritdoc/>
    public async Task<ThoughtStatistics> GetStatisticsAsync(string sessionId, CancellationToken ct = default)
    {
        var thoughts = await GetThoughtsAsync(sessionId, ct);

        return new ThoughtStatistics
        {
            TotalCount = thoughts.Count,
            CountByType = thoughts.GroupBy(t => t.Type).ToDictionary(g => g.Key, g => g.Count()),
            CountByOrigin = thoughts.GroupBy(t => t.Origin).ToDictionary(g => g.Key, g => g.Count()),
            AverageConfidence = thoughts.Count > 0 ? thoughts.Average(t => t.Confidence) : 0,
            AverageRelevance = thoughts.Count > 0 ? thoughts.Average(t => t.Relevance) : 0,
            EarliestThought = thoughts.MinBy(t => t.Timestamp)?.Timestamp,
            LatestThought = thoughts.MaxBy(t => t.Timestamp)?.Timestamp
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListSessionsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Get all points and extract unique session IDs
        var results = await _client.ScrollAsync(_config.ThoughtsCollection, limit: 10000, cancellationToken: ct);

        return results.Result
            .Select(p => p.Payload.TryGetValue("session_id", out var v) ? v.StringValue : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .Cast<string>()
            .ToList();
    }

    #endregion

    #region Neuro-Symbolic Extensions

    /// <summary>
    /// Saves a thought with automatic relation inference to recent thoughts.
    /// </summary>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="thought">The thought to save.</param>
    /// <param name="autoInferRelations">Whether to automatically infer relations to recent thoughts.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SaveWithRelationsAsync(
        string sessionId,
        PersistedThought thought,
        bool autoInferRelations = true,
        CancellationToken ct = default)
    {
        await SaveThoughtAsync(sessionId, thought, ct);

        if (autoInferRelations && _embeddingFunc != null)
        {
            // Find semantically similar recent thoughts
            var recent = await GetRecentThoughtsAsync(sessionId, 10, ct);
            var thoughtEmbedding = await _embeddingFunc(thought.Content);

            foreach (var recentThought in recent.Where(r => r.Id != thought.Id))
            {
                var recentEmbedding = await _embeddingFunc(recentThought.Content);
                var similarity = CosineSimilarity(thoughtEmbedding, recentEmbedding);

                if (similarity > 0.7) // High similarity threshold
                {
                    // Infer relation type based on thought types and content
                    var relationType = InferRelationType(thought, recentThought);
                    var relation = new ThoughtRelation(
                        Guid.NewGuid(),
                        recentThought.Id,
                        thought.Id,
                        relationType,
                        similarity,
                        DateTime.UtcNow);

                    await SaveRelationAsync(sessionId, relation, ct);
                }
            }
        }
    }

    /// <summary>
    /// Saves a relation between two thoughts.
    /// </summary>
    public async Task SaveRelationAsync(string sessionId, ThoughtRelation relation, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var text = $"{relation.RelationType}: {relation.SourceThoughtId} -> {relation.TargetThoughtId}";
        var embedding = await GenerateEmbeddingAsync(text, ct);

        var payload = new Dictionary<string, Value>
        {
            ["id"] = relation.Id.ToString(),
            ["session_id"] = sessionId,
            ["source_thought_id"] = relation.SourceThoughtId.ToString(),
            ["target_thought_id"] = relation.TargetThoughtId.ToString(),
            ["relation_type"] = relation.RelationType,
            ["strength"] = relation.Strength,
            ["created_at"] = relation.CreatedAt.ToString("O")
        };

        if (relation.Metadata != null)
        {
            payload["metadata_json"] = JsonSerializer.Serialize(relation.Metadata);
        }

        var point = new PointStruct
        {
            Id = new PointId { Uuid = relation.Id.ToString() },
            Vectors = embedding,
            Payload = { payload }
        };

        await _client.UpsertAsync(_config.RelationsCollection, new[] { point }, cancellationToken: ct);
    }

    /// <summary>
    /// Saves the result/outcome of a thought.
    /// </summary>
    public async Task SaveResultAsync(string sessionId, ThoughtResult result, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var embedding = await GenerateEmbeddingAsync(result.Content, ct);

        var payload = new Dictionary<string, Value>
        {
            ["id"] = result.Id.ToString(),
            ["session_id"] = sessionId,
            ["thought_id"] = result.ThoughtId.ToString(),
            ["result_type"] = result.ResultType,
            ["content"] = result.Content,
            ["success"] = result.Success,
            ["confidence"] = result.Confidence,
            ["created_at"] = result.CreatedAt.ToString("O")
        };

        if (result.ExecutionTime.HasValue)
        {
            payload["execution_time_ms"] = result.ExecutionTime.Value.TotalMilliseconds;
        }

        if (result.Metadata != null)
        {
            payload["metadata_json"] = JsonSerializer.Serialize(result.Metadata);
        }

        var point = new PointStruct
        {
            Id = new PointId { Uuid = result.Id.ToString() },
            Vectors = embedding,
            Payload = { payload }
        };

        await _client.UpsertAsync(_config.ResultsCollection, new[] { point }, cancellationToken: ct);

        // Create relation from thought to result
        var relation = new ThoughtRelation(
            Guid.NewGuid(),
            result.ThoughtId,
            result.Id,
            result.Success ? ThoughtRelation.Types.LeadsTo : ThoughtRelation.Types.Triggers,
            result.Confidence,
            DateTime.UtcNow);

        await SaveRelationAsync(sessionId, relation, ct);
    }

    /// <summary>
    /// Gets all relations for a thought (both incoming and outgoing).
    /// </summary>
    public async Task<IReadOnlyList<ThoughtRelation>> GetRelationsForThoughtAsync(
        Guid thoughtId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var filter = new Filter
        {
            Should =
            {
                new Condition { Field = new FieldCondition { Key = "source_thought_id", Match = new Match { Keyword = thoughtId.ToString() } } },
                new Condition { Field = new FieldCondition { Key = "target_thought_id", Match = new Match { Keyword = thoughtId.ToString() } } }
            }
        };

        var results = await _client.ScrollAsync(_config.RelationsCollection, filter: filter, limit: 100, cancellationToken: ct);

        return results.Result
            .Select(DeserializeRelation)
            .Where(r => r != null)
            .Cast<ThoughtRelation>()
            .ToList();
    }

    /// <summary>
    /// Gets results for a thought.
    /// </summary>
    public async Task<IReadOnlyList<ThoughtResult>> GetResultsForThoughtAsync(
        Guid thoughtId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var filter = new Filter
        {
            Must =
            {
                new Condition { Field = new FieldCondition { Key = "thought_id", Match = new Match { Keyword = thoughtId.ToString() } } }
            }
        };

        var results = await _client.ScrollAsync(_config.ResultsCollection, filter: filter, limit: 100, cancellationToken: ct);

        return results.Result
            .Select(DeserializeResult)
            .Where(r => r != null)
            .Cast<ThoughtResult>()
            .ToList();
    }

    /// <summary>
    /// Finds causal chains starting from a thought.
    /// </summary>
    public async Task<IReadOnlyList<List<PersistedThought>>> FindCausalChainsAsync(
        string sessionId, Guid startThoughtId, int maxDepth = 5, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var chains = new List<List<PersistedThought>>();
        var visited = new HashSet<Guid>();
        var allThoughts = (await GetThoughtsAsync(sessionId, ct)).ToDictionary(t => t.Id);

        if (!allThoughts.TryGetValue(startThoughtId, out var startThought))
            return chains;

        await FindChainsRecursiveAsync(
            sessionId, startThought, new List<PersistedThought> { startThought },
            chains, visited, allThoughts, maxDepth, ct);

        return chains;
    }

    /// <summary>
    /// Gets neuro-symbolic statistics.
    /// </summary>
    public async Task<NeuroSymbolicStats> GetNeuroSymbolicStatsAsync(string sessionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var thoughts = await GetThoughtsAsync(sessionId, ct);

        // Get relations
        var relFilter = CreateSessionFilter(sessionId);
        var relations = await _client.ScrollAsync(_config.RelationsCollection, filter: relFilter, limit: 10000, cancellationToken: ct);
        var relationList = relations.Result.Select(DeserializeRelation).Where(r => r != null).Cast<ThoughtRelation>().ToList();

        // Get results
        var results = await _client.ScrollAsync(_config.ResultsCollection, filter: relFilter, limit: 10000, cancellationToken: ct);
        var resultList = results.Result.Select(DeserializeResult).Where(r => r != null).Cast<ThoughtResult>().ToList();

        // Calculate chain statistics
        var chainStarts = thoughts.Where(t => !relationList.Any(r => r.TargetThoughtId == t.Id)).ToList();
        var avgChainLength = 0.0;
        if (chainStarts.Count > 0)
        {
            var totalLength = 0;
            foreach (var start in chainStarts.Take(10)) // Sample for performance
            {
                var chains = await FindCausalChainsAsync(sessionId, start.Id, 10, ct);
                if (chains.Count > 0)
                    totalLength += chains.Max(c => c.Count);
            }
            avgChainLength = chainStarts.Count > 0 ? (double)totalLength / Math.Min(chainStarts.Count, 10) : 0;
        }

        return new NeuroSymbolicStats(
            TotalThoughts: thoughts.Count,
            TotalRelations: relationList.Count,
            TotalResults: resultList.Count,
            ThoughtsByType: thoughts.GroupBy(t => t.Type).ToDictionary(g => g.Key, g => g.Count()),
            RelationsByType: relationList.GroupBy(r => r.RelationType).ToDictionary(g => g.Key, g => g.Count()),
            ResultsByType: resultList.GroupBy(r => r.ResultType).ToDictionary(g => g.Key, g => g.Count()),
            CausalChainCount: chainStarts.Count,
            AverageChainLength: avgChainLength,
            OldestThought: thoughts.MinBy(t => t.Timestamp)?.Timestamp,
            NewestThought: thoughts.MaxBy(t => t.Timestamp)?.Timestamp);
    }

    /// <summary>
    /// Queries the symbolic layer using a MeTTa-like pattern.
    /// Example: "?x leads_to Decision" finds all thoughts that lead to decisions.
    /// </summary>
    public async Task<IReadOnlyList<(PersistedThought Thought, ThoughtRelation Relation)>> QuerySymbolicAsync(
        string sessionId, string relationType, string? targetType = null, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Find relations of the given type
        var filter = new Filter
        {
            Must =
            {
                new Condition { Field = new FieldCondition { Key = "session_id", Match = new Match { Keyword = sessionId } } },
                new Condition { Field = new FieldCondition { Key = "relation_type", Match = new Match { Keyword = relationType } } }
            }
        };

        var relResults = await _client.ScrollAsync(_config.RelationsCollection, filter: filter, limit: 1000, cancellationToken: ct);
        var relations = relResults.Result.Select(DeserializeRelation).Where(r => r != null).Cast<ThoughtRelation>().ToList();

        var allThoughts = (await GetThoughtsAsync(sessionId, ct)).ToDictionary(t => t.Id);
        var results = new List<(PersistedThought, ThoughtRelation)>();

        foreach (var rel in relations)
        {
            if (allThoughts.TryGetValue(rel.SourceThoughtId, out var source))
            {
                if (targetType == null)
                {
                    results.Add((source, rel));
                }
                else if (allThoughts.TryGetValue(rel.TargetThoughtId, out var target) && target.Type == targetType)
                {
                    results.Add((source, rel));
                }
            }
        }

        return results;
    }

    #endregion

    #region Private Helpers

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (!_initialized)
            await InitializeAsync(ct);
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        if (_embeddingFunc != null)
        {
            return await _embeddingFunc(text);
        }
        return new float[_config.VectorSize]; // Zero vector if no embedding
    }

    private static Filter CreateSessionFilter(string sessionId)
    {
        return new Filter
        {
            Must =
            {
                new Condition { Field = new FieldCondition { Key = "session_id", Match = new Match { Keyword = sessionId } } }
            }
        };
    }

    private static PointStruct CreateThoughtPoint(string sessionId, PersistedThought thought, float[] embedding)
    {
        var payload = new Dictionary<string, Value>
        {
            ["id"] = thought.Id.ToString(),
            ["session_id"] = sessionId,
            ["type"] = thought.Type,
            ["origin"] = thought.Origin,
            ["content"] = thought.Content,
            ["confidence"] = thought.Confidence,
            ["relevance"] = thought.Relevance,
            ["timestamp"] = thought.Timestamp.ToString("O"),
            ["topic"] = thought.Topic ?? string.Empty
        };

        if (thought.ParentThoughtId.HasValue)
        {
            payload["parent_thought_id"] = thought.ParentThoughtId.Value.ToString();
        }

        if (thought.MetadataJson != null)
        {
            payload["metadata_json"] = thought.MetadataJson;
        }

        return new PointStruct
        {
            Id = new PointId { Uuid = thought.Id.ToString() },
            Vectors = embedding,
            Payload = { payload }
        };
    }

    private static PersistedThought? DeserializeThought(RetrievedPoint point)
    {
        return DeserializeThought(point.Payload);
    }

    private static PersistedThought? DeserializeThought(ScoredPoint point)
    {
        return DeserializeThought(point.Payload);
    }

    private static PersistedThought? DeserializeThought(IDictionary<string, Value> payload)
    {
        try
        {
            return new PersistedThought
            {
                Id = Guid.Parse(payload["id"].StringValue),
                Type = payload["type"].StringValue,
                Origin = payload["origin"].StringValue,
                Content = payload["content"].StringValue,
                Confidence = payload["confidence"].DoubleValue,
                Relevance = payload["relevance"].DoubleValue,
                Timestamp = DateTime.Parse(payload["timestamp"].StringValue),
                Topic = payload.TryGetValue("topic", out var topic) && !string.IsNullOrEmpty(topic.StringValue) ? topic.StringValue : null,
                ParentThoughtId = payload.TryGetValue("parent_thought_id", out var pid) && !string.IsNullOrEmpty(pid.StringValue) ? Guid.Parse(pid.StringValue) : null,
                MetadataJson = payload.TryGetValue("metadata_json", out var meta) ? meta.StringValue : null
            };
        }
        catch
        {
            return null;
        }
    }

    private static ThoughtRelation? DeserializeRelation(RetrievedPoint point)
    {
        try
        {
            var p = point.Payload;
            return new ThoughtRelation(
                Guid.Parse(p["id"].StringValue),
                Guid.Parse(p["source_thought_id"].StringValue),
                Guid.Parse(p["target_thought_id"].StringValue),
                p["relation_type"].StringValue,
                p["strength"].DoubleValue,
                DateTime.Parse(p["created_at"].StringValue),
                p.TryGetValue("metadata_json", out var meta) ? JsonSerializer.Deserialize<Dictionary<string, object>>(meta.StringValue) : null);
        }
        catch
        {
            return null;
        }
    }

    private static ThoughtResult? DeserializeResult(RetrievedPoint point)
    {
        try
        {
            var p = point.Payload;
            return new ThoughtResult(
                Guid.Parse(p["id"].StringValue),
                Guid.Parse(p["thought_id"].StringValue),
                p["result_type"].StringValue,
                p["content"].StringValue,
                p["success"].BoolValue,
                p["confidence"].DoubleValue,
                DateTime.Parse(p["created_at"].StringValue),
                p.TryGetValue("execution_time_ms", out var time) ? TimeSpan.FromMilliseconds(time.DoubleValue) : null,
                p.TryGetValue("metadata_json", out var meta) ? JsonSerializer.Deserialize<Dictionary<string, object>>(meta.StringValue) : null);
        }
        catch
        {
            return null;
        }
    }

    private string InferRelationType(PersistedThought newThought, PersistedThought existingThought)
    {
        // Use thought types to infer symbolic relation
        return (existingThought.Type, newThought.Type) switch
        {
            ("Observation", "Analytical") => ThoughtRelation.Types.LeadsTo,
            ("Analytical", "Decision") => ThoughtRelation.Types.LeadsTo,
            ("Emotional", "SelfReflection") => ThoughtRelation.Types.Triggers,
            ("MemoryRecall", _) => ThoughtRelation.Types.Supports,
            ("Strategic", "Decision") => ThoughtRelation.Types.LeadsTo,
            ("Synthesis", _) => ThoughtRelation.Types.Abstracts,
            ("Creative", _) => ThoughtRelation.Types.Elaborates,
            (_, "Synthesis") => ThoughtRelation.Types.PartOf,
            (_, "Decision") => ThoughtRelation.Types.LeadsTo,
            _ when newThought.ParentThoughtId == existingThought.Id => ThoughtRelation.Types.Refines,
            _ => ThoughtRelation.Types.SimilarTo
        };
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var mag = Math.Sqrt(magA) * Math.Sqrt(magB);
        return mag > 0 ? dot / mag : 0;
    }

    private async Task FindChainsRecursiveAsync(
        string sessionId,
        PersistedThought current,
        List<PersistedThought> currentChain,
        List<List<PersistedThought>> allChains,
        HashSet<Guid> visited,
        Dictionary<Guid, PersistedThought> allThoughts,
        int maxDepth,
        CancellationToken ct)
    {
        if (currentChain.Count >= maxDepth || visited.Contains(current.Id))
        {
            if (currentChain.Count > 1)
                allChains.Add(new List<PersistedThought>(currentChain));
            return;
        }

        visited.Add(current.Id);

        // Find outgoing relations
        var relations = await GetRelationsForThoughtAsync(current.Id, ct);
        var outgoing = relations.Where(r => r.SourceThoughtId == current.Id).ToList();

        if (outgoing.Count == 0)
        {
            if (currentChain.Count > 1)
                allChains.Add(new List<PersistedThought>(currentChain));
            return;
        }

        foreach (var rel in outgoing)
        {
            if (allThoughts.TryGetValue(rel.TargetThoughtId, out var nextThought) && !visited.Contains(nextThought.Id))
            {
                currentChain.Add(nextThought);
                await FindChainsRecursiveAsync(sessionId, nextThought, currentChain, allChains, visited, allThoughts, maxDepth, ct);
                currentChain.RemoveAt(currentChain.Count - 1);
            }
        }

        visited.Remove(current.Id);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _disposed = true;
        }
        await Task.CompletedTask;
    }

    #endregion
}
