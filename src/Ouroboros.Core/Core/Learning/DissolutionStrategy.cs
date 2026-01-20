// <copyright file="DissolutionStrategy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Strategies for dissolving distinctions.
/// </summary>
public enum DissolutionStrategy
{
    /// <summary>
    /// Dissolve distinctions below a fitness threshold.
    /// </summary>
    FitnessThreshold,

    /// <summary>
    /// Dissolve oldest distinctions first.
    /// </summary>
    OldestFirst,

    /// <summary>
    /// Dissolve least recently used distinctions.
    /// </summary>
    LeastRecentlyUsed,

    /// <summary>
    /// Dissolve all distinctions.
    /// </summary>
    All,
}
