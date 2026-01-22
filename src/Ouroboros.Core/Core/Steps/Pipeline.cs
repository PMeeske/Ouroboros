// <copyright file="Pipeline.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Ouroboros.Core.Steps;

/// <summary>
/// Fluent pipeline builder for composing Steps using functional programming patterns.
/// Provides a clean API for building type-safe, composable async pipelines.
/// </summary>
/// <typeparam name="TIn">The input type of the pipeline.</typeparam>
/// <typeparam name="TOut">The output type of the pipeline.</typeparam>
public readonly struct Pipeline<TIn, TOut>
{
    private readonly Step<TIn, TOut> _step;

    /// <summary>
    /// Initializes a new Pipeline from a Step.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pipeline(Step<TIn, TOut> step)
    {
        _step = step ?? throw new ArgumentNullException(nameof(step));
    }

    /// <summary>
    /// Composes this pipeline with another step (Kleisli composition).
    /// </summary>
    /// <typeparam name="TNext">The output type of the next step.</typeparam>
    /// <param name="next">The next step to compose.</param>
    /// <returns>A new pipeline representing the composition.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pipeline<TIn, TNext> Then<TNext>(Step<TOut, TNext> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        Step<TIn, TOut> current = _step;
        return new Pipeline<TIn, TNext>(async input =>
        {
            var mid = await current(input).ConfigureAwait(false);
            return await next(mid).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Composes this pipeline with a synchronous transformation (functor map).
    /// </summary>
    /// <typeparam name="TNext">The output type after transformation.</typeparam>
    /// <param name="func">The transformation function.</param>
    /// <returns>A new pipeline with the transformation applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pipeline<TIn, TNext> Map<TNext>(Func<TOut, TNext> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        Step<TIn, TOut> current = _step;
        return new Pipeline<TIn, TNext>(async input =>
        {
            var result = await current(input).ConfigureAwait(false);
            return func(result);
        });
    }

    /// <summary>
    /// Composes this pipeline with an async transformation.
    /// </summary>
    /// <typeparam name="TNext">The output type after transformation.</typeparam>
    /// <param name="func">The async transformation function.</param>
    /// <returns>A new pipeline with the async transformation applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pipeline<TIn, TNext> MapAsync<TNext>(Func<TOut, Task<TNext>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        Step<TIn, TOut> current = _step;
        return new Pipeline<TIn, TNext>(async input =>
        {
            var result = await current(input).ConfigureAwait(false);
            return await func(result).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Monadic bind (flatMap) operation.
    /// </summary>
    /// <typeparam name="TNext">The output type of the binding function.</typeparam>
    /// <param name="func">The binding function that returns a Step.</param>
    /// <returns>A new pipeline with the binding applied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pipeline<TIn, TNext> Bind<TNext>(Func<TOut, Step<TOut, TNext>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        Step<TIn, TOut> current = _step;
        return new Pipeline<TIn, TNext>(async input =>
        {
            var mid = await current(input).ConfigureAwait(false);
            var nextStep = func(mid);
            return await nextStep(mid).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Executes a side effect without modifying the pipeline output.
    /// </summary>
    /// <param name="action">The side effect action.</param>
    /// <returns>The same pipeline unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pipeline<TIn, TOut> Tap(Action<TOut> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Step<TIn, TOut> current = _step;
        return new Pipeline<TIn, TOut>(async input =>
        {
            var result = await current(input).ConfigureAwait(false);
            action(result);
            return result;
        });
    }

    /// <summary>
    /// Executes an async side effect without modifying the pipeline output.
    /// </summary>
    /// <param name="func">The async side effect function.</param>
    /// <returns>The same pipeline unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Pipeline<TIn, TOut> TapAsync(Func<TOut, Task> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        Step<TIn, TOut> current = _step;
        return new Pipeline<TIn, TOut>(async input =>
        {
            var result = await current(input).ConfigureAwait(false);
            await func(result).ConfigureAwait(false);
            return result;
        });
    }

    /// <summary>
    /// Wraps the pipeline execution in a try-catch, returning a Result.
    /// </summary>
    /// <returns>A pipeline that returns Result with exception as error.</returns>
    public Pipeline<TIn, Result<TOut, Exception>> TryCatch()
    {
        Step<TIn, TOut> current = _step;
        return new Pipeline<TIn, Result<TOut, Exception>>(async input =>
        {
            try
            {
                var result = await current(input).ConfigureAwait(false);
                return Result<TOut, Exception>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TOut, Exception>.Failure(ex);
            }
        });
    }

    /// <summary>
    /// Executes the pipeline with the given input.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns>A task representing the pipeline execution.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TOut> RunAsync(TIn input) => _step(input);

    /// <summary>
    /// Converts the pipeline to its underlying Step delegate.
    /// </summary>
    /// <returns>The underlying Step.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Step<TIn, TOut> ToStep() => _step;

    /// <summary>
    /// Implicit conversion to Step for seamless integration.
    /// </summary>
    public static implicit operator Step<TIn, TOut>(Pipeline<TIn, TOut> pipeline) => pipeline._step;

    /// <summary>
    /// Implicit conversion from Step for seamless integration.
    /// </summary>
    public static implicit operator Pipeline<TIn, TOut>(Step<TIn, TOut> step) => new(step);

    /// <summary>
    /// Pipe operator for fluent composition.
    /// Note: Due to C# limitations, generic operators cannot introduce new type parameters.
    /// Use the Then() method for composing with new output types.
    /// </summary>
    public static Pipeline<TIn, TOut> operator |(Pipeline<TIn, TOut> pipeline, Step<TOut, TOut> next)
        => pipeline.Then(next);
}

/// <summary>
/// Factory methods for creating Pipeline instances.
/// </summary>
public static class Pipeline
{
    /// <summary>
    /// Creates a pipeline that starts with the identity transformation.
    /// </summary>
    /// <typeparam name="T">The type flowing through the pipeline.</typeparam>
    /// <returns>An identity pipeline.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Pipeline<T, T> Pure<T>() => new(input => Task.FromResult(input));

    /// <summary>
    /// Creates a pipeline from an initial step.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="step">The initial step.</param>
    /// <returns>A pipeline wrapping the step.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Pipeline<TIn, TOut> From<TIn, TOut>(Step<TIn, TOut> step) => new(step);

    /// <summary>
    /// Creates a pipeline from a synchronous function.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The function to wrap.</param>
    /// <returns>A pipeline wrapping the function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Pipeline<TIn, TOut> Lift<TIn, TOut>(Func<TIn, TOut> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return new Pipeline<TIn, TOut>(input => Task.FromResult(func(input)));
    }

    /// <summary>
    /// Creates a pipeline from an async function.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="func">The async function to wrap.</param>
    /// <returns>A pipeline wrapping the async function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Pipeline<TIn, TOut> LiftAsync<TIn, TOut>(Func<TIn, Task<TOut>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return new Pipeline<TIn, TOut>(func.Invoke);
    }

    /// <summary>
    /// Creates a pipeline that always returns the specified value.
    /// </summary>
    /// <typeparam name="TIn">The input type (ignored).</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="value">The value to return.</param>
    /// <returns>A pipeline that always returns the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Pipeline<TIn, TOut> Constant<TIn, TOut>(TOut value) =>
        new(_ => Task.FromResult(value));

    /// <summary>
    /// Combines multiple pipelines to run in parallel, collecting all results.
    /// </summary>
    /// <typeparam name="TIn">The shared input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="pipelines">The pipelines to run in parallel.</param>
    /// <returns>A pipeline that returns all results.</returns>
    public static Pipeline<TIn, TOut[]> Parallel<TIn, TOut>(params Pipeline<TIn, TOut>[] pipelines)
    {
        return new Pipeline<TIn, TOut[]>(async input =>
        {
            var tasks = pipelines.Select(p => p.RunAsync(input)).ToArray();
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Creates a pipeline that races multiple pipelines and returns the first to complete.
    /// </summary>
    /// <typeparam name="TIn">The shared input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="pipelines">The pipelines to race.</param>
    /// <returns>A pipeline that returns the first result.</returns>
    public static Pipeline<TIn, TOut> Race<TIn, TOut>(params Pipeline<TIn, TOut>[] pipelines)
    {
        return new Pipeline<TIn, TOut>(async input =>
        {
            var tasks = pipelines.Select(p => p.RunAsync(input)).ToArray();
            var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
            return await completed.ConfigureAwait(false);
        });
    }
}
