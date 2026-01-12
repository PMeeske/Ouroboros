// <copyright file="FileSystemDistinctionStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.IO;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

namespace Ouroboros.Domain.Learning;

/// <summary>
/// File system implementation for distinction weight storage.
/// Stores weights as binary files with thread-safe operations.
/// </summary>
public sealed class FileSystemDistinctionStorage : IDistinctionWeightStorage
{
    private readonly DistinctionStorageConfig _config;
    private readonly ILogger<FileSystemDistinctionStorage> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemDistinctionStorage"/> class.
    /// </summary>
    /// <param name="config">Storage configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public FileSystemDistinctionStorage(
        DistinctionStorageConfig config,
        ILogger<FileSystemDistinctionStorage> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure base directory exists
        if (!Directory.Exists(_config.BaseDirectory))
        {
            Directory.CreateDirectory(_config.BaseDirectory);
            _logger.LogInformation("Created distinction storage directory: {Directory}", _config.BaseDirectory);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> StoreDistinctionWeightsAsync(
        DistinctionId id,
        DistinctionWeights weights,
        CancellationToken ct = default)
    {
        try
        {
            // Validate weights
            if (weights.Embedding.Length == 0)
            {
                return Result<string, string>.Failure("Embedding cannot be empty");
            }

            // Serialize to binary
            var binaryData = SerializeWeights(weights);

            // Check size limit
            if (binaryData.Length > _config.MaxWeightSizeBytes)
            {
                return Result<string, string>.Failure(
                    $"Weight size {binaryData.Length} exceeds maximum {_config.MaxWeightSizeBytes} bytes");
            }

            // Check total storage
            var currentSize = await GetTotalStorageSizeAsync(ct);
            if (currentSize.IsSuccess && currentSize.Value + binaryData.Length > _config.MaxTotalStorageBytes)
            {
                return Result<string, string>.Failure(
                    $"Total storage would exceed maximum {_config.MaxTotalStorageBytes} bytes");
            }

            var path = GetDistinctionPath(id, false);

            await _fileLock.WaitAsync(ct);
            try
            {
                await File.WriteAllBytesAsync(path, binaryData, ct);
                _logger.LogInformation("Stored distinction {Id} at {Path} ({Size} bytes)", id, path, binaryData.Length);
                return Result<string, string>.Success(path);
            }
            finally
            {
                _fileLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store distinction {Id}", id);
            return Result<string, string>.Failure($"Storage failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<DistinctionWeights, string>> GetDistinctionWeightsAsync(
        string path,
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Result<DistinctionWeights, string>.Failure($"Distinction file not found: {path}");
            }

            await _fileLock.WaitAsync(ct);
            try
            {
                var binaryData = await File.ReadAllBytesAsync(path, ct);
                var weights = DeserializeWeights(binaryData);
                _logger.LogInformation("Retrieved distinction from {Path} ({Size} bytes)", path, binaryData.Length);
                return Result<DistinctionWeights, string>.Success(weights);
            }
            finally
            {
                _fileLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve distinction from {Path}", path);
            return Result<DistinctionWeights, string>.Failure($"Retrieval failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> DissolveWeightsAsync(
        string path,
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Result<Unit, string>.Failure($"Distinction file not found: {path}");
            }

            await _fileLock.WaitAsync(ct);
            try
            {
                if (_config.ArchiveOnDissolution)
                {
                    // Move to dissolved archive
                    var dissolvedPath = path.Replace(".distinction.bin", ".dissolved.bin");
                    File.Move(path, dissolvedPath, overwrite: true);
                    _logger.LogInformation("Dissolved distinction: {Path} -> {DissolvedPath}", path, dissolvedPath);
                }
                else
                {
                    // Delete
                    File.Delete(path);
                    _logger.LogInformation("Deleted dissolved distinction: {Path}", path);
                }

                return Result<Unit, string>.Success(Unit.Value);
            }
            finally
            {
                _fileLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dissolve distinction at {Path}", path);
            return Result<Unit, string>.Failure($"Dissolution failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<DistinctionWeights, string>> MergeOnRecognitionAsync(
        IReadOnlyList<DistinctionWeights> weights,
        RecognitionContext context,
        CancellationToken ct = default)
    {
        try
        {
            if (weights.Count == 0)
            {
                return Result<DistinctionWeights, string>.Failure("Cannot merge empty weights list");
            }

            if (weights.Count == 1)
            {
                return Result<DistinctionWeights, string>.Success(weights[0]);
            }

            // Use geometric mean for recognition merge (representing i = ⌐)
            var embeddingLength = weights[0].Embedding.Length;
            var mergedEmbedding = new float[embeddingLength];
            var mergedDissolutionMask = new float[embeddingLength];
            var mergedRecognitionTransform = new float[embeddingLength];

            // Calculate geometric mean for each dimension
            for (int i = 0; i < embeddingLength; i++)
            {
                double embeddingProduct = 1.0;
                double dissolutionProduct = 1.0;
                double recognitionProduct = 1.0;

                foreach (var weight in weights)
                {
                    embeddingProduct *= Math.Abs(weight.Embedding[i]) + 1e-10; // Avoid zero
                    dissolutionProduct *= Math.Abs(weight.DissolutionMask[i]) + 1e-10;
                    recognitionProduct *= Math.Abs(weight.RecognitionTransform[i]) + 1e-10;
                }

                mergedEmbedding[i] = (float)Math.Pow(embeddingProduct, 1.0 / weights.Count);
                mergedDissolutionMask[i] = (float)Math.Pow(dissolutionProduct, 1.0 / weights.Count);
                mergedRecognitionTransform[i] = (float)Math.Pow(recognitionProduct, 1.0 / weights.Count);
            }

            // Average fitness
            var averageFitness = weights.Average(w => w.Fitness);

            // Create merged weights
            var merged = new DistinctionWeights(
                Id: DistinctionId.NewId(),
                Embedding: mergedEmbedding,
                DissolutionMask: mergedDissolutionMask,
                RecognitionTransform: mergedRecognitionTransform,
                LearnedAtStage: context.CurrentStage,
                Fitness: averageFitness,
                Circumstance: context.Circumstance,
                CreatedAt: DateTime.UtcNow,
                LastUpdatedAt: null);

            _logger.LogInformation("Merged {Count} distinctions at stage {Stage}", weights.Count, context.CurrentStage);

            return await Task.FromResult(Result<DistinctionWeights, string>.Success(merged));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge distinctions");
            return Result<DistinctionWeights, string>.Failure($"Merge failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DistinctionWeightInfo>, string>> ListWeightsAsync(
        CancellationToken ct = default)
    {
        try
        {
            if (!Directory.Exists(_config.BaseDirectory))
            {
                return Result<IReadOnlyList<DistinctionWeightInfo>, string>.Success(Array.Empty<DistinctionWeightInfo>());
            }

            var files = Directory.GetFiles(_config.BaseDirectory, "*.distinction.bin")
                .Concat(Directory.GetFiles(_config.BaseDirectory, "*.dissolved.bin"))
                .ToList();

            var infos = new List<DistinctionWeightInfo>();

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var isDissolved = file.EndsWith(".dissolved.bin");

                // Parse ID from filename
                var filename = Path.GetFileNameWithoutExtension(fileInfo.Name);
                if (isDissolved)
                {
                    filename = filename.Replace(".dissolved", string.Empty);
                }

                if (Guid.TryParse(filename, out var guid))
                {
                    // Read minimal info without full deserialization
                    var distinctionId = new DistinctionId(guid);

                    // For full info, we'd need to deserialize, but for listing we'll use defaults
                    infos.Add(new DistinctionWeightInfo(
                        Id: distinctionId,
                        Path: file,
                        LearnedAtStage: 1, // Distinction stage
                        Fitness: 0.0,
                        SizeBytes: fileInfo.Length,
                        CreatedAt: fileInfo.CreationTimeUtc,
                        IsDissolved: isDissolved));
                }
            }

            _logger.LogInformation("Listed {Count} distinctions", infos.Count);
            return await Task.FromResult(Result<IReadOnlyList<DistinctionWeightInfo>, string>.Success((IReadOnlyList<DistinctionWeightInfo>)infos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list distinctions");
            return Result<IReadOnlyList<DistinctionWeightInfo>, string>.Failure($"List failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<long, string>> GetTotalStorageSizeAsync(CancellationToken ct = default)
    {
        try
        {
            if (!Directory.Exists(_config.BaseDirectory))
            {
                return Result<long, string>.Success(0L);
            }

            var files = Directory.GetFiles(_config.BaseDirectory, "*.distinction.bin")
                .Concat(Directory.GetFiles(_config.BaseDirectory, "*.dissolved.bin"));

            var totalSize = files.Sum(f => new FileInfo(f).Length);

            _logger.LogDebug("Total distinction storage: {Size} bytes", totalSize);
            return await Task.FromResult(Result<long, string>.Success(totalSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get total storage size");
            return Result<long, string>.Failure($"Size calculation failed: {ex.Message}");
        }
    }

    private string GetDistinctionPath(DistinctionId id, bool dissolved)
    {
        var extension = dissolved ? ".dissolved.bin" : ".distinction.bin";
        return Path.Combine(_config.BaseDirectory, $"{id}{extension}");
    }

    private byte[] SerializeWeights(DistinctionWeights weights)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write header
        writer.Write(weights.Id.Value.ToByteArray());
        writer.Write((int)weights.LearnedAtStage);
        writer.Write(weights.Fitness);
        writer.Write(weights.Circumstance);
        writer.Write(weights.CreatedAt.ToBinary());
        writer.Write(weights.LastUpdatedAt?.ToBinary() ?? 0L);

        // Write arrays
        WriteFloatArray(writer, weights.Embedding);
        WriteFloatArray(writer, weights.DissolutionMask);
        WriteFloatArray(writer, weights.RecognitionTransform);

        return ms.ToArray();
    }

    private DistinctionWeights DeserializeWeights(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        // Read header
        var idBytes = reader.ReadBytes(16);
        var id = new DistinctionId(new Guid(idBytes));
        var stage = reader.ReadInt32();
        var fitness = reader.ReadDouble();
        var circumstance = reader.ReadString();
        var createdAt = DateTime.FromBinary(reader.ReadInt64());
        var lastUpdatedBinary = reader.ReadInt64();
        var lastUpdatedAt = lastUpdatedBinary != 0L ? DateTime.FromBinary(lastUpdatedBinary) : (DateTime?)null;

        // Read arrays
        var embedding = ReadFloatArray(reader);
        var dissolutionMask = ReadFloatArray(reader);
        var recognitionTransform = ReadFloatArray(reader);

        return new DistinctionWeights(
            Id: id,
            Embedding: embedding,
            DissolutionMask: dissolutionMask,
            RecognitionTransform: recognitionTransform,
            LearnedAtStage: stage,
            Fitness: fitness,
            Circumstance: circumstance,
            CreatedAt: createdAt,
            LastUpdatedAt: lastUpdatedAt);
    }

    private void WriteFloatArray(BinaryWriter writer, float[] array)
    {
        writer.Write(array.Length);
        foreach (var value in array)
        {
            writer.Write(value);
        }
    }

    private float[] ReadFloatArray(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        var array = new float[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = reader.ReadSingle();
        }

        return array;
    }
}
