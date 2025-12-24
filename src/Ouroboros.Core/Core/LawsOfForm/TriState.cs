// <copyright file="TriState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Three-valued configuration state for hierarchical configuration inheritance.
/// Maps to Laws of Form: On → Mark, Off → Void, Inherit → Imaginary.
/// </summary>
public enum TriState
{
    /// <summary>
    /// Explicitly disabled (maps to Void).
    /// </summary>
    Off = 0,

    /// <summary>
    /// Explicitly enabled (maps to Mark).
    /// </summary>
    On = 1,

    /// <summary>
    /// Inherit from parent scope (maps to Imaginary - unresolved).
    /// </summary>
    Inherit = 2,
}

/// <summary>
/// Extension methods for TriState configuration logic.
/// </summary>
public static class TriStateExtensions
{
    /// <summary>
    /// Converts TriState to Form for Laws of Form operations.
    /// </summary>
    /// <param name="state">The TriState value.</param>
    /// <returns>The corresponding Form value.</returns>
    public static Form ToForm(this TriState state) => state switch
    {
        TriState.On => Form.Mark,
        TriState.Off => Form.Void,
        TriState.Inherit => Form.Imaginary,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    /// <summary>
    /// Converts Form back to TriState.
    /// </summary>
    /// <param name="form">The Form value.</param>
    /// <returns>The corresponding TriState value.</returns>
    public static TriState ToTriState(this Form form) => form switch
    {
        Form.Mark => TriState.On,
        Form.Void => TriState.Off,
        Form.Imaginary => TriState.Inherit,
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    /// <summary>
    /// Resolves a TriState value using a parent value if inheritance is specified.
    /// Follows Laws of Form resolution semantics.
    /// </summary>
    /// <param name="state">The state to resolve.</param>
    /// <param name="parentValue">The parent value to inherit from.</param>
    /// <returns>The resolved boolean value.</returns>
    /// <example>
    /// // Explicit values don't change
    /// TriState.On.Resolve(false) // returns true
    /// TriState.Off.Resolve(true) // returns false
    /// // Inherit takes parent value
    /// TriState.Inherit.Resolve(true) // returns true
    /// TriState.Inherit.Resolve(false) // returns false
    /// </example>
    public static bool Resolve(this TriState state, bool parentValue) => state switch
    {
        TriState.On => true,
        TriState.Off => false,
        TriState.Inherit => parentValue,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    /// <summary>
    /// Resolves a chain of TriState values from most specific to most general.
    /// Walks the hierarchy until finding a non-Inherit value, or uses the default.
    /// </summary>
    /// <param name="defaultValue">The default value if all are Inherit.</param>
    /// <param name="chain">The chain of values from most specific to most general.</param>
    /// <returns>The resolved boolean value.</returns>
    /// <example>
    /// // User=Inherit, Team=On, Org=Inherit, System=Off → returns true (from Team)
    /// TriState.ResolveChain(false, TriState.Inherit, TriState.On, TriState.Inherit, TriState.Off);
    /// // User=Inherit, Team=Inherit, Org=Inherit, System=Inherit → returns false (default)
    /// TriState.ResolveChain(false, TriState.Inherit, TriState.Inherit, TriState.Inherit, TriState.Inherit);
    /// </example>
    public static bool ResolveChain(bool defaultValue, params TriState[] chain)
    {
        foreach (var state in chain)
        {
            if (state != TriState.Inherit)
            {
                return state == TriState.On;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Three-valued AND operation with correct inheritance semantics.
    /// Both operands must be On for result to be On.
    /// If either is Off, result is Off.
    /// If either is Inherit (and the other isn't Off), result is Inherit.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The conjunction result.</returns>
    /// <example>
    /// TriState.On.And(TriState.On) // returns On
    /// TriState.On.And(TriState.Off) // returns Off
    /// TriState.On.And(TriState.Inherit) // returns Inherit (uncertainty propagates)
    /// </example>
    public static TriState And(this TriState left, TriState right)
    {
        var result = left.ToForm().And(right.ToForm());
        return result.ToTriState();
    }

    /// <summary>
    /// Three-valued OR operation with correct inheritance semantics.
    /// Either operand being On makes result On.
    /// If either is Inherit (and the other isn't On), result is Inherit.
    /// Both must be Off for result to be Off.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The disjunction result.</returns>
    /// <example>
    /// TriState.Off.Or(TriState.Off) // returns Off
    /// TriState.Off.Or(TriState.On) // returns On
    /// TriState.Off.Or(TriState.Inherit) // returns Inherit (uncertainty propagates)
    /// </example>
    public static TriState Or(this TriState left, TriState right)
    {
        var result = left.ToForm().Or(right.ToForm());
        return result.ToTriState();
    }

    /// <summary>
    /// Converts bool to TriState.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>On for true, Off for false.</returns>
    public static TriState FromBool(bool value) => value ? TriState.On : TriState.Off;

    /// <summary>
    /// Converts nullable bool to TriState.
    /// </summary>
    /// <param name="value">The nullable boolean value.</param>
    /// <returns>On for true, Off for false, Inherit for null.</returns>
    public static TriState FromNullable(bool? value) => value switch
    {
        true => TriState.On,
        false => TriState.Off,
        null => TriState.Inherit,
    };

    /// <summary>
    /// Converts TriState to nullable bool.
    /// </summary>
    /// <param name="state">The TriState value.</param>
    /// <returns>True for On, false for Off, null for Inherit.</returns>
    public static bool? ToNullable(this TriState state) => state switch
    {
        TriState.On => true,
        TriState.Off => false,
        TriState.Inherit => null,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
