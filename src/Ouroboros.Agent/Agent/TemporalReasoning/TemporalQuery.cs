// <copyright file="TemporalQuery.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Default configuration values for temporal queries.
/// </summary>
public static class TemporalQueryDefaults
{
    /// <summary>
    /// Default maximum number of results to return from a temporal query.
    /// </summary>
    public const int DefaultMaxResults = 100;
}

/// <summary>
/// Represents a query for temporal events with various filtering criteria.
/// </summary>
public sealed record TemporalQuery(
    DateTime? After,
    DateTime? Before,
    TimeSpan? Duration,
    string? EventType,
    TemporalRelationType? RelationTo,
    Guid? RelatedEventId,
    int MaxResults = TemporalQueryDefaults.DefaultMaxResults);

/// <summary>
/// Represents a temporal constraint between two events.
/// </summary>
public sealed record TemporalConstraint(
    Guid Event1Id,
    Guid Event2Id,
    TemporalRelationType RequiredRelation);
