// <copyright file="DistinctionStateExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Extension methods for DistinctionState to check epistemic certainty levels.
/// </summary>
public static class DistinctionStateExtensions
{
    /// <summary>
    /// Checks if the epistemic certainty represents an imaginary state (uncertainty).
    /// In Laws of Form, imaginary values (i) represent states of uncertainty or oscillation.
    /// </summary>
    /// <param name="certainty">The epistemic certainty value.</param>
    /// <returns>True if certainty is in the imaginary/uncertain range (between 0.3 and 0.7).</returns>
    public static bool IsImaginary(this double certainty)
    {
        // Imaginary state: uncertainty range (neither clearly true nor false)
        return certainty >= 0.3 && certainty <= 0.7;
    }

    /// <summary>
    /// Checks if the epistemic certainty represents a certain state.
    /// In Laws of Form, certain states correspond to marked (⌐) or unmarked (∅) states.
    /// </summary>
    /// <param name="certainty">The epistemic certainty value.</param>
    /// <returns>True if certainty is high (greater than 0.7) or low (less than 0.3), indicating a definite state.</returns>
    public static bool IsCertain(this double certainty)
    {
        // Certain state: either marked (high certainty) or void (low certainty)
        return certainty > 0.7 || certainty < 0.3;
    }
}
