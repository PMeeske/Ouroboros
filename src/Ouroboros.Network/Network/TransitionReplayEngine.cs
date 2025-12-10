// <copyright file="TransitionReplayEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Network;

/// <summary>
/// Engine for replaying transitions in the Merkle-DAG.
/// Provides query and traversal capabilities for the transition history.
/// </summary>
public sealed class TransitionReplayEngine
{
    private readonly MerkleDag dag;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionReplayEngine"/> class.
    /// </summary>
    /// <param name="dag">The Merkle-DAG to replay from.</param>
    public TransitionReplayEngine(MerkleDag dag)
    {
        this.dag = dag ?? throw new ArgumentNullException(nameof(dag));
    }

    /// <summary>
    /// Replays the transition path from a root node to a target node.
    /// </summary>
    /// <param name="targetNodeId">The target node ID.</param>
    /// <returns>A Result containing the ordered list of transitions or an error.</returns>
    public Result<ImmutableArray<TransitionEdge>> ReplayPathToNode(Guid targetNodeId)
    {
        var nodeOption = this.dag.GetNode(targetNodeId);
        if (!nodeOption.HasValue)
        {
            return Result<ImmutableArray<TransitionEdge>>.Failure($"Target node {targetNodeId} not found");
        }

        var path = new List<TransitionEdge>();
        var visited = new HashSet<Guid>();
        
        if (!this.BuildPathRecursive(targetNodeId, path, visited))
        {
            return Result<ImmutableArray<TransitionEdge>>.Failure("Failed to build complete path (possible cycle)");
        }

        path.Reverse(); // Reverse to get chronological order
        return Result<ImmutableArray<TransitionEdge>>.Success(path.ToImmutableArray());
    }

    /// <summary>
    /// Gets all transition chains of a specific operation type.
    /// </summary>
    /// <param name="operationName">The operation name to filter by.</param>
    /// <returns>A collection of transition chains.</returns>
    public IEnumerable<ImmutableArray<TransitionEdge>> GetTransitionChainsByOperation(string operationName)
    {
        var matchingEdges = this.dag.GetTransitionsByOperation(operationName).ToList();
        var chains = new List<ImmutableArray<TransitionEdge>>();

        foreach (var edge in matchingEdges)
        {
            var chain = new List<TransitionEdge> { edge };
            this.ExtendChain(chain, edge.OutputId);
            chains.Add(chain.ToImmutableArray());
        }

        return chains;
    }

    /// <summary>
    /// Gets the transition history for a specific node (all transitions that led to it).
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>An ordered list of transitions leading to the node.</returns>
    public ImmutableArray<TransitionEdge> GetNodeHistory(Guid nodeId)
    {
        var result = this.ReplayPathToNode(nodeId);
        return result.IsSuccess ? result.Value : ImmutableArray<TransitionEdge>.Empty;
    }

    /// <summary>
    /// Queries transitions by various criteria.
    /// </summary>
    /// <param name="predicate">The predicate to filter transitions.</param>
    /// <returns>A collection of matching transitions.</returns>
    public IEnumerable<TransitionEdge> QueryTransitions(Func<TransitionEdge, bool> predicate)
    {
        return this.dag.Edges.Values.Where(predicate);
    }

    /// <summary>
    /// Queries nodes by various criteria.
    /// </summary>
    /// <param name="predicate">The predicate to filter nodes.</param>
    /// <returns>A collection of matching nodes.</returns>
    public IEnumerable<MonadNode> QueryNodes(Func<MonadNode, bool> predicate)
    {
        return this.dag.Nodes.Values.Where(predicate);
    }

    /// <summary>
    /// Gets all transitions within a time range.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <returns>A collection of transitions within the time range.</returns>
    public IEnumerable<TransitionEdge> GetTransitionsInTimeRange(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        return this.QueryTransitions(e => e.CreatedAt >= startTime && e.CreatedAt <= endTime);
    }

    /// <summary>
    /// Gets all nodes within a time range.
    /// </summary>
    /// <param name="startTime">The start time.</param>
    /// <param name="endTime">The end time.</param>
    /// <returns>A collection of nodes within the time range.</returns>
    public IEnumerable<MonadNode> GetNodesInTimeRange(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        return this.QueryNodes(n => n.CreatedAt >= startTime && n.CreatedAt <= endTime);
    }

    /// <summary>
    /// Builds the path recursively from a target node to its roots.
    /// </summary>
    private bool BuildPathRecursive(Guid nodeId, List<TransitionEdge> path, HashSet<Guid> visited)
    {
        if (visited.Contains(nodeId))
        {
            return false; // Cycle detected
        }

        visited.Add(nodeId);

        var incomingEdges = this.dag.GetIncomingEdges(nodeId).ToList();
        if (incomingEdges.Count == 0)
        {
            return true; // Reached a root node
        }

        // For simplicity, follow the first incoming edge
        // In a more complex scenario, you might want to follow all paths
        var edge = incomingEdges.First();
        path.Add(edge);

        // Continue from the first input node
        return this.BuildPathRecursive(edge.InputIds[0], path, visited);
    }

    /// <summary>
    /// Extends a transition chain forward from a given node.
    /// </summary>
    private void ExtendChain(List<TransitionEdge> chain, Guid nodeId)
    {
        var outgoingEdges = this.dag.GetOutgoingEdges(nodeId).ToList();
        if (outgoingEdges.Count > 0)
        {
            var edge = outgoingEdges.First();
            chain.Add(edge);
            this.ExtendChain(chain, edge.OutputId);
        }
    }
}
