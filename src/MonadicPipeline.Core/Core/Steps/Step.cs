#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
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
    private readonly Func<TIn, TOut> _f;

    public SyncStep(Func<TIn, TOut> f)
        => _f = f ?? throw new ArgumentNullException(nameof(f));

    /// <summary>
    /// Execute the synchronous step
    /// </summary>
    public TOut Invoke(TIn input) => _f(input);

    /// <summary>
    /// Convert to async Step
    /// </summary>
    public Step<TIn, TOut> ToAsync()
    {
        Func<TIn, TOut> func = _f; // Capture to avoid struct 'this' issues
        return input => Task.FromResult(func(input));
    }

    /// <summary>
    /// Pipe composition (heterogeneous) - synchronous version
    /// </summary>
    public SyncStep<TIn, TNext> Pipe<TNext>(SyncStep<TOut, TNext> next)
    {
        Func<TIn, TOut> func = _f; // Capture to avoid struct 'this' issues
        return new(input => next.Invoke(func(input)));
    }

    /// <summary>
    /// Pipe with async step
    /// </summary>
    public Step<TIn, TNext> Pipe<TNext>(Step<TOut, TNext> asyncNext)
    {
        Func<TIn, TOut> func = _f; // Capture to avoid struct 'this' issues
        return async input => await asyncNext(func(input));
    }

    /// <summary>
    /// Functor/Map operation
    /// </summary>
    public SyncStep<TIn, TNext> Map<TNext>(Func<TOut, TNext> map)
    {
        Func<TIn, TOut> func = _f; // Capture to avoid struct 'this' issues
        return new(input => map(func(input)));
    }

    /// <summary>
    /// Bind operation for monadic composition
    /// </summary>
    public SyncStep<TIn, TNext> Bind<TNext>(Func<TOut, SyncStep<TIn, TNext>> binder)
    {
        Func<TIn, TOut> func = _f; // Capture to avoid struct 'this' issues
        return new(input =>
        {
            TOut? intermediate = func(input);
            SyncStep<TIn, TNext> nextStep = binder(intermediate);
            return nextStep.Invoke(input);
        });
    }

    /// <summary>
    /// Equality (by delegate reference)
    /// </summary>
    public bool Equals(SyncStep<TIn, TOut> other) => ReferenceEquals(_f, other._f);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SyncStep<TIn, TOut> o && Equals(o);
    /// <inheritdoc/>
    public override int GetHashCode() => _f?.GetHashCode() ?? 0;

    /// <summary>
    /// Implicit conversion from function
    /// </summary>
    public static implicit operator SyncStep<TIn, TOut>(Func<TIn, TOut> f) => new(f);

    /// <summary>
    /// Implicit conversion to async Step
    /// </summary>
    public static implicit operator Step<TIn, TOut>(SyncStep<TIn, TOut> syncStep) => syncStep.ToAsync();

    /// <summary>
    /// Static helper for identity step
    /// </summary>
    public static SyncStep<TIn, TIn> Identity => new(x => x);
}

/// <summary>
/// Extensions for integrating SyncStep with our existing async system
/// </summary>
public static class SyncStepExtensions
{
    /// <summary>
    /// Lift a pure function to SyncStep
    /// </summary>
    public static SyncStep<TIn, TOut> ToSyncStep<TIn, TOut>(this Func<TIn, TOut> func)
        => new(func);

    /// <summary>
    /// Convert async Step to sync (blocking - use with caution)
    /// </summary>
    public static SyncStep<TIn, TOut> ToSync<TIn, TOut>(this Step<TIn, TOut> asyncStep)
        => new(input => Task.Run(() => asyncStep(input)).GetAwaiter().GetResult());

    /// <summary>
    /// Compose sync step with async step
    /// </summary>
    public static Step<TIn, TNext> Then<TIn, TMid, TNext>(
        this SyncStep<TIn, TMid> syncStep,
        Step<TMid, TNext> asyncStep)
        => async input =>
        {
            TMid? intermediate = syncStep.Invoke(input);
            return await asyncStep(intermediate);
        };

    /// <summary>
    /// Compose async step with sync step
    /// </summary>
    public static Step<TIn, TNext> Then<TIn, TMid, TNext>(
        this Step<TIn, TMid> asyncStep,
        SyncStep<TMid, TNext> syncStep)
        => async input =>
        {
            TMid? intermediate = await asyncStep(input);
            return syncStep.Invoke(intermediate);
        };

    /// <summary>
    /// Convert SyncStep to Result-based operation
    /// </summary>
    public static SyncStep<TIn, Result<TOut, Exception>> TrySync<TIn, TOut>(
        this SyncStep<TIn, TOut> syncStep)
        => new(input =>
        {
            try
            {
                TOut? result = syncStep.Invoke(input);
                return Result<TOut, Exception>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TOut, Exception>.Failure(ex);
            }
        });

    /// <summary>
    /// Convert SyncStep to Option-based operation
    /// </summary>
    public static SyncStep<TIn, Option<TOut>> TryOption<TIn, TOut>(
        this SyncStep<TIn, TOut> syncStep,
        Func<TOut, bool> predicate)
        => new(input =>
        {
            try
            {
                TOut? result = syncStep.Invoke(input);
                return predicate(result) ? Option<TOut>.Some(result) : Option<TOut>.None();
            }
            catch
            {
                return Option<TOut>.None();
            }
        });
}
