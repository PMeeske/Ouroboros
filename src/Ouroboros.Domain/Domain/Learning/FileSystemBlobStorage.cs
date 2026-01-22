// <copyright file="FileSystemBlobStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Learning;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// File system-based implementation of adapter blob storage.
/// </summary>
public sealed class FileSystemBlobStorage : IAdapterBlobStorage
{
    private readonly string _baseDirectory;
    private readonly ILogger<FileSystemBlobStorage>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemBlobStorage"/> class.
    /// </summary>
    /// <param name="baseDirectory">Base directory for storing adapter weights.</param>
    /// <param name="logger">Optional logger.</param>
    public FileSystemBlobStorage(string baseDirectory, ILogger<FileSystemBlobStorage>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory cannot be empty", nameof(baseDirectory));
        }

        _baseDirectory = baseDirectory;
        _logger = logger;

        // Ensure base directory exists
        Directory.CreateDirectory(_baseDirectory);
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> StoreWeightsAsync(
        AdapterId adapterId,
        byte[] weights,
        CancellationToken ct = default)
    {
        try
        {
            if (weights == null || weights.Length == 0)
            {
                return Result<string, string>.Failure("Weights cannot be empty");
            }

            var fileName = $"{adapterId}.bin";
            var filePath = Path.Combine(_baseDirectory, fileName);

            _logger?.LogInformation("Storing adapter weights to {FilePath}", filePath);

            await File.WriteAllBytesAsync(filePath, weights, ct);

            return Result<string, string>.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Store operation cancelled for adapter {AdapterId}", adapterId);
            return Result<string, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error storing adapter weights for {AdapterId}", adapterId);
            return Result<string, string>.Failure($"Failed to store weights: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<byte[], string>> GetWeightsAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Result<byte[], string>.Failure("Path cannot be empty");
            }

            if (!File.Exists(path))
            {
                return Result<byte[], string>.Failure($"File not found: {path}");
            }

            _logger?.LogInformation("Retrieving adapter weights from {Path}", path);

            var weights = await File.ReadAllBytesAsync(path, ct);

            return Result<byte[], string>.Success(weights);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Get operation cancelled for path {Path}", path);
            return Result<byte[], string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving adapter weights from {Path}", path);
            return Result<byte[], string>.Failure($"Failed to retrieve weights: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> DeleteWeightsAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Result<Unit, string>.Failure("Path cannot be empty");
            }

            if (!File.Exists(path))
            {
                // Treat non-existent file as success (idempotent)
                return Result<Unit, string>.Success(Unit.Value);
            }

            _logger?.LogInformation("Deleting adapter weights from {Path}", path);

            await Task.Run(() => File.Delete(path), ct);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Delete operation cancelled for path {Path}", path);
            return Result<Unit, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting adapter weights from {Path}", path);
            return Result<Unit, string>.Failure($"Failed to delete weights: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<long, string>> GetWeightsSizeAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Result<long, string>.Failure("Path cannot be empty");
            }

            if (!File.Exists(path))
            {
                return Result<long, string>.Failure($"File not found: {path}");
            }

            var fileInfo = new FileInfo(path);
            var size = await Task.Run(() => fileInfo.Length, ct);

            return Result<long, string>.Success(size);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Get size operation cancelled for path {Path}", path);
            return Result<long, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting adapter weights size from {Path}", path);
            return Result<long, string>.Failure($"Failed to get weights size: {ex.Message}");
        }
    }
}
