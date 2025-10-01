namespace LangChainPipeline.Core.Steps;

/// <summary>
/// Minimal append-only builder for composing <see cref="Step{TIn, TOut}"/> instances
/// using the <c>|</c> pipeline syntax that the CLI DSL expects.
/// </summary>
/// <typeparam name="TIn">Input type.</typeparam>
/// <typeparam name="TOut">Output type.</typeparam>
public readonly struct StepDefinition<TIn, TOut>
{
    private readonly Step<TIn, TOut> _compiled;

    /// <summary>
    /// Create definition from an asynchronous step.
    /// </summary>
    public StepDefinition(Step<TIn, TOut> step)
    {
        _compiled = step ?? throw new ArgumentNullException(nameof(step));
    }

    /// <summary>
    /// Create definition from a synchronous function.
    /// </summary>
    public StepDefinition(Func<TIn, TOut> func)
        : this(async input => await Task.FromResult(func(input)).ConfigureAwait(false))
    {
    }

    /// <summary>
    /// Compose with another step, returning a new definition representing the appended pipeline.
    /// </summary>
    public StepDefinition<TIn, TNext> Pipe<TNext>(Step<TOut, TNext> next)
    {
        if (next is null) throw new ArgumentNullException(nameof(next));

        var current = _compiled;
        return new StepDefinition<TIn, TNext>(async input =>
        {
            var mid = await current(input).ConfigureAwait(false);
            return await next(mid).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Compose with a synchronous function.
    /// </summary>
    public StepDefinition<TIn, TNext> Pipe<TNext>(Func<TOut, TNext> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));
        Step<TOut, TNext> next = async value => await Task.FromResult(func(value)).ConfigureAwait(false);
        return Pipe(next);
    }

    /// <summary>
    /// Build the composed step.
    /// </summary>
    public Step<TIn, TOut> Build() => _compiled;

    /// <summary>
    /// Operator syntax sugar to mirror the old DSL pipeline semantics.
    /// </summary>
    public static StepDefinition<TIn, TOut> operator |(StepDefinition<TIn, TOut> definition, Step<TOut, TOut> next)
        => definition.Pipe(next);

    /// <summary>
    /// Operator overload for synchronous functions.
    /// </summary>
    public static StepDefinition<TIn, TOut> operator |(StepDefinition<TIn, TOut> definition, Func<TOut, TOut> func)
        => definition.Pipe(func);
}
