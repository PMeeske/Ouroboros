// <copyright file="AdapterLearningEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Learning;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// Implementation of the adapter learning engine for Parameter-Efficient Fine-Tuning.
/// Coordinates PEFT integration, storage, and continual learning workflows.
/// </summary>
public sealed class AdapterLearningEngine : IAdapterLearningEngine
{
    private readonly IPeftIntegration _peft;
    private readonly IAdapterStorage _storage;
    private readonly IAdapterBlobStorage _blobStorage;
    private readonly ILogger<AdapterLearningEngine>? _logger;
    private readonly string _baseModelName;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdapterLearningEngine"/> class.
    /// </summary>
    /// <param name="peft">PEFT integration layer.</param>
    /// <param name="storage">Adapter metadata storage.</param>
    /// <param name="blobStorage">Adapter weights blob storage.</param>
    /// <param name="baseModelName">Base model name/path for PEFT.</param>
    /// <param name="logger">Optional logger.</param>
    public AdapterLearningEngine(
        IPeftIntegration peft,
        IAdapterStorage storage,
        IAdapterBlobStorage blobStorage,
        string baseModelName,
        ILogger<AdapterLearningEngine>? logger = null)
    {
        _peft = peft ?? throw new ArgumentNullException(nameof(peft));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _baseModelName = baseModelName ?? throw new ArgumentNullException(nameof(baseModelName));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<AdapterId, string>> CreateAdapterAsync(
        string taskName,
        AdapterConfig config,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Creating adapter for task: {TaskName}", taskName);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return Result<AdapterId, string>.Failure("Task name cannot be empty");
            }

            var validationResult = config.Validate();
            if (validationResult.IsFailure)
            {
                return Result<AdapterId, string>.Failure(validationResult.Error);
            }

            // Initialize adapter with PEFT
            var weightsResult = await _peft.InitializeAdapterAsync(_baseModelName, config, ct);
            if (weightsResult.IsFailure)
            {
                return Result<AdapterId, string>.Failure($"Failed to initialize adapter: {weightsResult.Error}");
            }

            var weights = weightsResult.Value;

            // Validate adapter size
            var sizeResult = await _peft.ValidateAdapterAsync(weights, ct);
            if (sizeResult.IsFailure)
            {
                return Result<AdapterId, string>.Failure($"Failed to validate adapter: {sizeResult.Error}");
            }

            var sizeInMb = sizeResult.Value / (1024.0 * 1024.0);
            if (sizeInMb > 10.0)
            {
                return Result<AdapterId, string>.Failure($"Adapter size ({sizeInMb:F2} MB) exceeds 10 MB limit");
            }

            // Generate adapter ID
            var adapterId = AdapterId.NewId();

            // Store weights in blob storage
            var blobPathResult = await _blobStorage.StoreWeightsAsync(adapterId, weights, ct);
            if (blobPathResult.IsFailure)
            {
                return Result<AdapterId, string>.Failure($"Failed to store adapter weights: {blobPathResult.Error}");
            }

            // Create and store metadata
            var metadata = AdapterMetadata.Create(adapterId, taskName, config, blobPathResult.Value);
            var storeResult = await _storage.StoreMetadataAsync(metadata, ct);
            if (storeResult.IsFailure)
            {
                // Clean up blob storage on metadata storage failure
                await _blobStorage.DeleteWeightsAsync(blobPathResult.Value, ct);
                return Result<AdapterId, string>.Failure($"Failed to store adapter metadata: {storeResult.Error}");
            }

            _logger?.LogInformation("Created adapter {AdapterId} for task {TaskName} (size: {Size:F2} MB)", adapterId, taskName, sizeInMb);
            return Result<AdapterId, string>.Success(adapterId);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Adapter creation cancelled for task: {TaskName}", taskName);
            return Result<AdapterId, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating adapter for task: {TaskName}", taskName);
            return Result<AdapterId, string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> TrainAdapterAsync(
        AdapterId adapterId,
        List<TrainingExample> examples,
        TrainingConfig config,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Training adapter {AdapterId} with {Count} examples", adapterId, examples.Count);

            // Validate inputs
            if (examples == null || examples.Count == 0)
            {
                return Result<Unit, string>.Failure("Training examples cannot be empty");
            }

            var configValidation = config.Validate();
            if (configValidation.IsFailure)
            {
                return Result<Unit, string>.Failure(configValidation.Error);
            }

            // Validate all examples
            foreach (var example in examples)
            {
                var exampleValidation = example.Validate();
                if (exampleValidation.IsFailure)
                {
                    return Result<Unit, string>.Failure($"Invalid training example: {exampleValidation.Error}");
                }
            }

            // Retrieve adapter metadata
            var metadataResult = await _storage.GetMetadataAsync(adapterId, ct);
            if (metadataResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Adapter not found: {metadataResult.Error}");
            }

            var metadata = metadataResult.Value;

            // Retrieve adapter weights
            var weightsResult = await _blobStorage.GetWeightsAsync(metadata.BlobStoragePath, ct);
            if (weightsResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Failed to retrieve adapter weights: {weightsResult.Error}");
            }

            // Train adapter
            var trainedWeightsResult = await _peft.TrainAdapterAsync(_baseModelName, weightsResult.Value, examples, config, ct);
            if (trainedWeightsResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Training failed: {trainedWeightsResult.Error}");
            }

            // Validate trained adapter size
            var sizeResult = await _peft.ValidateAdapterAsync(trainedWeightsResult.Value, ct);
            if (sizeResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Failed to validate trained adapter: {sizeResult.Error}");
            }

            var sizeInMb = sizeResult.Value / (1024.0 * 1024.0);
            if (sizeInMb > 10.0)
            {
                return Result<Unit, string>.Failure($"Trained adapter size ({sizeInMb:F2} MB) exceeds 10 MB limit");
            }

            // Store updated weights
            var storeWeightsResult = await _blobStorage.StoreWeightsAsync(adapterId, trainedWeightsResult.Value, ct);
            if (storeWeightsResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Failed to store trained weights: {storeWeightsResult.Error}");
            }

            // Update metadata
            var updatedMetadata = metadata.WithTraining(examples.Count);
            var updateMetadataResult = await _storage.UpdateMetadataAsync(updatedMetadata, ct);
            if (updateMetadataResult.IsFailure)
            {
                _logger?.LogWarning("Failed to update metadata after training: {Error}", updateMetadataResult.Error);
                // Don't fail the operation if metadata update fails
            }

            _logger?.LogInformation("Successfully trained adapter {AdapterId}", adapterId);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Training cancelled for adapter: {AdapterId}", adapterId);
            return Result<Unit, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error training adapter: {AdapterId}", adapterId);
            return Result<Unit, string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> MergeAdaptersAsync(
        List<AdapterId> adapters,
        MergeStrategy strategy,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Merging {Count} adapters using {Strategy} strategy", adapters.Count, strategy);

            if (adapters == null || adapters.Count < 2)
            {
                return Result<Unit, string>.Failure("At least 2 adapters are required for merging");
            }

            // Retrieve all adapter weights
            var weightsList = new List<byte[]>();
            foreach (var adapterId in adapters)
            {
                var metadataResult = await _storage.GetMetadataAsync(adapterId, ct);
                if (metadataResult.IsFailure)
                {
                    return Result<Unit, string>.Failure($"Adapter {adapterId} not found: {metadataResult.Error}");
                }

                var weightsResult = await _blobStorage.GetWeightsAsync(metadataResult.Value.BlobStoragePath, ct);
                if (weightsResult.IsFailure)
                {
                    return Result<Unit, string>.Failure($"Failed to retrieve weights for adapter {adapterId}: {weightsResult.Error}");
                }

                weightsList.Add(weightsResult.Value);
            }

            // Merge adapters
            var mergedWeightsResult = await _peft.MergeAdaptersAsync(_baseModelName, weightsList, strategy, ct);
            if (mergedWeightsResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Merge failed: {mergedWeightsResult.Error}");
            }

            // Store merged adapter
            var mergedId = AdapterId.NewId();
            var storeResult = await _blobStorage.StoreWeightsAsync(mergedId, mergedWeightsResult.Value, ct);
            if (storeResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Failed to store merged adapter: {storeResult.Error}");
            }

            // Create metadata for merged adapter
            var mergedMetadata = AdapterMetadata.Create(
                mergedId,
                $"merged_{strategy}",
                AdapterConfig.Default(),
                storeResult.Value);
            await _storage.StoreMetadataAsync(mergedMetadata, ct);

            _logger?.LogInformation("Successfully merged adapters into {MergedId}", mergedId);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Merge operation cancelled");
            return Result<Unit, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error merging adapters");
            return Result<Unit, string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> GenerateWithAdapterAsync(
        string prompt,
        AdapterId? adapterId = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Generating with adapter {AdapterId}", adapterId);

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Result<string, string>.Failure("Prompt cannot be empty");
            }

            byte[]? weights = null;

            if (adapterId != null)
            {
                // Retrieve adapter weights
                var metadataResult = await _storage.GetMetadataAsync(adapterId, ct);
                if (metadataResult.IsFailure)
                {
                    return Result<string, string>.Failure($"Adapter not found: {metadataResult.Error}");
                }

                var weightsResult = await _blobStorage.GetWeightsAsync(metadataResult.Value.BlobStoragePath, ct);
                if (weightsResult.IsFailure)
                {
                    return Result<string, string>.Failure($"Failed to retrieve adapter weights: {weightsResult.Error}");
                }

                weights = weightsResult.Value;
            }

            // Generate text
            var generateResult = await _peft.GenerateAsync(_baseModelName, weights, prompt, ct);
            if (generateResult.IsFailure)
            {
                return Result<string, string>.Failure($"Generation failed: {generateResult.Error}");
            }

            return Result<string, string>.Success(generateResult.Value);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Generation cancelled");
            return Result<string, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during generation");
            return Result<string, string>.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> LearnFromFeedbackAsync(
        string prompt,
        string generation,
        FeedbackSignal feedback,
        AdapterId adapterId,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Learning from feedback for adapter {AdapterId}", adapterId);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Result<Unit, string>.Failure("Prompt cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(generation))
            {
                return Result<Unit, string>.Failure("Generation cannot be empty");
            }

            var feedbackValidation = feedback.Validate();
            if (feedbackValidation.IsFailure)
            {
                return Result<Unit, string>.Failure(feedbackValidation.Error);
            }

            // Convert feedback to training example
            var trainingExample = feedback.Type == FeedbackType.UserCorrection && feedback.Correction != null
                ? new TrainingExample(prompt, feedback.Correction, Math.Abs(feedback.Score))
                : new TrainingExample(prompt, generation, Math.Abs(feedback.Score));

            // Train with single example using incremental update
            var trainingConfig = TrainingConfig.Default() with { IncrementalUpdate = true, Epochs = 1 };
            return await this.TrainAdapterAsync(adapterId, new List<TrainingExample> { trainingExample }, trainingConfig, ct);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Feedback learning cancelled");
            return Result<Unit, string>.Failure("Operation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error learning from feedback");
            return Result<Unit, string>.Failure($"Unexpected error: {ex.Message}");
        }
    }
}
