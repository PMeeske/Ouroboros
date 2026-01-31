// <copyright file="TemporalRelation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Allen Interval Algebra relation types for temporal reasoning.
/// </summary>
public enum TemporalRelationType
{
    /// <summary>A ends before B starts.</summary>
    Before,

    /// <summary>A starts after B ends.</summary>
    After,

    /// <summary>A ends exactly when B starts.</summary>
    Meets,

    /// <summary>A starts exactly when B ends.</summary>
    MetBy,

    /// <summary>A starts before B, ends during B.</summary>
    Overlaps,

    /// <summary>B starts before A, ends during A.</summary>
    OverlappedBy,

    /// <summary>A is contained within B.</summary>
    During,

    /// <summary>A contains B.</summary>
    Contains,

    /// <summary>A and B start together, A ends first.</summary>
    Starts,

    /// <summary>A and B start together, B ends first.</summary>
    StartedBy,

    /// <summary>A and B end together, A starts later.</summary>
    Finishes,

    /// <summary>A and B end together, B starts later.</summary>
    FinishedBy,

    /// <summary>A and B have same start and end.</summary>
    Equals,
}

/// <summary>
/// Represents a temporal relationship between two events using Allen Interval Algebra.
/// </summary>
public sealed record TemporalRelation(
    TemporalEvent Event1,
    TemporalEvent Event2,
    TemporalRelationType RelationType,
    double Confidence);
