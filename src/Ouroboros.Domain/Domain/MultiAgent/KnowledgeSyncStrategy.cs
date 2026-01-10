// <copyright file="KnowledgeSyncStrategy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Strategy for synchronizing knowledge between agents.
/// </summary>
public enum KnowledgeSyncStrategy
{
    /// <summary>
    /// Complete knowledge transfer - all knowledge is shared.
    /// </summary>
    Full,

    /// <summary>
    /// Only new knowledge since last sync is transferred.
    /// </summary>
    Incremental,

    /// <summary>
    /// Only relevant knowledge for current tasks is shared.
    /// </summary>
    Selective,

    /// <summary>
    /// Probabilistic propagation where knowledge spreads through network.
    /// </summary>
    Gossip,
}
