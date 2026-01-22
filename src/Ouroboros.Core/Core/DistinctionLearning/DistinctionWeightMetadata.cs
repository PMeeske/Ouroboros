// <copyright file="DistinctionWeightMetadata.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Metadata for stored distinction weights.
/// </summary>
public sealed record DistinctionWeightMetadata(
    string Id,
    string Path,
    double Fitness,
    string LearnedAtStage,
    DateTime CreatedAt,
    bool IsDissolved,
    long SizeBytes);
