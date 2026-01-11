// <copyright file="LawsOfForm.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Ouroboros.Core.SpencerBrown;

/// <summary>
/// Implementation of George Spencer-Brown's Laws of Form in a monadic context.
/// The calculus of indications provides a foundational algebra for distinction and self-reference.
/// </summary>
/// <remarks>
/// <para>The two fundamental axioms:</para>
/// <list type="bullet">
///   <item><description>Law of Calling: ⊢⊢ = ⊢ (The form of condensation - idempotence)</description></item>
///   <item><description>Law of Crossing: ⊢⊢ = ∅ (The form of cancellation - involution)</description></item>
/// </list>
/// <para>These map to monadic laws as follows:</para>
/// <list type="bullet">
///   <item><description>Mark (⊢) ≅ Unit/Return (η): entering the marked state</description></item>
///   <item><description>Cross ≅ Join/Flatten (μ): collapsing nested distinctions</description></item>
///   <item><description>The unmarked state ≅ Identity morphism</description></item>
/// </list>
/// </remarks>
public static class LawsOfForm
{
    /// <summary>
    /// The Mark (⊢) - Creates a distinction, entering the marked state.
    /// This is the fundamental act of indication in Spencer-Brown's calculus.
    /// Corresponds to monadic unit/return (η).
    /// </summary>
    /// <typeparam name="T">The type being marked.</typeparam>
    /// <param name="value">The value to mark.</param>
    /// <returns>A marked form containing the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Form<T> Mark<T>(T value) => Form<T>.Mark(value);

    /// <summary>
    /// The Void (∅) - The unmarked state, representing undifferentiated potential.
    /// </summary>
    /// <typeparam name="T">The phantom type.</typeparam>
    /// <returns>An unmarked/void form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Form<T> Void<T>() => Form<T>.Void();

    /// <summary>
    /// Cross (⊢→) - Crosses the boundary of a form.
    /// Entering a marked space from the unmarked, or vice versa.
    /// </summary>
    /// <typeparam name="T">The type being crossed.</typeparam>
    /// <param name="form">The form to cross.</param>
    /// <returns>The crossed form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Form<T> Cross<T>(Form<T> form) => form.Cross();

    /// <summary>
    /// Law of Calling (Condensation): ⊢⊢ = ⊢
    /// Calling from a marked state to a marked state remains marked.
    /// This expresses idempotence: marking what is already marked changes nothing.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="form">The form to condense.</param>
    /// <returns>The condensed form.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Form<T> Call<T>(Form<T> form) => form.Call();

    /// <summary>
    /// Law of Crossing (Cancellation): ⊢⊢ = ∅
    /// Two crossings return to the unmarked state.
    /// This expresses involution: distinction of distinction yields void.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="form">The form to apply double crossing.</param>
    /// <returns>The result after double crossing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Form<T> Recross<T>(Form<T> form) => form.Recross();

    /// <summary>
    /// Creates a Step that applies the Mark operation.
    /// </summary>
    /// <typeparam name="T">The type to mark.</typeparam>
    /// <returns>A step that marks its input.</returns>
    public static Step<T, Form<T>> MarkArrow<T>() =>
        input => Task.FromResult(Form<T>.Mark(input));

    /// <summary>
    /// Creates a Step that applies the Cross operation.
    /// </summary>
    /// <typeparam name="T">The type being crossed.</typeparam>
    /// <returns>A step that crosses its input form.</returns>
    public static Step<Form<T>, Form<T>> CrossArrow<T>() =>
        input => Task.FromResult(input.Cross());

    /// <summary>
    /// The Cross Product (×) - Combines two forms into a product form.
    /// In category theory, this is the categorical product in the category of forms.
    /// </summary>
    /// <typeparam name="T1">The first type.</typeparam>
    /// <typeparam name="T2">The second type.</typeparam>
    /// <param name="form1">The first form.</param>
    /// <param name="form2">The second form.</param>
    /// <returns>The product form.</returns>
    public static Form<(T1, T2)> Product<T1, T2>(Form<T1> form1, Form<T2> form2)
    {
        return (form1.IsMarked, form2.IsMarked) switch
        {
            (true, true) => Form<(T1, T2)>.Mark((form1.Value!, form2.Value!)),
            _ => Form<(T1, T2)>.Void()
        };
    }

    /// <summary>
    /// Creates a parallel cross product arrow that runs two steps and combines results.
    /// This is the bifunctor action in the category of forms.
    /// </summary>
    /// <typeparam name="TIn">The shared input type.</typeparam>
    /// <typeparam name="T1">The first output type.</typeparam>
    /// <typeparam name="T2">The second output type.</typeparam>
    /// <param name="step1">The first step.</param>
    /// <param name="step2">The second step.</param>
    /// <returns>A step producing the product of both results.</returns>
    public static Step<TIn, Form<(T1, T2)>> CrossProduct<TIn, T1, T2>(
        Step<TIn, Form<T1>> step1,
        Step<TIn, Form<T2>> step2) =>
        async input =>
        {
            var task1 = step1(input);
            var task2 = step2(input);
            await Task.WhenAll(task1, task2).ConfigureAwait(false);
            return Product(await task1, await task2);
        };

    /// <summary>
    /// Re-entry operator (⟲) - Creates self-referential forms.
    /// This models the imaginary value that emerges from self-distinction.
    /// f = ⊢f (the form that equals its own mark)
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="generator">Function that generates from the re-entrant value.</param>
    /// <returns>A form representing the fixed point.</returns>
    public static Form<T> ReEntry<T>(Func<Form<T>, Form<T>> generator)
    {
        // The imaginary state: neither marked nor unmarked, but oscillating
        // We approximate with a lazy thunk that represents the self-reference
        Form<T> seed = Form<T>.Void();
        return generator(seed);
    }

    /// <summary>
    /// Creates a re-entrant step that feeds its output back as input.
    /// Models the temporal dimension of Spencer-Brown's calculus.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="iterations">Number of re-entry cycles.</param>
    /// <param name="step">The step to iterate.</param>
    /// <returns>A step that performs re-entry iteration.</returns>
    public static Step<Form<T>, Form<T>> ReEntryArrow<T>(int iterations, Step<Form<T>, Form<T>> step)
    {
        return async input =>
        {
            Form<T> current = input;
            for (int i = 0; i < iterations; i++)
            {
                current = await step(current).ConfigureAwait(false);
            }
            return current;
        };
    }
}

/// <summary>
/// Represents a Form (distinction) in Spencer-Brown's calculus.
/// A form is either marked (containing a value) or unmarked (void).
/// </summary>
/// <typeparam name="T">The type of the indicated value.</typeparam>
public readonly struct Form<T> : IEquatable<Form<T>>
{
    private readonly T? _value;
    private readonly bool _isMarked;
    private readonly int _depth; // Tracks nesting depth for crossing

    private Form(T? value, bool isMarked, int depth = 0)
    {
        _value = value;
        _isMarked = isMarked;
        _depth = depth;
    }

    /// <summary>
    /// Gets a value indicating whether this form is in the marked state.
    /// </summary>
    public bool IsMarked => _isMarked;

    /// <summary>
    /// Gets a value indicating whether this form is in the unmarked (void) state.
    /// </summary>
    public bool IsVoid => !_isMarked;

    /// <summary>
    /// Gets the indicated value if marked; otherwise default.
    /// </summary>
    public T? Value => _isMarked ? _value : default;

    /// <summary>
    /// Gets the nesting depth of distinctions.
    /// </summary>
    public int Depth => _depth;

    /// <summary>
    /// Creates a marked form (⊢) containing the value.
    /// </summary>
    /// <param name="value">The value to indicate.</param>
    /// <returns>A marked form.</returns>
    public static Form<T> Mark(T value) => new(value, true, 1);

    /// <summary>
    /// Creates an unmarked/void form (∅).
    /// </summary>
    /// <returns>An unmarked form.</returns>
    public static Form<T> Void() => new(default, false, 0);

    /// <summary>
    /// Crosses the boundary of this form.
    /// Entering increments depth; crossing at depth 0 marks.
    /// </summary>
    /// <returns>The crossed form.</returns>
    public Form<T> Cross()
    {
        if (_isMarked)
        {
            // Crossing out of a marked state
            return new Form<T>(_value, true, _depth + 1);
        }
        else
        {
            // Crossing into void creates a mark
            return new Form<T>(default, true, 1);
        }
    }

    /// <summary>
    /// Law of Calling: ⊢⊢ = ⊢
    /// Condenses nested marks into a single mark.
    /// </summary>
    /// <returns>The condensed form.</returns>
    public Form<T> Call()
    {
        // Idempotence: marked remains marked at depth 1
        if (_isMarked)
        {
            return new Form<T>(_value, true, 1);
        }
        return this;
    }

    /// <summary>
    /// Law of Crossing: ⊢⊢ = ∅
    /// Double crossing returns to the unmarked state.
    /// </summary>
    /// <returns>The result of double crossing.</returns>
    public Form<T> Recross()
    {
        // Two crossings cancel out
        if (_depth >= 2)
        {
            return new Form<T>(_value, _isMarked, _depth - 2);
        }
        else if (_depth == 1)
        {
            return Void();
        }
        return this;
    }

    /// <summary>
    /// Monadic bind for forms. If marked, applies the function; otherwise returns void.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="func">The binding function.</param>
    /// <returns>The bound result.</returns>
    public Form<TResult> Bind<TResult>(Func<T, Form<TResult>> func)
    {
        if (_isMarked && _value is not null)
        {
            return func(_value);
        }
        return Form<TResult>.Void();
    }

    /// <summary>
    /// Functor map for forms.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="func">The mapping function.</param>
    /// <returns>The mapped form.</returns>
    public Form<TResult> Map<TResult>(Func<T, TResult> func)
    {
        if (_isMarked && _value is not null)
        {
            return Form<TResult>.Mark(func(_value));
        }
        return Form<TResult>.Void();
    }

    /// <summary>
    /// Match operation for forms (catamorphism).
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="whenMarked">Handler for marked state.</param>
    /// <param name="whenVoid">Handler for void state.</param>
    /// <returns>The matched result.</returns>
    public TResult Match<TResult>(Func<T, TResult> whenMarked, Func<TResult> whenVoid)
    {
        if (_isMarked && _value is not null)
        {
            return whenMarked(_value);
        }
        return whenVoid();
    }

    /// <summary>
    /// Extracts value or returns default.
    /// </summary>
    /// <param name="defaultValue">The default if void.</param>
    /// <returns>The value or default.</returns>
    public T GetValueOrDefault(T defaultValue) =>
        _isMarked && _value is not null ? _value : defaultValue;

    /// <summary>
    /// Mark composition operator (⊢).
    /// </summary>
    public static Form<T> operator !(Form<T> form) => form.Cross();

    /// <summary>
    /// Equality comparison.
    /// </summary>
    public bool Equals(Form<T> other) =>
        _isMarked == other._isMarked &&
        _depth == other._depth &&
        EqualityComparer<T?>.Default.Equals(_value, other._value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Form<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_isMarked, _depth, _value);

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Form<T> left, Form<T> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Form<T> left, Form<T> right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
    {
        if (!_isMarked) return "∅";
        string marks = new string('⊢', _depth);
        return $"{marks}[{_value}]";
    }
}

/// <summary>
/// Extension methods for Form operations and Step composition.
/// </summary>
public static class FormExtensions
{
    /// <summary>
    /// Lifts a value into a marked form.
    /// </summary>
    public static Form<T> ToForm<T>(this T value) => Form<T>.Mark(value);

    /// <summary>
    /// Lifts an Option into a Form (isomorphism).
    /// </summary>
    public static Form<T> ToForm<T>(this Option<T> option) =>
        option.HasValue ? Form<T>.Mark(option.GetValueOrDefault(default!)) : Form<T>.Void();

    /// <summary>
    /// Converts a Form back to Option.
    /// </summary>
    public static Option<T> ToOption<T>(this Form<T> form) =>
        form.IsMarked && form.Value is not null ? Option<T>.Some(form.Value) : Option<T>.None();

    /// <summary>
    /// Creates a Step that marks successful results.
    /// </summary>
    public static Step<TIn, Form<TOut>> MarkStep<TIn, TOut>(this Step<TIn, TOut> step) =>
        async input =>
        {
            var result = await step(input).ConfigureAwait(false);
            return Form<TOut>.Mark(result);
        };

    /// <summary>
    /// Composes two form-producing steps with the cross product.
    /// </summary>
    public static Step<TIn, Form<(T1, T2)>> CrossWith<TIn, T1, T2>(
        this Step<TIn, Form<T1>> step1,
        Step<TIn, Form<T2>> step2) =>
        LawsOfForm.CrossProduct(step1, step2);

    /// <summary>
    /// Applies the Law of Calling to a form-producing step.
    /// </summary>
    public static Step<TIn, Form<TOut>> WithCalling<TIn, TOut>(this Step<TIn, Form<TOut>> step) =>
        async input =>
        {
            var result = await step(input).ConfigureAwait(false);
            return result.Call();
        };

    /// <summary>
    /// Applies the Law of Crossing to a form-producing step.
    /// </summary>
    public static Step<TIn, Form<TOut>> WithCrossing<TIn, TOut>(this Step<TIn, Form<TOut>> step) =>
        async input =>
        {
            var result = await step(input).ConfigureAwait(false);
            return result.Cross();
        };

    /// <summary>
    /// Parallel tuple awaiter for async cross product.
    /// </summary>
    public static TaskAwaiter<(T1, T2)> GetAwaiter<T1, T2>(this (Task<T1>, Task<T2>) tasks)
    {
        return WaitBoth(tasks).GetAwaiter();

        static async Task<(T1, T2)> WaitBoth((Task<T1>, Task<T2>) t)
        {
            await Task.WhenAll(t.Item1, t.Item2).ConfigureAwait(false);
            return (t.Item1.Result, t.Item2.Result);
        }
    }
}
