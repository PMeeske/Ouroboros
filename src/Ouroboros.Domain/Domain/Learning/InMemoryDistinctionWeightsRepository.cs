// <copyright file="InMemoryDistinctionWeightsRepository.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Learning;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// In-memory implementation of distinction weights repository.
/// Used for testing and development.
/// </summary>
public sealed class InMemoryDistinctionWeightsRepository : IDistinctionWeightsRepository
{
    private readonly ConcurrentDictionary<DistinctionId, DistinctionWeights> _storage = new();
    private readonly ILogger<InMemoryDistinctionWeightsRepository>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDistinctionWeightsRepository"/> class.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public InMemoryDistinctionWeightsRepository(ILogger<InMemoryDistinctionWeightsRepository>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> StoreDistinctionWeightsAsync(
        DistinctionId id,
        DistinctionWeights weights,
        CancellationToken ct = default)
    {
        try
        {
            _storage[id] = weights;
            _logger?.LogDebug("Stored distinction weights for {Id}", id);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store distinction weights for {Id}", id);
            return Task.FromResult(Result<Unit, string>.Failure($"Storage failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<DistinctionWeights, string>> GetDistinctionWeightsAsync(
        DistinctionId id,
        CancellationToken ct = default)
    {
        if (_storage.TryGetValue(id, out var weights))
        {
            return Task.FromResult(Result<DistinctionWeights, string>.Success(weights));
        }

        return Task.FromResult(Result<DistinctionWeights, string>.Failure($"Distinction {id} not found"));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<DistinctionWeights>, string>> FindSimilarDistinctionsAsync(
        float[] embedding,
        int topK = 10,
        CancellationToken ct = default)
    {
        try
        {
            // Simple cosine similarity search
            var results = _storage.Values
                .Select(w => (Weights: w, Similarity: CosineSimilarity(embedding, w.Embedding)))
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => x.Weights)
                .ToList();

            return Task.FromResult(Result<IReadOnlyList<DistinctionWeights>, string>.Success(results));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to find similar distinctions");
            return Task.FromResult(Result<IReadOnlyList<DistinctionWeights>, string>.Failure($"Search failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> DeleteDistinctionWeightsAsync(
        DistinctionId id,
        CancellationToken ct = default)
    {
        if (_storage.TryRemove(id, out _))
        {
            _logger?.LogDebug("Deleted distinction weights for {Id}", id);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        return Task.FromResult(Result<Unit, string>.Failure($"Distinction {id} not found"));
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UpdateFitnessAsync(
        DistinctionId id,
        double newFitness,
        CancellationToken ct = default)
    {
        if (_storage.TryGetValue(id, out var weights))
        {
            var updated = weights with { Fitness = newFitness, LastUpdatedAt = DateTime.UtcNow };
            _storage[id] = updated;
            _logger?.LogDebug("Updated fitness for {Id} to {Fitness}", id, newFitness);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        return Task.FromResult(Result<Unit, string>.Failure($"Distinction {id} not found"));
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0;
        }

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }
}
