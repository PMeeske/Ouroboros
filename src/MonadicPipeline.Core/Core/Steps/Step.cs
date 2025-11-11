// <copyright file="Step.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Steps;

/// <summary>
/// Step{TA,TB} is unified with Kleisli{TA,TB} - they represent the same concept.
/// This delegates to the proper Kleisli arrow for conceptual clarity.
/// All functionality is provided through KleisliExtensions.
/// </summary>
/// <typeparam name="TA">The input type.</typeparam>
/// <typeparam name="TB">The output type.</typeparam>
/// <param name="input">The input value.</param>
/// <returns>A task representing the transformed output.</returns>
public delegate Task<TB> Step<in TA, TB>(TA input);

/// <summary>
/// Synchronous computation step that transforms input of type TIn to output of type TOut.
/// Provides a bridge between pure functional operations and our async Step system.
/// </summary>
public readonly struct SyncStep<TIn, TOut> : IEquatable<SyncStep<TIn, TOut>>
{
    private readonly Func<TIn, TOut> f;

    public SyncStep(Func<TIn, TOut> f)
        => this.f = f ?? throw new ArgumentNullException(nameof(f));

    /// <summary>
    /// Execute the synchronous step.
    /// </summary>
    /// <returns></returns>
    public TOut Invoke(TIn input) => this.f(input);

    /// <summary>
    /// Convert to async Step.
    /// </summary>
    /// <returns></returns>
    public Step<TIn, TOut> ToAsync()
    {
        var func = this.f; // Capture to avoid struct 'this' issues
        return input => Task.FromResult(func(input));
    }

    /// <summary>
    /// Pipe composition (heterogeneous) - synchronous version.
    /// </summary>
    /// <returns></returns>
    public SyncStep<TIn, TNext> Pipe<TNext>(SyncStep<TOut, TNext> next)
    {
        var func = this.f; // Capture to avoid struct 'this' issues
        return new(input => next.Invoke(func(input)));
    }

    /// <summary>
    /// Pipe with async step.
    /// </summary>
    /// <returns></returns>
    public Step<TIn, TNext> Pipe<TNext>(Step<TOut, TNext> asyncNext)
    {
        var func = this.f; // Capture to avoid struct 'this' issues
        return async input => await asyncNext(func(input));
    }

    /// <summary>
    /// Functor/Map operation.
    /// </summary>
    /// <returns></returns>
    public SyncStep<TIn, TNext> Map<TNext>(Func<TOut, TNext> map)
    {
        var func = this.f; // Capture to avoid struct 'this' issues
        return new(input => map(func(input)));
    }

    /// <summary>
    /// Bind operation for monadic composition.
    /// </summary>
    /// <returns></returns>
    public SyncStep<TIn, TNext> Bind<TNext>(Func<TOut, SyncStep<TIn, TNext>> binder)
    {
        var func = this.f; // Capture to avoid struct 'this' issues
        return new(input =>
        {
            var intermediate = func(input);
            var nextStep = binder(intermediate);
            return nextStep.Invoke(input);
        });
    }

    /// <summary>
    /// Equality (by delegate reference).
    /// </summary>
    /// <returns></returns>
    public bool Equals(SyncStep<TIn, TOut> other) => ReferenceEquals(this.f, other.f);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SyncStep<TIn, TOut> o && this.Equals(o);

    /// <inheritdoc/>
    public override int GetHashCode() => this.f?.GetHashCode() ?? 0;

    /// <summary>
    /// Implicit conversion from function.
    /// </summary>
    public static implicit operator SyncStep<TIn, TOut>(Func<TIn, TOut> f) => new(f);

    /// <summary>
    /// Implicit conversion to async Step.
    /// </summary>
    public static implicit operator Step<TIn, TOut>(SyncStep<TIn, TOut> syncStep) => syncStep.ToAsync();

    /// <summary>
    /// Gets static helper for identity step.
    /// </summary>
    public static SyncStep<TIn, TIn> Identity => new(x => x);
}

/// <summary>
/// Extensions for integrating SyncStep with our existing async system.
/// </summary>
public static class SyncStepExtensions
{
    /// <summary>
    /// Lift a pure function to SyncStep.
    /// </summary>
    /// <returns></returns>
    public static SyncStep<TIn, TOut> ToSyncStep<TIn, TOut>(this Func<TIn, TOut> func)
        => new(func);

    /// <summary>
    /// Convert async Step to sync (blocking - use with caution).
    /// </summary>
    /// <returns></returns>
    public static SyncStep<TIn, TOut> ToSync<TIn, TOut>(this Step<TIn, TOut> asyncStep)
        => new(input => Task.Run(() => asyncStep(input)).GetAwaiter().GetResult());

    /// <summary>
    /// Compose sync step with async step.
    /// </summary>
    /// <returns></returns>
    public static Step<TIn, TNext> Then<TIn, TMid, TNext>(
        this SyncStep<TIn, TMid> syncStep,
        Step<TMid, TNext> asyncStep)
        => async input =>
        {
            var intermediate = syncStep.Invoke(input);
            return await asyncStep(intermediate);
        };

    /// <summary>
    /// Compose async step with sync step.
    /// </summary>
    /// <returns></returns>
    public static Step<TIn, TNext> Then<TIn, TMid, TNext>(
        this Step<TIn, TMid> asyncStep,
        SyncStep<TMid, TNext> syncStep)
        => async input =>
        {
            var intermediate = await asyncStep(input);
            return syncStep.Invoke(intermediate);
        };

    /// <summary>
    /// Convert SyncStep to Result-based operation.
    /// </summary>
    /// <returns></returns>
    public static SyncStep<TIn, Result<TOut, Exception>> TrySync<TIn, TOut>(
        this SyncStep<TIn, TOut> syncStep)
        => new(input =>
        {
            try
            {
                var result = syncStep.Invoke(input);
                return Result<TOut, Exception>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TOut, Exception>.Failure(ex);
            }
        });

    /// <summary>
    /// Convert SyncStep to Option-based operation.
    /// </summary>
    /// <returns></returns>
    public static SyncStep<TIn, Option<TOut>> TryOption<TIn, TOut>(
        this SyncStep<TIn, TOut> syncStep,
        Func<TOut, bool> predicate)
        => new(input =>
        {
            try
            {
                var result = syncStep.Invoke(input);
                return predicate(result) ? Option<TOut>.Some(result) : Option<TOut>.None();
            }
            catch
            {
                return Option<TOut>.None();
            }
        });
}
