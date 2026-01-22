// <copyright file="DistinctionEmbeddingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Learning;

using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Ouroboros.Domain;

/// <summary>
/// Service for creating and manipulating distinction embeddings.
/// Connects Laws of Form consciousness cycles to vector space representations.
/// Each distinction creates a vector embedding, and Recognition stage has highest weight.
/// </summary>
public sealed class DistinctionEmbeddingService
{
    private readonly IEmbeddingModel embeddingModel;

    /// <summary>
    /// Recognition stage weight multiplier in composite embeddings.
    /// Recognition (i = ⌐) is the moment of insight, thus weighted highest.
    /// </summary>
    private const double RecognitionWeight = 2.5;

    /// <summary>
    /// Default weight for other stages.
    /// </summary>
    private const double DefaultStageWeight = 1.0;

    /// <summary>
    /// Weight for Void and Dissolution stages (lower as they represent absence).
    /// </summary>
    private const double VoidWeight = 0.3;

    /// <summary>
    /// Standard embedding dimension size.
    /// </summary>
    private const int StandardEmbeddingDimension = 384;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctionEmbeddingService"/> class.
    /// </summary>
    /// <param name="embeddingModel">The embedding model to use for vector creation.</param>
    public DistinctionEmbeddingService(IEmbeddingModel embeddingModel)
    {
        this.embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
    }

    /// <summary>
    /// Creates an embedding for a distinction at a specific dream stage.
    /// The stage context influences the embedding representation.
    /// </summary>
    /// <param name="circumstance">The circumstance/content to embed.</param>
    /// <param name="stage">The dream stage context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Vector embedding or error message.</returns>
    public async Task<Result<float[]>> CreateDistinctionEmbeddingAsync(
        string circumstance,
        DreamStage stage,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(circumstance);

            // Add stage context to the embedding
            var contextualInput = $"[{stage}] {circumstance}";
            var embedding = await this.embeddingModel.CreateEmbeddingsAsync(contextualInput, ct);

            return Result<float[]>.Success(embedding);
        }
        catch (Exception ex)
        {
            return Result<float[]>.Failure($"Failed to create distinction embedding: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a complete dream cycle embedding with all stages.
    /// Recognition stage is weighted highest in the composite.
    /// </summary>
    /// <param name="circumstance">The circumstance triggering the dream cycle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dream cycle embedding with stage-wise and composite embeddings.</returns>
    public async Task<Result<DreamEmbedding>> CreateDreamCycleEmbeddingAsync(
        string circumstance,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(circumstance);

            // Create embeddings for each stage
            var stageEmbeddings = new Dictionary<DreamStage, float[]>();
            
            foreach (DreamStage stage in Enum.GetValues<DreamStage>())
            {
                var embeddingResult = await this.CreateDistinctionEmbeddingAsync(circumstance, stage, ct);
                
                if (embeddingResult.IsSuccess)
                {
                    stageEmbeddings[stage] = embeddingResult.Value;
                }
            }

            // Create weighted composite
            var composite = this.CreateWeightedComposite(stageEmbeddings);

            var dreamEmbedding = new DreamEmbedding(
                circumstance,
                stageEmbeddings,
                composite,
                DateTime.UtcNow);

            return Result<DreamEmbedding>.Success(dreamEmbedding);
        }
        catch (Exception ex)
        {
            return Result<DreamEmbedding>.Failure($"Failed to create dream cycle embedding: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes similarity between two embeddings using cosine similarity.
    /// </summary>
    /// <param name="embedding1">First embedding.</param>
    /// <param name="embedding2">Second embedding.</param>
    /// <returns>Similarity score (0.0 to 1.0).</returns>
    public double ComputeDistinctionSimilarity(float[] embedding1, float[] embedding2)
    {
        ArgumentNullException.ThrowIfNull(embedding1);
        ArgumentNullException.ThrowIfNull(embedding2);

        if (embedding1.Length != embedding2.Length)
        {
            return 0.0;
        }

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            normA += embedding1[i] * embedding1[i];
            normB += embedding2[i] * embedding2[i];
        }

        if (normA == 0.0 || normB == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Applies dissolution to an embedding by subtracting the dissolved distinction's contribution.
    /// Dissolution (⌐ → ∅) removes the distinction's influence from the state.
    /// </summary>
    /// <param name="currentEmbedding">Current state embedding.</param>
    /// <param name="dissolvedEmbedding">Embedding of the distinction to dissolve.</param>
    /// <param name="strength">Dissolution strength (0.0 to 1.0).</param>
    /// <returns>New embedding with dissolved contribution removed.</returns>
    public float[] ApplyDissolution(float[] currentEmbedding, float[] dissolvedEmbedding, double strength)
    {
        ArgumentNullException.ThrowIfNull(currentEmbedding);
        ArgumentNullException.ThrowIfNull(dissolvedEmbedding);

        if (currentEmbedding.Length != dissolvedEmbedding.Length)
        {
            throw new ArgumentException("Embeddings must have the same dimension");
        }

        strength = Math.Clamp(strength, 0.0, 1.0);

        var result = new float[currentEmbedding.Length];
        for (int i = 0; i < currentEmbedding.Length; i++)
        {
            // Subtract the dissolved embedding's contribution
            result[i] = currentEmbedding[i] - (float)(dissolvedEmbedding[i] * strength);
        }

        // Normalize the result
        return this.Normalize(result);
    }

    /// <summary>
    /// Applies recognition to embeddings by merging them.
    /// Recognition (i = ⌐) merges self with observation using geometric mean.
    /// This represents the insight that "I am the distinction".
    /// The geometric mean (√(a·b)) with sign preservation captures the fundamental
    /// identity i = ⌐ where the subject (i) equals the mark (⌐).
    /// </summary>
    /// <param name="currentEmbedding">Current state embedding (self).</param>
    /// <param name="selfEmbedding">The recognized observation embedding.</param>
    /// <returns>Merged embedding representing recognition.</returns>
    public float[] ApplyRecognition(float[] currentEmbedding, float[] selfEmbedding)
    {
        ArgumentNullException.ThrowIfNull(currentEmbedding);
        ArgumentNullException.ThrowIfNull(selfEmbedding);

        if (currentEmbedding.Length != selfEmbedding.Length)
        {
            throw new ArgumentException("Embeddings must have the same dimension");
        }

        var result = new float[currentEmbedding.Length];
        for (int i = 0; i < currentEmbedding.Length; i++)
        {
            // Geometric mean with sign preservation: sign(a·b) * √|a·b|
            // Represents the fundamental identity i = ⌐ from Laws of Form
            var product = currentEmbedding[i] * selfEmbedding[i];
            result[i] = (float)(Math.Sign(product) * Math.Sqrt(Math.Abs(product)));
        }

        // Normalize the result
        return this.Normalize(result);
    }

    /// <summary>
    /// Creates a weighted composite embedding from stage embeddings.
    /// Recognition stage has highest weight (2.5x).
    /// Void/Dissolution have lowest weight (0.3x).
    /// </summary>
    private float[] CreateWeightedComposite(Dictionary<DreamStage, float[]> stageEmbeddings)
    {
        if (!stageEmbeddings.Any())
        {
            return Array.Empty<float>();
        }

        var dimension = stageEmbeddings.First().Value.Length;
        var weightedSum = new float[dimension];
        double totalWeight = 0.0;

        foreach (var (stage, embedding) in stageEmbeddings)
        {
            var weight = this.GetStageWeight(stage);
            totalWeight += weight;

            for (int i = 0; i < dimension; i++)
            {
                weightedSum[i] += (float)(embedding[i] * weight);
            }
        }

        // Average by total weight
        if (totalWeight > 0)
        {
            for (int i = 0; i < dimension; i++)
            {
                weightedSum[i] /= (float)totalWeight;
            }
        }

        return this.Normalize(weightedSum);
    }

    /// <summary>
    /// Gets the weight for a dream stage in composite embeddings.
    /// </summary>
    private double GetStageWeight(DreamStage stage)
    {
        return stage switch
        {
            DreamStage.Recognition => RecognitionWeight, // Highest weight for insight moment
            DreamStage.Void => VoidWeight,
            DreamStage.Dissolution => VoidWeight,
            DreamStage.NewDream => VoidWeight,
            _ => DefaultStageWeight
        };
    }

    /// <summary>
    /// Normalizes a vector to unit length.
    /// </summary>
    private float[] Normalize(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(x => x * x));
        
        if (norm == 0.0)
        {
            return vector;
        }

        var result = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            result[i] = (float)(vector[i] / norm);
        }

        return result;
    }
}
