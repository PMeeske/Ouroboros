// <copyright file="ReorganizationStats.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Statistics for knowledge reorganization operations.
/// Tracks patterns, hot content, and co-access clusters.
/// </summary>
/// <param name="TrackedPatterns">Number of patterns being tracked.</param>
/// <param name="HotContentCount">Number of hot content items identified.</param>
/// <param name="CoAccessClusters">Number of co-access clusters formed.</param>
public sealed record ReorganizationStats(
    int TrackedPatterns,
    int HotContentCount,
    int CoAccessClusters);
