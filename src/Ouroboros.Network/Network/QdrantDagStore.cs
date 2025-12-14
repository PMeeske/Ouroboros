// <copyright file="QdrantDagStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace LangChainPipeline.Network;

/// <summary>
/// Configuration for Qdrant DAG storage.
/// </summary>
/// <param name="Endpoint">Qdrant server endpoint (e.g., "http://localhost:6334").</param>
/// <param name="NodesCollection">Collection name for MonadNodes.</param>
/// <param name="EdgesCollection">Collection name for TransitionEdges.</param>
/// <param name="VectorSize">Embedding vector dimension (default 1536 for OpenAI).</param>
/// <param name="UseHttps">Whether to use HTTPS connection.</param>
public sealed record QdrantDagConfig(
    string Endpoint = "http://localhost:6334",
    string NodesCollection = "ouroboros_dag_nodes",
    string EdgesCollection = "ouroboros_dag_edges",
    int VectorSize = 1536,
    bool UseHttps = false);

/// <summary>
/// Persists MerkleDag nodes and transitions to Qdrant for durable storage and semantic search.
/// Enables loading DAG history across sessions and searching nodes by semantic similarity.
/// </summary>
public sealed class QdrantDagStore : IAsyncDisposable
{
    private readonly QdrantDagConfig _config;
    private readonly QdrantClient _client;
    private readonly Func<string, Task<float[]>>? _embeddingFunc;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantDagStore"/> class.
    /// </summary>
    /// <param name="config">Qdrant configuration.</param>
    /// <param name="embeddingFunc">Optional function to generate embeddings for semantic search.</param>
    public QdrantDagStore(QdrantDagConfig config, Func<string, Task<float[]>>? embeddingFunc = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _embeddingFunc = embeddingFunc;

        var uri = new Uri(config.Endpoint);
        _client = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, config.UseHttps);
    }

    /// <summary>
    /// Gets whether this store supports semantic search (has embedding function).
    /// </summary>
    public bool SupportsSemanticSearch => _embeddingFunc != null;

    /// <summary>
    /// Initializes the Qdrant collections for DAG storage.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        try
        {
            // Create nodes collection
            if (!await _client.CollectionExistsAsync(_config.NodesCollection, ct))
            {
                await _client.CreateCollectionAsync(
                    _config.NodesCollection,
                    new VectorParams { Size = (ulong)_config.VectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            // Create edges collection (smaller vectors for edge metadata)
            if (!await _client.CollectionExistsAsync(_config.EdgesCollection, ct))
            {
                await _client.CreateCollectionAsync(
                    _config.EdgesCollection,
                    new VectorParams { Size = (ulong)_config.VectorSize, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize Qdrant collections: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves a MonadNode to Qdrant.
    /// </summary>
    /// <param name="node">The node to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<MonadNode>> SaveNodeAsync(MonadNode node, CancellationToken ct = default)
    {
        if (node == null)
        {
            return Result<MonadNode>.Failure("Node cannot be null");
        }

        await EnsureInitializedAsync(ct);

        try
        {
            // Generate embedding if function available
            var embedding = await GenerateNodeEmbeddingAsync(node);

            var payload = new Dictionary<string, Value>
            {
                ["id"] = node.Id.ToString(),
                ["type_name"] = node.TypeName,
                ["payload_json"] = node.PayloadJson,
                ["created_at"] = node.CreatedAt.ToString("O"),
                ["parent_ids"] = string.Join(",", node.ParentIds.Select(p => p.ToString())),
                ["hash"] = node.Hash,
            };

            var point = new PointStruct
            {
                Id = new PointId { Uuid = node.Id.ToString() },
                Vectors = embedding,
                Payload = { payload },
            };

            await _client.UpsertAsync(_config.NodesCollection, new[] { point }, cancellationToken: ct);
            return Result<MonadNode>.Success(node);
        }
        catch (Exception ex)
        {
            return Result<MonadNode>.Failure($"Failed to save node: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves a TransitionEdge to Qdrant.
    /// </summary>
    /// <param name="edge">The edge to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<TransitionEdge>> SaveEdgeAsync(TransitionEdge edge, CancellationToken ct = default)
    {
        if (edge == null)
        {
            return Result<TransitionEdge>.Failure("Edge cannot be null");
        }

        await EnsureInitializedAsync(ct);

        try
        {
            // Generate embedding for the edge
            var embedding = await GenerateEdgeEmbeddingAsync(edge);

            var payload = new Dictionary<string, Value>
            {
                ["id"] = edge.Id.ToString(),
                ["input_ids"] = string.Join(",", edge.InputIds.Select(i => i.ToString())),
                ["output_id"] = edge.OutputId.ToString(),
                ["operation_name"] = edge.OperationName,
                ["operation_spec_json"] = edge.OperationSpecJson,
                ["created_at"] = edge.CreatedAt.ToString("O"),
                ["hash"] = edge.Hash,
            };

            if (edge.Confidence.HasValue)
            {
                payload["confidence"] = edge.Confidence.Value;
            }

            if (edge.DurationMs.HasValue)
            {
                payload["duration_ms"] = edge.DurationMs.Value;
            }

            var point = new PointStruct
            {
                Id = new PointId { Uuid = edge.Id.ToString() },
                Vectors = embedding,
                Payload = { payload },
            };

            await _client.UpsertAsync(_config.EdgesCollection, new[] { point }, cancellationToken: ct);
            return Result<TransitionEdge>.Success(edge);
        }
        catch (Exception ex)
        {
            return Result<TransitionEdge>.Failure($"Failed to save edge: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves an entire MerkleDag to Qdrant.
    /// </summary>
    /// <param name="dag">The DAG to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the count of saved items.</returns>
    public async Task<Result<DagSaveResult>> SaveDagAsync(MerkleDag dag, CancellationToken ct = default)
    {
        if (dag == null)
        {
            return Result<DagSaveResult>.Failure("DAG cannot be null");
        }

        await EnsureInitializedAsync(ct);

        var nodesSaved = 0;
        var edgesSaved = 0;
        var errors = new List<string>();

        // Save all nodes
        foreach (var node in dag.Nodes.Values)
        {
            var result = await SaveNodeAsync(node, ct);
            if (result.IsSuccess)
            {
                nodesSaved++;
            }
            else
            {
                errors.Add($"Node {node.Id}: {result.Error}");
            }
        }

        // Save all edges
        foreach (var edge in dag.Edges.Values)
        {
            var result = await SaveEdgeAsync(edge, ct);
            if (result.IsSuccess)
            {
                edgesSaved++;
            }
            else
            {
                errors.Add($"Edge {edge.Id}: {result.Error}");
            }
        }

        return Result<DagSaveResult>.Success(new DagSaveResult(nodesSaved, edgesSaved, errors));
    }

    /// <summary>
    /// Loads all nodes and edges from Qdrant into a MerkleDag.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the loaded DAG.</returns>
    public async Task<Result<MerkleDag>> LoadDagAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        try
        {
            var dag = new MerkleDag();

            // Load all nodes
            var nodesResult = await LoadAllNodesAsync(ct);
            if (!nodesResult.IsSuccess)
            {
                return Result<MerkleDag>.Failure($"Failed to load nodes: {nodesResult.Error}");
            }

            // Sort nodes topologically (parents before children)
            var sortedNodes = TopologicalSort(nodesResult.Value);

            // Add nodes to DAG in order
            foreach (var node in sortedNodes)
            {
                dag.AddNode(node);
            }

            // Load and add all edges
            var edgesResult = await LoadAllEdgesAsync(ct);
            if (!edgesResult.IsSuccess)
            {
                return Result<MerkleDag>.Failure($"Failed to load edges: {edgesResult.Error}");
            }

            foreach (var edge in edgesResult.Value)
            {
                dag.AddEdge(edge);
            }

            return Result<MerkleDag>.Success(dag);
        }
        catch (Exception ex)
        {
            return Result<MerkleDag>.Failure($"Failed to load DAG: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for nodes semantically similar to the query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing matching nodes with scores.</returns>
    public async Task<Result<IReadOnlyList<ScoredNode>>> SearchNodesAsync(
        string query,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (!SupportsSemanticSearch)
        {
            return Result<IReadOnlyList<ScoredNode>>.Failure("Semantic search requires an embedding function");
        }

        await EnsureInitializedAsync(ct);

        try
        {
            var embedding = await _embeddingFunc!(query);

            var results = await _client.SearchAsync(
                _config.NodesCollection,
                embedding,
                limit: (ulong)limit,
                cancellationToken: ct);

            var scoredNodes = new List<ScoredNode>();
            foreach (var result in results)
            {
                var node = DeserializeNode(result.Payload);
                if (node != null)
                {
                    scoredNodes.Add(new ScoredNode(node, result.Score));
                }
            }

            return Result<IReadOnlyList<ScoredNode>>.Success(scoredNodes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ScoredNode>>.Failure($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for nodes by type name.
    /// </summary>
    /// <param name="typeName">The type name to filter by.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing matching nodes.</returns>
    public async Task<Result<IReadOnlyList<MonadNode>>> GetNodesByTypeAsync(
        string typeName,
        int limit = 100,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        try
        {
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "type_name",
                            Match = new Match { Keyword = typeName },
                        },
                    },
                },
            };

            var scrollResponse = await _client.ScrollAsync(
                _config.NodesCollection,
                filter: filter,
                limit: (uint)limit,
                cancellationToken: ct);

            var nodes = scrollResponse.Result
                .Select(r => DeserializeNode(r.Payload))
                .Where(n => n != null)
                .Cast<MonadNode>()
                .ToList();

            return Result<IReadOnlyList<MonadNode>>.Success(nodes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<MonadNode>>.Failure($"Query failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a specific node by ID.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An Option containing the node if found.</returns>
    public async Task<Option<MonadNode>> GetNodeByIdAsync(Guid nodeId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        try
        {
            var results = await _client.RetrieveAsync(
                _config.NodesCollection,
                new[] { new PointId { Uuid = nodeId.ToString() } },
                withPayload: true,
                cancellationToken: ct);

            var point = results.FirstOrDefault();
            if (point == null)
            {
                return Option<MonadNode>.None();
            }

            var node = DeserializeNode(point.Payload);
            return node != null ? Option<MonadNode>.Some(node) : Option<MonadNode>.None();
        }
        catch
        {
            return Option<MonadNode>.None();
        }
    }

    /// <summary>
    /// Deletes all DAG data from Qdrant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        try
        {
            if (await _client.CollectionExistsAsync(_config.NodesCollection))
            {
                await _client.DeleteCollectionAsync(_config.NodesCollection);
            }

            if (await _client.CollectionExistsAsync(_config.EdgesCollection))
            {
                await _client.DeleteCollectionAsync(_config.EdgesCollection);
            }

            _initialized = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to clear DAG data: {ex.Message}", ex);
        }
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

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
        }
    }

    private async Task<float[]> GenerateNodeEmbeddingAsync(MonadNode node)
    {
        if (_embeddingFunc == null)
        {
            // Return zero vector if no embedding function
            return new float[_config.VectorSize];
        }

        // Generate semantic text for embedding
        var semanticText = $"{node.TypeName}: {node.PayloadJson}";
        return await _embeddingFunc(semanticText);
    }

    private async Task<float[]> GenerateEdgeEmbeddingAsync(TransitionEdge edge)
    {
        if (_embeddingFunc == null)
        {
            return new float[_config.VectorSize];
        }

        var semanticText = $"{edge.OperationName}: {edge.OperationSpecJson}";
        return await _embeddingFunc(semanticText);
    }

    private async Task<Result<IReadOnlyList<MonadNode>>> LoadAllNodesAsync(CancellationToken ct)
    {
        try
        {
            var nodes = new List<MonadNode>();
            PointId? offset = null;

            while (true)
            {
                var scrollResponse = await _client.ScrollAsync(
                    _config.NodesCollection,
                    limit: 100,
                    offset: offset,
                    cancellationToken: ct);

                foreach (var point in scrollResponse.Result)
                {
                    var node = DeserializeNode(point.Payload);
                    if (node != null)
                    {
                        nodes.Add(node);
                    }
                }

                if (scrollResponse.NextPageOffset == null) break;
                offset = scrollResponse.NextPageOffset;
            }

            return Result<IReadOnlyList<MonadNode>>.Success(nodes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<MonadNode>>.Failure($"Failed to load nodes: {ex.Message}");
        }
    }

    private async Task<Result<IReadOnlyList<TransitionEdge>>> LoadAllEdgesAsync(CancellationToken ct)
    {
        try
        {
            var edges = new List<TransitionEdge>();
            PointId? offset = null;

            while (true)
            {
                var scrollResponse = await _client.ScrollAsync(
                    _config.EdgesCollection,
                    limit: 100,
                    offset: offset,
                    cancellationToken: ct);

                foreach (var point in scrollResponse.Result)
                {
                    var edge = DeserializeEdge(point.Payload);
                    if (edge != null)
                    {
                        edges.Add(edge);
                    }
                }

                if (scrollResponse.NextPageOffset == null) break;
                offset = scrollResponse.NextPageOffset;
            }

            return Result<IReadOnlyList<TransitionEdge>>.Success(edges);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<TransitionEdge>>.Failure($"Failed to load edges: {ex.Message}");
        }
    }

    private static MonadNode? DeserializeNode(IDictionary<string, Value> payload)
    {
        try
        {
            var id = Guid.Parse(payload["id"].StringValue);
            var typeName = payload["type_name"].StringValue;
            var payloadJson = payload["payload_json"].StringValue;
            var createdAt = DateTimeOffset.Parse(payload["created_at"].StringValue);
            var parentIdsStr = payload["parent_ids"].StringValue;

            var parentIds = string.IsNullOrEmpty(parentIdsStr)
                ? ImmutableArray<Guid>.Empty
                : parentIdsStr.Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(Guid.Parse).ToImmutableArray();

            return new MonadNode(id, typeName, payloadJson, createdAt, parentIds);
        }
        catch
        {
            return null;
        }
    }

    private static TransitionEdge? DeserializeEdge(IDictionary<string, Value> payload)
    {
        try
        {
            var id = Guid.Parse(payload["id"].StringValue);
            var inputIdsStr = payload["input_ids"].StringValue;
            var outputId = Guid.Parse(payload["output_id"].StringValue);
            var operationName = payload["operation_name"].StringValue;
            var operationSpecJson = payload["operation_spec_json"].StringValue;
            var createdAt = DateTimeOffset.Parse(payload["created_at"].StringValue);

            var inputIds = inputIdsStr.Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(Guid.Parse).ToImmutableArray();

            double? confidence = payload.TryGetValue("confidence", out var confVal) ? confVal.DoubleValue : null;
            long? durationMs = payload.TryGetValue("duration_ms", out var durVal) ? (long)durVal.IntegerValue : null;

            return new TransitionEdge(id, inputIds, outputId, operationName, operationSpecJson, createdAt, confidence, durationMs);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<MonadNode> TopologicalSort(IReadOnlyList<MonadNode> nodes)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        var visited = new HashSet<Guid>();
        var result = new List<MonadNode>();

        void Visit(MonadNode node)
        {
            if (visited.Contains(node.Id)) return;
            visited.Add(node.Id);

            // Visit parents first
            foreach (var parentId in node.ParentIds)
            {
                if (nodeMap.TryGetValue(parentId, out var parent))
                {
                    Visit(parent);
                }
            }

            result.Add(node);
        }

        foreach (var node in nodes)
        {
            Visit(node);
        }

        return result;
    }
}

/// <summary>
/// Result of saving a DAG to Qdrant.
/// </summary>
/// <param name="NodesSaved">Number of nodes saved.</param>
/// <param name="EdgesSaved">Number of edges saved.</param>
/// <param name="Errors">Any errors encountered.</param>
public sealed record DagSaveResult(int NodesSaved, int EdgesSaved, IReadOnlyList<string> Errors);

/// <summary>
/// A node with its semantic search score.
/// </summary>
/// <param name="Node">The matching node.</param>
/// <param name="Score">Similarity score (0-1).</param>
public sealed record ScoredNode(MonadNode Node, float Score);
