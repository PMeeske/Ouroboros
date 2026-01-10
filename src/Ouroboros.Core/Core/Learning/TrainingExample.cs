// <copyright file="TrainingExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Represents a single training example for adapter learning.
/// </summary>
/// <param name="Input">The input text/prompt.</param>
/// <param name="Output">The expected output text.</param>
/// <param name="Weight">The importance weight of this example. Default: 1.0.</param>
public sealed record TrainingExample(
    string Input,
    string Output,
    double Weight = 1.0)
{
    /// <summary>
    /// Validates the training example.
    /// </summary>
    /// <returns>Success if valid, Failure with error message otherwise.</returns>
    public Monads.Result<TrainingExample, string> Validate()
    {
        if (string.IsNullOrWhiteSpace(this.Input))
        {
            return Monads.Result<TrainingExample, string>.Failure("Input cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(this.Output))
        {
            return Monads.Result<TrainingExample, string>.Failure("Output cannot be empty");
        }

        if (this.Weight <= 0)
        {
            return Monads.Result<TrainingExample, string>.Failure("Weight must be positive");
        }

        return Monads.Result<TrainingExample, string>.Success(this);
    }
}
