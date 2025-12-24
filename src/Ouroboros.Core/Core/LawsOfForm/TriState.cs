// <copyright file="TriState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Three-valued logic state based on Laws of Form.
/// Used for reasoning about certainty in AI systems.
/// </summary>
public enum TriState
{
    /// <summary>
    /// Void state - represents no distinction, certain negative, false.
    /// The unmarked state.
    /// </summary>
    Void = 0,

    /// <summary>
    /// Mark state - represents a distinction drawn, certain affirmative, true.
    /// The marked state (Cross).
    /// </summary>
    Mark = 1,

    /// <summary>
    /// Imaginary state - represents re-entry, uncertainty, or paradox.
    /// Occurs when a form refers to itself: f = ⌐f.
    /// In AI safety contexts, this indicates uncertainty requiring human review.
    /// </summary>
    Imaginary = 2
}
