// <copyright file="ReactiveKleisli.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Reactive.Linq;

namespace LangChainPipeline.Core.Monads;

/// <summary>
/// Kleisli arrow for reactive computations (IObservable).
/// Represents a function A → IObservable(B) with monadic composition.
/// Supports covariance for output and contravariance for input.
/// </summary>
/// <typeparam name="TIn">The input type (contravariant).</typeparam>
/// <typeparam name="TOut">The output type (covariant).</typeparam>
/// <param name="input">The input value.</param>
/// <returns>An observable sequence of results.</returns>
public delegate IObservable<TOut> ReactiveKleisli<in TIn, out TOut>(TIn input);

/// <summary>
/// Extension methods for ReactiveKleisli arrows.
/// Provides monadic composition and reactive stream operations.
/// </summary>
public static class ReactiveKleisliExtensions
{
    /// <summary>
    /// Kleisli composition for reactive computations.
    /// Composes two ReactiveKleisli arrows: (f >=> g)(a) = f(a) >>= g
    /// This is monadic bind for the IObservable monad.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>A composed arrow that applies f then g to each result.</returns>
    public static ReactiveKleisli<TIn, TOut> Compose<TIn, TMid, TOut>(
        this ReactiveKleisli<TIn, TMid> f,
        ReactiveKleisli<TMid, TOut> g)
        => input => System.Reactive.Linq.Observable.SelectMany<TMid, TOut>(f(input), new Func<TMid, IObservable<TOut>>(g));

    /// <summary>
    /// Alternate composition syntax (Then) for consistency with other Kleisli types.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>A composed arrow that applies f then g to each result.</returns>
    public static ReactiveKleisli<TIn, TOut> Then<TIn, TMid, TOut>(
        this ReactiveKleisli<TIn, TMid> f,
        ReactiveKleisli<TMid, TOut> g)
        => input => System.Reactive.Linq.Observable.SelectMany<TMid, TOut>(f(input), new Func<TMid, IObservable<TOut>>(g));

    /// <summary>
    /// SelectMany (monadic bind) for LINQ query syntax support.
    /// Enables query comprehension syntax for ReactiveKleisli arrows.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The source arrow.</param>
    /// <param name="g">The selector function.</param>
    /// <returns>A composed arrow.</returns>
    public static ReactiveKleisli<TIn, TOut> SelectMany<TIn, TMid, TOut>(
        this ReactiveKleisli<TIn, TMid> f,
        Func<TMid, ReactiveKleisli<TMid, TOut>> g)
        => input => f(input).SelectMany(mid => g(mid)(mid));

    /// <summary>
    /// SelectMany with result selector for LINQ query syntax.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The source arrow.</param>
    /// <param name="g">The collection selector.</param>
    /// <param name="selector">The result selector.</param>
    /// <returns>A composed arrow with projected results.</returns>
    public static ReactiveKleisli<TIn, TOut> SelectMany<TIn, TMid, TOut>(
        this ReactiveKleisli<TIn, TMid> f,
        Func<TMid, IObservable<TOut>> g,
        Func<TMid, TOut, TOut> selector)
        => input => f(input).SelectMany(mid => g(mid).Select(result => selector(mid, result)));

    /// <summary>
    /// Maps a function over all results (functor operation).
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="func">The mapping function.</param>
    /// <returns>An arrow with mapped results.</returns>
    public static ReactiveKleisli<TIn, TOut> Map<TIn, TMid, TOut>(
        this ReactiveKleisli<TIn, TMid> arrow,
        Func<TMid, TOut> func)
        => input => arrow(input).Select(func);

    /// <summary>
    /// Union operation: merges results from two reactive streams.
    /// Produces all results from both observables as they arrive.
    /// This is the Merge operation in reactive programming.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>An arrow producing merged results from both streams.</returns>
    public static ReactiveKleisli<TIn, TOut> Union<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> f,
        ReactiveKleisli<TIn, TOut> g)
        => input => f(input).Merge(g(input));

    /// <summary>
    /// Merge operation (alias for Union).
    /// Combines multiple reactive streams into a single stream.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>An arrow producing merged results from both streams.</returns>
    public static ReactiveKleisli<TIn, TOut> Merge<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> f,
        ReactiveKleisli<TIn, TOut> g)
        => input => f(input).Merge(g(input));

    /// <summary>
    /// Removes duplicate results from the reactive stream.
    /// Only emits values that are different from the previous value.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <returns>An arrow with distinct consecutive results.</returns>
    public static ReactiveKleisli<TIn, TOut> Distinct<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow)
        => input => arrow(input).Distinct();

    /// <summary>
    /// Removes duplicate consecutive results from the reactive stream.
    /// Only emits values when they change from the previous value.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <returns>An arrow with distinct consecutive results.</returns>
    public static ReactiveKleisli<TIn, TOut> DistinctUntilChanged<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow)
        => input => arrow(input).DistinctUntilChanged();

    /// <summary>
    /// Filters results based on a predicate.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>An arrow with filtered results.</returns>
    public static ReactiveKleisli<TIn, TOut> Where<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        Func<TOut, bool> predicate)
        => input => arrow(input).Where(predicate);

    /// <summary>
    /// Takes only the first n results from the stream.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="count">The number of results to take.</param>
    /// <returns>An arrow with limited results.</returns>
    public static ReactiveKleisli<TIn, TOut> Take<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        int count)
        => input => arrow(input).Take(count);

    /// <summary>
    /// Skips the first n results from the stream.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="count">The number of results to skip.</param>
    /// <returns>An arrow with skipped results.</returns>
    public static ReactiveKleisli<TIn, TOut> Skip<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        int count)
        => input => arrow(input).Skip(count);

    /// <summary>
    /// Throttles the stream to emit at most one value per time window.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="dueTime">The throttle window duration.</param>
    /// <returns>An arrow with throttled results.</returns>
    public static ReactiveKleisli<TIn, TOut> Throttle<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        TimeSpan dueTime)
        => input => arrow(input).Throttle(dueTime);

    /// <summary>
    /// Debounces the stream to emit only after a period of silence.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="dueTime">The debounce duration.</param>
    /// <returns>An arrow with debounced results.</returns>
    public static ReactiveKleisli<TIn, TOut> Debounce<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        TimeSpan dueTime)
        => input => arrow(input).Throttle(dueTime);

    /// <summary>
    /// Buffers results into groups of the specified size.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="count">The buffer size.</param>
    /// <returns>An arrow producing buffered results.</returns>
    public static ReactiveKleisli<TIn, IList<TOut>> Buffer<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        int count)
        => input => arrow(input).Buffer(count);

    /// <summary>
    /// Scans (accumulates) the stream with a function.
    /// Similar to Aggregate but emits intermediate results.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <typeparam name="TAcc">The accumulator type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="seed">The initial accumulator value.</param>
    /// <param name="accumulator">The accumulation function.</param>
    /// <returns>An arrow producing accumulated results.</returns>
    public static ReactiveKleisli<TIn, TAcc> Scan<TIn, TOut, TAcc>(
        this ReactiveKleisli<TIn, TOut> arrow,
        TAcc seed,
        Func<TAcc, TOut, TAcc> accumulator)
        => input => arrow(input).Scan(seed, accumulator);

    /// <summary>
    /// Catches errors and continues with an alternative observable.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="handler">Function that provides an alternative observable on error.</param>
    /// <returns>An arrow with error handling.</returns>
    public static ReactiveKleisli<TIn, TOut> Catch<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        Func<Exception, IObservable<TOut>> handler)
        => input => arrow(input).Catch(handler);

    /// <summary>
    /// Executes a side effect for each result without modifying the stream.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="action">The side effect to execute.</param>
    /// <returns>An arrow with side effects.</returns>
    public static ReactiveKleisli<TIn, TOut> Do<TIn, TOut>(
        this ReactiveKleisli<TIn, TOut> arrow,
        Action<TOut> action)
        => input => arrow(input).Do(action);

    /// <summary>
    /// Creates an identity arrow that wraps a single value in an observable.
    /// This is the monadic return/pure operation for ReactiveKleisli.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>An arrow that returns a singleton observable.</returns>
    public static ReactiveKleisli<T, T> Identity<T>()
        => input => Observable.Return(input);

    /// <summary>
    /// Lifts a pure function into a ReactiveKleisli arrow.
    /// The function result is wrapped in a singleton observable.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function to lift.</param>
    /// <returns>A ReactiveKleisli arrow.</returns>
    public static ReactiveKleisli<TIn, TOut> Lift<TIn, TOut>(Func<TIn, TOut> func)
        => input => Observable.Return(func(input));

    /// <summary>
    /// Lifts an async function into a ReactiveKleisli arrow.
    /// The function result is wrapped in a singleton observable.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The async function to lift.</param>
    /// <returns>A ReactiveKleisli arrow.</returns>
    public static ReactiveKleisli<TIn, TOut> LiftAsync<TIn, TOut>(Func<TIn, Task<TOut>> func)
        => input => Observable.FromAsync(() => func(input));

    /// <summary>
    /// Lifts a function that returns an observable into a ReactiveKleisli arrow.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function to lift.</param>
    /// <returns>A ReactiveKleisli arrow.</returns>
    public static ReactiveKleisli<TIn, TOut> LiftObservable<TIn, TOut>(Func<TIn, IObservable<TOut>> func)
        => input => func(input);

    /// <summary>
    /// Converts an enumerable into a ReactiveKleisli arrow.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function that returns an enumerable.</param>
    /// <returns>A ReactiveKleisli arrow.</returns>
    public static ReactiveKleisli<TIn, TOut> FromEnumerable<TIn, TOut>(Func<TIn, IEnumerable<TOut>> func)
        => input => func(input).ToObservable();
}
