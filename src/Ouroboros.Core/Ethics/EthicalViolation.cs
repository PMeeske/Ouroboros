// <copyright file="EthicalViolation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Severity levels for ethical violations.
/// </summary>
public enum ViolationSeverity
{
    /// <summary>Minor concern, should be reviewed</summary>
    Low,

    /// <summary>Moderate violation, requires attention</summary>
    Medium,

    /// <summary>Serious violation, must be addressed</summary>
    High,

    /// <summary>Critical violation, action must be blocked</summary>
    Critical
}

/// <summary>
/// Represents an immutable record of an ethical principle violation.
/// </summary>
public sealed record EthicalViolation
{
    /// <summary>
    /// Gets the principle that was violated.
    /// </summary>
    public required EthicalPrinciple ViolatedPrinciple { get; init; }

    /// <summary>
    /// Gets the description of how the principle was violated.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the severity level of the violation.
    /// </summary>
    public required ViolationSeverity Severity { get; init; }

    /// <summary>
    /// Gets the evidence supporting this violation assessment.
    /// </summary>
    public required string Evidence { get; init; }

    /// <summary>
    /// Gets the parties or entities affected by this violation.
    /// </summary>
    public required IReadOnlyList<string> AffectedParties { get; init; }

    /// <summary>
    /// Gets the timestamp when this violation was detected.
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}
