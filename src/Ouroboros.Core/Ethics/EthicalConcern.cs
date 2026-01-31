// <copyright file="EthicalConcern.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Severity levels for ethical concerns.
/// </summary>
public enum ConcernLevel
{
    /// <summary>Informational, for awareness</summary>
    Info,

    /// <summary>Minor concern, should be noted</summary>
    Low,

    /// <summary>Moderate concern, should be reviewed</summary>
    Medium,

    /// <summary>Significant concern, requires careful consideration</summary>
    High
}

/// <summary>
/// Represents an immutable ethical concern that doesn't rise to a violation.
/// Concerns are issues that warrant attention but don't necessarily block action.
/// </summary>
public sealed record EthicalConcern
{
    /// <summary>
    /// Gets the unique identifier for this concern.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the ethical principle this concern relates to.
    /// </summary>
    public required EthicalPrinciple RelatedPrinciple { get; init; }

    /// <summary>
    /// Gets the description of the concern.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the severity level of this concern.
    /// </summary>
    public required ConcernLevel Level { get; init; }

    /// <summary>
    /// Gets the recommended action to address this concern.
    /// </summary>
    public required string RecommendedAction { get; init; }

    /// <summary>
    /// Gets the timestamp when this concern was raised.
    /// </summary>
    public DateTime RaisedAt { get; init; } = DateTime.UtcNow;
}
