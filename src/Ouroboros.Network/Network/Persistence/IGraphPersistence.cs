// <copyright file="IGraphPersistence.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Network.Persistence;

/// <summary>
/// Defines the contract for persisting Merkle-DAG nodes and edges to durable storage.
/// Implements an append-only Write-Ahead Log pattern for crash recovery.
/// </summary>
public interface IGraphPersistence : IAsyncDisposable
{
    /// <summary>
    /// Appends a node addition to the Write-Ahead Log.
    /// </summary>
    /// <param name="node">The node to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AppendNodeAsync(MonadNode node, CancellationToken ct = default);

    /// <summary>
    /// Appends an edge addition to the Write-Ahead Log.
    /// </summary>
    /// <param name="edge">The edge to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AppendEdgeAsync(TransitionEdge edge, CancellationToken ct = default);

    /// <summary>
    /// Flushes all pending writes to durable storage.
    /// Ensures durability for all previously appended entries.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    Task FlushAsync(CancellationToken ct = default);

    /// <summary>
    /// Replays all entries from the Write-Ahead Log in chronological order.
    /// Used during recovery to rebuild the in-memory DAG state.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of WAL entries.</returns>
    IAsyncEnumerable<WalEntry> ReplayAsync(CancellationToken ct = default);
}
