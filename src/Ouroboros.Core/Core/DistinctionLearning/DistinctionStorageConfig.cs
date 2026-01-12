// <copyright file="DistinctionStorageConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Configuration for distinction weight storage.
/// </summary>
public sealed record DistinctionStorageConfig(
    string StoragePath,
    long MaxTotalStorageBytes,
    TimeSpan DissolvedRetentionPeriod)
{
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static DistinctionStorageConfig Default => new(
        StoragePath: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ouroboros", "Distinctions"),
        MaxTotalStorageBytes: 1024L * 1024 * 1024, // 1 GB
        DissolvedRetentionPeriod: TimeSpan.FromDays(30));
}
