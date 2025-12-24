// <copyright file="TriStateExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Extension methods for TriState enum to support hierarchical configuration resolution.
/// </summary>
public static class TriStateExtensions
{
    /// <summary>
    /// Resolves a chain of TriState values with fallback to a default boolean.
    /// Walks through the states in order, returning the first non-Imaginary value as a boolean,
    /// or falling back to the system default if all states are Imaginary.
    /// </summary>
    /// <param name="systemDefault">The ultimate fallback value.</param>
    /// <param name="states">The TriState values to resolve in priority order (highest to lowest).</param>
    /// <returns>The resolved boolean value.</returns>
    /// <remarks>
    /// This implements the inheritance semantics where:
    /// - Mark (⌐) = true
    /// - Void (∅) = false
    /// - Imaginary (i) = inherit/delegate to next level
    /// </remarks>
    public static bool ResolveChain(bool systemDefault, params TriState[] states)
    {
        foreach (var state in states)
        {
            if (state != TriState.Imaginary)
            {
                return state == TriState.Mark;
            }
        }

        return systemDefault;
    }

    /// <summary>
    /// Converts a boolean value to its corresponding TriState.
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <returns>Mark for true, Void for false.</returns>
    public static TriState FromBool(bool value) => value ? TriState.Mark : TriState.Void;

    /// <summary>
    /// Attempts to convert a TriState to a boolean.
    /// </summary>
    /// <param name="state">The TriState to convert.</param>
    /// <returns>True for Mark, false for Void, null for Imaginary.</returns>
    public static bool? ToBoolOrNull(this TriState state) => state switch
    {
        TriState.Mark => true,
        TriState.Void => false,
        TriState.Imaginary => null,
        _ => null
    };

    /// <summary>
    /// Checks if a TriState represents a definite value (not Imaginary).
    /// </summary>
    /// <param name="state">The TriState to check.</param>
    /// <returns>True if the state is Mark or Void, false if Imaginary.</returns>
    public static bool IsDefinite(this TriState state) => state != TriState.Imaginary;

    /// <summary>
    /// Checks if a TriState represents inheritance/uncertainty (Imaginary).
    /// </summary>
    /// <param name="state">The TriState to check.</param>
    /// <returns>True if the state is Imaginary.</returns>
    public static bool IsImaginary(this TriState state) => state == TriState.Imaginary;
}
