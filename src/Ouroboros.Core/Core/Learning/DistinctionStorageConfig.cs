// <copyright file="DistinctionStorageConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Configuration for distinction weight storage.
/// </summary>
public sealed record DistinctionStorageConfig(
    string BaseDirectory = "/var/ouroboros/distinctions",
    long MaxWeightSizeBytes = 5 * 1024 * 1024,
    long MaxTotalStorageBytes = 500 * 1024 * 1024,
    bool ArchiveOnDissolution = true,
    TimeSpan DissolvedRetentionPeriod = default)
{
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static DistinctionStorageConfig Default => new(
        DissolvedRetentionPeriod: TimeSpan.FromDays(30));
}
