// <copyright file="DistinctionWeights.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents the learned weights and metadata for a distinction.
/// This combines PEFT adapter weights with distinction-specific information.
/// </summary>
/// <param name="Id">Unique identifier for this distinction.</param>
/// <param name="Embedding">Semantic embedding of the distinction context.</param>
/// <param name="DissolutionMask">Mask indicating which weights to remove during dissolution.</param>
/// <param name="RecognitionTransform">Transform applied during recognition stage.</param>
/// <param name="LearnedAtStage">The dream stage at which this distinction was learned.</param>
/// <param name="Fitness">Current fitness score (0.0 to 1.0) based on prediction accuracy.</param>
/// <param name="Circumstance">The original circumstance that led to this distinction.</param>
/// <param name="CreatedAt">Timestamp when the distinction was created.</param>
/// <param name="LastUpdatedAt">Timestamp of last update (null if never updated).</param>
public sealed record DistinctionWeights(
    DistinctionId Id,
    float[] Embedding,
    float[] DissolutionMask,
    float[] RecognitionTransform,
    DreamStage LearnedAtStage,
    double Fitness,
    string Circumstance,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt)
{
    /// <summary>
    /// Updates the fitness score based on prediction outcome.
    /// Uses exponential moving average to smooth updates.
    /// </summary>
    /// <param name="correct">Whether the prediction was correct.</param>
    /// <param name="alpha">Smoothing factor (0.0 to 1.0). Default: 0.3.</param>
    /// <returns>Updated distinction weights with new fitness.</returns>
    public DistinctionWeights UpdateFitness(bool correct, double alpha = 0.3)
    {
        var newScore = correct ? 1.0 : 0.0;
        var updatedFitness = (alpha * newScore) + ((1.0 - alpha) * this.Fitness);
        return this with
        {
            Fitness = updatedFitness,
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Checks if this distinction should be dissolved based on fitness threshold.
    /// </summary>
    /// <param name="threshold">Minimum fitness threshold. Default: 0.3.</param>
    /// <returns>True if fitness is below threshold.</returns>
    public bool ShouldDissolve(double threshold = 0.3) => this.Fitness < threshold;
}
