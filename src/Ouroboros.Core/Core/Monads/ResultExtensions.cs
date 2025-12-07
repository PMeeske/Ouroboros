// <copyright file="ResultExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace LangChainPipeline.Core.Monads;

/// <summary>
/// Additional extension methods for Result monads to support advanced functional programming patterns.
/// These extensions provide utilities for working with Result types in monadic pipelines.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Applies a function to the value inside a Result if it's successful, otherwise returns the original error.
    /// This is an alias for Map for better readability in some contexts.
    /// </summary>
    /// <typeparam name="TValue">The type of the original value.</typeparam>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the error.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="func">The transformation function.</param>
    /// <returns>A Result containing the transformed value or the original error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> Select<TValue, TResult, TError>(
        this Result<TValue, TError> result,
        Func<TValue, TResult> func)
        => result.Map(func);

    /// <summary>
    /// LINQ-style SelectMany for Result monad (monadic bind operation).
    /// Enables query syntax and nested Result operations.
    /// </summary>
    /// <typeparam name="TValue">The type of the original value.</typeparam>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <typeparam name="TError">The type of the error.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="func">The binding function.</param>
    /// <returns>The result of the binding function or the original error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> SelectMany<TValue, TResult, TError>(
        this Result<TValue, TError> result,
        Func<TValue, Result<TResult, TError>> func)
        => result.Bind(func);

    /// <summary>
    /// LINQ-style SelectMany with result selector for Result monad.
    /// Enables complex query expressions with Results.
    /// </summary>
    /// <typeparam name="TValue">The type of the original value.</typeparam>
    /// <typeparam name="TIntermediate">The type of the intermediate value.</typeparam>
    /// <typeparam name="TResult">The type of the final result.</typeparam>
    /// <typeparam name="TError">The type of the error.</typeparam>
    /// <param name="result">The original result.</param>
    /// <param name="selector">Function to select the intermediate result.</param>
    /// <param name="resultSelector">Function to combine the original and intermediate values.</param>
    /// <returns>The combined result or the first error encountered.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TResult, TError> SelectMany<TValue, TIntermediate, TResult, TError>(
        this Result<TValue, TError> result,
        Func<TValue, Result<TIntermediate, TError>> selector,
        Func<TValue, TIntermediate, TResult> resultSelector)
        => result.Bind(value => selector(value).Map(intermediate => resultSelector(value, intermediate)));

    /// <summary>
    /// Combines two Results into a single Result containing a tuple.
    /// Both Results must be successful for the operation to succeed.
    /// </summary>
    /// <typeparam name="T1">Type of the first Result value.</typeparam>
    /// <typeparam name="T2">Type of the second Result value.</typeparam>
    /// <typeparam name="TError">Type of the error.</typeparam>
    /// <param name="first">The first Result.</param>
    /// <param name="second">The second Result.</param>
    /// <returns>A Result containing a tuple of both values, or the first error encountered.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<(T1, T2), TError> Combine<T1, T2, TError>(
        this Result<T1, TError> first,
        Result<T2, TError> second)
        => first.Bind(firstValue => second.Map(secondValue => (firstValue, secondValue)));

    /// <summary>
    /// Combines three Results into a single Result containing a tuple.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<(T1, T2, T3), TError> Combine<T1, T2, T3, TError>(
        this Result<T1, TError> first,
        Result<T2, TError> second,
        Result<T3, TError> third)
        => first.Combine(second).Bind(tuple => third.Map(thirdValue => (tuple.Item1, tuple.Item2, thirdValue)));

    /// <summary>
    /// Filters a Result based on a predicate.
    /// If the Result is successful but the predicate fails, returns a failure.
    /// </summary>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <typeparam name="TError">Type of the error.</typeparam>
    /// <param name="result">The Result to filter.</param>
    /// <param name="predicate">The predicate to test.</param>
    /// <param name="errorOnFalse">The error to return if predicate fails.</param>
    /// <returns>The original Result if predicate passes, otherwise a failure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue, TError> Where<TValue, TError>(
        this Result<TValue, TError> result,
        Func<TValue, bool> predicate,
        TError errorOnFalse)
        => result.Bind(value => predicate(value)
            ? Result<TValue, TError>.Success(value)
            : Result<TValue, TError>.Failure(errorOnFalse));

    /// <summary>
    /// Executes a side effect if the Result is successful, without modifying the Result.
    /// </summary>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <typeparam name="TError">Type of the error.</typeparam>
    /// <param name="result">The Result.</param>
    /// <param name="action">The side effect to execute.</param>
    /// <returns>The original Result unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue, TError> Tap<TValue, TError>(
        this Result<TValue, TError> result,
        Action<TValue> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }

    /// <summary>
    /// Executes a side effect if the Result is a failure, without modifying the Result.
    /// </summary>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <typeparam name="TError">Type of the error.</typeparam>
    /// <param name="result">The Result.</param>
    /// <param name="action">The side effect to execute on error.</param>
    /// <returns>The original Result unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue, TError> TapError<TValue, TError>(
        this Result<TValue, TError> result,
        Action<TError> action)
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }

    /// <summary>
    /// Provides a fallback value if the Result is a failure.
    /// </summary>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <typeparam name="TError">Type of the error.</typeparam>
    /// <param name="result">The Result.</param>
    /// <param name="fallback">The fallback value.</param>
    /// <returns>The successful value or the fallback.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue OrElse<TValue, TError>(
        this Result<TValue, TError> result,
        TValue fallback)
        => result.IsSuccess ? result.Value : fallback;

    /// <summary>
    /// Provides a fallback value computed from the error if the Result is a failure.
    /// </summary>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <typeparam name="TError">Type of the error.</typeparam>
    /// <param name="result">The Result.</param>
    /// <param name="fallbackFunc">Function to compute fallback from error.</param>
    /// <returns>The successful value or the computed fallback.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue OrElse<TValue, TError>(
        this Result<TValue, TError> result,
        Func<TError, TValue> fallbackFunc)
        => result.IsSuccess ? result.Value : fallbackFunc(result.Error);

    /// <summary>
    /// Converts a Result with Exception error to one with string error.
    /// </summary>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    /// <param name="result">The Result with Exception error.</param>
    /// <returns>A Result with string error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> ToStringError<TValue>(
        this Result<TValue, Exception> result)
        => result.IsSuccess
            ? Result<TValue>.Success(result.Value)
            : Result<TValue>.Failure(result.Error.Message);

    /// <summary>
    /// Applies a sequence of transformations to a Result, short-circuiting on first failure.
    /// </summary>
    /// <typeparam name="T">The type flowing through the transformations.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="result">The initial Result.</param>
    /// <param name="transformations">The sequence of transformations.</param>
    /// <returns>The final Result after all transformations, or the first error.</returns>
    public static Result<T, TError> Pipe<T, TError>(
        this Result<T, TError> result,
        params Func<T, Result<T, TError>>[] transformations)
    {
        Result<T, TError> current = result;
        foreach (Func<T, Result<T, TError>> transform in transformations)
        {
            if (current.IsFailure)
            {
                break;
            }

            current = current.Bind(transform);
        }

        return current;
    }
}
