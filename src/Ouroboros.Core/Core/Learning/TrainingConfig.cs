// <copyright file="TrainingConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Configuration for adapter training process.
/// </summary>
/// <param name="BatchSize">Number of examples per training batch. Default: 4.</param>
/// <param name="Epochs">Number of training epochs. Default: 1.</param>
/// <param name="IncrementalUpdate">Whether to update adapter incrementally without full retraining. Default: false.</param>
public sealed record TrainingConfig(
    int BatchSize = 4,
    int Epochs = 1,
    bool IncrementalUpdate = false)
{
    /// <summary>
    /// Creates a default training configuration.
    /// </summary>
    /// <returns>Default training configuration.</returns>
    public static TrainingConfig Default() => new();

    /// <summary>
    /// Creates a fast training configuration for quick iterations.
    /// </summary>
    /// <returns>Fast training configuration.</returns>
    public static TrainingConfig Fast() => new(BatchSize: 8, Epochs: 1);

    /// <summary>
    /// Creates a thorough training configuration for better quality.
    /// </summary>
    /// <returns>Thorough training configuration.</returns>
    public static TrainingConfig Thorough() => new(BatchSize: 4, Epochs: 3);

    /// <summary>
    /// Validates the training configuration.
    /// </summary>
    /// <returns>Success if valid, Failure with error message otherwise.</returns>
    public Monads.Result<TrainingConfig, string> Validate()
    {
        if (this.BatchSize <= 0)
        {
            return Monads.Result<TrainingConfig, string>.Failure("Batch size must be positive");
        }

        if (this.Epochs <= 0)
        {
            return Monads.Result<TrainingConfig, string>.Failure("Epochs must be positive");
        }

        return Monads.Result<TrainingConfig, string>.Success(this);
    }
}
