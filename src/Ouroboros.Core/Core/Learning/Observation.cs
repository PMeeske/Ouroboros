// <copyright file="Observation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents an observation used for distinction-based learning.
/// An observation is a circumstance marked at a moment in time,
/// with epistemic certainty tracked as a Form.
/// </summary>
/// <param name="Content">The content/text of the observation.</param>
/// <param name="Timestamp">When the observation was made.</param>
/// <param name="PriorCertainty">Epistemic certainty before observation (Form: Mark/Void/Imaginary).</param>
/// <param name="Context">Optional contextual information about the observation.</param>
public sealed record Observation(
    string Content,
    DateTime Timestamp,
    Form PriorCertainty,
    string? Context = null)
{
    /// <summary>
    /// Creates an observation with Mark (certain) prior.
    /// </summary>
    /// <param name="content">The observation content.</param>
    /// <param name="context">Optional context.</param>
    /// <returns>An observation with certain prior.</returns>
    public static Observation WithCertainPrior(string content, string? context = null)
        => new(content, DateTime.UtcNow, Form.Mark, context);

    /// <summary>
    /// Creates an observation with Void (certain negative) prior.
    /// </summary>
    /// <param name="content">The observation content.</param>
    /// <param name="context">Optional context.</param>
    /// <returns>An observation with void prior.</returns>
    public static Observation WithVoidPrior(string content, string? context = null)
        => new(content, DateTime.UtcNow, Form.Void, context);

    /// <summary>
    /// Creates an observation with Imaginary (uncertain) prior.
    /// </summary>
    /// <param name="content">The observation content.</param>
    /// <param name="context">Optional context.</param>
    /// <returns>An observation with imaginary prior.</returns>
    public static Observation WithUncertainPrior(string content, string? context = null)
        => new(content, DateTime.UtcNow, Form.Imaginary, context);
}
