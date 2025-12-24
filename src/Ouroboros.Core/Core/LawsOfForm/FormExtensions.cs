// <copyright file="FormExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Extension methods for working with Forms and converting to/from other types.
/// </summary>
public static class FormExtensions
{
    /// <summary>
    /// Converts a boolean to a Form.
    /// True -> Mark, False -> Void.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>Mark if true, Void if false.</returns>
    public static Form ToForm(this bool value)
    {
        return value ? Form.Cross() : Form.Void;
    }

    /// <summary>
    /// Converts a confidence score to a Form based on thresholds.
    /// High confidence (&gt;= highThreshold) -&gt; Mark
    /// Low confidence (&lt;= lowThreshold) -&gt; Void
    /// Uncertain (between thresholds) -&gt; Imaginary
    /// </summary>
    /// <param name="confidence">The confidence score (0.0 to 1.0).</param>
    /// <param name="highThreshold">Threshold for Mark state (default 0.8).</param>
    /// <param name="lowThreshold">Threshold for Void state (default 0.3).</param>
    /// <returns>A Form representing the confidence level.</returns>
    public static Form ToForm(this double confidence, double highThreshold = 0.8, double lowThreshold = 0.3)
    {
        if (confidence >= highThreshold)
        {
            return Form.Cross();
        }

        if (confidence <= lowThreshold)
        {
            return Form.Void;
        }

        return Form.Imaginary;
    }

    /// <summary>
    /// Converts a nullable value to a Form.
    /// HasValue -> Mark, null -> Void.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The nullable value.</param>
    /// <returns>Mark if has value, Void if null.</returns>
    public static Form ToForm<T>(this T? value)
        where T : struct
    {
        return value.HasValue ? Form.Cross() : Form.Void;
    }

    /// <summary>
    /// Converts a reference type to a Form.
    /// Non-null -> Mark, null -> Void.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>Mark if non-null, Void if null.</returns>
    public static Form ToFormRef<T>(this T? value)
        where T : class
    {
        return value is not null ? Form.Cross() : Form.Void;
    }

    /// <summary>
    /// Combines multiple forms using conjunction (AND).
    /// All must be Mark for result to be Mark.
    /// Any Imaginary propagates Imaginary.
    /// Otherwise, result is Void.
    /// </summary>
    /// <param name="forms">The forms to combine.</param>
    /// <returns>The conjunction of all forms.</returns>
    public static Form All(params Form[] forms)
    {
        if (forms.Length == 0)
        {
            return Form.Cross();
        }

        var result = forms[0];
        for (int i = 1; i < forms.Length; i++)
        {
            result = result.And(forms[i]);
        }

        return result;
    }

    /// <summary>
    /// Combines multiple forms using disjunction (OR).
    /// Any Mark makes result Mark.
    /// Any Imaginary (without Mark) propagates Imaginary.
    /// Otherwise, result is Void.
    /// </summary>
    /// <param name="forms">The forms to combine.</param>
    /// <returns>The disjunction of all forms.</returns>
    public static Form Any(params Form[] forms)
    {
        if (forms.Length == 0)
        {
            return Form.Void;
        }

        var result = forms[0];
        for (int i = 1; i < forms.Length; i++)
        {
            result = result.Or(forms[i]);
        }

        return result;
    }

    /// <summary>
    /// Superposition of forms with weights for combining multiple opinions.
    /// If all opinions are the same, returns that opinion.
    /// If there's disagreement:
    /// - Any Imaginary -> Imaginary
    /// - Mixed Mark/Void -> weighted decision or Imaginary if close
    /// </summary>
    /// <param name="opinions">Weighted opinions as (form, weight) tuples.</param>
    /// <returns>The superposed form.</returns>
    public static Form Superposition(params (Form opinion, double weight)[] opinions)
    {
        if (opinions.Length == 0)
        {
            return Form.Void;
        }

        double totalWeight = 0;
        double markWeight = 0;
        double voidWeight = 0;
        bool hasImaginary = false;

        foreach (var (opinion, weight) in opinions)
        {
            totalWeight += weight;

            if (opinion.IsImaginary())
            {
                hasImaginary = true;
            }
            else if (opinion.IsMark())
            {
                markWeight += weight;
            }
            else
            {
                voidWeight += weight;
            }
        }

        // Any imaginary opinion propagates uncertainty
        if (hasImaginary)
        {
            return Form.Imaginary;
        }

        if (totalWeight == 0)
        {
            return Form.Void;
        }

        double markRatio = markWeight / totalWeight;
        double voidRatio = voidWeight / totalWeight;

        // Clear consensus
        if (markRatio >= 0.7)
        {
            return Form.Cross();
        }

        if (voidRatio >= 0.7)
        {
            return Form.Void;
        }

        // Mixed opinions without clear consensus -> Imaginary
        return Form.Imaginary;
    }

    /// <summary>
    /// Maps a Form to a Result type.
    /// Mark -> Success with provided value
    /// Void -> Failure with provided error
    /// Imaginary -> Failure with uncertainty message
    /// </summary>
    /// <typeparam name="TValue">The success value type.</typeparam>
    /// <param name="form">The form to map.</param>
    /// <param name="value">Value to use if Mark.</param>
    /// <param name="error">Error to use if Void.</param>
    /// <returns>A Result based on the form state.</returns>
    public static Result<TValue, string> ToResult<TValue>(this Form form, TValue value, string error)
    {
        return form.Match(
            onMark: () => Result<TValue, string>.Success(value),
            onVoid: () => Result<TValue, string>.Failure(error),
            onImaginary: () => Result<TValue, string>.Failure("Uncertain state - requires human review"));
    }

    /// <summary>
    /// Maps a Form to an Option type.
    /// Mark -> Some with provided value
    /// Void or Imaginary -> None
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="form">The form to map.</param>
    /// <param name="value">Value to use if Mark.</param>
    /// <returns>An Option based on the form state.</returns>
    public static Option<TValue> ToOption<TValue>(this Form form, TValue value)
    {
        return form.IsMark() ? Option<TValue>.Some(value) : Option<TValue>.None();
    }
}
