// <copyright file="DistinctionStorageConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Configuration for distinction weight storage.
/// </summary>
/// <param name="StoragePath">Path to the storage directory.</param>
/// <param name="DissolvedRetentionPeriod">Retention period for dissolved weights.</param>
/// <param name="MaxTotalStorageBytes">Maximum total storage size in bytes.</param>
public record DistinctionStorageConfig(
    string StoragePath,
    TimeSpan DissolvedRetentionPeriod = default,
    long MaxTotalStorageBytes = 1024L * 1024L * 1024L) // Default 1GB
{
    /// <summary>
    /// Gets the retention period for dissolved weights, defaulting to 30 days.
    /// </summary>
    public TimeSpan DissolvedRetentionPeriod { get; init; } = DissolvedRetentionPeriod == default ? TimeSpan.FromDays(30) : DissolvedRetentionPeriod;

    /// <summary>
    /// Gets the maximum total storage size, defaulting to 1GB.
    /// </summary>
    public long MaxTotalStorageBytes { get; init; } = MaxTotalStorageBytes;
}
