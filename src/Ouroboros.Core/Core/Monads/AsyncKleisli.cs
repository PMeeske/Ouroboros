// <copyright file="AsyncKleisli.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Monads;

/// <summary>
/// Kleisli arrow for asynchronous streaming computations (IAsyncEnumerable).
/// Represents a function A → IAsyncEnumerable(B) with monadic composition.
/// Supports covariance for output and contravariance for input.
/// </summary>
/// <typeparam name="TIn">The input type (contravariant).</typeparam>
/// <typeparam name="TOut">The output type (covariant).</typeparam>
/// <param name="input">The input value.</param>
/// <returns>An asynchronous enumerable sequence of results.</returns>
public delegate IAsyncEnumerable<TOut> AsyncKleisli<in TIn, out TOut>(TIn input);

/// <summary>
/// Extension methods for AsyncKleisli arrows.
/// Provides monadic composition and asynchronous stream operations.
/// </summary>
public static class AsyncKleisliExtensions
{
    /// <summary>
    /// Kleisli composition for async streaming computations.
    /// Composes two AsyncKleisli arrows: (f >=> g)(a) = f(a) >>= g
    /// This is monadic bind for the IAsyncEnumerable monad.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>A composed arrow that applies f then g to each result.</returns>
    public static AsyncKleisli<TIn, TOut> Then<TIn, TMid, TOut>(
        this AsyncKleisli<TIn, TMid> f,
        AsyncKleisli<TMid, TOut> g)
        => input => FlattenAsync(f(input), g);

    /// <summary>
    /// SelectMany (monadic bind) for LINQ query syntax support.
    /// Enables query comprehension syntax for AsyncKleisli arrows.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The source arrow.</param>
    /// <param name="g">The selector function.</param>
    /// <returns>A composed arrow.</returns>
    public static AsyncKleisli<TIn, TOut> SelectMany<TIn, TMid, TOut>(
        this AsyncKleisli<TIn, TMid> f,
        Func<TMid, AsyncKleisli<TMid, TOut>> g)
        => input => FlattenAsync(f(input), mid => g(mid)(mid));

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
    public static AsyncKleisli<TIn, TOut> SelectMany<TIn, TMid, TOut>(
        this AsyncKleisli<TIn, TMid> f,
        Func<TMid, IAsyncEnumerable<TOut>> g,
        Func<TMid, TOut, TOut> selector)
        => input => FlattenWithSelectorAsync(f(input), g, selector);

    /// <summary>
    /// Maps a function over all results (functor operation).
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="func">The mapping function.</param>
    /// <returns>An arrow with mapped results.</returns>
    public static AsyncKleisli<TIn, TOut> Map<TIn, TMid, TOut>(
        this AsyncKleisli<TIn, TMid> arrow,
        Func<TMid, TOut> func)
        => input => MapAsync(arrow(input), func);

    /// <summary>
    /// Maps an async function over all results.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TMid">The intermediate type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="func">The async mapping function.</param>
    /// <returns>An arrow with mapped results.</returns>
    public static AsyncKleisli<TIn, TOut> MapAsync<TIn, TMid, TOut>(
        this AsyncKleisli<TIn, TMid> arrow,
        Func<TMid, Task<TOut>> func)
        => input => MapAsyncImpl(arrow(input), func);

    /// <summary>
    /// Union operation: merges results from two async streams.
    /// Produces all results from both computations as they arrive.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="f">The first arrow.</param>
    /// <param name="g">The second arrow.</param>
    /// <returns>An arrow producing merged results from both streams.</returns>
    public static AsyncKleisli<TIn, TOut> Union<TIn, TOut>(
        this AsyncKleisli<TIn, TOut> f,
        AsyncKleisli<TIn, TOut> g)
        => input => MergeAsync(f(input), g(input));

    /// <summary>
    /// Filters results based on a predicate.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>An arrow with filtered results.</returns>
    public static AsyncKleisli<TIn, TOut> Where<TIn, TOut>(
        this AsyncKleisli<TIn, TOut> arrow,
        Func<TOut, bool> predicate)
        => input => WhereAsync(arrow(input), predicate);

    /// <summary>
    /// Filters results based on an async predicate.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="predicate">The async filter predicate.</param>
    /// <returns>An arrow with filtered results.</returns>
    public static AsyncKleisli<TIn, TOut> WhereAsync<TIn, TOut>(
        this AsyncKleisli<TIn, TOut> arrow,
        Func<TOut, Task<bool>> predicate)
        => input => WhereAsyncImpl(arrow(input), predicate);

    /// <summary>
    /// Takes only the first n results from the stream.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <param name="count">The number of results to take.</param>
    /// <returns>An arrow with limited results.</returns>
    public static AsyncKleisli<TIn, TOut> Take<TIn, TOut>(
        this AsyncKleisli<TIn, TOut> arrow,
        int count)
        => input => TakeAsync(arrow(input), count);

    /// <summary>
    /// Removes duplicate results from the stream.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="arrow">The source arrow.</param>
    /// <returns>An arrow with distinct results.</returns>
    public static AsyncKleisli<TIn, TOut> Distinct<TIn, TOut>(
        this AsyncKleisli<TIn, TOut> arrow)
        => input => DistinctAsync(arrow(input));

    /// <summary>
    /// Creates an identity arrow that wraps a single value in an async sequence.
    /// This is the monadic return/pure operation for AsyncKleisli.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>An arrow that returns a singleton async sequence.</returns>
    public static AsyncKleisli<T, T> Identity<T>()
        => input => SingleAsync(input);

    /// <summary>
    /// Lifts a pure function into an AsyncKleisli arrow.
    /// The function result is wrapped in a singleton async sequence.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function to lift.</param>
    /// <returns>An AsyncKleisli arrow.</returns>
    public static AsyncKleisli<TIn, TOut> Lift<TIn, TOut>(Func<TIn, TOut> func)
        => input => SingleAsync(func(input));

    /// <summary>
    /// Lifts an async function into an AsyncKleisli arrow.
    /// The function result is wrapped in a singleton async sequence.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The async function to lift.</param>
    /// <returns>An AsyncKleisli arrow.</returns>
    public static AsyncKleisli<TIn, TOut> LiftAsync<TIn, TOut>(Func<TIn, Task<TOut>> func)
        => input => LiftAsyncImpl(func, input);

    /// <summary>
    /// Lifts a function that returns an async enumerable into an AsyncKleisli arrow.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function to lift.</param>
    /// <returns>An AsyncKleisli arrow.</returns>
    public static AsyncKleisli<TIn, TOut> LiftMany<TIn, TOut>(Func<TIn, IAsyncEnumerable<TOut>> func)
        => input => func(input);

    // Helper methods for async enumerable operations
    private static async IAsyncEnumerable<TOut> FlattenAsync<TMid, TOut>(
        IAsyncEnumerable<TMid> source,
        AsyncKleisli<TMid, TOut> selector)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            await foreach (var result in selector(item).ConfigureAwait(false))
            {
                yield return result;
            }
        }
    }

    private static async IAsyncEnumerable<TOut> FlattenWithSelectorAsync<TMid, TOut>(
        IAsyncEnumerable<TMid> source,
        Func<TMid, IAsyncEnumerable<TOut>> selector,
        Func<TMid, TOut, TOut> resultSelector)
    {
        await foreach (var mid in source.ConfigureAwait(false))
        {
            await foreach (var item in selector(mid).ConfigureAwait(false))
            {
                yield return resultSelector(mid, item);
            }
        }
    }

    private static async IAsyncEnumerable<TOut> MapAsync<TIn, TOut>(
        IAsyncEnumerable<TIn> source,
        Func<TIn, TOut> selector)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            yield return selector(item);
        }
    }

    private static async IAsyncEnumerable<TOut> MapAsyncImpl<TIn, TOut>(
        IAsyncEnumerable<TIn> source,
        Func<TIn, Task<TOut>> selector)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            yield return await selector(item).ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<T> MergeAsync<T>(
        IAsyncEnumerable<T> first,
        IAsyncEnumerable<T> second)
    {
        var firstEnumerator = first.GetAsyncEnumerator();
        var secondEnumerator = second.GetAsyncEnumerator();
        
        try
        {
            var firstTask = firstEnumerator.MoveNextAsync();
            var secondTask = secondEnumerator.MoveNextAsync();
            
            var firstActive = true;
            var secondActive = true;
            
            while (firstActive || secondActive)
            {
                if (firstActive && firstTask.IsCompleted)
                {
                    if (await firstTask.ConfigureAwait(false))
                    {
                        yield return firstEnumerator.Current;
                        firstTask = firstEnumerator.MoveNextAsync();
                    }
                    else
                    {
                        firstActive = false;
                    }
                }
                
                if (secondActive && secondTask.IsCompleted)
                {
                    if (await secondTask.ConfigureAwait(false))
                    {
                        yield return secondEnumerator.Current;
                        secondTask = secondEnumerator.MoveNextAsync();
                    }
                    else
                    {
                        secondActive = false;
                    }
                }
                
                if (firstActive && !firstTask.IsCompleted)
                {
                    await Task.Yield();
                }
                
                if (secondActive && !secondTask.IsCompleted)
                {
                    await Task.Yield();
                }
            }
        }
        finally
        {
            await firstEnumerator.DisposeAsync().ConfigureAwait(false);
            await secondEnumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<T> WhereAsync<T>(
        IAsyncEnumerable<T> source,
        Func<T, bool> predicate)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            if (predicate(item))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> WhereAsyncImpl<T>(
        IAsyncEnumerable<T> source,
        Func<T, Task<bool>> predicate)
    {
        await foreach (var item in source.ConfigureAwait(false))
        {
            if (await predicate(item).ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> TakeAsync<T>(
        IAsyncEnumerable<T> source,
        int count)
    {
        var taken = 0;
        await foreach (var item in source.ConfigureAwait(false))
        {
            if (taken >= count)
            {
                yield break;
            }

            yield return item;
            taken++;
        }
    }

    private static async IAsyncEnumerable<T> DistinctAsync<T>(IAsyncEnumerable<T> source)
    {
        var seen = new HashSet<T>();
        await foreach (var item in source.ConfigureAwait(false))
        {
            if (seen.Add(item))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<T> SingleAsync<T>(T value)
    {
        yield return value;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<TOut> LiftAsyncImpl<TIn, TOut>(
        Func<TIn, Task<TOut>> func,
        TIn input)
    {
        yield return await func(input).ConfigureAwait(false);
    }
}
