// <copyright file="MerkleDagExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Network;

/// <summary>
/// Extension methods for MerkleDag persistence and querying.
/// </summary>
public static class MerkleDagExtensions
{
    /// <summary>
    /// Saves the MerkleDag to a Qdrant store.
    /// </summary>
    /// <param name="dag">The DAG to save.</param>
    /// <param name="store">The Qdrant store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the save result.</returns>
    public static async Task<Result<DagSaveResult>> SaveToQdrantAsync(
        this MerkleDag dag,
        QdrantDagStore store,
        CancellationToken ct = default)
    {
        return await store.SaveDagAsync(dag, ct);
    }

    /// <summary>
    /// Loads a MerkleDag from a Qdrant store.
    /// </summary>
    /// <param name="store">The Qdrant store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the loaded DAG.</returns>
    public static async Task<Result<MerkleDag>> LoadFromQdrantAsync(
        this QdrantDagStore store,
        CancellationToken ct = default)
    {
        return await store.LoadDagAsync(ct);
    }

    /// <summary>
    /// Creates a Qdrant store connected to this DAG.
    /// </summary>
    /// <param name="dag">The DAG to associate with the store.</param>
    /// <param name="config">Qdrant configuration.</param>
    /// <param name="embeddingFunc">Optional embedding function for semantic search.</param>
    /// <returns>A connected QdrantDagStore.</returns>
    public static QdrantDagStore CreateQdrantStore(
        this MerkleDag dag,
        QdrantDagConfig config,
        Func<string, Task<float[]>>? embeddingFunc = null)
    {
        return new QdrantDagStore(config, embeddingFunc);
    }

    /// <summary>
    /// Serializes the MerkleDag to JSON.
    /// </summary>
    /// <param name="dag">The DAG to serialize.</param>
    /// <returns>JSON string representation of the DAG.</returns>
    public static string ToJson(this MerkleDag dag)
    {
        var data = new DagSerializationData(
            Nodes: dag.Nodes.Values.Select(n => new NodeData(
                n.Id,
                n.TypeName,
                n.PayloadJson,
                n.CreatedAt,
                n.ParentIds.ToArray(),
                n.Hash)).ToArray(),
            Edges: dag.Edges.Values.Select(e => new EdgeData(
                e.Id,
                e.InputIds.ToArray(),
                e.OutputId,
                e.OperationName,
                e.OperationSpecJson,
                e.CreatedAt,
                e.Confidence,
                e.DurationMs,
                e.Hash)).ToArray());

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Deserializes a MerkleDag from JSON.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>A Result containing the deserialized DAG.</returns>
    public static Result<MerkleDag> FromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<DagSerializationData>(json);
            if (data == null)
            {
                return Result<MerkleDag>.Failure("Failed to deserialize DAG data");
            }

            var dag = new MerkleDag();

            // Build node dictionary for sorting
            var nodeMap = data.Nodes.ToDictionary(n => n.Id);

            // Topological sort
            var visited = new HashSet<Guid>();
            var sortedNodes = new List<NodeData>();

            void Visit(NodeData node)
            {
                if (visited.Contains(node.Id)) return;
                visited.Add(node.Id);

                foreach (var parentId in node.ParentIds)
                {
                    if (nodeMap.TryGetValue(parentId, out var parent))
                    {
                        Visit(parent);
                    }
                }

                sortedNodes.Add(node);
            }

            foreach (var node in data.Nodes)
            {
                Visit(node);
            }

            // Add nodes
            foreach (var n in sortedNodes)
            {
                var node = new MonadNode(
                    n.Id,
                    n.TypeName,
                    n.PayloadJson,
                    n.CreatedAt,
                    n.ParentIds.ToImmutableArray());
                dag.AddNode(node);
            }

            // Add edges
            foreach (var e in data.Edges)
            {
                var edge = new TransitionEdge(
                    e.Id,
                    e.InputIds.ToImmutableArray(),
                    e.OutputId,
                    e.OperationName,
                    e.OperationSpecJson,
                    e.CreatedAt,
                    e.Confidence,
                    e.DurationMs);
                dag.AddEdge(edge);
            }

            return Result<MerkleDag>.Success(dag);
        }
        catch (Exception ex)
        {
            return Result<MerkleDag>.Failure($"Failed to deserialize DAG: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all step execution nodes from the DAG.
    /// </summary>
    /// <param name="dag">The DAG to query.</param>
    /// <returns>All nodes representing step executions.</returns>
    public static IEnumerable<MonadNode> GetStepExecutionNodes(this MerkleDag dag)
    {
        return dag.Nodes.Values.Where(n => n.TypeName.StartsWith("Step:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets all reasoning nodes from the DAG.
    /// </summary>
    /// <param name="dag">The DAG to query.</param>
    /// <returns>All nodes representing reasoning states (Draft, Critique, FinalSpec, etc.).</returns>
    public static IEnumerable<MonadNode> GetReasoningNodes(this MerkleDag dag)
    {
        var reasoningTypes = new[] { "Draft", "Critique", "Improve", "FinalSpec" };
        return dag.Nodes.Values.Where(n => reasoningTypes.Contains(n.TypeName));
    }

    /// <summary>
    /// Gets the execution timeline as an ordered list of nodes.
    /// </summary>
    /// <param name="dag">The DAG to query.</param>
    /// <returns>Nodes ordered by creation time.</returns>
    public static IReadOnlyList<MonadNode> GetTimeline(this MerkleDag dag)
    {
        return dag.Nodes.Values.OrderBy(n => n.CreatedAt).ToList();
    }

    /// <summary>
    /// Gets a summary of the DAG contents.
    /// </summary>
    /// <param name="dag">The DAG to summarize.</param>
    /// <returns>A formatted summary string.</returns>
    public static string GetSummary(this MerkleDag dag)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== MerkleDag Summary ===");
        sb.AppendLine($"Total Nodes: {dag.NodeCount}");
        sb.AppendLine($"Total Edges: {dag.EdgeCount}");

        var nodesByType = dag.Nodes.Values.GroupBy(n => n.TypeName).OrderByDescending(g => g.Count());
        sb.AppendLine("\nNodes by Type:");
        foreach (var group in nodesByType.Take(10))
        {
            sb.AppendLine($"  {group.Key}: {group.Count()}");
        }

        var edgesByOp = dag.Edges.Values.GroupBy(e => e.OperationName).OrderByDescending(g => g.Count());
        if (edgesByOp.Any())
        {
            sb.AppendLine("\nTransitions by Operation:");
            foreach (var group in edgesByOp.Take(10))
            {
                sb.AppendLine($"  {group.Key}: {group.Count()}");
            }
        }

        var rootNodes = dag.GetRootNodes().ToList();
        var leafNodes = dag.GetLeafNodes().ToList();
        sb.AppendLine($"\nRoot Nodes: {rootNodes.Count}");
        sb.AppendLine($"Leaf Nodes: {leafNodes.Count}");

        return sb.ToString();
    }
}

/// <summary>
/// Internal serialization data for MerkleDag.
/// </summary>
internal sealed record DagSerializationData(NodeData[] Nodes, EdgeData[] Edges);

/// <summary>
/// Internal serialization data for MonadNode.
/// </summary>
internal sealed record NodeData(
    Guid Id,
    string TypeName,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    Guid[] ParentIds,
    string Hash);

/// <summary>
/// Internal serialization data for TransitionEdge.
/// </summary>
internal sealed record EdgeData(
    Guid Id,
    Guid[] InputIds,
    Guid OutputId,
    string OperationName,
    string OperationSpecJson,
    DateTimeOffset CreatedAt,
    double? Confidence,
    long? DurationMs,
    string Hash);
