// <copyright file="TaskEmbedding.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents an embedding of a task in a continuous vector space.
/// Used for computing task similarity and transfer learning.
/// </summary>
/// <param name="Vector">The embedding vector.</param>
/// <param name="Characteristics">Named characteristics extracted from the task.</param>
/// <param name="TaskDescription">Human-readable description of the task.</param>
public sealed record TaskEmbedding(
    float[] Vector,
    Dictionary<string, double> Characteristics,
    string TaskDescription)
{
    /// <summary>
    /// Computes cosine similarity between two task embeddings.
    /// </summary>
    /// <param name="other">The other task embedding.</param>
    /// <returns>Similarity score between 0 and 1.</returns>
    public double CosineSimilarity(TaskEmbedding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Vector.Length != other.Vector.Length)
        {
            throw new ArgumentException("Vectors must have the same dimension", nameof(other));
        }

        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (var i = 0; i < Vector.Length; i++)
        {
            dotProduct += Vector[i] * other.Vector[i];
            normA += Vector[i] * Vector[i];
            normB += other.Vector[i] * other.Vector[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        var similarity = dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));

        // Clamp to [0, 1] range due to floating-point precision errors
        return Math.Min(1.0, Math.Max(0.0, similarity));
    }

    /// <summary>
    /// Computes Euclidean distance between two task embeddings.
    /// </summary>
    /// <param name="other">The other task embedding.</param>
    /// <returns>Euclidean distance.</returns>
    public double EuclideanDistance(TaskEmbedding other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Vector.Length != other.Vector.Length)
        {
            throw new ArgumentException("Vectors must have the same dimension", nameof(other));
        }

        var sumSquares = 0.0;
        for (var i = 0; i < Vector.Length; i++)
        {
            var diff = Vector[i] - other.Vector[i];
            sumSquares += diff * diff;
        }

        return Math.Sqrt(sumSquares);
    }

    /// <summary>
    /// Gets the dimension of the embedding vector.
    /// </summary>
    public int Dimension => Vector.Length;

    /// <summary>
    /// Creates a task embedding from characteristics only (without vector).
    /// </summary>
    /// <param name="characteristics">Task characteristics.</param>
    /// <param name="description">Task description.</param>
    /// <returns>A task embedding with a zero vector.</returns>
    public static TaskEmbedding FromCharacteristics(
        Dictionary<string, double> characteristics,
        string description)
    {
        var vector = new float[characteristics.Count];
        var i = 0;
        foreach (var value in characteristics.Values)
        {
            vector[i++] = (float)value;
        }

        return new TaskEmbedding(vector, characteristics, description);
    }
}
