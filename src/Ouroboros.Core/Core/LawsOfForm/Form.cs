// <copyright file="Form.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Represents the three fundamental states in Laws of Form logic.
/// Based on G. Spencer-Brown's calculus of indications.
/// </summary>
public enum Form
{
    /// <summary>
    /// Void - the unmarked state (absence, false, nothing).
    /// Represents the empty or undistinguished state.
    /// </summary>
    Void = 0,

    /// <summary>
    /// Mark - the marked state (presence, true, distinction).
    /// Represents a clear distinction or boundary.
    /// </summary>
    Mark = 1,

    /// <summary>
    /// Imaginary - the indeterminate or oscillating state.
    /// Represents uncertainty, superposition, or unresolved state.
    /// In Spencer-Brown's calculus, this emerges from self-referential paradoxes.
    /// </summary>
    Imaginary = 2,
}

/// <summary>
/// Extension methods implementing Laws of Form algebraic operations.
/// Provides the fundamental laws: Calling, Crossing, and their consequences.
/// </summary>
public static class FormExtensions
{
    /// <summary>
    /// Law of Calling: The idempotent property of Forms.
    /// In Spencer-Brown's notation, juxtaposition represents calling the marked state.
    /// Here we implement the simpler idempotence: f(f(x)) = f(x).
    /// Mark remains Mark, Void remains Void, Imaginary remains Imaginary.
    /// </summary>
    /// <param name="form">The form to apply calling to.</param>
    /// <returns>The result of the calling operation.</returns>
    public static Form Calling(this Form form) => form switch
    {
        Form.Mark => Form.Mark,
        Form.Void => Form.Void,
        Form.Imaginary => Form.Imaginary,
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    /// <summary>
    /// Law of Crossing: Crossing twice returns to the original state.
    /// Not(Not(x)) = x.
    /// </summary>
    /// <param name="form">The form to cross.</param>
    /// <returns>The complement of the form.</returns>
    public static Form Cross(this Form form) => form switch
    {
        Form.Mark => Form.Void,
        Form.Void => Form.Mark,
        Form.Imaginary => Form.Imaginary, // Imaginary is self-dual
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    /// <summary>
    /// Three-valued AND operation following Laws of Form semantics.
    /// Imaginary propagates (represents uncertainty).
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The conjunction of the two forms.</returns>
    public static Form And(this Form left, Form right)
    {
        if (left == Form.Imaginary || right == Form.Imaginary)
        {
            return Form.Imaginary;
        }

        if (left == Form.Mark && right == Form.Mark)
        {
            return Form.Mark;
        }

        return Form.Void;
    }

    /// <summary>
    /// Three-valued OR operation following Laws of Form semantics.
    /// Imaginary propagates (represents uncertainty).
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The disjunction of the two forms.</returns>
    public static Form Or(this Form left, Form right)
    {
        if (left == Form.Imaginary || right == Form.Imaginary)
        {
            return Form.Imaginary;
        }

        if (left == Form.Mark || right == Form.Mark)
        {
            return Form.Mark;
        }

        return Form.Void;
    }

    /// <summary>
    /// Determines if the form represents a certain (definite) state.
    /// Mark and Void are certain; Imaginary is uncertain.
    /// </summary>
    /// <param name="form">The form to check.</param>
    /// <returns>True if the form is certain (Mark or Void), false if Imaginary.</returns>
    public static bool IsCertain(this Form form) => form != Form.Imaginary;

    /// <summary>
    /// Converts a boolean to a Form (Void = false, Mark = true).
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>Mark if true, Void if false.</returns>
    public static Form ToForm(this bool value) => value ? Form.Mark : Form.Void;

    /// <summary>
    /// Attempts to convert a Form to a boolean.
    /// Returns None for Imaginary (uncertain) values.
    /// </summary>
    /// <param name="form">The form to convert.</param>
    /// <returns>Some(true) for Mark, Some(false) for Void, None for Imaginary.</returns>
    public static Monads.Option<bool> ToBool(this Form form) => form switch
    {
        Form.Mark => Monads.Option<bool>.Some(true),
        Form.Void => Monads.Option<bool>.Some(false),
        Form.Imaginary => Monads.Option<bool>.None(),
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    /// <summary>
    /// Resolves a nullable boolean to a Form.
    /// Null maps to Imaginary (uncertain state).
    /// </summary>
    /// <param name="value">The nullable boolean value.</param>
    /// <returns>Mark for true, Void for false, Imaginary for null.</returns>
    public static Form ToForm(this bool? value) => value switch
    {
        true => Form.Mark,
        false => Form.Void,
        null => Form.Imaginary,
    };
}
