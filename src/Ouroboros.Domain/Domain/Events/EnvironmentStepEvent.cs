// <copyright file="EnvironmentStepEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Domain.Environment;

namespace Ouroboros.Domain.Events;

/// <summary>
/// Event representing a single environment interaction step.
/// Captures state → action → observation transition in the DAG.
/// </summary>
/// <param name="Id">Unique identifier for this event</param>
/// <param name="EpisodeId">The episode this step belongs to</param>
/// <param name="Step">The environment step data</param>
/// <param name="Timestamp">When this step occurred</param>
public sealed record EnvironmentStepEvent(
    Guid Id,
    Guid EpisodeId,
    EnvironmentStep Step,
    DateTime Timestamp) : PipelineEvent(Id, "EnvironmentStep", Timestamp);
