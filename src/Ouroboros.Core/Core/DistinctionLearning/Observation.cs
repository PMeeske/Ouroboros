// <copyright file="Observation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Represents an observation from which distinctions can be learned.
/// </summary>
public sealed record Observation(
    string Content,
    DateTime Timestamp,
    double PriorCertainty,
    Dictionary<string, object> Context)
{
    /// <summary>
    /// Creates an observation with certain prior (high confidence).
    /// </summary>
    /// <param name="content">The content of the observation.</param>
    /// <param name="contextKey">Context key for the observation source.</param>
    /// <returns>An observation with high prior certainty.</returns>
    public static Observation WithCertainPrior(string content, string contextKey = "default")
    {
        return new Observation(
            Content: content,
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.9,
            Context: new Dictionary<string, object> { ["source"] = contextKey });
    }

    /// <summary>
    /// Creates an observation with uncertain prior (low confidence).
    /// </summary>
    /// <param name="content">The content of the observation.</param>
    /// <param name="contextKey">Context key for the observation source.</param>
    /// <returns>An observation with low prior certainty.</returns>
    public static Observation WithUncertainPrior(string content, string contextKey = "default")
    {
        return new Observation(
            Content: content,
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.3,
            Context: new Dictionary<string, object> { ["source"] = contextKey });
    }
}
