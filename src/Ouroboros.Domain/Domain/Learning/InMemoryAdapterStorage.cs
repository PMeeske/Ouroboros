// <copyright file="InMemoryAdapterStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Learning;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// In-memory implementation of adapter storage for development and testing.
/// Production systems should use Qdrant-based implementation.
/// </summary>
public sealed class InMemoryAdapterStorage : IAdapterStorage
{
    private readonly ConcurrentDictionary<AdapterId, AdapterMetadata> _store;
    private readonly ILogger<InMemoryAdapterStorage>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryAdapterStorage"/> class.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public InMemoryAdapterStorage(ILogger<InMemoryAdapterStorage>? logger = null)
    {
        _store = new ConcurrentDictionary<AdapterId, AdapterMetadata>();
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> StoreMetadataAsync(
        AdapterMetadata metadata,
        CancellationToken ct = default)
    {
        try
        {
            if (metadata == null)
            {
                return Task.FromResult(Result<Unit, string>.Failure("Metadata cannot be null"));
            }

            _store[metadata.Id] = metadata;
            _logger?.LogInformation("Stored metadata for adapter {AdapterId}", metadata.Id);

            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error storing metadata for adapter {AdapterId}", metadata?.Id);
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to store metadata: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<AdapterMetadata, string>> GetMetadataAsync(
        AdapterId adapterId,
        CancellationToken ct = default)
    {
        try
        {
            if (_store.TryGetValue(adapterId, out var metadata))
            {
                _logger?.LogInformation("Retrieved metadata for adapter {AdapterId}", adapterId);
                return Task.FromResult(Result<AdapterMetadata, string>.Success(metadata));
            }

            _logger?.LogWarning("Adapter not found: {AdapterId}", adapterId);
            return Task.FromResult(Result<AdapterMetadata, string>.Failure($"Adapter not found: {adapterId}"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving metadata for adapter {AdapterId}", adapterId);
            return Task.FromResult(Result<AdapterMetadata, string>.Failure($"Failed to retrieve metadata: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<List<AdapterMetadata>, string>> GetAdaptersByTaskAsync(
        string taskName,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return Task.FromResult(Result<List<AdapterMetadata>, string>.Failure("Task name cannot be empty"));
            }

            var adapters = _store.Values
                .Where(m => m.TaskName.Equals(taskName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger?.LogInformation("Found {Count} adapters for task {TaskName}", adapters.Count, taskName);

            return Task.FromResult(Result<List<AdapterMetadata>, string>.Success(adapters));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving adapters for task {TaskName}", taskName);
            return Task.FromResult(Result<List<AdapterMetadata>, string>.Failure($"Failed to retrieve adapters: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UpdateMetadataAsync(
        AdapterMetadata metadata,
        CancellationToken ct = default)
    {
        try
        {
            if (metadata == null)
            {
                return Task.FromResult(Result<Unit, string>.Failure("Metadata cannot be null"));
            }

            if (!_store.ContainsKey(metadata.Id))
            {
                return Task.FromResult(Result<Unit, string>.Failure($"Adapter not found: {metadata.Id}"));
            }

            _store[metadata.Id] = metadata;
            _logger?.LogInformation("Updated metadata for adapter {AdapterId}", metadata.Id);

            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating metadata for adapter {AdapterId}", metadata?.Id);
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to update metadata: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> DeleteMetadataAsync(
        AdapterId adapterId,
        CancellationToken ct = default)
    {
        try
        {
            if (_store.TryRemove(adapterId, out _))
            {
                _logger?.LogInformation("Deleted metadata for adapter {AdapterId}", adapterId);
            }
            else
            {
                _logger?.LogWarning("Adapter not found for deletion: {AdapterId}", adapterId);
            }

            // Treat as success even if not found (idempotent)
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting metadata for adapter {AdapterId}", adapterId);
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to delete metadata: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets the total number of stored adapters.
    /// </summary>
    /// <returns>The count of adapters.</returns>
    public int Count => _store.Count;

    /// <summary>
    /// Clears all stored metadata.
    /// </summary>
    public void Clear()
    {
        _store.Clear();
        _logger?.LogInformation("Cleared all adapter metadata");
    }
}
