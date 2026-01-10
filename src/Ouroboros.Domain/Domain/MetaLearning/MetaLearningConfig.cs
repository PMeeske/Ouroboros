// <copyright file="MetaLearningConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Configuration for meta-learning training.
/// Specifies hyperparameters for the meta-learning process.
/// </summary>
/// <param name="Algorithm">The meta-learning algorithm to use.</param>
/// <param name="InnerLearningRate">Learning rate for inner loop (task-specific adaptation).</param>
/// <param name="OuterLearningRate">Learning rate for outer loop (meta-parameter updates).</param>
/// <param name="InnerSteps">Number of gradient steps in inner loop.</param>
/// <param name="TaskBatchSize">Number of tasks to sample per meta-iteration.</param>
/// <param name="MetaIterations">Total number of meta-training iterations.</param>
public sealed record MetaLearningConfig(
    MetaAlgorithm Algorithm,
    double InnerLearningRate,
    double OuterLearningRate,
    int InnerSteps,
    int TaskBatchSize,
    int MetaIterations)
{
    /// <summary>
    /// Gets a default configuration for MAML algorithm.
    /// Uses standard hyperparameters from the original MAML paper.
    /// </summary>
    public static MetaLearningConfig DefaultMAML => new(
        Algorithm: MetaAlgorithm.MAML,
        InnerLearningRate: 0.01,
        OuterLearningRate: 0.001,
        InnerSteps: 5,
        TaskBatchSize: 4,
        MetaIterations: 1000);

    /// <summary>
    /// Gets a default configuration for Reptile algorithm.
    /// Uses recommended hyperparameters for efficient training.
    /// </summary>
    public static MetaLearningConfig DefaultReptile => new(
        Algorithm: MetaAlgorithm.Reptile,
        InnerLearningRate: 0.01,
        OuterLearningRate: 0.001,
        InnerSteps: 10,
        TaskBatchSize: 1,
        MetaIterations: 2000);
}
