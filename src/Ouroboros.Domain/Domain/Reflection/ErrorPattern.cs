// <copyright file="ErrorPattern.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Represents a recurring error pattern detected through clustering/pattern matching.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Description">Human-readable description of the error pattern</param>
/// <param name="Frequency">Number of times this pattern has been observed</param>
/// <param name="Examples">Example failed episodes exhibiting this pattern</param>
/// <param name="SuggestedFix">Optional suggestion for fixing this error pattern</param>
public sealed record ErrorPattern(
    string Description,
    int Frequency,
    IReadOnlyList<FailedEpisode> Examples,
    string? SuggestedFix)
{
    /// <summary>
    /// Gets the severity score based on frequency and recency.
    /// Higher values indicate more critical patterns.
    /// </summary>
    public double SeverityScore
    {
        get
        {
            if (this.Examples.Count == 0)
            {
                return 0.0;
            }

            // Weight frequency and recency
            var frequencyScore = Math.Min(1.0, this.Frequency / 10.0);
            var now = DateTime.UtcNow;
            var avgRecency = this.Examples
                .Select(e => (now - e.Timestamp).TotalDays)
                .Average();

            // Recent errors are more severe
            var recencyScore = Math.Max(0.0, 1.0 - (avgRecency / 30.0));

            return (frequencyScore * 0.7) + (recencyScore * 0.3);
        }
    }
}
