// <copyright file="KleisliExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Kleisli;

/// <summary>
/// Unified extension methods for Kleisli arrows.
/// Since Step{TInput,TOutput} and Kleisli{TInput,TOutput} are conceptually identical (both are Task monad arrows),
/// this provides a single, elegant set of operations for both.
/// </summary>
public static class KleisliExtensions
{

    /// <summary>
    /// Kleisli composition: (f >=> g)(a) = f(a) >>= g
    /// This is the fundamental operation for chaining monadic computations.
    /// Maintains associativity law for proper category theory compliance.
    /// Works with both Step{TInput,TOutput} and Kleisli{TInput,TOutput}.
    /// </summary>
    /// <typeparam name="TA">The input type of the first arrow.</typeparam>
    /// <typeparam name="TB">The intermediate type between the two arrows.</typeparam>
    /// <typeparam name="TC">The output type of the second arrow.</typeparam>
    /// <param name="f">The first arrow to apply.</param>
    /// <param name="g">The second arrow to apply to the result of f.</param>
    /// <returns>A new arrow representing the composition.</returns>
    public static Step<TA, TC> Then<TA, TB, TC>(
        this Step<TA, TB> f,
        Step<TB, TC> g)
        => async input => await g(await f(input).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Kleisli composition for pure Kleisli{TInput,TOutput} delegates.
    /// </summary>
    /// <typeparam name="TA">The input type of the first arrow.</typeparam>
    /// <typeparam name="TB">The intermediate type between the two arrows.</typeparam>
    /// <typeparam name="TC">The output type of the second arrow.</typeparam>
    /// <param name="f">The first arrow to apply.</param>
    /// <param name="g">The second arrow to apply to the result of f.</param>
    /// <returns>A new arrow representing the composition.</returns>
    public static Kleisli<TA, TC> Then<TA, TB, TC>(
        this Kleisli<TA, TB> f,
        Kleisli<TB, TC> g)
        => async input => await g(await f(input).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Mixed composition: Step -> Kleisli.
    /// </summary>
    /// <typeparam name="TA">The input type of the first arrow.</typeparam>
    /// <typeparam name="TB">The intermediate type between the two arrows.</typeparam>
    /// <typeparam name="TC">The output type of the second arrow.</typeparam>
    /// <param name="f">The first arrow to apply.</param>
    /// <param name="g">The second arrow to apply to the result of f.</param>
    /// <returns>A new arrow representing the composition.</returns>
    public static Kleisli<TA, TC> Then<TA, TB, TC>(
        this Step<TA, TB> f,
        Kleisli<TB, TC> g)
        => async input => await g(await f(input).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Mixed composition: Kleisli -> Step.
    /// </summary>
    /// <typeparam name="TA">The input type of the first arrow.</typeparam>
    /// <typeparam name="TB">The intermediate type between the two arrows.</typeparam>
    /// <typeparam name="TC">The output type of the second arrow.</typeparam>
    /// <param name="f">The first arrow to apply.</param>
    /// <param name="g">The second arrow to apply to the result of f.</param>
    /// <returns>A new arrow representing the composition.</returns>
    public static Step<TA, TC> Then<TA, TB, TC>(
        this Kleisli<TA, TB> f,
        Step<TB, TC> g)
        => async input => await g(await f(input).ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Maps a function over the result of a Step arrow (functor operation).
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TC">The result type after mapping.</typeparam>
    /// <param name="arrow">The arrow to map over.</param>
    /// <param name="func">The function to apply to the arrow result.</param>
    /// <returns>A new arrow with the mapped result.</returns>
    public static Step<TA, TC> Map<TA, TB, TC>(
        this Step<TA, TB> arrow,
        Func<TB, TC> func)
        => async input => func(await arrow(input).ConfigureAwait(false));

    /// <summary>
    /// Maps a function over the result of a Kleisli arrow (functor operation).
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TC">The result type after mapping.</typeparam>
    /// <param name="arrow">The arrow to map over.</param>
    /// <param name="func">The function to apply to the arrow result.</param>
    /// <returns>A new arrow with the mapped result.</returns>
    public static Kleisli<TA, TC> Map<TA, TB, TC>(
        this Kleisli<TA, TB> arrow,
        Func<TB, TC> func)
        => async input => func(await arrow(input).ConfigureAwait(false));

    /// <summary>
    /// Maps an async function over the result of a Step arrow.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TC">The result type after mapping.</typeparam>
    /// <param name="arrow">The arrow to map over.</param>
    /// <param name="func">The async function to apply to the arrow result.</param>
    /// <returns>A new arrow with the mapped result.</returns>
    public static Step<TA, TC> MapAsync<TA, TB, TC>(
        this Step<TA, TB> arrow,
        Func<TB, Task<TC>> func)
        => async input => await func(await arrow(input)).ConfigureAwait(false);

    /// <summary>
    /// Maps an async function over the result of a Kleisli arrow.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TC">The result type after mapping.</typeparam>
    /// <param name="arrow">The arrow to map over.</param>
    /// <param name="func">The async function to apply to the arrow result.</param>
    /// <returns>A new arrow with the mapped result.</returns>
    public static Kleisli<TA, TC> MapAsync<TA, TB, TC>(
        this Kleisli<TA, TB> arrow,
        Func<TB, Task<TC>> func)
        => async input => await func(await arrow(input)).ConfigureAwait(false);

    /// <summary>
    /// Executes a side effect on the result of a Step without modifying it (tap operation).
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <param name="arrow">The arrow to tap.</param>
    /// <param name="action">The action to execute on the result.</param>
    /// <returns>The original arrow with side effect applied.</returns>
    public static Step<TA, TB> Tap<TA, TB>(
        this Step<TA, TB> arrow,
        Action<TB> action)
        => async input =>
        {
            TB? result = await arrow(input).ConfigureAwait(false);
            action(result);
            return result;
        };

    /// <summary>
    /// Executes a side effect on the result of a Kleisli arrow without modifying it (tap operation).
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <param name="arrow">The arrow to tap.</param>
    /// <param name="action">The action to execute on the result.</param>
    /// <returns>The original arrow with side effect applied.</returns>
    public static Kleisli<TA, TB> Tap<TA, TB>(
        this Kleisli<TA, TB> arrow,
        Action<TB> action)
        => async input =>
        {
            TB? result = await arrow(input).ConfigureAwait(false);
            action(result);
            return result;
        };

    /// <summary>
    /// Catches exceptions in a Step arrow and converts them to a Result.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <param name="arrow">The arrow to wrap with exception handling.</param>
    /// <returns>A KleisliResult arrow that catches exceptions.</returns>
    public static KleisliResult<TA, TB, Exception> Catch<TA, TB>(
        this Step<TA, TB> arrow)
        => async input =>
        {
            try
            {
                TB? result = await arrow(input).ConfigureAwait(false);
                return Result<TB, Exception>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TB, Exception>.Failure(ex);
            }
        };

    /// <summary>
    /// Catches exceptions in a Kleisli arrow and converts them to a Result.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <param name="arrow">The arrow to wrap with exception handling.</param>
    /// <returns>A KleisliResult arrow that catches exceptions.</returns>
    public static KleisliResult<TA, TB, Exception> Catch<TA, TB>(
        this Kleisli<TA, TB> arrow)
        => async input =>
        {
            try
            {
                TB? result = await arrow(input).ConfigureAwait(false);
                return Result<TB, Exception>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TB, Exception>.Failure(ex);
            }
        };

    /// <summary>
    /// Composes KleisliResult arrows with proper error handling.
    /// If the first computation fails, the error is propagated without executing the second.
    /// </summary>
    /// <typeparam name="TA">The input type of the first arrow.</typeparam>
    /// <typeparam name="TB">The intermediate type between arrows.</typeparam>
    /// <typeparam name="TC">The output type of the second arrow.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="first">The first arrow to apply.</param>
    /// <param name="second">The second arrow to apply to the result of first.</param>
    /// <returns>A composed KleisliResult arrow.</returns>
    public static KleisliResult<TA, TC, TError> Then<TA, TB, TC, TError>(
        this KleisliResult<TA, TB, TError> first,
        KleisliResult<TB, TC, TError> second)
        => async input =>
        {
            Result<TB, TError> firstResult = await first(input);
            return firstResult.IsSuccess
                ? await second(firstResult.Value)
                : Result<TC, TError>.Failure(firstResult.Error);
        };

    /// <summary>
    /// Maps a function over the success result of a KleisliResult.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TC">The result type after mapping.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="arrow">The arrow to map over.</param>
    /// <param name="func">The function to apply to the success result.</param>
    /// <returns>A new KleisliResult arrow with the mapped result.</returns>
    public static KleisliResult<TA, TC, TError> Map<TA, TB, TC, TError>(
        this KleisliResult<TA, TB, TError> arrow,
        Func<TB, TC> func)
        => async input =>
        {
            Result<TB, TError> result = await arrow(input);
            return result.IsSuccess
                ? Result<TC, TError>.Success(func(result.Value))
                : Result<TC, TError>.Failure(result.Error);
        };

    /// <summary>
    /// Executes a side effect on the success result without modifying it.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="arrow">The arrow to tap.</param>
    /// <param name="action">The action to execute on the success result.</param>
    /// <returns>The original KleisliResult arrow with side effect applied.</returns>
    public static KleisliResult<TA, TB, TError> Tap<TA, TB, TError>(
        this KleisliResult<TA, TB, TError> arrow,
        Action<TB> action)
        => async input =>
        {
            Result<TB, TError> result = await arrow(input);
            if (result.IsSuccess)
            {
                action(result.Value);
            }

            return result;
        };

    /// <summary>
    /// Composes KleisliOption arrows with proper None handling.
    /// If the first computation returns None, the second is not executed.
    /// </summary>
    /// <typeparam name="TA">The input type of the first arrow.</typeparam>
    /// <typeparam name="TB">The intermediate type between arrows.</typeparam>
    /// <typeparam name="TC">The output type of the second arrow.</typeparam>
    /// <param name="first">The first arrow to apply.</param>
    /// <param name="second">The second arrow to apply to the result of first.</param>
    /// <returns>A composed KleisliOption arrow.</returns>
    public static KleisliOption<TA, TC> Then<TA, TB, TC>(
        this KleisliOption<TA, TB> first,
        KleisliOption<TB, TC> second)
        => async input =>
        {
            Option<TB> firstResult = await first(input);
            return firstResult.HasValue && firstResult.Value is not null
                ? await second(firstResult.Value)
                : Option<TC>.None();
        };

    /// <summary>
    /// Maps a function over the Some result of a KleisliOption.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TC">The result type after mapping.</typeparam>
    /// <param name="arrow">The arrow to map over.</param>
    /// <param name="func">The function to apply to the Some result.</param>
    /// <returns>A new KleisliOption arrow with the mapped result.</returns>
    public static KleisliOption<TA, TC> Map<TA, TB, TC>(
        this KleisliOption<TA, TB> arrow,
        Func<TB, TC> func)
        => async input =>
        {
            Option<TB> result = await arrow(input);
            return result.HasValue && result.Value is not null
                ? Option<TC>.Some(func(result.Value))
                : Option<TC>.None();
        };

    /// <summary>
    /// Converts a KleisliOption to a KleisliResult.
    /// None becomes a Failure with the provided error.
    /// </summary>
    /// <typeparam name="TA">The input type of the arrow.</typeparam>
    /// <typeparam name="TB">The output type of the arrow.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="arrow">The KleisliOption arrow to convert.</param>
    /// <param name="error">The error to use for None results.</param>
    /// <returns>A KleisliResult arrow.</returns>
    public static KleisliResult<TA, TB, TError> ToResult<TA, TB, TError>(
        this KleisliOption<TA, TB> arrow,
        TError error)
        => async input =>
        {
            Option<TB> result = await arrow(input);
            return result.HasValue && result.Value is not null
                ? Result<TB, TError>.Success(result.Value)
                : Result<TB, TError>.Failure(error);
        };

    /// <summary>
    /// Applies a KleisliCompose function to compose two Kleisli arrows.
    /// This enables functional composition patterns using higher-order functions.
    /// </summary>
    /// <typeparam name="TIn">The input type of the first arrow.</typeparam>
    /// <typeparam name="TMid">The intermediate type between arrows.</typeparam>
    /// <typeparam name="TOut">The output type of the second arrow.</typeparam>
    /// <param name="f">The first Kleisli arrow.</param>
    /// <param name="composer">The KleisliCompose function to use.</param>
    /// <param name="g">The second Kleisli arrow.</param>
    /// <returns>The composed Kleisli arrow.</returns>
    public static Kleisli<TIn, TOut> ComposeWith<TIn, TMid, TOut>(
        this Kleisli<TIn, TMid> f,
        KleisliCompose<TIn, TMid, TOut> composer,
        Kleisli<TMid, TOut> g)
        => composer(f, g);

    /// <summary>
    /// Creates a partially applied composition where the first arrow is fixed.
    /// Returns a function that takes the second arrow to complete the composition.
    /// </summary>
    /// <typeparam name="TIn">The input type of the first arrow.</typeparam>
    /// <typeparam name="TMid">The intermediate type between arrows.</typeparam>
    /// <typeparam name="TOut">The output type of the second arrow.</typeparam>
    /// <param name="f">The first Kleisli arrow.</param>
    /// <returns>A function awaiting the second arrow for composition.</returns>
    public static Func<Kleisli<TMid, TOut>, Kleisli<TIn, TOut>> PartialCompose<TIn, TMid, TOut>(
        this Kleisli<TIn, TMid> f)
        => Arrow.ComposeWith<TIn, TMid, TOut>(f);

    /// <summary>
    /// Applies a composition function in a fluent manner.
    /// Useful for building complex composition pipelines.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first Kleisli arrow.</param>
    /// <param name="composeFunc">Function that takes the first arrow and returns composed result.</param>
    /// <returns>The composed Kleisli arrow.</returns>
    public static Kleisli<TIn, TOut> Compose<TIn, TMid, TOut>(
        this Kleisli<TIn, TMid> f,
        Func<Kleisli<TIn, TMid>, Kleisli<TIn, TOut>> composeFunc)
        => composeFunc(f);
}
