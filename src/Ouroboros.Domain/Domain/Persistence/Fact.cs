// <copyright file="Fact.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Persistence;

/// <summary>
/// Represents a fact or piece of evidence that can be used in reasoning and certainty assessment.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Id">Unique identifier for this fact</param>
/// <param name="Content">The textual content of the fact</param>
/// <param name="Source">Source or origin of this fact</param>
/// <param name="Confidence">Confidence level in this fact (0.0 to 1.0)</param>
/// <param name="Timestamp">When this fact was recorded</param>
/// <param name="Metadata">Additional metadata about the fact</param>
public sealed record Fact(
    Guid Id,
    string Content,
    string Source,
    double Confidence,
    DateTime Timestamp,
    IReadOnlyDictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Gets whether this fact has high confidence (>= 0.8).
    /// </summary>
    public bool IsHighConfidence => this.Confidence >= 0.8;

    /// <summary>
    /// Gets whether this fact is recent (within last 30 days).
    /// </summary>
    public bool IsRecent => (DateTime.UtcNow - this.Timestamp).TotalDays <= 30;
}
