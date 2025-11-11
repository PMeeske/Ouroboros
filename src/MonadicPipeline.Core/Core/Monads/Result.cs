// <copyright file="Result.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Monads;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// Implements the Result monad for robust error handling.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public readonly struct Result<TValue, TError>
{
    private readonly TValue? value;
    private readonly TError? error;
    private readonly bool isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents success.
    /// </summary>
    public bool IsSuccess => this.isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents failure.
    /// </summary>
    public bool IsFailure => !this.isSuccess;

    /// <summary>
    /// Gets the success value (only valid when IsSuccess is true).
    /// </summary>
    public TValue Value => this.isSuccess ? this.value! : throw new InvalidOperationException("Cannot access Value of a failed Result");

    /// <summary>
    /// Gets the error value (only valid when IsFailure is true).
    /// </summary>
    public TError Error => !this.isSuccess ? this.error! : throw new InvalidOperationException("Cannot access Error of a successful Result");

    private Result(TValue value)
    {
        this.value = value;
        this.error = default;
        this.isSuccess = true;
    }

    private Result(TError error)
    {
        this.value = default;
        this.error = error;
        this.isSuccess = false;
    }

    /// <summary>
    /// Creates a successful Result with the given value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful Result.</returns>
    public static Result<TValue, TError> Success(TValue value) => new(value);

    /// <summary>
    /// Creates a failed Result with the given error.
    /// </summary>
    /// <param name="error">The error value.</param>
    /// <returns>A failed Result.</returns>
    public static Result<TValue, TError> Failure(TError error) => new(error);

    /// <summary>
    /// Monadic bind operation. Applies a function that returns a Result to the wrapped value.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="func">Function to apply if this Result is successful.</param>
    /// <returns>The result of the function, or the original error if this Result failed.</returns>
    public Result<TResult, TError> Bind<TResult>(Func<TValue, Result<TResult, TError>> func)
    {
        return this.IsSuccess ? func(this.value!) : Result<TResult, TError>.Failure(this.error!);
    }

    /// <summary>
    /// Functor map operation. Transforms the wrapped value if the Result is successful.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="func">Function to apply to the wrapped value.</param>
    /// <returns>A Result containing the transformed value, or the original error.</returns>
    public Result<TResult, TError> Map<TResult>(Func<TValue, TResult> func)
    {
        return this.IsSuccess ? Result<TResult, TError>.Success(func(this.value!)) : Result<TResult, TError>.Failure(this.error!);
    }

    /// <summary>
    /// Transforms the error type while preserving the success value.
    /// </summary>
    /// <typeparam name="TNewError">The new error type.</typeparam>
    /// <param name="func">Function to transform the error.</param>
    /// <returns>A Result with the transformed error type.</returns>
    public Result<TValue, TNewError> MapError<TNewError>(Func<TError, TNewError> func)
    {
        return this.IsSuccess ? Result<TValue, TNewError>.Success(this.value!) : Result<TValue, TNewError>.Failure(func(this.error!));
    }

    /// <summary>
    /// Executes one of two functions based on whether the Result is successful or failed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="onSuccess">Function to execute if Result is successful.</param>
    /// <param name="onFailure">Function to execute if Result failed.</param>
    /// <returns>The result of the appropriate function.</returns>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        return this.IsSuccess ? onSuccess(this.value!) : onFailure(this.error!);
    }

    /// <summary>
    /// Executes one of two actions based on whether the Result is successful or failed.
    /// </summary>
    /// <param name="onSuccess">Action to execute if Result is successful.</param>
    /// <param name="onFailure">Action to execute if Result failed.</param>
    public void Match(Action<TValue> onSuccess, Action<TError> onFailure)
    {
        if (this.IsSuccess)
        {
            onSuccess(this.value!);
        }
        else
        {
            onFailure(this.error!);
        }
    }

    /// <summary>
    /// Returns the success value or a default value if the Result failed.
    /// </summary>
    /// <param name="defaultValue">The default value to return on failure.</param>
    /// <returns>The success value or the default value.</returns>
    public TValue GetValueOrDefault(TValue defaultValue)
    {
        return this.IsSuccess ? this.value! : defaultValue;
    }

    /// <summary>
    /// Converts a Result to an Option, discarding error information.
    /// </summary>
    /// <returns>Some(value) if successful, None if failed.</returns>
    public Option<TValue> ToOption()
    {
        return this.IsSuccess ? Option<TValue>.Some(this.value!) : Option<TValue>.None();
    }

    /// <summary>
    /// Implicit conversion from success value to Result.
    /// </summary>
    public static implicit operator Result<TValue, TError>(TValue value) => Success(value);

    /// <summary>
    /// Returns a string representation of the Result.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return this.IsSuccess ? $"Success({this.value})" : $"Failure({this.error})";
    }

    /// <summary>
    /// Determines equality between two Results.
    /// </summary>
    /// <returns></returns>
    public bool Equals(Result<TValue, TError> other)
    {
        if (this.IsSuccess != other.IsSuccess)
        {
            return false;
        }

        return this.IsSuccess
            ? EqualityComparer<TValue>.Default.Equals(this.value, other.value)
            : EqualityComparer<TError>.Default.Equals(this.error, other.error);
    }

    /// <summary>
    /// Determines equality with an object.
    /// </summary>
    /// <returns></returns>
    public override bool Equals(object? obj)
    {
        return obj is Result<TValue, TError> other && this.Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this Result.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return this.IsSuccess
            ? HashCode.Combine(this.isSuccess, this.value)
            : HashCode.Combine(this.isSuccess, this.error);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Result<TValue, TError> left, Result<TValue, TError> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Result<TValue, TError> left, Result<TValue, TError> right) => !left.Equals(right);
}

/// <summary>
/// Convenience type for Results with string errors.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
public readonly struct Result<TValue> : IEquatable<Result<TValue>>
{
    private readonly Result<TValue, string> inner;

    private Result(Result<TValue, string> inner) => this.inner = inner;

    /// <summary>
    /// Gets a value indicating whether this result represents success.
    /// </summary>
    public bool IsSuccess => this.inner.IsSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents failure.
    /// </summary>
    public bool IsFailure => this.inner.IsFailure;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    public TValue Value => this.inner.Value;

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Error => this.inner.Error;

    /// <summary>
    /// Creates a successful Result.
    /// </summary>
    /// <returns></returns>
    public static Result<TValue> Success(TValue value) => new(Result<TValue, string>.Success(value));

    /// <summary>
    /// Creates a failed Result.
    /// </summary>
    /// <returns></returns>
    public static Result<TValue> Failure(string error) => new(Result<TValue, string>.Failure(error));

    /// <summary>
    /// Monadic bind operation.
    /// </summary>
    /// <returns></returns>
    public Result<TResult> Bind<TResult>(Func<TValue, Result<TResult>> func)
    {
        return new(this.inner.Bind(v => func(v).inner));
    }

    /// <summary>
    /// Functor map operation.
    /// </summary>
    /// <returns></returns>
    public Result<TResult> Map<TResult>(Func<TValue, TResult> func)
    {
        return new(this.inner.Map(func));
    }

    /// <summary>
    /// Pattern matching.
    /// </summary>
    /// <returns></returns>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<string, TResult> onFailure)
    {
        return this.inner.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// Pattern matching with actions.
    /// </summary>
    public void Match(Action<TValue> onSuccess, Action<string> onFailure)
    {
        this.inner.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// Gets value or default.
    /// </summary>
    /// <returns></returns>
    public TValue GetValueOrDefault(TValue defaultValue) => this.inner.GetValueOrDefault(defaultValue);

    /// <summary>
    /// Converts to Option.
    /// </summary>
    /// <returns></returns>
    public Option<TValue> ToOption() => this.inner.ToOption();

    /// <summary>
    /// Implicit conversion from value.
    /// </summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>
    /// String representation.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => this.inner.ToString();

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <returns></returns>
    public bool Equals(Result<TValue> other) => this.inner.Equals(other.inner);

    /// <summary>
    /// Object equality.
    /// </summary>
    /// <returns></returns>
    public override bool Equals(object? obj) => obj is Result<TValue> other && this.Equals(other);

    /// <summary>
    /// Hash code.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => this.inner.GetHashCode();

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Result<TValue> left, Result<TValue> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Result<TValue> left, Result<TValue> right) => !left.Equals(right);
}
