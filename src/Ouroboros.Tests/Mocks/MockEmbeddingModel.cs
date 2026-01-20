// <copyright file="MockEmbeddingModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Domain;

/// <summary>
/// Mock embedding model for testing.
/// </summary>
public sealed class MockEmbeddingModel : IEmbeddingModel
{
    private readonly int _embeddingSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockEmbeddingModel"/> class.
    /// </summary>
    /// <param name="embeddingSize">Size of embeddings to generate. Default: 384.</param>
    public MockEmbeddingModel(int embeddingSize = 384)
    {
        _embeddingSize = embeddingSize;
    }

    /// <inheritdoc/>
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        // Create deterministic embedding based on input hash
        var hash = input.GetHashCode();
        var embedding = new float[_embeddingSize];
        var random = new Random(hash);

        for (int i = 0; i < _embeddingSize; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range: -1.0 to 1.0
        }

        // Normalize
        var norm = Math.Sqrt(embedding.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < _embeddingSize; i++)
            {
                embedding[i] /= (float)norm;
            }
        }

        return Task.FromResult(embedding);
    }
}
