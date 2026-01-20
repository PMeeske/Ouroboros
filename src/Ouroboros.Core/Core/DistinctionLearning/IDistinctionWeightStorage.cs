// <copyright file="IDistinctionWeightStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

using Ouroboros.Core.Monads;

/// <summary>
/// Storage interface for distinction learning weights.
/// </summary>
public interface IDistinctionWeightStorage
{
    /// <summary>
    /// Stores distinction weights to persistent storage.
    /// </summary>
    Task<Result<string, string>> StoreWeightsAsync(
        string id,
        byte[] weights,
        DistinctionWeightMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Loads distinction weights from persistent storage.
    /// </summary>
    Task<Result<byte[], string>> LoadWeightsAsync(
        string id,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all stored distinction weight metadata.
    /// </summary>
    Task<Result<List<DistinctionWeightMetadata>, string>> ListWeightsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Dissolves (archives) distinction weights.
    /// </summary>
    Task<Result<Unit, string>> DissolveWeightsAsync(
        string path,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the total storage size used by distinction weights.
    /// </summary>
    Task<Result<long, string>> GetTotalStorageSizeAsync(
        CancellationToken ct = default);
}
