#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Contextual Step Definition - Append-only Builder Pattern
// Implements the Reader/Writer monad pattern with fluent composition
// ==========================================================

namespace LangChainPipeline.Core.Steps;

/// <summary>
/// Append-only builder for contextual pipelines:
/// steps that depend on a context (Reader) and may log/trace (Writer).
/// </summary>
public struct ContextualStepDefinition<TIn, TOut, TContext>
{
    private ContextualStep<TIn, TOut, TContext> _compiled;

    /// <summary>
    /// Constructor: from "context → pure step"
    /// </summary>
    public ContextualStepDefinition(Step<TContext, Step<TIn, TOut>> pure)
    {
        this._compiled = async (input, context) =>
        {
            Step<TIn, TOut> innerStep = await pure(context);  // Step<TIn,TOut>
            TOut? result = await innerStep(input);  // apply inner step
            return (result, []);
        };
    }

    /// <summary>
    /// Constructor: from compiled contextual step
    /// </summary>
    public ContextualStepDefinition(ContextualStep<TIn, TOut, TContext> step)
    {
        this._compiled = step;
    }

    /// <summary>
    /// Constructor: from pure function
    /// </summary>
    public ContextualStepDefinition(Func<TIn, TOut> func, string? log = null)
    {
        this._compiled = ContextualStep.LiftPure<TIn, TOut, TContext>(func, log);
    }

    /// <summary>
    /// Constructor: from pure Step{TIn,TOut}
    /// </summary>
    /// <param name="pure">The pure step to lift.</param>
    /// <param name="log">Optional logging string.</param>
    public ContextualStepDefinition(Step<TIn, TOut> pure, string? log = null)
    {
        this._compiled = ContextualStep.FromPure<TIn, TOut, TContext>(pure, log);
    }

    /// <summary>
    /// Static Lift helpers
    /// </summary>
    public static ContextualStepDefinition<TIn, TOut, TContext> LiftPure(Func<TIn, TOut> func, string? log = null)
        => new(func, log);

    public static ContextualStepDefinition<TIn, TOut, TContext> FromPure(Step<TIn, TOut> step, string? log = null) => new(step, log);

    public static ContextualStepDefinition<TIn, TOut, TContext> FromContext(Step<TContext, Step<TIn, TOut>> ctxStep) => new(ctxStep);

    /// <summary>
    /// Implicit conversion to compiled step
    /// </summary>
    public static implicit operator ContextualStep<TIn, TOut, TContext>(ContextualStepDefinition<TIn, TOut, TContext> d)
        => d._compiled;

    /// <summary>
    /// Pipe method: append contextual step
    /// </summary>
    public ContextualStepDefinition<TIn, TNext, TContext> Pipe<TNext>(ContextualStep<TOut, TNext, TContext> next)
    {
        ContextualStep<TIn, TNext, TContext> newCompiled = _compiled.Then(next);
        return new ContextualStepDefinition<TIn, TNext, TContext>(newCompiled);
    }

    /// <summary>
    /// Pipe method: append pure step
    /// </summary>
    public ContextualStepDefinition<TIn, TNext, TContext> Pipe<TNext>(Step<TOut, TNext> pure, string? log = null)
    {
        ContextualStep<TOut, TNext, TContext> contextualNext = ContextualStep.FromPure<TOut, TNext, TContext>(pure, log);
        return Pipe(contextualNext);
    }

    /// <summary>
    /// Pipe method: append pure function
    /// </summary>
    public ContextualStepDefinition<TIn, TNext, TContext> Pipe<TNext>(Func<TOut, TNext> func, string? log = null)
    {
        ContextualStep<TOut, TNext, TContext> contextualNext = ContextualStep.LiftPure<TOut, TNext, TContext>(func, log);
        return Pipe(contextualNext);
    }

    /// <summary>
    /// Execute pipeline (synchronous convenience method)
    /// </summary>
    public async Task<(TOut result, List<string> logs)> InvokeAsync(TIn input, TContext context)
        => await _compiled(input, context);

    /// <summary>
    /// Execute pipeline (synchronous convenience method)
    /// </summary>
    public (TOut result, List<string> logs) Invoke(TIn input, TContext context)
        => InvokeAsync(input, context).GetAwaiter().GetResult();

    /// <summary>
    /// Forget context: collapse into pure Step
    /// </summary>
    public Step<TIn, (TOut result, List<string> logs)> Forget(TContext context)
        => _compiled.Forget(context);

    /// <summary>
    /// Forget context and logs: collapse to pure result Step
    /// </summary>
    public Step<TIn, TOut> ForgetAll(TContext context)
        => _compiled.ForgetAll(context);

    /// <summary>
    /// Add logging to the current step
    /// </summary>
    public ContextualStepDefinition<TIn, TOut, TContext> WithLog(string logMessage)
    {
        ContextualStep<TIn, TOut, TContext> newCompiled = _compiled.WithLog(logMessage);
        return new ContextualStepDefinition<TIn, TOut, TContext>(newCompiled);
    }

    /// <summary>
    /// Add conditional logging based on result
    /// </summary>
    public ContextualStepDefinition<TIn, TOut, TContext> WithConditionalLog(Func<TOut, string?> logFunction)
    {
        ContextualStep<TIn, TOut, TContext> newCompiled = _compiled.WithConditionalLog(logFunction);
        return new ContextualStepDefinition<TIn, TOut, TContext>(newCompiled);
    }

    /// <summary>
    /// Convert to Result-based contextual step for error handling
    /// </summary>
    public ContextualStepDefinition<TIn, Result<TOut, Exception>, TContext> TryStep()
    {
        ContextualStep<TIn, Result<TOut, Exception>, TContext> newCompiled = _compiled.TryStep();
        return new ContextualStepDefinition<TIn, Result<TOut, Exception>, TContext>(newCompiled);
    }

    /// <summary>
    /// Convert to Option-based contextual step
    /// </summary>
    public ContextualStepDefinition<TIn, Option<TOut>, TContext> TryOption(Func<TOut, bool> predicate)
    {
        ContextualStep<TIn, Option<TOut>, TContext> newCompiled = _compiled.TryOption(predicate);
        return new ContextualStepDefinition<TIn, Option<TOut>, TContext>(newCompiled);
    }
}

/// <summary>
/// Helper class for creating contextual step definitions
/// </summary>
public static class ContextualDef
{
    /// <summary>
    /// Create from pure function
    /// </summary>
    public static ContextualStepDefinition<TIn, TOut, TContext> LiftPure<TIn, TOut, TContext>(
        Func<TIn, TOut> func,
        string? log = null)
        => ContextualStepDefinition<TIn, TOut, TContext>.LiftPure(func, log);

    /// <summary>
    /// Create from pure Step
    /// </summary>
    public static ContextualStepDefinition<TIn, TOut, TContext> FromPure<TIn, TOut, TContext>(
        Step<TIn, TOut> step,
        string? log = null)
        => ContextualStepDefinition<TIn, TOut, TContext>.FromPure(step, log);

    /// <summary>
    /// Create from context-dependent step factory
    /// </summary>
    public static ContextualStepDefinition<TIn, TOut, TContext> FromContext<TIn, TOut, TContext>(
        Step<TContext, Step<TIn, TOut>> ctxStep)
        => ContextualStepDefinition<TIn, TOut, TContext>.FromContext(ctxStep);

    /// <summary>
    /// Identity contextual step
    /// </summary>
    public static ContextualStepDefinition<TIn, TIn, TContext> Identity<TIn, TContext>(string? log = null)
        => LiftPure<TIn, TIn, TContext>(x => x, log);
}
