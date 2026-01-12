// <copyright file="DreamEmbedding.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Learning;

using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents embeddings for a complete dream cycle.
/// Each stage of consciousness has its own vector representation,
/// with Recognition weighted highest in the composite.
/// </summary>
/// <param name="Circumstance">The circumstance that triggered this dream cycle.</param>
/// <param name="StageEmbeddings">Vector embeddings for each dream stage.</param>
/// <param name="CompositeEmbedding">Weighted composite of all stage embeddings.</param>
/// <param name="Timestamp">When this embedding was created.</param>
public sealed record DreamEmbedding(
    string Circumstance,
    IReadOnlyDictionary<DreamStage, float[]> StageEmbeddings,
    float[] CompositeEmbedding,
    DateTime Timestamp)
{
    /// <summary>
    /// Gets the embedding for a specific dream stage.
    /// </summary>
    /// <param name="stage">The dream stage.</param>
    /// <returns>The embedding for that stage, or null if not present.</returns>
    public float[]? GetStageEmbedding(DreamStage stage)
    {
        return StageEmbeddings.TryGetValue(stage, out var embedding) ? embedding : null;
    }

    /// <summary>
    /// Computes similarity between this embedding and another.
    /// Uses cosine similarity on composite embeddings.
    /// </summary>
    /// <param name="other">The other dream embedding.</param>
    /// <returns>Similarity score (0.0 to 1.0).</returns>
    public double ComputeSimilarity(DreamEmbedding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        
        return CosineSimilarity(CompositeEmbedding, other.CompositeEmbedding);
    }

    /// <summary>
    /// Computes cosine similarity between two vectors.
    /// </summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0.0;
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0.0 || normB == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
