// <copyright file="Episode.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;

/// <summary>
/// Represents a single episode in the episodic memory system.
/// Captures complete execution context including reasoning trace, outcome, and learned lessons.
/// </summary>
/// <param name="Id">Unique identifier for this episode.</param>
/// <param name="Timestamp">When this episode occurred.</param>
/// <param name="Goal">The goal or objective of this execution.</param>
/// <param name="ReasoningTrace">The complete pipeline branch with reasoning events.</param>
/// <param name="Result">The outcome of the execution.</param>
/// <param name="SuccessScore">Numerical score indicating quality of outcome (0.0 to 1.0).</param>
/// <param name="LessonsLearned">Key insights or lessons from this episode.</param>
/// <param name="Context">Additional contextual metadata.</param>
/// <param name="Embedding">Vector embedding for semantic search.</param>
public sealed record Episode(
    Guid Id,
    DateTime Timestamp,
    string Goal,
    PipelineBranch ReasoningTrace,
    Outcome Result,
    double SuccessScore,
    ImmutableList<string> LessonsLearned,
    ImmutableDictionary<string, object> Context,
    float[] Embedding);
