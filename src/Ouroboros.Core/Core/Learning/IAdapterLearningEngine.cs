// <copyright file="IAdapterLearningEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for Parameter-Efficient Fine-Tuning (PEFT) adapter learning engine.
/// Provides continual learning capabilities without catastrophic forgetting through LoRA adapters.
/// </summary>
public interface IAdapterLearningEngine
{
    /// <summary>
    /// Creates a new adapter for a specific task.
    /// </summary>
    /// <param name="taskName">The name of the task this adapter will be trained for.</param>
    /// <param name="config">Configuration for the adapter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the adapter ID or an error message.</returns>
    Task<Result<AdapterId, string>> CreateAdapterAsync(
        string taskName,
        AdapterConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Trains an adapter on a set of training examples.
    /// </summary>
    /// <param name="adapterId">The ID of the adapter to train.</param>
    /// <param name="examples">List of training examples.</param>
    /// <param name="config">Training configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Task<Result<Unit, string>> TrainAdapterAsync(
        AdapterId adapterId,
        List<TrainingExample> examples,
        TrainingConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Merges multiple adapters into a single adapter using the specified strategy.
    /// </summary>
    /// <param name="adapters">List of adapter IDs to merge.</param>
    /// <param name="strategy">The merge strategy to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Task<Result<Unit, string>> MergeAdaptersAsync(
        List<AdapterId> adapters,
        MergeStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Generates text using the base model with an optional adapter.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="adapterId">Optional adapter ID to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the generated text or an error message.</returns>
    Task<Result<string, string>> GenerateWithAdapterAsync(
        string prompt,
        AdapterId? adapterId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Learns from user feedback by updating the adapter incrementally.
    /// Enables continual learning without catastrophic forgetting.
    /// </summary>
    /// <param name="prompt">The original input prompt.</param>
    /// <param name="generation">The generated output.</param>
    /// <param name="feedback">The feedback signal from the user.</param>
    /// <param name="adapterId">The adapter ID to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Task<Result<Unit, string>> LearnFromFeedbackAsync(
        string prompt,
        string generation,
        FeedbackSignal feedback,
        AdapterId adapterId,
        CancellationToken ct = default);
}
