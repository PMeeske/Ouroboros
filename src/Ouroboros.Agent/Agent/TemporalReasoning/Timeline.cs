// <copyright file="Timeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Represents a timeline constructed from a set of temporal events.
/// </summary>
public sealed record Timeline(
    IReadOnlyList<TemporalEvent> Events,
    IReadOnlyList<TemporalRelation> Relations,
    DateTime EarliestTime,
    DateTime LatestTime,
    IReadOnlyDictionary<string, IReadOnlyList<TemporalEvent>> EventsByType);
