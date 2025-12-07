// <copyright file="KleisliSet.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Linq;

namespace LangChainPipeline.Core.Monads;

/// <summary>
/// Kleisli arrow for computations that produce multiple results (IEnumerable).
/// Represents a function A → IEnumerable(B) with monadic composition.
/// Supports covariance for output and contravariance for input.
/// </summary>
/// <typeparam name="TIn">The input type (contravariant).</typeparam>
/// <typeparam name="TOut">The output type (covariant).</typeparam>
/// <param name="input">The input value.</param>
/// <returns>An enumerable sequence of results.</returns>
public delegate IEnumerable<TOut> KleisliSet<in TIn, out TOut>(TIn input);

/// <summary>
/// Extension methods for KleisliSet arrows.
/// Provides monadic composition and set-theoretic operations.
/// </summary>
public static class KleisliSetExtensions
{
    /// <summary>
    /// Kleisli composition for set-based computations.
    /// Composes two KleisliSet arrows: (f >=> g)(a) = f(a) >>= g
    /// This is monadic bind for the IEnumerable monad.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>A composed arrow that applies f then g to each result.</returns>
    public static KleisliSet<TIn, TOut> Then<TIn, TMid, TOut>(
        this KleisliSet<TIn, TMid> f,
        KleisliSet<TMid, TOut> g)
        => input => System.Linq.Enumerable.SelectMany<TMid, TOut>(f(input), new Func<TMid, IEnumerable<TOut>>(g));

    /// <summary>
    /// SelectMany (monadic bind) for LINQ query syntax support.
    /// Enables query comprehension syntax for KleisliSet arrows.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The source arrow.</param>
    /// <param name="g">The selector function.</param>
    /// <returns>A composed arrow.</returns>
    public static KleisliSet<TIn, TOut> SelectMany<TIn, TMid, TOut>(
        this KleisliSet<TIn, TMid> f,
        Func<TMid, KleisliSet<TMid, TOut>> g)
        => input => f(input).SelectMany(x => g(x)(x));

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
    public static KleisliSet<TIn, TOut> SelectMany<TIn, TMid, TOut>(
        this KleisliSet<TIn, TMid> f,
        Func<TMid, IEnumerable<TOut>> g,
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
    public static KleisliSet<TIn, TOut> Map<TIn, TMid, TOut>(
        this KleisliSet<TIn, TMid> arrow,
        Func<TMid, TOut> func)
        => input => arrow(input).Select(func);

    /// <summary>
    /// Union operation: combines results from two arrows.
    /// Produces the set union of results from both computations.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>An arrow producing the union of both result sets.</returns>
    public static KleisliSet<TIn, TOut> Union<TIn, TOut>(
        this KleisliSet<TIn, TOut> f,
        KleisliSet<TIn, TOut> g)
        => input => f(input).Union(g(input));

    /// <summary>
    /// Intersect operation: produces common results from two arrows.
    /// Returns only values that appear in both result sets.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>An arrow producing the intersection of both result sets.</returns>
    public static KleisliSet<TIn, TOut> Intersect<TIn, TOut>(
        this KleisliSet<TIn, TOut> f,
        KleisliSet<TIn, TOut> g)
        => input => f(input).Intersect(g(input));

    /// <summary>
    /// Except operation: removes results of second arrow from first.
    /// Returns values from f that are not in g.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>An arrow producing the set difference.</returns>
    public static KleisliSet<TIn, TOut> Except<TIn, TOut>(
        this KleisliSet<TIn, TOut> f,
        KleisliSet<TIn, TOut> g)
        => input => f(input).Except(g(input));

    /// <summary>
    /// Filters results based on a predicate.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>An arrow with filtered results.</returns>
    public static KleisliSet<TIn, TOut> Where<TIn, TOut>(
        this KleisliSet<TIn, TOut> arrow,
        Func<TOut, bool> predicate)
        => input => arrow(input).Where(predicate);

    /// <summary>
    /// Removes duplicate results.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <returns>An arrow with distinct results.</returns>
    public static KleisliSet<TIn, TOut> Distinct<TIn, TOut>(
        this KleisliSet<TIn, TOut> arrow)
        => input => arrow(input).Distinct();

    /// <summary>
    /// Creates an identity arrow that wraps a single value in a sequence.
    /// This is the monadic return/pure operation for KleisliSet.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>An arrow that returns a singleton sequence.</returns>
    public static KleisliSet<T, T> Identity<T>()
        => input => new[] { input };

    /// <summary>
    /// Lifts a pure function into a KleisliSet arrow.
    /// The function result is wrapped in a singleton sequence.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function to lift.</param>
    /// <returns>A KleisliSet arrow.</returns>
    public static KleisliSet<TIn, TOut> Lift<TIn, TOut>(Func<TIn, TOut> func)
        => input => new[] { func(input) };

    /// <summary>
    /// Lifts a function that returns an enumerable into a KleisliSet arrow.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function to lift.</param>
    /// <returns>A KleisliSet arrow.</returns>
    public static KleisliSet<TIn, TOut> LiftMany<TIn, TOut>(Func<TIn, IEnumerable<TOut>> func)
        => input => func(input);
}
