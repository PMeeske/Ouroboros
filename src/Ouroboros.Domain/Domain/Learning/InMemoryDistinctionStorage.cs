// <copyright file="InMemoryDistinctionStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Learning;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// In-memory implementation of distinction weight storage.
/// Used for testing and development.
/// </summary>
public sealed class InMemoryDistinctionStorage : IDistinctionWeightStorage
{
    private readonly ConcurrentDictionary<DistinctionId, DistinctionWeights> _storage = new();
    private readonly ILogger<InMemoryDistinctionStorage>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDistinctionStorage"/> class.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public InMemoryDistinctionStorage(ILogger<InMemoryDistinctionStorage>? logger = null)
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
            _logger?.LogInformation("Stored distinction weights for {Id}", id);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error storing distinction weights");
            return Task.FromResult(Result<Unit, string>.Failure($"Store failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<DistinctionWeights, string>> GetDistinctionWeightsAsync(
        DistinctionId id,
        CancellationToken ct = default)
    {
        try
        {
            if (_storage.TryGetValue(id, out var weights))
            {
                _logger?.LogInformation("Retrieved distinction weights for {Id}", id);
                return Task.FromResult(Result<DistinctionWeights, string>.Success(weights));
            }

            return Task.FromResult(Result<DistinctionWeights, string>.Failure($"Distinction {id} not found"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving distinction weights");
            return Task.FromResult(Result<DistinctionWeights, string>.Failure($"Retrieval failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<DistinctionWeights>, string>> FindSimilarDistinctionsAsync(
        float[] embedding,
        int topK = 10,
        CancellationToken ct = default)
    {
        try
        {
            // Simple cosine similarity implementation
            var similarities = _storage.Values
                .Select(w => (weights: w, similarity: CosineSimilarity(embedding, w.Embedding)))
                .OrderByDescending(x => x.similarity)
                .Take(topK)
                .Select(x => x.weights)
                .ToList();

            _logger?.LogInformation("Found {Count} similar distinctions", similarities.Count);
            return Task.FromResult(Result<IReadOnlyList<DistinctionWeights>, string>.Success(
                (IReadOnlyList<DistinctionWeights>)similarities));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error finding similar distinctions");
            return Task.FromResult(Result<IReadOnlyList<DistinctionWeights>, string>.Failure(
                $"Search failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> DeleteDistinctionWeightsAsync(
        DistinctionId id,
        CancellationToken ct = default)
    {
        try
        {
            if (_storage.TryRemove(id, out _))
            {
                _logger?.LogInformation("Deleted distinction weights for {Id}", id);
                return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
            }

            return Task.FromResult(Result<Unit, string>.Failure($"Distinction {id} not found"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting distinction weights");
            return Task.FromResult(Result<Unit, string>.Failure($"Deletion failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UpdateFitnessAsync(
        DistinctionId id,
        double newFitness,
        CancellationToken ct = default)
    {
        try
        {
            if (_storage.TryGetValue(id, out var weights))
            {
                var updated = weights with
                {
                    Fitness = newFitness,
                    LastUpdatedAt = DateTime.UtcNow
                };
                _storage[id] = updated;
                _logger?.LogInformation("Updated fitness for {Id} to {Fitness:F3}", id, newFitness);
                return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
            }

            return Task.FromResult(Result<Unit, string>.Failure($"Distinction {id} not found"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating fitness");
            return Task.FromResult(Result<Unit, string>.Failure($"Update failed: {ex.Message}"));
        }
    }

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
