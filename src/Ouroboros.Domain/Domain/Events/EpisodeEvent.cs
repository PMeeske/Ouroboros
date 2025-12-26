// <copyright file="EpisodeEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Domain.Environment;

namespace Ouroboros.Domain.Events;

/// <summary>
/// Event representing a complete episode of environment interaction.
/// Groups together all steps in an episode for replay and analysis.
/// </summary>
/// <param name="Id">Unique identifier for this event</param>
/// <param name="Episode">The complete episode data</param>
/// <param name="Timestamp">When this episode was recorded</param>
public sealed record EpisodeEvent(
    Guid Id,
    Episode Episode,
    DateTime Timestamp) : PipelineEvent(Id, "Episode", Timestamp);
