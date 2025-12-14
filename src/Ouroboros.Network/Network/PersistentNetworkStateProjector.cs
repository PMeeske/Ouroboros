// <copyright file="PersistentNetworkStateProjector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Network;

using System.Text.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// A persistent version of NetworkStateProjector that saves snapshots and learnings to Qdrant.
/// Enables state recovery across sessions and continuous learning accumulation.
/// </summary>
public sealed class PersistentNetworkStateProjector : IAsyncDisposable
{
    private const string SnapshotCollectionName = "network_state_snapshots";
    private const string LearningsCollectionName = "network_learnings";
    private const int VectorDimension = 384; // For nomic-embed-text

    private readonly MerkleDag dag;
    private readonly QdrantClient qdrantClient;
    private readonly Func<string, Task<float[]>> embeddingFunc;
    private readonly List<GlobalNetworkState> snapshots;
    private readonly List<Learning> recentLearnings;
    private long currentEpoch;
    private bool initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistentNetworkStateProjector"/> class.
    /// </summary>
    /// <param name="dag">The Merkle-DAG to project from.</param>
    /// <param name="qdrantEndpoint">The Qdrant endpoint (e.g., "http://localhost:6334").</param>
    /// <param name="embeddingFunc">Function to generate embeddings for semantic storage.</param>
    public PersistentNetworkStateProjector(
        MerkleDag dag,
        string qdrantEndpoint,
        Func<string, Task<float[]>> embeddingFunc)
    {
        this.dag = dag ?? throw new ArgumentNullException(nameof(dag));
        this.embeddingFunc = embeddingFunc ?? throw new ArgumentNullException(nameof(embeddingFunc));
        this.qdrantClient = new QdrantClient(qdrantEndpoint);
        this.snapshots = new List<GlobalNetworkState>();
        this.recentLearnings = new List<Learning>();
        this.currentEpoch = 0;
        this.initialized = false;
    }

    /// <summary>
    /// Gets all loaded snapshots.
    /// </summary>
    public IReadOnlyList<GlobalNetworkState> Snapshots => this.snapshots;

    /// <summary>
    /// Gets the current epoch number.
    /// </summary>
    public long CurrentEpoch => this.currentEpoch;

    /// <summary>
    /// Gets recent learnings (from current session + loaded from Qdrant).
    /// </summary>
    public IReadOnlyList<Learning> RecentLearnings => this.recentLearnings;

    /// <summary>
    /// Initializes the persistent projector by loading previous state from Qdrant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (this.initialized)
        {
            return;
        }

        await EnsureCollectionsExistAsync(ct);
        await LoadPreviousStateAsync(ct);
        this.initialized = true;
    }

    /// <summary>
    /// Projects and persists the current global network state.
    /// This should be called during "thinking" to capture learnings.
    /// </summary>
    /// <param name="metadata">Optional metadata to include.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created and persisted snapshot.</returns>
    public async Task<GlobalNetworkState> ProjectAndPersistAsync(
        ImmutableDictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        if (!this.initialized)
        {
            await InitializeAsync(ct);
        }

        // Count nodes by type
        var nodeCountByType = this.dag.Nodes.Values
            .GroupBy(n => n.TypeName)
            .ToImmutableDictionary(g => g.Key, g => g.Count());

        // Count transitions by operation
        var transitionCountByOperation = this.dag.Edges.Values
            .GroupBy(e => e.OperationName)
            .ToImmutableDictionary(g => g.Key, g => g.Count());

        // Get root and leaf nodes
        var rootNodeIds = this.dag.GetRootNodes().Select(n => n.Id).ToImmutableArray();
        var leafNodeIds = this.dag.GetLeafNodes().Select(n => n.Id).ToImmutableArray();

        // Calculate average confidence
        var transitionsWithConfidence = this.dag.Edges.Values
            .Where(e => e.Confidence.HasValue)
            .ToList();

        var averageConfidence = transitionsWithConfidence.Any()
            ? transitionsWithConfidence.Average(e => e.Confidence!.Value)
            : (double?)null;

        // Calculate total processing time
        var totalProcessingTimeMs = this.dag.Edges.Values
            .Where(e => e.DurationMs.HasValue)
            .Sum(e => e.DurationMs!.Value);

        var totalProcessingTime = totalProcessingTimeMs > 0 ? (long?)totalProcessingTimeMs : null;

        var state = new GlobalNetworkState(
            this.currentEpoch,
            DateTimeOffset.UtcNow,
            this.dag.NodeCount,
            this.dag.EdgeCount,
            nodeCountByType,
            transitionCountByOperation,
            rootNodeIds,
            leafNodeIds,
            averageConfidence,
            totalProcessingTime,
            metadata);

        // Add to local cache
        this.snapshots.Add(state);

        // Persist to Qdrant
        await PersistSnapshotAsync(state, ct);

        this.currentEpoch++;
        return state;
    }

    /// <summary>
    /// Records a learning from the thinking process for persistent storage.
    /// </summary>
    /// <param name="category">Learning category (e.g., "skill", "pattern", "insight").</param>
    /// <param name="content">The content of what was learned.</param>
    /// <param name="context">The context in which it was learned.</param>
    /// <param name="confidence">Confidence level 0-1.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RecordLearningAsync(
        string category,
        string content,
        string context,
        double confidence = 1.0,
        CancellationToken ct = default)
    {
        if (!this.initialized)
        {
            await InitializeAsync(ct);
        }

        var learning = new Learning(
            Id: Guid.NewGuid().ToString("N"),
            Category: category,
            Content: content,
            Context: context,
            Confidence: confidence,
            Epoch: this.currentEpoch,
            Timestamp: DateTimeOffset.UtcNow);

        this.recentLearnings.Add(learning);

        // Persist to Qdrant with embedding for semantic retrieval
        await PersistLearningAsync(learning, ct);
    }

    /// <summary>
    /// Retrieves relevant learnings for a given context using semantic search.
    /// </summary>
    /// <param name="context">The context to search for.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of relevant learnings.</returns>
    public async Task<List<Learning>> GetRelevantLearningsAsync(
        string context,
        int limit = 5,
        CancellationToken ct = default)
    {
        if (!this.initialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            var embedding = await this.embeddingFunc(context);

            var results = await this.qdrantClient.SearchAsync(
                LearningsCollectionName,
                embedding,
                limit: (ulong)limit,
                scoreThreshold: 0.6f,
                cancellationToken: ct);

            var learnings = new List<Learning>();
            foreach (var result in results)
            {
                if (result.Payload.TryGetValue("learning_json", out var jsonValue))
                {
                    var learning = JsonSerializer.Deserialize<Learning>(jsonValue.StringValue);
                    if (learning != null)
                    {
                        learnings.Add(learning);
                    }
                }
            }

            return learnings;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to retrieve learnings: {ex.Message}");
            return new List<Learning>();
        }
    }

    /// <summary>
    /// Gets all learnings from a specific category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of learnings in that category.</returns>
    public async Task<List<Learning>> GetLearningsByCategoryAsync(
        string category,
        CancellationToken ct = default)
    {
        if (!this.initialized)
        {
            await InitializeAsync(ct);
        }

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
                            Key = "category",
                            Match = new Match { Keyword = category },
                        },
                    },
                },
            };

            var results = await this.qdrantClient.ScrollAsync(
                LearningsCollectionName,
                filter: filter,
                limit: 100,
                cancellationToken: ct);

            var learnings = new List<Learning>();
            foreach (var point in results.Result)
            {
                if (point.Payload.TryGetValue("learning_json", out var jsonValue))
                {
                    var learning = JsonSerializer.Deserialize<Learning>(jsonValue.StringValue);
                    if (learning != null)
                    {
                        learnings.Add(learning);
                    }
                }
            }

            return learnings;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to retrieve learnings by category: {ex.Message}");
            return new List<Learning>();
        }
    }

    private async Task EnsureCollectionsExistAsync(CancellationToken ct)
    {
        try
        {
            // Create snapshots collection
            if (!await this.qdrantClient.CollectionExistsAsync(SnapshotCollectionName, ct))
            {
                await this.qdrantClient.CreateCollectionAsync(
                    SnapshotCollectionName,
                    new VectorParams { Size = VectorDimension, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            // Create learnings collection
            if (!await this.qdrantClient.CollectionExistsAsync(LearningsCollectionName, ct))
            {
                await this.qdrantClient.CreateCollectionAsync(
                    LearningsCollectionName,
                    new VectorParams { Size = VectorDimension, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to create Qdrant collections: {ex.Message}");
        }
    }

    private async Task LoadPreviousStateAsync(CancellationToken ct)
    {
        try
        {
            // Load the most recent snapshot to resume from
            var scrollResult = await this.qdrantClient.ScrollAsync(
                SnapshotCollectionName,
                limit: 100,
                cancellationToken: ct);

            GlobalNetworkState? latestSnapshot = null;
            long maxEpoch = -1;

            foreach (var point in scrollResult.Result)
            {
                if (point.Payload.TryGetValue("snapshot_json", out var jsonValue))
                {
                    var snapshot = JsonSerializer.Deserialize<GlobalNetworkState>(jsonValue.StringValue);
                    if (snapshot != null)
                    {
                        this.snapshots.Add(snapshot);
                        if (snapshot.Epoch > maxEpoch)
                        {
                            maxEpoch = snapshot.Epoch;
                            latestSnapshot = snapshot;
                        }
                    }
                }
            }

            if (latestSnapshot != null)
            {
                this.currentEpoch = latestSnapshot.Epoch + 1;
                Console.WriteLine($"[NetworkState] Resumed from epoch {latestSnapshot.Epoch} ({this.snapshots.Count} snapshots loaded)");
            }

            // Load recent learnings (last 100)
            var learningsResult = await this.qdrantClient.ScrollAsync(
                LearningsCollectionName,
                limit: 100,
                cancellationToken: ct);

            foreach (var point in learningsResult.Result)
            {
                if (point.Payload.TryGetValue("learning_json", out var jsonValue))
                {
                    var learning = JsonSerializer.Deserialize<Learning>(jsonValue.StringValue);
                    if (learning != null)
                    {
                        this.recentLearnings.Add(learning);
                    }
                }
            }

            if (this.recentLearnings.Count > 0)
            {
                Console.WriteLine($"[NetworkState] Loaded {this.recentLearnings.Count} previous learnings");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load previous state: {ex.Message}");
        }
    }

    private async Task PersistSnapshotAsync(GlobalNetworkState state, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(state);
            var embedding = await this.embeddingFunc($"network state epoch {state.Epoch} nodes {state.TotalNodes} transitions {state.TotalTransitions}");

            var point = new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["epoch"] = state.Epoch,
                    ["total_nodes"] = state.TotalNodes,
                    ["total_transitions"] = state.TotalTransitions,
                    ["timestamp"] = state.Timestamp.ToString("O"),
                    ["snapshot_json"] = json,
                },
            };

            await this.qdrantClient.UpsertAsync(SnapshotCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to persist snapshot: {ex.Message}");
        }
    }

    private async Task PersistLearningAsync(Learning learning, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(learning);
            var embedding = await this.embeddingFunc($"{learning.Category}: {learning.Content}");

            var point = new PointStruct
            {
                Id = new PointId { Uuid = learning.Id },
                Vectors = embedding,
                Payload =
                {
                    ["category"] = learning.Category,
                    ["content"] = learning.Content,
                    ["context"] = learning.Context,
                    ["confidence"] = learning.Confidence,
                    ["epoch"] = learning.Epoch,
                    ["timestamp"] = learning.Timestamp.ToString("O"),
                    ["learning_json"] = json,
                },
            };

            await this.qdrantClient.UpsertAsync(LearningsCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to persist learning: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Final snapshot before shutdown
        if (this.initialized && this.dag.NodeCount > 0)
        {
            try
            {
                await ProjectAndPersistAsync(
                    ImmutableDictionary<string, string>.Empty.Add("event", "shutdown"));
            }
            catch
            {
                // Ignore errors during disposal
            }
        }

        this.qdrantClient.Dispose();
    }
}

/// <summary>
/// Represents a learning captured during the thinking process.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Category">Category of learning (skill, pattern, insight, fact, etc.).</param>
/// <param name="Content">What was learned.</param>
/// <param name="Context">The context in which it was learned.</param>
/// <param name="Confidence">Confidence level 0-1.</param>
/// <param name="Epoch">The epoch when this was learned.</param>
/// <param name="Timestamp">When this was learned.</param>
public sealed record Learning(
    string Id,
    string Category,
    string Content,
    string Context,
    double Confidence,
    long Epoch,
    DateTimeOffset Timestamp);
