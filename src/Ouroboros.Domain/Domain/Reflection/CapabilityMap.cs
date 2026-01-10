// <copyright file="CapabilityMap.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Maps cognitive dimensions to performance scores.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Scores">Performance scores for each cognitive dimension (0.0 to 1.0)</param>
/// <param name="Strengths">List of identified strength areas</param>
/// <param name="Weaknesses">List of identified weakness areas</param>
public sealed record CapabilityMap(
    IReadOnlyDictionary<CognitiveDimension, double> Scores,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses)
{
    /// <summary>
    /// Gets the overall capability score (average across all dimensions).
    /// </summary>
    public double OverallScore =>
        this.Scores.Count > 0 ? this.Scores.Values.Average() : 0.0;

    /// <summary>
    /// Gets the strongest cognitive dimension.
    /// </summary>
    public CognitiveDimension? StrongestDimension =>
        this.Scores.Count > 0 ? this.Scores.OrderByDescending(kvp => kvp.Value).First().Key : null;

    /// <summary>
    /// Gets the weakest cognitive dimension.
    /// </summary>
    public CognitiveDimension? WeakestDimension =>
        this.Scores.Count > 0 ? this.Scores.OrderBy(kvp => kvp.Value).First().Key : null;
}
