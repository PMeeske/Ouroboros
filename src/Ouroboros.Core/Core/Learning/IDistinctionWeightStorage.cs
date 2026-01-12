// <copyright file="IDistinctionWeightStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Core.Learning;

/// <summary>
/// Storage interface for distinction embeddings/weights.
/// Handles persistence of learned distinctions to file system with metadata in Qdrant.
/// </summary>
public interface IDistinctionWeightStorage
{
    /// <summary>
    /// Stores distinction embedding weights after learning.
    /// </summary>
    /// <param name="id">The distinction identifier.</param>
    /// <param name="weights">The distinction weights to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the storage path or an error message.</returns>
    Task<Result<string, string>> StoreDistinctionWeightsAsync(
        DistinctionId id,
        DistinctionWeights weights,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves distinction weights for inference.
    /// </summary>
    /// <param name="path">The path to the stored weights.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the distinction weights or an error message.</returns>
    Task<Result<DistinctionWeights, string>> GetDistinctionWeightsAsync(
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Applies dissolution - archives/removes a distinction's weights.
    /// </summary>
    /// <param name="path">The path to the weights to dissolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing Unit on success or an error message.</returns>
    Task<Result<Unit, string>> DissolveWeightsAsync(
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Merges multiple distinction weights after Recognition stage (i = ⌐).
    /// </summary>
    /// <param name="weights">The weights to merge.</param>
    /// <param name="context">The recognition context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the merged weights or an error message.</returns>
    Task<Result<DistinctionWeights, string>> MergeOnRecognitionAsync(
        IReadOnlyList<DistinctionWeights> weights,
        RecognitionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all stored distinction weights.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the list of weight info or an error message.</returns>
    Task<Result<IReadOnlyList<DistinctionWeightInfo>, string>> ListWeightsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Gets total storage size used by distinctions.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the total size in bytes or an error message.</returns>
    Task<Result<long, string>> GetTotalStorageSizeAsync(
        CancellationToken ct = default);
}
