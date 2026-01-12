// <copyright file="FileSystemDistinctionWeightStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.DistinctionLearning;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// File system-based storage for distinction learning weights.
/// </summary>
public sealed class FileSystemDistinctionWeightStorage : IDistinctionWeightStorage
{
    private readonly DistinctionStorageConfig _config;
    private readonly ILogger<FileSystemDistinctionWeightStorage>? _logger;
    private readonly string _metadataPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDistinctionWeightStorage"/> class.
    /// </summary>
    public FileSystemDistinctionWeightStorage(
        DistinctionStorageConfig config,
        ILogger<FileSystemDistinctionWeightStorage>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _metadataPath = Path.Combine(_config.StoragePath, "metadata.json");

        // Ensure storage directory exists
        Directory.CreateDirectory(_config.StoragePath);
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> StoreWeightsAsync(
        string id,
        byte[] weights,
        DistinctionWeightMetadata metadata,
        CancellationToken ct = default)
    {
        try
        {
            var filePath = Path.Combine(_config.StoragePath, $"{id}.weights");
            await File.WriteAllBytesAsync(filePath, weights, ct);

            // Update metadata
            await UpdateMetadataAsync(metadata, ct);

            _logger?.LogDebug("Stored weights {Id} at {Path}", id, filePath);
            return Result<string, string>.Success(filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store weights {Id}", id);
            return Result<string, string>.Failure($"Storage failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<byte[], string>> LoadWeightsAsync(
        string id,
        CancellationToken ct = default)
    {
        try
        {
            var filePath = Path.Combine(_config.StoragePath, $"{id}.weights");
            if (!File.Exists(filePath))
            {
                return Result<byte[], string>.Failure($"Weights file not found: {id}");
            }

            var weights = await File.ReadAllBytesAsync(filePath, ct);
            return Result<byte[], string>.Success(weights);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load weights {Id}", id);
            return Result<byte[], string>.Failure($"Load failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<DistinctionWeightMetadata>, string>> ListWeightsAsync(
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_metadataPath))
            {
                return Result<List<DistinctionWeightMetadata>, string>.Success(new List<DistinctionWeightMetadata>());
            }

            var json = await File.ReadAllTextAsync(_metadataPath, ct);
            var metadata = JsonSerializer.Deserialize<List<DistinctionWeightMetadata>>(json)
                ?? new List<DistinctionWeightMetadata>();

            return Result<List<DistinctionWeightMetadata>, string>.Success(metadata);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list weights");
            return Result<List<DistinctionWeightMetadata>, string>.Failure($"List failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> DissolveWeightsAsync(
        string path,
        CancellationToken ct = default)
    {
        try
        {
            // Move file to .dissolved extension
            var dissolvedPath = path + ".dissolved";
            if (File.Exists(path))
            {
                File.Move(path, dissolvedPath, overwrite: true);
            }

            // Update metadata
            var listResult = await ListWeightsAsync(ct);
            if (listResult.IsSuccess)
            {
                var allMetadata = listResult.Value;
                var updated = allMetadata.Select(m =>
                    m.Path == path ? m with { IsDissolved = true, Path = dissolvedPath } : m).ToList();

                await SaveMetadataAsync(updated, ct);
            }

            _logger?.LogDebug("Dissolved weights at {Path}", path);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to dissolve weights at {Path}", path);
            return Result<Unit, string>.Failure($"Dissolution failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<long, string>> GetTotalStorageSizeAsync(
        CancellationToken ct = default)
    {
        try
        {
            var files = Directory.GetFiles(_config.StoragePath, "*.weights");
            var totalSize = files.Sum(f => new FileInfo(f).Length);

            return Result<long, string>.Success(totalSize);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get total storage size");
            return Result<long, string>.Failure($"Failed to calculate size: {ex.Message}");
        }
    }

    private async Task UpdateMetadataAsync(DistinctionWeightMetadata metadata, CancellationToken ct)
    {
        var listResult = await ListWeightsAsync(ct);
        var allMetadata = listResult.IsSuccess ? listResult.Value : new List<DistinctionWeightMetadata>();

        // Add or update metadata
        var existing = allMetadata.FindIndex(m => m.Id == metadata.Id);
        if (existing >= 0)
        {
            allMetadata[existing] = metadata;
        }
        else
        {
            allMetadata.Add(metadata);
        }

        await SaveMetadataAsync(allMetadata, ct);
    }

    private async Task SaveMetadataAsync(List<DistinctionWeightMetadata> metadata, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_metadataPath, json, ct);
    }
}
