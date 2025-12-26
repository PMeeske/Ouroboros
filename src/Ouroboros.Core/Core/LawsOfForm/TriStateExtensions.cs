// <copyright file="TriStateExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

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

    /// <summary>
    /// Converts a TriState to a Form.
    /// </summary>
    /// <param name="state">The TriState to convert.</param>
    /// <returns>The corresponding Form.</returns>
    public static Form ToForm(this TriState state) => state switch
    {
        TriState.Mark => Form.Mark,
        TriState.Void => Form.Void,
        TriState.Imaginary => Form.Imaginary,
        _ => Form.Void
    };

    /// <summary>
    /// Converts a nullable boolean to TriState.
    /// </summary>
    /// <param name="value">The nullable boolean.</param>
    /// <returns>Mark for true, Void for false, Imaginary for null.</returns>
    public static TriState FromNullable(bool? value) => value switch
    {
        true => TriState.Mark,
        false => TriState.Void,
        null => TriState.Imaginary
    };

    /// <summary>
    /// Converts a TriState to a nullable boolean.
    /// </summary>
    /// <param name="state">The TriState to convert.</param>
    /// <returns>True for Mark/On, false for Void/Off, null for Imaginary/Inherit.</returns>
    public static bool? ToNullable(this TriState state) => state switch
    {
        TriState.Mark => true,
        TriState.Void => false,
        TriState.Imaginary => null,
        _ => null
    };

    /// <summary>
    /// Resolves a TriState to a boolean using a parent value as default.
    /// </summary>
    /// <param name="state">The TriState to resolve.</param>
    /// <param name="parentValue">The parent value to use if state is Inherit/Imaginary.</param>
    /// <returns>The resolved boolean value.</returns>
    public static bool Resolve(this TriState state, bool parentValue) => state switch
    {
        TriState.Mark => true,
        TriState.Void => false,
        TriState.Imaginary => parentValue,
        _ => parentValue
    };

    /// <summary>
    /// Logical AND operation on TriState values.
    /// Mark AND Mark = Mark
    /// Void AND anything = Void (except Imaginary takes precedence)
    /// Imaginary AND anything = Imaginary
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of the AND operation.</returns>
    public static TriState And(this TriState left, TriState right)
    {
        // Imaginary propagates (uncertainty)
        if (left == TriState.Imaginary || right == TriState.Imaginary)
        {
            return TriState.Imaginary;
        }

        // Void (false) dominates
        if (left == TriState.Void || right == TriState.Void)
        {
            return TriState.Void;
        }

        // Both must be Mark
        return TriState.Mark;
    }

    /// <summary>
    /// Logical OR operation on TriState values.
    /// Void OR Void = Void
    /// Mark OR anything = Mark (except Imaginary takes precedence)
    /// Imaginary OR anything = Imaginary
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result of the OR operation.</returns>
    public static TriState Or(this TriState left, TriState right)
    {
        // Imaginary propagates (uncertainty)
        if (left == TriState.Imaginary || right == TriState.Imaginary)
        {
            return TriState.Imaginary;
        }

        // Mark (true) dominates
        if (left == TriState.Mark || right == TriState.Mark)
        {
            return TriState.Mark;
        }

        // Both must be Void
        return TriState.Void;
    }
}
