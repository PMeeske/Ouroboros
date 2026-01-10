// <copyright file="TrainingExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Represents a training example with input and expected output.
/// </summary>
/// <param name="Input">The input for training.</param>
/// <param name="ExpectedOutput">The expected output for training.</param>
public sealed record TrainingExample(
    string Input,
    string ExpectedOutput);
