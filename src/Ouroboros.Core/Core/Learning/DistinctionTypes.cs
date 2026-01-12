// <copyright file="DistinctionTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Unique identifier for a distinction.
/// </summary>
public readonly record struct DistinctionId(Guid Value)
{
    /// <summary>
    /// Creates a new distinction ID.
    /// </summary>
    /// <returns>A new distinction ID.</returns>
    public static DistinctionId NewId() => new(Guid.NewGuid());

    /// <summary>
    /// Returns the string representation of the distinction ID.
    /// </summary>
    /// <returns>The ID as a string without hyphens.</returns>
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// The actual weight data for a distinction.
/// </summary>
public sealed record DistinctionWeights(
    DistinctionId Id,
    float[] Embedding,
    float[] DissolutionMask,
    float[] RecognitionTransform,
    int LearnedAtStage,
    double Fitness,
    string Circumstance,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

/// <summary>
/// Context for the Recognition stage merge.
/// </summary>
public sealed record RecognitionContext(
    string Circumstance,
    float[] SelfEmbedding,
    int CurrentStage);

/// <summary>
/// Lightweight info about stored weights (for listing).
/// </summary>
public sealed record DistinctionWeightInfo(
    DistinctionId Id,
    string Path,
    int LearnedAtStage,
    double Fitness,
    long SizeBytes,
    DateTime CreatedAt,
    bool IsDissolved);
