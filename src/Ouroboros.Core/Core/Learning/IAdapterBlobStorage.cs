// <copyright file="IAdapterBlobStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for adapter weights blob storage.
/// </summary>
public interface IAdapterBlobStorage
{
    /// <summary>
    /// Stores adapter weights to blob storage.
    /// </summary>
    /// <param name="adapterId">The adapter ID.</param>
    /// <param name="weights">The adapter weights as byte array.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the storage path or error message.</returns>
    Task<Result<string, string>> StoreWeightsAsync(AdapterId adapterId, byte[] weights, CancellationToken ct = default);

    /// <summary>
    /// Retrieves adapter weights from blob storage.
    /// </summary>
    /// <param name="path">The storage path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the weights as byte array or error message.</returns>
    Task<Result<byte[], string>> GetWeightsAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Deletes adapter weights from blob storage.
    /// </summary>
    /// <param name="path">The storage path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit, string>> DeleteWeightsAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Gets the size of stored adapter weights in bytes.
    /// </summary>
    /// <param name="path">The storage path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the size in bytes or error message.</returns>
    Task<Result<long, string>> GetWeightsSizeAsync(string path, CancellationToken ct = default);
}
