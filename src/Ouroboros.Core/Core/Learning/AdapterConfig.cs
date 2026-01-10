// <copyright file="AdapterConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Configuration for LoRA/PEFT adapter creation and training.
/// </summary>
/// <param name="Rank">LoRA rank (dimension of low-rank decomposition). Default: 8.</param>
/// <param name="LearningRate">Learning rate for training. Default: 3e-4.</param>
/// <param name="MaxSteps">Maximum training steps. Default: 1000.</param>
/// <param name="TargetModules">Comma-separated list of target modules for adaptation. Default: "q_proj,v_proj".</param>
/// <param name="UseRSLoRA">Whether to use Rank-Stabilized LoRA. Default: false.</param>
public sealed record AdapterConfig(
    int Rank = 8,
    double LearningRate = 3e-4,
    int MaxSteps = 1000,
    string TargetModules = "q_proj,v_proj",
    bool UseRSLoRA = false)
{
    /// <summary>
    /// Creates a default adapter configuration.
    /// </summary>
    /// <returns>Default adapter configuration.</returns>
    public static AdapterConfig Default() => new();

    /// <summary>
    /// Creates a low-rank configuration for smaller models.
    /// </summary>
    /// <returns>Low-rank adapter configuration.</returns>
    public static AdapterConfig LowRank() => new(Rank: 4);

    /// <summary>
    /// Creates a high-rank configuration for larger models.
    /// </summary>
    /// <returns>High-rank adapter configuration.</returns>
    public static AdapterConfig HighRank() => new(Rank: 16);

    /// <summary>
    /// Validates the adapter configuration.
    /// </summary>
    /// <returns>Success if valid, Failure with error message otherwise.</returns>
    public Monads.Result<AdapterConfig, string> Validate()
    {
        if (this.Rank <= 0)
        {
            return Monads.Result<AdapterConfig, string>.Failure("Rank must be positive");
        }

        if (this.LearningRate <= 0)
        {
            return Monads.Result<AdapterConfig, string>.Failure("Learning rate must be positive");
        }

        if (this.MaxSteps <= 0)
        {
            return Monads.Result<AdapterConfig, string>.Failure("Max steps must be positive");
        }

        if (string.IsNullOrWhiteSpace(this.TargetModules))
        {
            return Monads.Result<AdapterConfig, string>.Failure("Target modules cannot be empty");
        }

        return Monads.Result<AdapterConfig, string>.Success(this);
    }
}
