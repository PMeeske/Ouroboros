// <copyright file="ImprovementSuggestion.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Represents an actionable improvement suggestion based on performance analysis.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Area">The area or domain this suggestion targets</param>
/// <param name="Suggestion">The improvement suggestion description</param>
/// <param name="ExpectedImpact">Expected impact if implemented (0.0 to 1.0, higher is better)</param>
/// <param name="Implementation">Guidance on how to implement this suggestion</param>
public sealed record ImprovementSuggestion(
    string Area,
    string Suggestion,
    double ExpectedImpact,
    string Implementation)
{
    /// <summary>
    /// Gets the priority level based on expected impact.
    /// </summary>
    public string Priority => this.ExpectedImpact switch
    {
        >= 0.7 => "High",
        >= 0.4 => "Medium",
        _ => "Low"
    };
}
