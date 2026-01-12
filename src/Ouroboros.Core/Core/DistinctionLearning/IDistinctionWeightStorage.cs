// <copyright file="IDistinctionWeightStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// Interface for storing and managing distinction learning weights.
/// </summary>
public interface IDistinctionWeightStorage
{
    /// <summary>
    /// Stores distinction weights to disk.
    /// </summary>
    /// <param name="id">Unique identifier for the weights.</param>
    /// <param name="weights">The weight data.</param>
    /// <param name="metadata">Metadata about the weights.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with the file path or error.</returns>
    Task<Result<string, string>> StoreWeightsAsync(
        string id,
        byte[] weights,
        DistinctionWeightMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Loads distinction weights from disk.
    /// </summary>
    /// <param name="id">Unique identifier for the weights.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with the weight data or error.</returns>
    Task<Result<byte[], string>> LoadWeightsAsync(
        string id,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all stored distinction weights.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with list of metadata or error.</returns>
    Task<Result<List<DistinctionWeightMetadata>, string>> ListWeightsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Marks weights as dissolved (archived).
    /// </summary>
    /// <param name="path">Path to the weight file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error.</returns>
    Task<Result<Unit, string>> DissolveWeightsAsync(
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Gets total storage size used by distinctions.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result with total bytes or error.</returns>
    Task<Result<long, string>> GetTotalStorageSizeAsync(
        CancellationToken ct = default);
}
