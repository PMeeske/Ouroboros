// <copyright file="Observation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Represents an observation in the Distinction Learning framework.
/// An observation is input from the environment that may trigger learning.
/// </summary>
/// <param name="Content">The content of the observation.</param>
/// <param name="Timestamp">When the observation occurred.</param>
/// <param name="Confidence">Confidence level of the observation (0.0 to 1.0).</param>
public sealed record Observation(
    string Content,
    DateTime Timestamp,
    double Confidence = 1.0)
{
    /// <summary>
    /// Creates an observation with current timestamp.
    /// </summary>
    /// <param name="content">The observation content.</param>
    /// <param name="confidence">Confidence level. Default: 1.0.</param>
    /// <returns>A new observation.</returns>
    public static Observation Now(string content, double confidence = 1.0) =>
        new(content, DateTime.UtcNow, confidence);
}
