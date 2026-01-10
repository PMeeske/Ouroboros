// <copyright file="LearningTask.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Represents a learning task with training and test data.
/// </summary>
/// <param name="Name">The name of the learning task.</param>
/// <param name="TrainingData">The training examples for the task.</param>
/// <param name="TestData">The test examples for evaluation.</param>
public sealed record LearningTask(
    string Name,
    List<TrainingExample> TrainingData,
    List<TestExample> TestData);
