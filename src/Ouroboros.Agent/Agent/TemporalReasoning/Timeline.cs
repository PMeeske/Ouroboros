// <copyright file="Timeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Represents a timeline constructed from a set of temporal events.
/// </summary>
public sealed record Timeline(
    List<TemporalEvent> Events,
    List<TemporalRelation> Relations,
    DateTime EarliestTime,
    DateTime LatestTime,
    Dictionary<string, List<TemporalEvent>> EventsByType);
