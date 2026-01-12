// <copyright file="DissolutionStrategy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Strategy for dissolving (forgetting) distinctions.
/// Based on Laws of Form: dissolution is principled return to void (∅).
/// </summary>
public enum DissolutionStrategy
{
    /// <summary>
    /// Remove distinctions below a fitness threshold.
    /// Low-performing distinctions dissolve back to void.
    /// </summary>
    FitnessThreshold,

    /// <summary>
    /// Remove distinctions that contradict observations.
    /// Contradictions indicate the distinction is imaginary (not stable).
    /// </summary>
    ContradictionBased,

    /// <summary>
    /// Complete dissolution - return all distinctions to void (∅).
    /// Fresh start, tabula rasa.
    /// </summary>
    Complete,

    /// <summary>
    /// Remove distinctions that haven't been reinforced recently.
    /// Time-based decay back to void.
    /// </summary>
    TemporalDecay
}
