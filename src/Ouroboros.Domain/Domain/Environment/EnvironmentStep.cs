// <copyright file="EnvironmentStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Environment;

/// <summary>
/// Represents a single step in an environment episode.
/// Captures the complete state → action → observation transition.
/// </summary>
/// <param name="StepNumber">The sequential number of this step within the episode</param>
/// <param name="State">The environment state before the action</param>
/// <param name="Action">The action taken</param>
/// <param name="Observation">The observation received after the action</param>
/// <param name="Timestamp">When this step occurred</param>
/// <param name="Metadata">Optional metadata for this step</param>
public sealed record EnvironmentStep(
    int StepNumber,
    EnvironmentState State,
    EnvironmentAction Action,
    Observation Observation,
    DateTime Timestamp,
    IReadOnlyDictionary<string, object>? Metadata = null);
