// <copyright file="GlobalNetworkState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Network;

/// <summary>
/// Represents a snapshot of the global network state at a specific point in time.
/// Aggregates information from all nodes and transitions in the DAG.
/// </summary>
public sealed record GlobalNetworkState
{
    /// <summary>
    /// Gets the epoch number for this snapshot.
    /// </summary>
    public long Epoch { get; init; }

    /// <summary>
    /// Gets the timestamp of this snapshot.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the total number of nodes in the network.
    /// </summary>
    public int TotalNodes { get; init; }

    /// <summary>
    /// Gets the total number of transitions in the network.
    /// </summary>
    public int TotalTransitions { get; init; }

    /// <summary>
    /// Gets the count of nodes by type.
    /// </summary>
    public ImmutableDictionary<string, int> NodeCountByType { get; init; }

    /// <summary>
    /// Gets the count of transitions by operation.
    /// </summary>
    public ImmutableDictionary<string, int> TransitionCountByOperation { get; init; }

    /// <summary>
    /// Gets the IDs of root nodes (nodes with no parents).
    /// </summary>
    public ImmutableArray<Guid> RootNodeIds { get; init; }

    /// <summary>
    /// Gets the IDs of leaf nodes (nodes with no children).
    /// </summary>
    public ImmutableArray<Guid> LeafNodeIds { get; init; }

    /// <summary>
    /// Gets the average confidence across all transitions (if available).
    /// </summary>
    public double? AverageConfidence { get; init; }

    /// <summary>
    /// Gets the total processing time in milliseconds across all transitions.
    /// </summary>
    public long? TotalProcessingTimeMs { get; init; }

    /// <summary>
    /// Gets the metadata associated with this snapshot.
    /// </summary>
    public ImmutableDictionary<string, string> Metadata { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalNetworkState"/> class.
    /// </summary>
    public GlobalNetworkState(
        long epoch,
        DateTimeOffset timestamp,
        int totalNodes,
        int totalTransitions,
        ImmutableDictionary<string, int> nodeCountByType,
        ImmutableDictionary<string, int> transitionCountByOperation,
        ImmutableArray<Guid> rootNodeIds,
        ImmutableArray<Guid> leafNodeIds,
        double? averageConfidence = null,
        long? totalProcessingTimeMs = null,
        ImmutableDictionary<string, string>? metadata = null)
    {
        this.Epoch = epoch;
        this.Timestamp = timestamp;
        this.TotalNodes = totalNodes;
        this.TotalTransitions = totalTransitions;
        this.NodeCountByType = nodeCountByType ?? ImmutableDictionary<string, int>.Empty;
        this.TransitionCountByOperation = transitionCountByOperation ?? ImmutableDictionary<string, int>.Empty;
        this.RootNodeIds = rootNodeIds;
        this.LeafNodeIds = leafNodeIds;
        this.AverageConfidence = averageConfidence;
        this.TotalProcessingTimeMs = totalProcessingTimeMs;
        this.Metadata = metadata ?? ImmutableDictionary<string, string>.Empty;
    }
}
