// <copyright file="Option.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Monads;

/// <summary>
/// Represents an optional value that may or may not contain data.
/// Implements the Option monad with proper monadic operations (bind, return, map).
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public readonly struct Option<T>
{
    private readonly T? value;
    private readonly bool hasValue;

    /// <summary>
    /// Gets the underlying value if present.
    /// </summary>
    public T? Value => this.value;

    /// <summary>
    /// Gets a value indicating whether this instance contains a value.
    /// </summary>
    public bool HasValue => this.hasValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="Option{T}"/> struct.
    /// </summary>
    /// <param name="value">The optional value.</param>
    public Option(T? value)
    {
        this.value = value;
        this.hasValue = value is not null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Option{T}"/> struct with explicit hasValue flag.
    /// Used internally for creating None instances.
    /// </summary>
    /// <param name="value">The optional value.</param>
    /// <param name="hasValue">Whether the option has a value.</param>
    private Option(T? value, bool hasValue)
    {
        this.value = value;
        this.hasValue = hasValue;
    }

    /// <summary>
    /// Creates an Option with a value (monadic return/pure operation).
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>An Option containing the value.</returns>
    public static Option<T> Some(T value) => new(value);

    /// <summary>
    /// Creates an empty Option (represents None/Nothing).
    /// </summary>
    /// <returns>An empty Option.</returns>
    public static Option<T> None() => default;

    /// <summary>
    /// Monadic bind operation. Applies a function that returns an Option to the wrapped value.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="func">Function to apply if value is present.</param>
    /// <returns>The result of the function, or None if this Option is empty.</returns>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> func)
    {
        return this.HasValue && this.value is not null ? func(this.value) : Option<TResult>.None();
    }

    /// <summary>
    /// Functor map operation. Transforms the wrapped value if present.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="func">Function to apply to the wrapped value.</param>
    /// <returns>An Option containing the transformed value, or None if this Option is empty.</returns>
    public Option<TResult> Map<TResult>(Func<T, TResult> func)
    {
        return this.HasValue && this.value is not null ? Option<TResult>.Some(func(this.value)) : Option<TResult>.None();
    }

    /// <summary>
    /// Applies a function if the value is present, otherwise returns the default value.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="func">Function to apply to the value.</param>
    /// <param name="defaultValue">Value to return if Option is empty.</param>
    /// <returns>The result of the function or the default value.</returns>
    public TResult Match<TResult>(Func<T, TResult> func, TResult defaultValue)
    {
        return this.HasValue && this.value is not null ? func(this.value) : defaultValue;
    }

    /// <summary>
    /// Executes one of two actions based on whether the Option has a value.
    /// </summary>
    /// <param name="onSome">Action to execute if value is present.</param>
    /// <param name="onNone">Action to execute if value is absent.</param>
    public void Match(Action<T> onSome, Action onNone)
    {
        if (this.HasValue && this.value is not null)
        {
            onSome(this.value);
        }
        else
        {
            onNone();
        }
    }

    /// <summary>
    /// Returns the wrapped value or the provided default value.
    /// </summary>
    /// <param name="defaultValue">The default value to return if Option is empty.</param>
    /// <returns>The wrapped value or the default value.</returns>
    public T GetValueOrDefault(T defaultValue)
    {
        return this.HasValue && this.value is not null ? this.value : defaultValue;
    }

    /// <summary>
    /// Implicit conversion from value to Option.
    /// </summary>
    public static implicit operator Option<T>(T value) => new(value);

    /// <summary>
    /// Returns a string representation of the Option.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return this.HasValue ? $"Some({this.value})" : "None";
    }

    /// <summary>
    /// Determines equality between two Options.
    /// </summary>
    /// <returns></returns>
    public bool Equals(Option<T> other)
    {
        if (!this.HasValue && !other.HasValue)
        {
            return true;
        }

        if (this.HasValue != other.HasValue)
        {
            return false;
        }

        return EqualityComparer<T>.Default.Equals(this.value, other.value);
    }

    /// <summary>
    /// Determines equality with an object.
    /// </summary>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        return obj is Option<T> other && this.Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this Option.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return this.HasValue ? EqualityComparer<T>.Default.GetHashCode(this.value!) : 0;
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);
}
