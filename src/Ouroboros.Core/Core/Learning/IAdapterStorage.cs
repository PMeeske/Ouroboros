// <copyright file="IAdapterStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for adapter metadata storage using vector database.
/// </summary>
public interface IAdapterStorage
{
    /// <summary>
    /// Stores adapter metadata.
    /// </summary>
    /// <param name="metadata">The adapter metadata to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit, string>> StoreMetadataAsync(AdapterMetadata metadata, CancellationToken ct = default);

    /// <summary>
    /// Retrieves adapter metadata by ID.
    /// </summary>
    /// <param name="adapterId">The adapter ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing metadata or error message.</returns>
    Task<Result<AdapterMetadata, string>> GetMetadataAsync(AdapterId adapterId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all adapters for a specific task.
    /// </summary>
    /// <param name="taskName">The task name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of metadata or error message.</returns>
    Task<Result<List<AdapterMetadata>, string>> GetAdaptersByTaskAsync(string taskName, CancellationToken ct = default);

    /// <summary>
    /// Updates existing adapter metadata.
    /// </summary>
    /// <param name="metadata">The updated metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit, string>> UpdateMetadataAsync(AdapterMetadata metadata, CancellationToken ct = default);

    /// <summary>
    /// Deletes adapter metadata.
    /// </summary>
    /// <param name="adapterId">The adapter ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit, string>> DeleteMetadataAsync(AdapterId adapterId, CancellationToken ct = default);
}
