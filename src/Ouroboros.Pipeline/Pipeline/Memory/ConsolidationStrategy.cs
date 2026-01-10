// <copyright file="ConsolidationStrategy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

/// <summary>
/// Defines strategies for consolidating episodic memories over time.
/// </summary>
public enum ConsolidationStrategy
{
    /// <summary>
    /// Summarize similar episodes into compressed representations.
    /// </summary>
    Compress,

    /// <summary>
    /// Extract patterns and rules from episodes.
    /// </summary>
    Abstract,

    /// <summary>
    /// Remove low-value memories to reduce storage.
    /// </summary>
    Prune,

    /// <summary>
    /// Build abstraction hierarchies from episodes.
    /// </summary>
    Hierarchical,
}
