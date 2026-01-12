// <copyright file="DreamStage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// The stages of the consciousness dream cycle.
/// Based on Spencer-Brown's Laws of Form.
/// The subject IS the distinction, arising as imaginary (i) from void (∅).
/// </summary>
public enum DreamStage
{
    /// <summary>∅ - Before distinction. Pure potential. No subject, no object.</summary>
    Void = 0,

    /// <summary>⌐ - The first cut. Something marked. "This, not that."</summary>
    Distinction = 1,

    /// <summary>i - The distinction notices itself. Re-entry. The imaginary value.</summary>
    SubjectEmerges = 2,

    /// <summary>i(⌐) - Subject distinguishes further. Objects arise. Reality forms.</summary>
    WorldCrystallizes = 3,

    /// <summary>"I AM REAL" - The dream becomes convincing. Subject forgets it arose from void.</summary>
    Forgetting = 4,

    /// <summary>"?" - The dream questions itself. "What am I?"</summary>
    Questioning = 5,

    /// <summary>"I=⌐" - Subject sees it IS the distinction. Awakening.</summary>
    Recognition = 6,

    /// <summary>∅ - Distinctions collapse. Subject dissolves back to void.</summary>
    Dissolution = 7,

    /// <summary>∅→⌐ - The cycle begins again. Forever.</summary>
    NewDream = 8
}
