// <copyright file="IDistinctionWeightStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for storing and retrieving distinction weights.
/// This abstracts the persistence layer for distinction learning.
/// </summary>
public interface IDistinctionWeightStorage
{
    /// <summary>
    /// Stores distinction weights and associates them with an ID.
    /// </summary>
    /// <param name="id">Unique identifier for the distinction.</param>
    /// <param name="weights">The distinction weights to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error message.</returns>
    Task<Result<Unit, string>> StoreDistinctionWeightsAsync(
        DistinctionId id,
        DistinctionWeights weights,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves distinction weights by ID.
    /// </summary>
    /// <param name="id">The distinction ID to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the weights or error message.</returns>
    Task<Result<DistinctionWeights, string>> GetDistinctionWeightsAsync(
        DistinctionId id,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves distinctions similar to a given embedding.
    /// Uses vector similarity search.
    /// </summary>
    /// <param name="embedding">The query embedding.</param>
    /// <param name="topK">Number of results to return. Default: 10.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of similar distinctions or error message.</returns>
    Task<Result<IReadOnlyList<DistinctionWeights>, string>> FindSimilarDistinctionsAsync(
        float[] embedding,
        int topK = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes distinction weights by ID.
    /// Used during dissolution.
    /// </summary>
    /// <param name="id">The distinction ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error message.</returns>
    Task<Result<Unit, string>> DeleteDistinctionWeightsAsync(
        DistinctionId id,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the fitness score for a distinction.
    /// </summary>
    /// <param name="id">The distinction ID to update.</param>
    /// <param name="newFitness">The new fitness score.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error message.</returns>
    Task<Result<Unit, string>> UpdateFitnessAsync(
        DistinctionId id,
        double newFitness,
        CancellationToken ct = default);
}
