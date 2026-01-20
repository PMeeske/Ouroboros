// <copyright file="DistinctionWeightMetadata.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Metadata for stored distinction weights.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Path">File path to the weights.</param>
/// <param name="Fitness">Fitness score.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="IsDissolved">Whether the weights have been dissolved.</param>
public record DistinctionWeightMetadata(
    string Id,
    string Path,
    double Fitness,
    DateTime CreatedAt,
    bool IsDissolved);
