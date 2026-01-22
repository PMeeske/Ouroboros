// <copyright file="TaskSequence.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Represents a sequence of learning tasks for continual learning benchmarks.
/// </summary>
/// <param name="Name">The name of the task sequence.</param>
/// <param name="Tasks">The ordered list of learning tasks.</param>
/// <param name="MeasureRetention">Whether to measure retention across tasks.</param>
public sealed record TaskSequence(
    string Name,
    List<LearningTask> Tasks,
    bool MeasureRetention);
