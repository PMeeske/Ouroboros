// <copyright file="MetaAlgorithm.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Specifies the meta-learning algorithm to use for model training.
/// Each algorithm has different trade-offs between performance and computational complexity.
/// </summary>
public enum MetaAlgorithm
{
    /// <summary>
    /// Model-Agnostic Meta-Learning (MAML).
    /// Uses second-order gradients for fast adaptation.
    /// Most powerful but computationally expensive.
    /// </summary>
    MAML,

    /// <summary>
    /// Reptile - simplified MAML variant.
    /// Uses first-order gradients, computationally efficient.
    /// Good balance between performance and speed.
    /// </summary>
    Reptile,

    /// <summary>
    /// Prototypical Networks.
    /// Uses metric learning for few-shot classification.
    /// Efficient for classification tasks.
    /// </summary>
    ProtoNet,

    /// <summary>
    /// Meta-SGD with learned learning rates.
    /// Learns optimal learning rates per parameter.
    /// More flexible than standard MAML.
    /// </summary>
    MetaSGD,

    /// <summary>
    /// Latent Embedding Optimization.
    /// Uses latent space for parameter updates.
    /// Good for high-dimensional parameter spaces.
    /// </summary>
    LEO,
}
