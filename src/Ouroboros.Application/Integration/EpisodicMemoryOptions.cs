// <copyright file="EpisodicMemoryOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Configuration options for episodic memory system.
/// Controls retention, capacity, and retrieval behavior.
/// </summary>
public sealed class EpisodicMemoryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of episodes to retain.
    /// Default: 10,000 episodes.
    /// </summary>
    public int MaxEpisodes { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the retention period for episodes.
    /// Default: 30 days.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the maximum number of episodes to retrieve per query.
    /// Default: 50 episodes.
    /// </summary>
    public int MaxRetrievalSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets the similarity threshold for episode retrieval.
    /// Range: 0.0 to 1.0. Default: 0.7.
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic consolidation.
    /// When enabled, similar episodes are merged over time.
    /// Default: true.
    /// </summary>
    public bool EnableConsolidation { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval for running consolidation.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan ConsolidationInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets a value indicating whether to persist episodes to storage.
    /// Default: true.
    /// </summary>
    public bool EnablePersistence { get; set; } = true;

    /// <summary>
    /// Gets or sets the storage connection string.
    /// Default: null (uses in-memory storage).
    /// </summary>
    public string? StorageConnectionString { get; set; }
}
