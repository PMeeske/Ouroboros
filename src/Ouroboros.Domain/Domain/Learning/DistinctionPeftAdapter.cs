// <copyright file="DistinctionPeftAdapter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Learning;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// Adapter that wraps PEFT to provide distinction-specific training logic.
/// Connects distinction observations to actual weight updates.
/// </summary>
public sealed class DistinctionPeftAdapter
{
    private readonly IPeftIntegration _peft;
    private readonly IDistinctionWeightStorage _storage;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly string _baseModelName;
    private readonly ILogger<DistinctionPeftAdapter>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctionPeftAdapter"/> class.
    /// </summary>
    /// <param name="peft">PEFT integration for weight training.</param>
    /// <param name="storage">Storage for distinction weights.</param>
    /// <param name="embeddingModel">Model for creating embeddings.</param>
    /// <param name="baseModelName">Name of the base model.</param>
    /// <param name="logger">Optional logger.</param>
    public DistinctionPeftAdapter(
        IPeftIntegration peft,
        IDistinctionWeightStorage storage,
        IEmbeddingModel embeddingModel,
        string baseModelName,
        ILogger<DistinctionPeftAdapter>? logger = null)
    {
        _peft = peft;
        _storage = storage;
        _embeddingModel = embeddingModel;
        _baseModelName = baseModelName;
        _logger = logger;
    }

    /// <summary>
    /// Trains on a distinction and persists the updated weights.
    /// </summary>
    /// <param name="observation">The observation to learn from.</param>
    /// <param name="stage">The current dream stage.</param>
    /// <param name="currentState">The current distinction state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing trained distinction weights or error message.</returns>
    public async Task<Result<DistinctionWeights, string>> LearnDistinctionAsync(
        Observation observation,
        DreamStage stage,
        DistinctionState currentState,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Learning distinction from observation at stage {Stage}", stage);

            // 1. Create embedding for the observation
            var embedding = await _embeddingModel.CreateEmbeddingsAsync(observation.Content, ct);

            // 2. Load current weights (or create new)
            var currentWeights = await LoadOrCreateWeightsAsync(currentState, ct);

            // 3. Create training example
            var example = new DistinctionTrainingExample(
                Circumstance: observation.Content,
                DistinctionMade: ExtractDistinction(observation, stage),
                Stage: stage,
                ContextEmbedding: embedding,
                ImportanceWeight: CalculateImportance(stage));

            // 4. Train via PEFT
            var config = DistinctionTrainingConfig.ForStage(stage);
            var trainedResult = await _peft.TrainOnDistinctionAsync(
                _baseModelName,
                currentWeights,
                example,
                config,
                ct);

            if (trainedResult.IsFailure)
            {
                return Result<DistinctionWeights, string>.Failure(trainedResult.Error);
            }

            // 5. Create and persist distinction weights
            var distinctionWeights = new DistinctionWeights(
                Id: DistinctionId.NewId(),
                Embedding: embedding,
                DissolutionMask: ComputeDissolutionMask(embedding, stage),
                RecognitionTransform: new float[embedding.Length],
                LearnedAtStage: stage,
                Fitness: 0.5, // Initial fitness
                Circumstance: observation.Content,
                CreatedAt: DateTime.UtcNow,
                LastUpdatedAt: null);

            var storeResult = await _storage.StoreDistinctionWeightsAsync(
                distinctionWeights.Id, distinctionWeights, ct);

            if (storeResult.IsFailure)
            {
                return Result<DistinctionWeights, string>.Failure(storeResult.Error);
            }

            _logger?.LogInformation(
                "Successfully learned distinction {Id} at stage {Stage}",
                distinctionWeights.Id,
                stage);

            return Result<DistinctionWeights, string>.Success(distinctionWeights);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error learning distinction");
            return Result<DistinctionWeights, string>.Failure($"Learning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies dissolution - removes distinction from learned weights.
    /// </summary>
    /// <param name="id">The distinction ID to dissolve.</param>
    /// <param name="currentState">The current distinction state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error message.</returns>
    public async Task<Result<Unit, string>> DissolveDistinctionAsync(
        DistinctionId id,
        DistinctionState currentState,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Dissolving distinction {Id}", id);

            // 1. Retrieve the distinction weights
            var weightsResult = await _storage.GetDistinctionWeightsAsync(id, ct);
            if (weightsResult.IsFailure)
            {
                return Result<Unit, string>.Failure(weightsResult.Error);
            }

            var weights = weightsResult.Value;

            // 2. Load current model weights
            var currentWeights = await LoadOrCreateWeightsAsync(currentState, ct);

            // 3. Apply dissolution via PEFT
            var dissolutionResult = await _peft.ApplyDissolutionAsync(
                _baseModelName,
                currentWeights,
                weights.DissolutionMask,
                0.8, // Dissolution strength
                ct);

            if (dissolutionResult.IsFailure)
            {
                return Result<Unit, string>.Failure(dissolutionResult.Error);
            }

            // 4. Delete from storage
            var deleteResult = await _storage.DeleteDistinctionWeightsAsync(id, ct);
            if (deleteResult.IsFailure)
            {
                return Result<Unit, string>.Failure(deleteResult.Error);
            }

            _logger?.LogInformation("Successfully dissolved distinction {Id}", id);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error dissolving distinction {Id}", id);
            return Result<Unit, string>.Failure($"Dissolution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs recognition merge - subject becomes the distinction (i = ⌐).
    /// </summary>
    /// <param name="distinctionIds">IDs of distinctions to merge.</param>
    /// <param name="circumstance">The circumstance triggering recognition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing merged distinction weights or error message.</returns>
    public async Task<Result<DistinctionWeights, string>> RecognizeAsync(
        IReadOnlyList<DistinctionId> distinctionIds,
        string circumstance,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Performing recognition merge on {Count} distinctions", distinctionIds.Count);

            // 1. Load all distinction weights
            var weightsList = new List<byte[]>();
            var embeddings = new List<float[]>();

            foreach (var id in distinctionIds)
            {
                var weightsResult = await _storage.GetDistinctionWeightsAsync(id, ct);
                if (weightsResult.IsSuccess)
                {
                    // For mock: use a simple byte array representation
                    weightsList.Add(new byte[1024]); // Placeholder weights
                    embeddings.Add(weightsResult.Value.Embedding);
                }
            }

            if (weightsList.Count == 0)
            {
                return Result<DistinctionWeights, string>.Failure("No valid distinctions to merge");
            }

            // 2. Compute self-embedding (average of all embeddings)
            var selfEmbedding = AverageEmbeddings(embeddings);

            // 3. Perform recognition merge via PEFT
            var mergeResult = await _peft.MergeOnRecognitionAsync(
                _baseModelName,
                weightsList,
                selfEmbedding,
                ct);

            if (mergeResult.IsFailure)
            {
                return Result<DistinctionWeights, string>.Failure(mergeResult.Error);
            }

            // 4. Create new distinction weights for the recognized state
            var recognitionWeights = new DistinctionWeights(
                Id: DistinctionId.NewId(),
                Embedding: selfEmbedding,
                DissolutionMask: new float[selfEmbedding.Length],
                RecognitionTransform: selfEmbedding,
                LearnedAtStage: DreamStage.Recognition,
                Fitness: 1.0, // Recognition has high fitness
                Circumstance: circumstance,
                CreatedAt: DateTime.UtcNow,
                LastUpdatedAt: null);

            // 5. Store the recognition result
            var storeResult = await _storage.StoreDistinctionWeightsAsync(
                recognitionWeights.Id, recognitionWeights, ct);

            if (storeResult.IsFailure)
            {
                return Result<DistinctionWeights, string>.Failure(storeResult.Error);
            }

            _logger?.LogInformation("Successfully performed recognition merge");

            return Result<DistinctionWeights, string>.Success(recognitionWeights);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error performing recognition merge");
            return Result<DistinctionWeights, string>.Failure($"Recognition failed: {ex.Message}");
        }
    }

    private static double CalculateImportance(DreamStage stage) => stage switch
    {
        DreamStage.Void => 0.1,
        DreamStage.Distinction => 0.3,
        DreamStage.SubjectEmerges => 0.5,
        DreamStage.WorldCrystallizes => 0.7,
        DreamStage.Forgetting => 0.8,
        DreamStage.Questioning => 0.6,
        DreamStage.Recognition => 1.0, // Highest importance
        DreamStage.Dissolution => 0.2,
        DreamStage.NewDream => 0.1,
        _ => 0.5
    };

    private static string ExtractDistinction(Observation observation, DreamStage stage)
    {
        // Simple extraction - in real implementation, this would be more sophisticated
        return stage switch
        {
            DreamStage.Distinction => $"First distinction: {observation.Content}",
            DreamStage.SubjectEmerges => $"Subject notices: {observation.Content}",
            DreamStage.WorldCrystallizes => $"Object emerges: {observation.Content}",
            DreamStage.Recognition => $"I am the distinction: {observation.Content}",
            _ => observation.Content
        };
    }

    private static float[] ComputeDissolutionMask(float[] embedding, DreamStage stage)
    {
        // Create a mask based on embedding magnitude
        var mask = new float[embedding.Length];
        for (int i = 0; i < embedding.Length; i++)
        {
            mask[i] = Math.Abs(embedding[i]);
        }

        return mask;
    }

    private static float[] AverageEmbeddings(List<float[]> embeddings)
    {
        if (embeddings.Count == 0)
        {
            return Array.Empty<float>();
        }

        var size = embeddings[0].Length;
        var average = new float[size];

        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < Math.Min(size, embedding.Length); i++)
            {
                average[i] += embedding[i];
            }
        }

        for (int i = 0; i < size; i++)
        {
            average[i] /= embeddings.Count;
        }

        return average;
    }

    private async Task<byte[]> LoadOrCreateWeightsAsync(
        DistinctionState currentState,
        CancellationToken ct)
    {
        // For now, create new weights
        // In real implementation, would load from storage
        var config = AdapterConfig.Default();
        var initResult = await _peft.InitializeAdapterAsync(_baseModelName, config, ct);
        return initResult.IsSuccess ? initResult.Value : new byte[1024];
    }
}
