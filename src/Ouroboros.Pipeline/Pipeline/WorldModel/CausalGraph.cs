// <copyright file="CausalGraph.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Defines the type of causal node in the graph.
/// </summary>
public enum CausalNodeType
{
    /// <summary>
    /// Represents a state or condition in the world.
    /// </summary>
    State,

    /// <summary>
    /// Represents an action that can be taken.
    /// </summary>
    Action,

    /// <summary>
    /// Represents an event that occurs.
    /// </summary>
    Event,
}

/// <summary>
/// Represents a node in the causal graph.
/// </summary>
/// <param name="Id">Unique identifier for the node.</param>
/// <param name="Name">Human-readable name of the node.</param>
/// <param name="Description">Detailed description of what this node represents.</param>
/// <param name="NodeType">The type of causal node (State, Action, or Event).</param>
public sealed record CausalNode(
    Guid Id,
    string Name,
    string Description,
    CausalNodeType NodeType)
{
    /// <summary>
    /// Creates a new causal node with a generated ID.
    /// </summary>
    /// <param name="name">The name of the node.</param>
    /// <param name="description">The description of the node.</param>
    /// <param name="nodeType">The type of node.</param>
    /// <returns>A new causal node.</returns>
    public static CausalNode Create(string name, string description, CausalNodeType nodeType)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        return new CausalNode(Guid.NewGuid(), name, description, nodeType);
    }

    /// <summary>
    /// Creates a new state node.
    /// </summary>
    /// <param name="name">The name of the state.</param>
    /// <param name="description">The description of the state.</param>
    /// <returns>A new state node.</returns>
    public static CausalNode CreateState(string name, string description)
    {
        return Create(name, description, CausalNodeType.State);
    }

    /// <summary>
    /// Creates a new action node.
    /// </summary>
    /// <param name="name">The name of the action.</param>
    /// <param name="description">The description of the action.</param>
    /// <returns>A new action node.</returns>
    public static CausalNode CreateAction(string name, string description)
    {
        return Create(name, description, CausalNodeType.Action);
    }

    /// <summary>
    /// Creates a new event node.
    /// </summary>
    /// <param name="name">The name of the event.</param>
    /// <param name="description">The description of the event.</param>
    /// <returns>A new event node.</returns>
    public static CausalNode CreateEvent(string name, string description)
    {
        return Create(name, description, CausalNodeType.Event);
    }
}

/// <summary>
/// Represents a directed edge in the causal graph indicating a cause-effect relationship.
/// </summary>
/// <param name="SourceId">The ID of the source (cause) node.</param>
/// <param name="TargetId">The ID of the target (effect) node.</param>
/// <param name="Strength">The strength/probability of the causal relationship (0.0 to 1.0).</param>
/// <param name="Condition">Optional condition that must be met for the causation to occur.</param>
public sealed record CausalEdge(
    Guid SourceId,
    Guid TargetId,
    double Strength,
    Option<string> Condition)
{
    /// <summary>
    /// Creates a new causal edge with no condition.
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <param name="strength">The strength of the causal relationship.</param>
    /// <returns>A new causal edge.</returns>
    public static CausalEdge Create(Guid sourceId, Guid targetId, double strength)
    {
        double clampedStrength = Math.Clamp(strength, 0.0, 1.0);
        return new CausalEdge(sourceId, targetId, clampedStrength, Option<string>.None());
    }

    /// <summary>
    /// Creates a new causal edge with a condition.
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <param name="strength">The strength of the causal relationship.</param>
    /// <param name="condition">The condition for the causation.</param>
    /// <returns>A new causal edge.</returns>
    public static CausalEdge Create(Guid sourceId, Guid targetId, double strength, string condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        double clampedStrength = Math.Clamp(strength, 0.0, 1.0);
        return new CausalEdge(sourceId, targetId, clampedStrength, Option<string>.Some(condition));
    }

    /// <summary>
    /// Creates a deterministic causal edge (strength = 1.0).
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <returns>A deterministic causal edge.</returns>
    public static CausalEdge Deterministic(Guid sourceId, Guid targetId)
    {
        return new CausalEdge(sourceId, targetId, 1.0, Option<string>.None());
    }
}

/// <summary>
/// Represents a predicted effect with its probability.
/// </summary>
/// <param name="Node">The node representing the predicted effect.</param>
/// <param name="Probability">The probability of this effect occurring (0.0 to 1.0).</param>
/// <param name="PathStrength">The cumulative strength along the causal path.</param>
public sealed record PredictedEffect(
    CausalNode Node,
    double Probability,
    double PathStrength);

/// <summary>
/// Represents a causal path between two nodes.
/// </summary>
/// <param name="Nodes">The ordered list of nodes in the path.</param>
/// <param name="Edges">The edges connecting the nodes.</param>
/// <param name="TotalStrength">The cumulative strength of the path (product of edge strengths).</param>
public sealed record CausalPath(
    ImmutableList<CausalNode> Nodes,
    ImmutableList<CausalEdge> Edges,
    double TotalStrength)
{
    /// <summary>
    /// Gets the length of the path (number of edges).
    /// </summary>
    public int Length => Edges.Count;

    /// <summary>
    /// Creates an empty path starting from a node.
    /// </summary>
    /// <param name="startNode">The starting node.</param>
    /// <returns>A new causal path with just the start node.</returns>
    public static CausalPath FromNode(CausalNode startNode)
    {
        ArgumentNullException.ThrowIfNull(startNode);

        return new CausalPath(
            ImmutableList.Create(startNode),
            ImmutableList<CausalEdge>.Empty,
            1.0);
    }

    /// <summary>
    /// Extends the path with a new node and edge.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <param name="edge">The edge connecting to the new node.</param>
    /// <returns>A new extended path.</returns>
    public CausalPath Extend(CausalNode node, CausalEdge edge)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(edge);

        return new CausalPath(
            Nodes.Add(node),
            Edges.Add(edge),
            TotalStrength * edge.Strength);
    }
}

/// <summary>
/// An immutable directed graph modeling cause-effect relationships for predictive planning.
/// Supports causal inference, effect prediction, and path finding between nodes.
/// </summary>
public sealed class CausalGraph
{
    private readonly ImmutableDictionary<Guid, CausalNode> nodes;
    private readonly ImmutableList<CausalEdge> edges;
    private readonly ImmutableDictionary<Guid, ImmutableList<CausalEdge>> outgoingEdges;
    private readonly ImmutableDictionary<Guid, ImmutableList<CausalEdge>> incomingEdges;

    /// <summary>
    /// Initializes a new instance of the <see cref="CausalGraph"/> class.
    /// </summary>
    private CausalGraph(
        ImmutableDictionary<Guid, CausalNode> nodes,
        ImmutableList<CausalEdge> edges,
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> outgoingEdges,
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> incomingEdges)
    {
        this.nodes = nodes;
        this.edges = edges;
        this.outgoingEdges = outgoingEdges;
        this.incomingEdges = incomingEdges;
    }

    /// <summary>
    /// Gets all nodes in the graph.
    /// </summary>
    public IReadOnlyCollection<CausalNode> Nodes => this.nodes.Values.ToImmutableList();

    /// <summary>
    /// Gets all edges in the graph.
    /// </summary>
    public IReadOnlyCollection<CausalEdge> Edges => this.edges;

    /// <summary>
    /// Gets the number of nodes in the graph.
    /// </summary>
    public int NodeCount => this.nodes.Count;

    /// <summary>
    /// Gets the number of edges in the graph.
    /// </summary>
    public int EdgeCount => this.edges.Count;

    /// <summary>
    /// Creates an empty causal graph.
    /// </summary>
    /// <returns>A new empty causal graph.</returns>
    public static CausalGraph Empty()
    {
        return new CausalGraph(
            ImmutableDictionary<Guid, CausalNode>.Empty,
            ImmutableList<CausalEdge>.Empty,
            ImmutableDictionary<Guid, ImmutableList<CausalEdge>>.Empty,
            ImmutableDictionary<Guid, ImmutableList<CausalEdge>>.Empty);
    }

    /// <summary>
    /// Creates a causal graph from collections of nodes and edges.
    /// </summary>
    /// <param name="nodes">The nodes to include.</param>
    /// <param name="edges">The edges to include.</param>
    /// <returns>A Result containing the new graph or an error if validation fails.</returns>
    public static Result<CausalGraph, string> Create(
        IEnumerable<CausalNode> nodes,
        IEnumerable<CausalEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        CausalGraph graph = Empty();

        foreach (CausalNode node in nodes)
        {
            Result<CausalGraph, string> nodeResult = graph.AddNode(node);
            if (nodeResult.IsFailure)
            {
                return nodeResult;
            }

            graph = nodeResult.Value;
        }

        foreach (CausalEdge edge in edges)
        {
            Result<CausalGraph, string> edgeResult = graph.AddEdge(edge);
            if (edgeResult.IsFailure)
            {
                return edgeResult;
            }

            graph = edgeResult.Value;
        }

        return Result<CausalGraph, string>.Success(graph);
    }

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <returns>A Result containing a new graph with the node added, or an error.</returns>
    public Result<CausalGraph, string> AddNode(CausalNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (this.nodes.ContainsKey(node.Id))
        {
            return Result<CausalGraph, string>.Failure($"Node with ID {node.Id} already exists in the graph.");
        }

        ImmutableDictionary<Guid, CausalNode> newNodes = this.nodes.Add(node.Id, node);

        return Result<CausalGraph, string>.Success(
            new CausalGraph(newNodes, this.edges, this.outgoingEdges, this.incomingEdges));
    }

    /// <summary>
    /// Adds an edge to the graph.
    /// </summary>
    /// <param name="edge">The edge to add.</param>
    /// <returns>A Result containing a new graph with the edge added, or an error.</returns>
    public Result<CausalGraph, string> AddEdge(CausalEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (!this.nodes.ContainsKey(edge.SourceId))
        {
            return Result<CausalGraph, string>.Failure($"Source node with ID {edge.SourceId} does not exist in the graph.");
        }

        if (!this.nodes.ContainsKey(edge.TargetId))
        {
            return Result<CausalGraph, string>.Failure($"Target node with ID {edge.TargetId} does not exist in the graph.");
        }

        ImmutableList<CausalEdge> newEdges = this.edges.Add(edge);

        ImmutableList<CausalEdge> existingOutgoing = this.outgoingEdges.GetValueOrDefault(
            edge.SourceId,
            ImmutableList<CausalEdge>.Empty);
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newOutgoing =
            this.outgoingEdges.SetItem(edge.SourceId, existingOutgoing.Add(edge));

        ImmutableList<CausalEdge> existingIncoming = this.incomingEdges.GetValueOrDefault(
            edge.TargetId,
            ImmutableList<CausalEdge>.Empty);
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newIncoming =
            this.incomingEdges.SetItem(edge.TargetId, existingIncoming.Add(edge));

        return Result<CausalGraph, string>.Success(
            new CausalGraph(this.nodes, newEdges, newOutgoing, newIncoming));
    }

    /// <summary>
    /// Removes a node and all its connected edges from the graph.
    /// </summary>
    /// <param name="nodeId">The ID of the node to remove.</param>
    /// <returns>A Result containing a new graph with the node removed, or an error.</returns>
    public Result<CausalGraph, string> RemoveNode(Guid nodeId)
    {
        if (!this.nodes.ContainsKey(nodeId))
        {
            return Result<CausalGraph, string>.Failure($"Node with ID {nodeId} does not exist in the graph.");
        }

        ImmutableDictionary<Guid, CausalNode> newNodes = this.nodes.Remove(nodeId);
        ImmutableList<CausalEdge> newEdges = this.edges
            .Where(e => e.SourceId != nodeId && e.TargetId != nodeId)
            .ToImmutableList();

        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newOutgoing = this.outgoingEdges.Remove(nodeId);
        ImmutableDictionary<Guid, ImmutableList<CausalEdge>> newIncoming = this.incomingEdges.Remove(nodeId);

        // Remove references to this node from other nodes' edge lists
        foreach (Guid key in newOutgoing.Keys.ToList())
        {
            ImmutableList<CausalEdge> filtered = newOutgoing[key]
                .Where(e => e.TargetId != nodeId)
                .ToImmutableList();
            newOutgoing = newOutgoing.SetItem(key, filtered);
        }

        foreach (Guid key in newIncoming.Keys.ToList())
        {
            ImmutableList<CausalEdge> filtered = newIncoming[key]
                .Where(e => e.SourceId != nodeId)
                .ToImmutableList();
            newIncoming = newIncoming.SetItem(key, filtered);
        }

        return Result<CausalGraph, string>.Success(
            new CausalGraph(newNodes, newEdges, newOutgoing, newIncoming));
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="id">The node ID.</param>
    /// <returns>An Option containing the node if found.</returns>
    public Option<CausalNode> GetNode(Guid id)
    {
        return this.nodes.TryGetValue(id, out CausalNode? node)
            ? Option<CausalNode>.Some(node)
            : Option<CausalNode>.None();
    }

    /// <summary>
    /// Gets a node by its name.
    /// </summary>
    /// <param name="name">The node name.</param>
    /// <returns>An Option containing the first node with the matching name if found.</returns>
    public Option<CausalNode> GetNodeByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        CausalNode? node = this.nodes.Values
            .FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return node is not null
            ? Option<CausalNode>.Some(node)
            : Option<CausalNode>.None();
    }

    /// <summary>
    /// Gets all nodes that cause the specified node (direct predecessors).
    /// </summary>
    /// <param name="nodeId">The ID of the target node.</param>
    /// <returns>A list of nodes that have causal edges pointing to the specified node.</returns>
    public IReadOnlyList<CausalNode> GetCauses(Guid nodeId)
    {
        if (!this.incomingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? incoming))
        {
            return ImmutableList<CausalNode>.Empty;
        }

        return incoming
            .Select(e => this.nodes.GetValueOrDefault(e.SourceId))
            .Where(n => n is not null)
            .Cast<CausalNode>()
            .ToImmutableList();
    }

    /// <summary>
    /// Gets all nodes that are effects of the specified node (direct successors).
    /// </summary>
    /// <param name="nodeId">The ID of the source node.</param>
    /// <returns>A list of nodes that the specified node has causal edges pointing to.</returns>
    public IReadOnlyList<CausalNode> GetEffects(Guid nodeId)
    {
        if (!this.outgoingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? outgoing))
        {
            return ImmutableList<CausalNode>.Empty;
        }

        return outgoing
            .Select(e => this.nodes.GetValueOrDefault(e.TargetId))
            .Where(n => n is not null)
            .Cast<CausalNode>()
            .ToImmutableList();
    }

    /// <summary>
    /// Gets the causal edges between two nodes.
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <returns>A list of edges connecting the source to the target.</returns>
    public IReadOnlyList<CausalEdge> GetEdgesBetween(Guid sourceId, Guid targetId)
    {
        if (!this.outgoingEdges.TryGetValue(sourceId, out ImmutableList<CausalEdge>? outgoing))
        {
            return ImmutableList<CausalEdge>.Empty;
        }

        return outgoing
            .Where(e => e.TargetId == targetId)
            .ToImmutableList();
    }

    /// <summary>
    /// Predicts the effects of taking an action, returning affected nodes with their probabilities.
    /// Uses breadth-first traversal to calculate cumulative probabilities along causal chains.
    /// </summary>
    /// <param name="actionNodeId">The ID of the action node to predict effects for.</param>
    /// <param name="maxDepth">Maximum depth of causal chains to explore (default: 5).</param>
    /// <param name="minProbability">Minimum probability threshold to continue exploration (default: 0.01).</param>
    /// <returns>A Result containing predicted effects or an error.</returns>
    public Result<IReadOnlyList<PredictedEffect>, string> PredictEffects(
        Guid actionNodeId,
        int maxDepth = 5,
        double minProbability = 0.01)
    {
        if (!this.nodes.TryGetValue(actionNodeId, out CausalNode? actionNode))
        {
            return Result<IReadOnlyList<PredictedEffect>, string>.Failure(
                $"Node with ID {actionNodeId} does not exist in the graph.");
        }

        Dictionary<Guid, PredictedEffect> effects = new();
        Queue<(Guid NodeId, double CumulativeProbability, int Depth)> queue = new();

        // Initialize with direct effects
        if (this.outgoingEdges.TryGetValue(actionNodeId, out ImmutableList<CausalEdge>? directEdges))
        {
            foreach (CausalEdge edge in directEdges)
            {
                queue.Enqueue((edge.TargetId, edge.Strength, 1));
            }
        }

        while (queue.Count > 0)
        {
            (Guid nodeId, double probability, int depth) = queue.Dequeue();

            if (depth > maxDepth || probability < minProbability)
            {
                continue;
            }

            if (!this.nodes.TryGetValue(nodeId, out CausalNode? node))
            {
                continue;
            }

            // Update or add the effect with the maximum probability seen
            if (!effects.TryGetValue(nodeId, out PredictedEffect? existing) ||
                existing.Probability < probability)
            {
                effects[nodeId] = new PredictedEffect(node, probability, probability);
            }

            // Continue to downstream effects
            if (this.outgoingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? outgoing))
            {
                foreach (CausalEdge edge in outgoing)
                {
                    double newProbability = probability * edge.Strength;
                    if (newProbability >= minProbability)
                    {
                        queue.Enqueue((edge.TargetId, newProbability, depth + 1));
                    }
                }
            }
        }

        IReadOnlyList<PredictedEffect> sortedEffects = effects.Values
            .OrderByDescending(e => e.Probability)
            .ToImmutableList();

        return Result<IReadOnlyList<PredictedEffect>, string>.Success(sortedEffects);
    }

    /// <summary>
    /// Finds the shortest causal path between two nodes using breadth-first search.
    /// </summary>
    /// <param name="fromId">The starting node ID.</param>
    /// <param name="toId">The destination node ID.</param>
    /// <returns>An Option containing the path if one exists.</returns>
    public Option<CausalPath> FindPath(Guid fromId, Guid toId)
    {
        if (!this.nodes.TryGetValue(fromId, out CausalNode? fromNode) ||
            !this.nodes.TryGetValue(toId, out CausalNode? _))
        {
            return Option<CausalPath>.None();
        }

        if (fromId == toId)
        {
            return Option<CausalPath>.Some(CausalPath.FromNode(fromNode));
        }

        Queue<CausalPath> queue = new();
        HashSet<Guid> visited = new() { fromId };

        queue.Enqueue(CausalPath.FromNode(fromNode));

        while (queue.Count > 0)
        {
            CausalPath currentPath = queue.Dequeue();
            CausalNode lastNode = currentPath.Nodes[^1];

            if (!this.outgoingEdges.TryGetValue(lastNode.Id, out ImmutableList<CausalEdge>? outgoing))
            {
                continue;
            }

            foreach (CausalEdge edge in outgoing)
            {
                if (visited.Contains(edge.TargetId))
                {
                    continue;
                }

                if (!this.nodes.TryGetValue(edge.TargetId, out CausalNode? targetNode))
                {
                    continue;
                }

                CausalPath newPath = currentPath.Extend(targetNode, edge);

                if (edge.TargetId == toId)
                {
                    return Option<CausalPath>.Some(newPath);
                }

                visited.Add(edge.TargetId);
                queue.Enqueue(newPath);
            }
        }

        return Option<CausalPath>.None();
    }

    /// <summary>
    /// Finds all causal paths between two nodes up to a maximum depth.
    /// </summary>
    /// <param name="fromId">The starting node ID.</param>
    /// <param name="toId">The destination node ID.</param>
    /// <param name="maxDepth">Maximum path length to search (default: 10).</param>
    /// <returns>A list of all paths found.</returns>
    public IReadOnlyList<CausalPath> FindAllPaths(Guid fromId, Guid toId, int maxDepth = 10)
    {
        List<CausalPath> results = new();

        if (!this.nodes.TryGetValue(fromId, out CausalNode? fromNode) ||
            !this.nodes.TryGetValue(toId, out CausalNode? _))
        {
            return results;
        }

        if (fromId == toId)
        {
            results.Add(CausalPath.FromNode(fromNode));
            return results;
        }

        Stack<(CausalPath Path, HashSet<Guid> Visited)> stack = new();
        HashSet<Guid> initialVisited = new() { fromId };
        stack.Push((CausalPath.FromNode(fromNode), initialVisited));

        while (stack.Count > 0)
        {
            (CausalPath currentPath, HashSet<Guid> visited) = stack.Pop();

            if (currentPath.Length >= maxDepth)
            {
                continue;
            }

            CausalNode lastNode = currentPath.Nodes[^1];

            if (!this.outgoingEdges.TryGetValue(lastNode.Id, out ImmutableList<CausalEdge>? outgoing))
            {
                continue;
            }

            foreach (CausalEdge edge in outgoing)
            {
                if (visited.Contains(edge.TargetId))
                {
                    continue;
                }

                if (!this.nodes.TryGetValue(edge.TargetId, out CausalNode? targetNode))
                {
                    continue;
                }

                CausalPath newPath = currentPath.Extend(targetNode, edge);

                if (edge.TargetId == toId)
                {
                    results.Add(newPath);
                    continue;
                }

                HashSet<Guid> newVisited = new(visited) { edge.TargetId };
                stack.Push((newPath, newVisited));
            }
        }

        return results.OrderByDescending(p => p.TotalStrength).ToImmutableList();
    }

    /// <summary>
    /// Gets all root nodes (nodes with no incoming edges).
    /// </summary>
    /// <returns>A list of root nodes.</returns>
    public IReadOnlyList<CausalNode> GetRootNodes()
    {
        return this.nodes.Values
            .Where(n => !this.incomingEdges.ContainsKey(n.Id) ||
                        this.incomingEdges[n.Id].IsEmpty)
            .ToImmutableList();
    }

    /// <summary>
    /// Gets all leaf nodes (nodes with no outgoing edges).
    /// </summary>
    /// <returns>A list of leaf nodes.</returns>
    public IReadOnlyList<CausalNode> GetLeafNodes()
    {
        return this.nodes.Values
            .Where(n => !this.outgoingEdges.ContainsKey(n.Id) ||
                        this.outgoingEdges[n.Id].IsEmpty)
            .ToImmutableList();
    }

    /// <summary>
    /// Checks if the graph contains a cycle.
    /// </summary>
    /// <returns>True if the graph contains at least one cycle.</returns>
    public bool HasCycle()
    {
        HashSet<Guid> visited = new();
        HashSet<Guid> recursionStack = new();

        foreach (Guid nodeId in this.nodes.Keys)
        {
            if (HasCycleUtil(nodeId, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets nodes by their type.
    /// </summary>
    /// <param name="nodeType">The type of nodes to retrieve.</param>
    /// <returns>A list of nodes of the specified type.</returns>
    public IReadOnlyList<CausalNode> GetNodesByType(CausalNodeType nodeType)
    {
        return this.nodes.Values
            .Where(n => n.NodeType == nodeType)
            .ToImmutableList();
    }

    /// <summary>
    /// Calculates the total causal strength from one node to another,
    /// considering all possible paths.
    /// </summary>
    /// <param name="fromId">The source node ID.</param>
    /// <param name="toId">The target node ID.</param>
    /// <param name="maxDepth">Maximum path depth to consider.</param>
    /// <returns>The combined causal strength (using probabilistic OR).</returns>
    public double CalculateTotalCausalStrength(Guid fromId, Guid toId, int maxDepth = 10)
    {
        IReadOnlyList<CausalPath> paths = FindAllPaths(fromId, toId, maxDepth);

        if (paths.Count == 0)
        {
            return 0.0;
        }

        // Combine probabilities using probabilistic OR: P(A or B) = P(A) + P(B) - P(A and B)
        // For independent paths: P(at least one) = 1 - (1 - P1)(1 - P2)...
        double probabilityNone = 1.0;
        foreach (CausalPath path in paths)
        {
            probabilityNone *= (1.0 - path.TotalStrength);
        }

        return 1.0 - probabilityNone;
    }

    /// <summary>
    /// Creates a subgraph containing only the specified nodes and edges between them.
    /// </summary>
    /// <param name="nodeIds">The IDs of nodes to include in the subgraph.</param>
    /// <returns>A Result containing the subgraph or an error.</returns>
    public Result<CausalGraph, string> CreateSubgraph(IEnumerable<Guid> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);

        HashSet<Guid> nodeIdSet = nodeIds.ToHashSet();
        List<CausalNode> subgraphNodes = new();
        List<CausalEdge> subgraphEdges = new();

        foreach (Guid nodeId in nodeIdSet)
        {
            if (this.nodes.TryGetValue(nodeId, out CausalNode? node))
            {
                subgraphNodes.Add(node);
            }
            else
            {
                return Result<CausalGraph, string>.Failure($"Node with ID {nodeId} does not exist in the graph.");
            }
        }

        foreach (CausalEdge edge in this.edges)
        {
            if (nodeIdSet.Contains(edge.SourceId) && nodeIdSet.Contains(edge.TargetId))
            {
                subgraphEdges.Add(edge);
            }
        }

        return Create(subgraphNodes, subgraphEdges);
    }

    /// <summary>
    /// Helper method for cycle detection using DFS.
    /// </summary>
    private bool HasCycleUtil(Guid nodeId, HashSet<Guid> visited, HashSet<Guid> recursionStack)
    {
        if (recursionStack.Contains(nodeId))
        {
            return true;
        }

        if (visited.Contains(nodeId))
        {
            return false;
        }

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        if (this.outgoingEdges.TryGetValue(nodeId, out ImmutableList<CausalEdge>? outgoing))
        {
            foreach (CausalEdge edge in outgoing)
            {
                if (HasCycleUtil(edge.TargetId, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(nodeId);
        return false;
    }
}
