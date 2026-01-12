// <copyright file="IPeftIntegration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for HuggingFace PEFT (Parameter-Efficient Fine-Tuning) integration.
/// Abstracts the communication with Python PEFT library via Python.NET or REST API.
/// </summary>
public interface IPeftIntegration
{
    /// <summary>
    /// Initializes a new PEFT adapter with the specified configuration.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="config">Adapter configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing adapter weights or error message.</returns>
    Task<Result<byte[], string>> InitializeAdapterAsync(
        string modelName,
        AdapterConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Trains an adapter on the provided examples.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="adapterWeights">Current adapter weights.</param>
    /// <param name="examples">Training examples.</param>
    /// <param name="config">Training configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated adapter weights or error message.</returns>
    Task<Result<byte[], string>> TrainAdapterAsync(
        string modelName,
        byte[] adapterWeights,
        List<TrainingExample> examples,
        TrainingConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Generates text using the base model with an adapter.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="adapterWeights">Adapter weights to use (null for base model).</param>
    /// <param name="prompt">Input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing generated text or error message.</returns>
    Task<Result<string, string>> GenerateAsync(
        string modelName,
        byte[]? adapterWeights,
        string prompt,
        CancellationToken ct = default);

    /// <summary>
    /// Merges multiple adapters using the specified strategy.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="adapterWeights">List of adapter weights to merge.</param>
    /// <param name="strategy">Merge strategy.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing merged adapter weights or error message.</returns>
    Task<Result<byte[], string>> MergeAdaptersAsync(
        string modelName,
        List<byte[]> adapterWeights,
        MergeStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Validates adapter weights and checks their size.
    /// </summary>
    /// <param name="weights">Adapter weights to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing size in bytes or error message.</returns>
    Task<Result<long, string>> ValidateAdapterAsync(byte[] weights, CancellationToken ct = default);

    /// <summary>
    /// Trains adapter weights based on a distinction observation.
    /// The distinction's stage determines the training strategy.
    /// </summary>
    /// <param name="baseModelName">Base model name/path.</param>
    /// <param name="currentWeights">Current adapter weights.</param>
    /// <param name="example">Distinction training example.</param>
    /// <param name="config">Distinction training configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated adapter weights or error message.</returns>
    Task<Result<byte[], string>> TrainOnDistinctionAsync(
        string baseModelName,
        byte[] currentWeights,
        DistinctionTrainingExample example,
        DistinctionTrainingConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Applies dissolution to adapter weights - removes learned patterns.
    /// </summary>
    /// <param name="baseModelName">Base model name/path.</param>
    /// <param name="currentWeights">Current adapter weights.</param>
    /// <param name="dissolutionMask">Mask indicating which weights to remove.</param>
    /// <param name="dissolutionStrength">Strength of dissolution (0.0 to 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing modified adapter weights or error message.</returns>
    Task<Result<byte[], string>> ApplyDissolutionAsync(
        string baseModelName,
        byte[] currentWeights,
        float[] dissolutionMask,
        double dissolutionStrength,
        CancellationToken ct = default);

    /// <summary>
    /// Merges weights during Recognition stage (i = ⌐).
    /// Uses geometric mean to represent subject-object unity.
    /// </summary>
    /// <param name="baseModelName">Base model name/path.</param>
    /// <param name="weights">List of adapter weights to merge.</param>
    /// <param name="selfEmbedding">Embedding representing the self.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing merged adapter weights or error message.</returns>
    Task<Result<byte[], string>> MergeOnRecognitionAsync(
        string baseModelName,
        IReadOnlyList<byte[]> weights,
        float[] selfEmbedding,
        CancellationToken ct = default);
}

/// <summary>
/// Training example for distinction-based learning.
/// </summary>
/// <param name="Circumstance">The circumstance in which the distinction was made.</param>
/// <param name="DistinctionMade">The distinction itself: "This, not that".</param>
/// <param name="Stage">The dream stage at which this distinction occurs.</param>
/// <param name="ContextEmbedding">Semantic embedding of the context.</param>
/// <param name="ImportanceWeight">How important this distinction is (0.0 to 1.0).</param>
public sealed record DistinctionTrainingExample(
    string Circumstance,
    string DistinctionMade,
    DreamStage Stage,
    float[] ContextEmbedding,
    double ImportanceWeight);

/// <summary>
/// Configuration for distinction-based training.
/// </summary>
/// <param name="MaxSteps">Maximum training steps. Default: 100.</param>
/// <param name="LearningRate">Learning rate for training. Default: 1e-4.</param>
/// <param name="DistinctionWeight">Weight for this specific distinction. Default: 1.0.</param>
/// <param name="UseContrastiveLoss">Whether to use contrastive loss for "this, not that". Default: true.</param>
/// <param name="TargetStage">Target dream stage for training. Default: Recognition.</param>
public sealed record DistinctionTrainingConfig(
    int MaxSteps = 100,
    double LearningRate = 1e-4,
    double DistinctionWeight = 1.0,
    bool UseContrastiveLoss = true,
    DreamStage TargetStage = DreamStage.Recognition)
{
    /// <summary>
    /// Creates a configuration optimized for a specific dream stage.
    /// </summary>
    /// <param name="stage">The dream stage.</param>
    /// <returns>A training configuration tailored to the stage.</returns>
    public static DistinctionTrainingConfig ForStage(DreamStage stage) => stage switch
    {
        DreamStage.Void => new(MaxSteps: 10, LearningRate: 1e-5, DistinctionWeight: 0.1),
        DreamStage.Distinction => new(MaxSteps: 50, LearningRate: 5e-5, DistinctionWeight: 0.3),
        DreamStage.SubjectEmerges => new(MaxSteps: 100, LearningRate: 1e-4, DistinctionWeight: 0.5),
        DreamStage.WorldCrystallizes => new(MaxSteps: 150, LearningRate: 1e-4, DistinctionWeight: 0.7),
        DreamStage.Forgetting => new(MaxSteps: 200, LearningRate: 5e-5, DistinctionWeight: 0.8),
        DreamStage.Questioning => new(MaxSteps: 100, LearningRate: 1e-4, DistinctionWeight: 0.6),
        DreamStage.Recognition => new(MaxSteps: 200, LearningRate: 1e-4, DistinctionWeight: 1.0),
        DreamStage.Dissolution => new(MaxSteps: 50, LearningRate: 5e-5, DistinctionWeight: 0.2),
        DreamStage.NewDream => new(MaxSteps: 10, LearningRate: 1e-5, DistinctionWeight: 0.1),
        _ => new()
    };
}
