// <copyright file="IMetaLearningEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Implements meta-learning capabilities for fast adaptation to new tasks.
/// Supports MAML, Reptile, and other meta-learning algorithms.
/// </summary>
public interface IMetaLearningEngine
{
    /// <summary>
    /// Performs meta-training across multiple task families.
    /// Learns initial parameters that enable fast adaptation to new tasks.
    /// </summary>
    /// <param name="taskFamilies">Collection of task families to meta-train on.</param>
    /// <param name="config">Configuration for meta-learning algorithm.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the meta-trained model or error message.</returns>
    Task<Result<MetaModel, string>> MetaTrainAsync(
        List<TaskFamily> taskFamilies,
        MetaLearningConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Adapts a meta-trained model to a new task using few-shot examples.
    /// Performs rapid fine-tuning using the meta-learned initialization.
    /// </summary>
    /// <param name="metaModel">The meta-trained model to adapt.</param>
    /// <param name="fewShotExamples">Small number of examples from the target task.</param>
    /// <param name="adaptationSteps">Number of gradient steps for adaptation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the adapted model or error message.</returns>
    Task<Result<AdaptedModel, string>> AdaptToTaskAsync(
        MetaModel metaModel,
        List<Example> fewShotExamples,
        int adaptationSteps,
        CancellationToken ct = default);

    /// <summary>
    /// Computes similarity between two tasks using the meta-model.
    /// Useful for transfer learning and task routing.
    /// </summary>
    /// <param name="taskA">First task.</param>
    /// <param name="taskB">Second task.</param>
    /// <param name="metaModel">Meta-model for computing task representations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing similarity score (0-1) or error message.</returns>
    Task<Result<double, string>> ComputeTaskSimilarityAsync(
        SynthesisTask taskA,
        SynthesisTask taskB,
        MetaModel metaModel,
        CancellationToken ct = default);

    /// <summary>
    /// Embeds a task into a continuous vector space for similarity computation.
    /// Creates a representation capturing task characteristics.
    /// </summary>
    /// <param name="task">The task to embed.</param>
    /// <param name="metaModel">Meta-model for generating embeddings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing task embedding or error message.</returns>
    Task<Result<TaskEmbedding, string>> EmbedTaskAsync(
        SynthesisTask task,
        MetaModel metaModel,
        CancellationToken ct = default);
}
