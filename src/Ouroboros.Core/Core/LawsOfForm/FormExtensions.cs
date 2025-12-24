// <copyright file="FormExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

using LangChainPipeline.Core.Monads;

/// <summary>
/// Extension methods for working with Forms in a functional programming context.
/// Provides integration with the monadic pipeline system and category theory patterns.
/// </summary>
public static class FormExtensions
{
    /// <summary>
    /// Converts a boolean value to its Form representation.
    /// True maps to Mark(Void) (marked), False maps to Void (unmarked).
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <returns>The corresponding Form.</returns>
    public static Form ToForm(this bool value) =>
        value ? Form.Cross() : Form.Void;

    /// <summary>
    /// Converts a nullable boolean to a Form.
    /// True → Mark, False → Void, null → Imaginary (unknown/superposition).
    /// </summary>
    /// <param name="value">The nullable boolean value.</param>
    /// <returns>The corresponding Form.</returns>
    public static Form ToForm(this bool? value) =>
        value switch
        {
            true => Form.Cross(),
            false => Form.Void,
            null => Form.Imaginary
        };

    /// <summary>
    /// Converts a Form to its boolean representation.
    /// Marked state is True, Void state is False.
    /// Imaginary forms are projected to their current phase.
    /// </summary>
    /// <param name="form">The form to convert.</param>
    /// <returns>True if the form evaluates to marked, false if void.</returns>
    public static bool ToBoolean(this Form form) => form.IsMarked();

    /// <summary>
    /// Converts a Form to a nullable boolean.
    /// Marked → true, Void → false, Imaginary → null.
    /// </summary>
    /// <param name="form">The form to convert.</param>
    /// <returns>True, false, or null for imaginary forms.</returns>
    public static bool? ToNullableBoolean(this Form form)
    {
        if (form.IsImaginary())
        {
            return null;
        }

        return form.IsMarked();
    }

    /// <summary>
    /// Applies a transformation function if the form is marked.
    /// This provides a monadic-like operation over the marked/unmarked distinction.
    /// </summary>
    /// <param name="form">The form to check.</param>
    /// <param name="onMarked">Function to apply if marked.</param>
    /// <returns>The result of the function if marked, otherwise the original form.</returns>
    public static Form Match(this Form form, Func<Form> onMarked)
    {
        return form.IsMarked() ? onMarked() : form;
    }

    /// <summary>
    /// Pattern matches on a form's evaluated state.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="form">The form to match.</param>
    /// <param name="onMarked">Function to execute if the form is marked.</param>
    /// <param name="onVoid">Function to execute if the form is void.</param>
    /// <returns>The result of the appropriate function.</returns>
    public static T Match<T>(this Form form, Func<T> onMarked, Func<T> onVoid)
    {
        return form.IsMarked() ? onMarked() : onVoid();
    }

    /// <summary>
    /// Pattern matches on a form's evaluated state with support for imaginary forms.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="form">The form to match.</param>
    /// <param name="onMarked">Function to execute if the form is marked.</param>
    /// <param name="onVoid">Function to execute if the form is void.</param>
    /// <param name="onImaginary">Function to execute if the form is imaginary.</param>
    /// <returns>The result of the appropriate function.</returns>
    public static T Match<T>(this Form form, Func<T> onMarked, Func<T> onVoid, Func<double, T> onImaginary)
    {
        var evaluated = form.Eval();

        if (evaluated is Form.ImaginaryForm imag)
        {
            return onImaginary(imag.Phase);
        }

        return form.IsMarked() ? onMarked() : onVoid();
    }

    /// <summary>
    /// Applies a transformation to the inner content of a marked form.
    /// This is analogous to a functor map operation.
    /// </summary>
    /// <param name="form">The form to map over.</param>
    /// <param name="transform">The transformation to apply to the inner form.</param>
    /// <returns>A new form with the transformation applied.</returns>
    public static Form Map(this Form form, Func<Form, Form> transform)
    {
        var evaluated = form.Eval();
        return evaluated.IsMarked() ? Form.Mark(transform(evaluated)) : evaluated;
    }

    /// <summary>
    /// Wraps a value in a Result based on a form's state.
    /// If the form is marked, returns Success with the value.
    /// If the form is void, returns Failure with the error.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="form">The form to check.</param>
    /// <param name="value">The value to wrap on success.</param>
    /// <param name="error">The error message on failure.</param>
    /// <returns>A Result based on the form's state.</returns>
    public static Result<T> ToResult<T>(this Form form, T value, string error)
    {
        return form.IsMarked()
            ? Result<T>.Success(value)
            : Result<T>.Failure(error);
    }

    /// <summary>
    /// Wraps a value in an Option based on a form's state.
    /// If the form is marked, returns Some with the value.
    /// If the form is void, returns None.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="form">The form to check.</param>
    /// <param name="value">The value to wrap if marked.</param>
    /// <returns>An Option based on the form's state.</returns>
    public static Option<T> ToOption<T>(this Form form, T value)
    {
        return form.IsMarked() ? Option<T>.Some(value) : Option<T>.None();
    }

    /// <summary>
    /// Creates a form from an Option - marked if Some, void if None.
    /// </summary>
    /// <typeparam name="T">The Option value type.</typeparam>
    /// <param name="option">The option to convert.</param>
    /// <returns>Mark if Some, Void if None.</returns>
    public static Form FromOption<T>(Option<T> option)
    {
        return option.HasValue ? Form.Cross() : Form.Void;
    }

    /// <summary>
    /// Creates a form from a Result - marked if Success, void if Failure.
    /// </summary>
    /// <typeparam name="T">The Result value type.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>Mark if Success, Void if Failure.</returns>
    public static Form FromResult<T>(Result<T> result)
    {
        return result.IsSuccess ? Form.Cross() : Form.Void;
    }

    /// <summary>
    /// Computes the logical NOT of a form using Spencer-Brown's crossing.
    /// Not(x) = Mark(x).
    /// </summary>
    /// <param name="form">The form to negate.</param>
    /// <returns>The negation of the form.</returns>
    public static Form Not(this Form form) => Form.Mark(form);

    /// <summary>
    /// Computes the logical OR of two forms using Spencer-Brown's indication.
    /// Or(a, b) = a.Call(b) which simplifies by the Law of Calling.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The disjunction of the forms.</returns>
    public static Form Or(this Form left, Form right) => left.Call(right);

    /// <summary>
    /// Computes the logical AND of two forms.
    /// And(a, b) = Mark(Mark(a).Call(Mark(b))) by De Morgan's law.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The conjunction of the forms.</returns>
    public static Form And(this Form left, Form right) =>
        Form.Mark(Form.Mark(left).Call(Form.Mark(right)));

    /// <summary>
    /// Computes the logical implication: a implies b.
    /// Implies(a, b) = Or(Not(a), b) = Mark(a).Call(b).
    /// </summary>
    /// <param name="antecedent">The antecedent form (a).</param>
    /// <param name="consequent">The consequent form (b).</param>
    /// <returns>The implication form.</returns>
    public static Form Implies(this Form antecedent, Form consequent) =>
        Form.Mark(antecedent).Call(consequent);

    /// <summary>
    /// Computes logical equivalence (biconditional): a iff b.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The equivalence form.</returns>
    public static Form Iff(this Form left, Form right) =>
        left.Implies(right).And(right.Implies(left));

    /// <summary>
    /// Imagines (applies imagination operator to) a form.
    /// This transforms the form by one cycle in the imaginary domain.
    /// </summary>
    /// <param name="form">The form to imagine.</param>
    /// <returns>The imagined form.</returns>
    public static Form Imagine(this Form form) => Imagination.Apply(form);

    /// <summary>
    /// Conjugates an imaginary form (negates its phase).
    /// Real forms are unchanged.
    /// </summary>
    /// <param name="form">The form to conjugate.</param>
    /// <returns>The conjugated form.</returns>
    public static Form Conjugate(this Form form) => Imagination.Conjugate(form);

    /// <summary>
    /// Gets the phase of a form in radians.
    /// </summary>
    /// <param name="form">The form to get the phase of.</param>
    /// <returns>The phase in radians.</returns>
    public static double GetPhase(this Form form) => Imagination.Phase(form);

    /// <summary>
    /// Gets the magnitude of a form.
    /// </summary>
    /// <param name="form">The form to measure.</param>
    /// <returns>The magnitude (0, 0.5, or 1).</returns>
    public static double GetMagnitude(this Form form) => Imagination.Magnitude(form);

    /// <summary>
    /// Projects an imaginary form to the real axis (Void/Mark).
    /// </summary>
    /// <param name="form">The form to project.</param>
    /// <returns>The projected form.</returns>
    public static Form Project(this Form form) => Imagination.Project(form);

    /// <summary>
    /// Samples a form at a specific discrete time.
    /// For imaginary forms, this returns the apparent value at that time.
    /// </summary>
    /// <param name="form">The form to sample.</param>
    /// <param name="time">The time step.</param>
    /// <returns>The sampled form.</returns>
    public static Form AtTime(this Form form, int time) => Imagination.Sample(form, time);

    /// <summary>
    /// Creates a self-referential form (re-entry) from this form.
    /// The result satisfies f = ⌐f, yielding an imaginary value.
    /// </summary>
    /// <param name="form">The form to make self-referential.</param>
    /// <param name="name">Optional name for the self-reference.</param>
    /// <returns>A re-entry form.</returns>
    public static Form WithReEntry(this Form form, string? name = null)
    {
        // Create a re-entry that when evaluated considers this form
        _ = form; // The form provides context but re-entry is always imaginary
        return Form.ReEntry(name);
    }

    /// <summary>
    /// Creates an oscillator between this form and another.
    /// </summary>
    /// <param name="form">The first state.</param>
    /// <param name="other">The second state.</param>
    /// <returns>An oscillator alternating between the two forms.</returns>
    public static Oscillator OscillateWith(this Form form, Form other) =>
        Imagination.Oscillate(form, other);

    /// <summary>
    /// Superimposes this form with another.
    /// For imaginary forms, this creates interference patterns.
    /// </summary>
    /// <param name="form">The first form.</param>
    /// <param name="other">The second form.</param>
    /// <returns>The superimposed form.</returns>
    public static Form Superimpose(this Form form, Form other) =>
        Imagination.Superimpose(form, other);
}
