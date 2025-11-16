// <copyright file="Kleisli.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Kleisli;

/// <summary>
/// Kleisli arrow for computations in a monadic context.
/// Since Step{T,U} and Kleisli{T,U} are identical, this primarily serves as an alias for conceptual clarity.
/// A Kleisli arrow from A to B in monad M is a function A → M(B).
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
/// <param name="input">The input value.</param>
/// <returns>A Task representing the monadic computation.</returns>
public delegate Task<TOutput> Kleisli<in TInput, TOutput>(TInput input);

/// <summary>
/// Kleisli arrow for Result monad computations.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
/// <typeparam name="TError">The error type.</typeparam>
/// <param name="input">The input value.</param>
/// <returns>A Task containing a Result of the computation.</returns>
public delegate Task<Result<TOutput, TError>> KleisliResult<in TInput, TOutput, TError>(TInput input);

/// <summary>
/// Kleisli arrow for Option monad computations.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TOutput">The output type.</typeparam>
/// <param name="input">The input value.</param>
/// <returns>A Task containing an Option of the computation result.</returns>
public delegate Task<Option<TOutput>> KleisliOption<in TInput, TOutput>(TInput input);

/// <summary>
/// Represents Kleisli composition as a higher-order function.
/// Takes two Kleisli arrows and returns their composition.
/// This enables functional composition patterns and currying.
/// </summary>
/// <typeparam name="TIn">The input type of the first arrow.</typeparam>
/// <typeparam name="TMid">The intermediate type between arrows.</typeparam>
/// <typeparam name="TOut">The output type of the second arrow.</typeparam>
/// <param name="f">The first Kleisli arrow.</param>
/// <param name="g">The second Kleisli arrow.</param>
/// <returns>A composed Kleisli arrow.</returns>
public delegate Kleisli<TIn, TOut> KleisliCompose<TIn, TMid, TOut>(
    Kleisli<TIn, TMid> f,
    Kleisli<TMid, TOut> g);

/// <summary>
/// Unified factory methods for creating Kleisli arrows.
/// These work with both Step{T,U} and Kleisli{T,U} since they are conceptually identical.
/// </summary>
public static class Arrow
{
    /// <summary>
    /// Creates the identity arrow that returns input unchanged.
    /// This is the identity element for Kleisli composition.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>An arrow representing monadic identity/return.</returns>
    public static Step<T, T> Identity<T>() => input => Task.FromResult(input);

    /// <summary>
    /// Lifts a pure function into an arrow.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="func">The pure function to lift.</param>
    /// <returns>An arrow wrapping the function.</returns>
    public static Step<TInput, TOutput> Lift<TInput, TOutput>(Func<TInput, TOutput> func)
        => input => Task.FromResult(func(input));

    /// <summary>
    /// Lifts an async function into an arrow.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="func">The async function to lift.</param>
    /// <returns>An arrow wrapping the async function.</returns>
    public static Step<TInput, TOutput> LiftAsync<TInput, TOutput>(Func<TInput, Task<TOutput>> func)
        => func.Invoke;

    /// <summary>
    /// Lifts a function that might throw into a Result-based arrow.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="func">The function that might throw.</param>
    /// <returns>A KleisliResult that catches exceptions.</returns>
    public static KleisliResult<TInput, TOutput, Exception> TryLift<TInput, TOutput>(Func<TInput, TOutput> func)
        => input =>
        {
            try
            {
                return Task.FromResult(Result<TOutput, Exception>.Success(func(input)));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<TOutput, Exception>.Failure(ex));
            }
        };

    /// <summary>
    /// Lifts an async function that might throw into a Result-based arrow.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="func">The async function that might throw.</param>
    /// <returns>A KleisliResult that catches exceptions.</returns>
    public static KleisliResult<TInput, TOutput, Exception> TryLiftAsync<TInput, TOutput>(Func<TInput, Task<TOutput>> func)
        => async input =>
        {
            try
            {
                TOutput? result = await func(input);
                return Result<TOutput, Exception>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TOutput, Exception>.Failure(ex);
            }
        };

    /// <summary>
    /// Creates an arrow that always succeeds with the given value.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="value">The value to return.</param>
    /// <returns>A KleisliResult that always succeeds.</returns>
    public static KleisliResult<TInput, TOutput, TError> Success<TInput, TOutput, TError>(TOutput value)
        => _ => Task.FromResult(Result<TOutput, TError>.Success(value));

    /// <summary>
    /// Creates an arrow that always fails with the given error.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="error">The error to return.</param>
    /// <returns>A KleisliResult that always fails.</returns>
    public static KleisliResult<TInput, TOutput, TError> Failure<TInput, TOutput, TError>(TError error)
        => _ => Task.FromResult(Result<TOutput, TError>.Failure(error));

    /// <summary>
    /// Creates an arrow that always returns Some with the given value.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="value">The value to return.</param>
    /// <returns>A KleisliOption that always returns Some.</returns>
    public static KleisliOption<TInput, TOutput> Some<TInput, TOutput>(TOutput value)
        => _ => Task.FromResult(Option<TOutput>.Some(value));

    /// <summary>
    /// Creates an arrow that always returns None.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <returns>A KleisliOption that always returns None.</returns>
    public static KleisliOption<TInput, TOutput> None<TInput, TOutput>()
        => _ => Task.FromResult(Option<TOutput>.None());

    /// <summary>
    /// Creates a KleisliCompose function that implements standard Kleisli composition.
    /// This enables functional composition patterns and partial application.
    /// </summary>
    /// <typeparam name="TIn">The input type of the first arrow.</typeparam>
    /// <typeparam name="TMid">The intermediate type between arrows.</typeparam>
    /// <typeparam name="TOut">The output type of the second arrow.</typeparam>
    /// <returns>A KleisliCompose function for the specified types.</returns>
    public static KleisliCompose<TIn, TMid, TOut> Compose<TIn, TMid, TOut>()
        => (f, g) => async input => await g(await f(input));

    /// <summary>
    /// Creates a curried composition function that takes the first arrow and returns
    /// a function waiting for the second arrow.
    /// </summary>
    /// <typeparam name="TIn">The input type of the first arrow.</typeparam>
    /// <typeparam name="TMid">The intermediate type between arrows.</typeparam>
    /// <typeparam name="TOut">The output type of the second arrow.</typeparam>
    /// <param name="f">The first Kleisli arrow.</param>
    /// <returns>A function waiting for the second arrow to complete the composition.</returns>
    public static Func<Kleisli<TMid, TOut>, Kleisli<TIn, TOut>> ComposeWith<TIn, TMid, TOut>(
        Kleisli<TIn, TMid> f)
        => g => async input => await g(await f(input));
}
