// <copyright file="MerkleDag.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Network;

/// <summary>
/// Represents a Merkle-DAG (Directed Acyclic Graph) of monadic nodes and transitions.
/// Provides efficient storage, retrieval, and traversal of the network state.
/// </summary>
public sealed class MerkleDag
{
    private readonly Dictionary<Guid, MonadNode> nodes;
    private readonly Dictionary<Guid, TransitionEdge> edges;
    private readonly Dictionary<Guid, List<Guid>> nodeToIncomingEdges;
    private readonly Dictionary<Guid, List<Guid>> nodeToOutgoingEdges;

    /// <summary>
    /// Initializes a new instance of the <see cref="MerkleDag"/> class.
    /// </summary>
    public MerkleDag()
    {
        this.nodes = new Dictionary<Guid, MonadNode>();
        this.edges = new Dictionary<Guid, TransitionEdge>();
        this.nodeToIncomingEdges = new Dictionary<Guid, List<Guid>>();
        this.nodeToOutgoingEdges = new Dictionary<Guid, List<Guid>>();
    }

    /// <summary>
    /// Gets all nodes in the DAG.
    /// </summary>
    public IReadOnlyDictionary<Guid, MonadNode> Nodes => this.nodes;

    /// <summary>
    /// Gets all edges in the DAG.
    /// </summary>
    public IReadOnlyDictionary<Guid, TransitionEdge> Edges => this.edges;

    /// <summary>
    /// Gets the count of nodes in the DAG.
    /// </summary>
    public int NodeCount => this.nodes.Count;

    /// <summary>
    /// Gets the count of edges in the DAG.
    /// </summary>
    public int EdgeCount => this.edges.Count;

    /// <summary>
    /// Adds a node to the DAG.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<MonadNode> AddNode(MonadNode node)
    {
        if (node == null)
        {
            return Result<MonadNode>.Failure("Node cannot be null");
        }

        if (!node.VerifyHash())
        {
            return Result<MonadNode>.Failure("Node hash verification failed");
        }

        if (this.nodes.ContainsKey(node.Id))
        {
            return Result<MonadNode>.Failure($"Node with ID {node.Id} already exists");
        }

        // Verify parent nodes exist
        foreach (var parentId in node.ParentIds)
        {
            if (!this.nodes.ContainsKey(parentId))
            {
                return Result<MonadNode>.Failure($"Parent node {parentId} does not exist");
            }
        }

        this.nodes[node.Id] = node;
        this.nodeToIncomingEdges[node.Id] = new List<Guid>();
        this.nodeToOutgoingEdges[node.Id] = new List<Guid>();

        return Result<MonadNode>.Success(node);
    }

    /// <summary>
    /// Adds an edge to the DAG.
    /// </summary>
    /// <param name="edge">The edge to add.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<TransitionEdge> AddEdge(TransitionEdge edge)
    {
        if (edge == null)
        {
            return Result<TransitionEdge>.Failure("Edge cannot be null");
        }

        if (!edge.VerifyHash())
        {
            return Result<TransitionEdge>.Failure("Edge hash verification failed");
        }

        if (this.edges.ContainsKey(edge.Id))
        {
            return Result<TransitionEdge>.Failure($"Edge with ID {edge.Id} already exists");
        }

        // Verify all input and output nodes exist
        foreach (var inputId in edge.InputIds)
        {
            if (!this.nodes.ContainsKey(inputId))
            {
                return Result<TransitionEdge>.Failure($"Input node {inputId} does not exist");
            }
        }

        if (!this.nodes.ContainsKey(edge.OutputId))
        {
            return Result<TransitionEdge>.Failure($"Output node {edge.OutputId} does not exist");
        }

        this.edges[edge.Id] = edge;

        // Update adjacency information
        foreach (var inputId in edge.InputIds)
        {
            this.nodeToOutgoingEdges[inputId].Add(edge.Id);
        }

        this.nodeToIncomingEdges[edge.OutputId].Add(edge.Id);

        return Result<TransitionEdge>.Success(edge);
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>An Option containing the node if found.</returns>
    public Option<MonadNode> GetNode(Guid nodeId)
    {
        return this.nodes.TryGetValue(nodeId, out var node)
            ? Option<MonadNode>.Some(node)
            : Option<MonadNode>.None();
    }

    /// <summary>
    /// Gets an edge by its ID.
    /// </summary>
    /// <param name="edgeId">The edge ID.</param>
    /// <returns>An Option containing the edge if found.</returns>
    public Option<TransitionEdge> GetEdge(Guid edgeId)
    {
        return this.edges.TryGetValue(edgeId, out var edge)
            ? Option<TransitionEdge>.Some(edge)
            : Option<TransitionEdge>.None();
    }

    /// <summary>
    /// Gets all incoming edges for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>A collection of incoming edges.</returns>
    public IEnumerable<TransitionEdge> GetIncomingEdges(Guid nodeId)
    {
        if (!this.nodeToIncomingEdges.TryGetValue(nodeId, out var edgeIds))
        {
            return Enumerable.Empty<TransitionEdge>();
        }

        return edgeIds.Select(id => this.edges[id]);
    }

    /// <summary>
    /// Gets all outgoing edges for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>A collection of outgoing edges.</returns>
    public IEnumerable<TransitionEdge> GetOutgoingEdges(Guid nodeId)
    {
        if (!this.nodeToOutgoingEdges.TryGetValue(nodeId, out var edgeIds))
        {
            return Enumerable.Empty<TransitionEdge>();
        }

        return edgeIds.Select(id => this.edges[id]);
    }

    /// <summary>
    /// Gets all root nodes (nodes with no parents).
    /// </summary>
    /// <returns>A collection of root nodes.</returns>
    public IEnumerable<MonadNode> GetRootNodes()
    {
        return this.nodes.Values.Where(n => n.ParentIds.IsEmpty);
    }

    /// <summary>
    /// Gets all leaf nodes (nodes with no outgoing edges).
    /// </summary>
    /// <returns>A collection of leaf nodes.</returns>
    public IEnumerable<MonadNode> GetLeafNodes()
    {
        return this.nodes.Values.Where(n =>
            !this.nodeToOutgoingEdges.TryGetValue(n.Id, out var edges) || edges.Count == 0);
    }

    /// <summary>
    /// Performs a topological sort of the DAG.
    /// </summary>
    /// <returns>A Result containing the sorted nodes or an error if the graph has cycles.</returns>
    public Result<ImmutableArray<MonadNode>> TopologicalSort()
    {
        var inDegree = new Dictionary<Guid, int>();
        var queue = new Queue<Guid>();
        var sorted = new List<MonadNode>();

        // Initialize in-degrees based on incoming edges
        foreach (var node in this.nodes.Values)
        {
            inDegree[node.Id] = this.GetIncomingEdges(node.Id).Count();
            if (inDegree[node.Id] == 0)
            {
                queue.Enqueue(node.Id);
            }
        }

        // Process nodes
        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            sorted.Add(this.nodes[nodeId]);

            // Reduce in-degree for children
            foreach (var edge in this.GetOutgoingEdges(nodeId))
            {
                inDegree[edge.OutputId]--;
                if (inDegree[edge.OutputId] == 0)
                {
                    queue.Enqueue(edge.OutputId);
                }
            }
        }

        if (sorted.Count != this.nodes.Count)
        {
            return Result<ImmutableArray<MonadNode>>.Failure("Graph contains cycles");
        }

        return Result<ImmutableArray<MonadNode>>.Success(sorted.ToImmutableArray());
    }

    /// <summary>
    /// Gets all nodes of a specific type.
    /// </summary>
    /// <param name="typeName">The type name to filter by.</param>
    /// <returns>A collection of nodes with the specified type.</returns>
    public IEnumerable<MonadNode> GetNodesByType(string typeName)
    {
        return this.nodes.Values.Where(n => n.TypeName == typeName);
    }

    /// <summary>
    /// Gets all transitions with a specific operation name.
    /// </summary>
    /// <param name="operationName">The operation name to filter by.</param>
    /// <returns>A collection of edges with the specified operation.</returns>
    public IEnumerable<TransitionEdge> GetTransitionsByOperation(string operationName)
    {
        return this.edges.Values.Where(e => e.OperationName == operationName);
    }

    /// <summary>
    /// Verifies the integrity of the entire DAG.
    /// </summary>
    /// <returns>A Result indicating whether the DAG is valid.</returns>
    public Result<bool> VerifyIntegrity()
    {
        // Verify all node hashes
        foreach (var node in this.nodes.Values)
        {
            if (!node.VerifyHash())
            {
                return Result<bool>.Failure($"Node {node.Id} hash verification failed");
            }
        }

        // Verify all edge hashes
        foreach (var edge in this.edges.Values)
        {
            if (!edge.VerifyHash())
            {
                return Result<bool>.Failure($"Edge {edge.Id} hash verification failed");
            }
        }

        // Verify no cycles
        var sortResult = this.TopologicalSort();
        if (sortResult.IsFailure)
        {
            return Result<bool>.Failure(sortResult.Error);
        }

        return Result<bool>.Success(true);
    }
}
