// <copyright file="WalEntry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Network.Persistence;

/// <summary>
/// Represents the type of entry in the Write-Ahead Log.
/// </summary>
public enum WalEntryType
{
    /// <summary>
    /// Entry represents addition of a node to the DAG.
    /// </summary>
    AddNode,

    /// <summary>
    /// Entry represents addition of an edge to the DAG.
    /// </summary>
    AddEdge,
}

/// <summary>
/// Represents a single entry in the Write-Ahead Log.
/// Each entry captures either a node or edge addition with its serialized payload.
/// </summary>
public sealed record WalEntry(
    WalEntryType Type,
    DateTimeOffset Timestamp,
    string PayloadJson);
