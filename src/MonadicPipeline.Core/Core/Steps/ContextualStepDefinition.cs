// <copyright file="ContextualStepDefinition.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Steps;

/// <summary>
/// Append-only builder for contextual pipelines:
/// steps that depend on a context (Reader) and may log/trace (Writer).
/// </summary>
public struct ContextualStepDefinition<TIn, TOut, TContext>
{
    private readonly ContextualStep<TIn, TOut, TContext> compiled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextualStepDefinition{TIn, TOut, TContext}"/> struct.
    /// Constructor: from "context → pure step".
    /// </summary>
    public ContextualStepDefinition(Step<TContext, Step<TIn, TOut>> pure)
    {
        this.compiled = async (input, context) =>
        {
            var innerStep = await pure(context);  // Step<TIn,TOut>
            var result = await innerStep(input);  // apply inner step
            return (result, []);
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextualStepDefinition{TIn, TOut, TContext}"/> struct.
    /// Constructor: from compiled contextual step.
    /// </summary>
    public ContextualStepDefinition(ContextualStep<TIn, TOut, TContext> step)
    {
        this.compiled = step;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextualStepDefinition{TIn, TOut, TContext}"/> struct.
    /// Constructor: from pure function.
    /// </summary>
    public ContextualStepDefinition(Func<TIn, TOut> func, string? log = null)
    {
        this.compiled = ContextualStep.LiftPure<TIn, TOut, TContext>(func, log);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextualStepDefinition{TIn, TOut, TContext}"/> struct.
    /// Constructor: from pure Step{TIn,TOut}.
    /// </summary>
    /// <param name="pure">The pure step to lift.</param>
    /// <param name="log">Optional logging string.</param>
    public ContextualStepDefinition(Step<TIn, TOut> pure, string? log = null)
    {
        this.compiled = ContextualStep.FromPure<TIn, TOut, TContext>(pure, log);
    }

    /// <summary>
    /// Static Lift helpers.
    /// </summary>
    /// <returns></returns>
    public static ContextualStepDefinition<TIn, TOut, TContext> LiftPure(Func<TIn, TOut> func, string? log = null)
        => new(func, log);

    public static ContextualStepDefinition<TIn, TOut, TContext> FromPure(Step<TIn, TOut> step, string? log = null) => new(step, log);

    public static ContextualStepDefinition<TIn, TOut, TContext> FromContext(Step<TContext, Step<TIn, TOut>> ctxStep) => new(ctxStep);

    /// <summary>
    /// Implicit conversion to compiled step.
    /// </summary>
    public static implicit operator ContextualStep<TIn, TOut, TContext>(ContextualStepDefinition<TIn, TOut, TContext> d)
        => d.compiled;

    /// <summary>
    /// Pipe method: append contextual step.
    /// </summary>
    /// <returns></returns>
    public ContextualStepDefinition<TIn, TNext, TContext> Pipe<TNext>(ContextualStep<TOut, TNext, TContext> next)
    {
        var newCompiled = this.compiled.Then(next);
        return new ContextualStepDefinition<TIn, TNext, TContext>(newCompiled);
    }

    /// <summary>
    /// Pipe method: append pure step.
    /// </summary>
    /// <returns></returns>
    public ContextualStepDefinition<TIn, TNext, TContext> Pipe<TNext>(Step<TOut, TNext> pure, string? log = null)
    {
        var contextualNext = ContextualStep.FromPure<TOut, TNext, TContext>(pure, log);
        return this.Pipe(contextualNext);
    }

    /// <summary>
    /// Pipe method: append pure function.
    /// </summary>
    /// <returns></returns>
    public ContextualStepDefinition<TIn, TNext, TContext> Pipe<TNext>(Func<TOut, TNext> func, string? log = null)
    {
        var contextualNext = ContextualStep.LiftPure<TOut, TNext, TContext>(func, log);
        return this.Pipe(contextualNext);
    }

    /// <summary>
    /// Execute pipeline (synchronous convenience method).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<(TOut result, List<string> logs)> InvokeAsync(TIn input, TContext context)
        => await this.compiled(input, context);

    /// <summary>
    /// Execute pipeline (synchronous convenience method).
    /// </summary>
    /// <returns></returns>
    public (TOut result, List<string> logs) Invoke(TIn input, TContext context)
        => this.InvokeAsync(input, context).GetAwaiter().GetResult();

    /// <summary>
    /// Forget context: collapse into pure Step.
    /// </summary>
    /// <returns></returns>
    public Step<TIn, (TOut result, List<string> logs)> Forget(TContext context)
        => this.compiled.Forget(context);

    /// <summary>
    /// Forget context and logs: collapse to pure result Step.
    /// </summary>
    /// <returns></returns>
    public Step<TIn, TOut> ForgetAll(TContext context)
        => this.compiled.ForgetAll(context);

    /// <summary>
    /// Add logging to the current step.
    /// </summary>
    /// <returns></returns>
    public ContextualStepDefinition<TIn, TOut, TContext> WithLog(string logMessage)
    {
        var newCompiled = this.compiled.WithLog(logMessage);
        return new ContextualStepDefinition<TIn, TOut, TContext>(newCompiled);
    }

    /// <summary>
    /// Add conditional logging based on result.
    /// </summary>
    /// <returns></returns>
    public ContextualStepDefinition<TIn, TOut, TContext> WithConditionalLog(Func<TOut, string?> logFunction)
    {
        var newCompiled = this.compiled.WithConditionalLog(logFunction);
        return new ContextualStepDefinition<TIn, TOut, TContext>(newCompiled);
    }

    /// <summary>
    /// Convert to Result-based contextual step for error handling.
    /// </summary>
    /// <returns></returns>
    public ContextualStepDefinition<TIn, Result<TOut, Exception>, TContext> TryStep()
    {
        var newCompiled = this.compiled.TryStep();
        return new ContextualStepDefinition<TIn, Result<TOut, Exception>, TContext>(newCompiled);
    }

    /// <summary>
    /// Convert to Option-based contextual step.
    /// </summary>
    /// <returns></returns>
    public ContextualStepDefinition<TIn, Option<TOut>, TContext> TryOption(Func<TOut, bool> predicate)
    {
        var newCompiled = this.compiled.TryOption(predicate);
        return new ContextualStepDefinition<TIn, Option<TOut>, TContext>(newCompiled);
    }
}

/// <summary>
/// Helper class for creating contextual step definitions.
/// </summary>
public static class ContextualDef
{
    /// <summary>
    /// Create from pure function.
    /// </summary>
    /// <returns></returns>
    public static ContextualStepDefinition<TIn, TOut, TContext> LiftPure<TIn, TOut, TContext>(
        Func<TIn, TOut> func,
        string? log = null)
        => ContextualStepDefinition<TIn, TOut, TContext>.LiftPure(func, log);

    /// <summary>
    /// Create from pure Step.
    /// </summary>
    /// <returns></returns>
    public static ContextualStepDefinition<TIn, TOut, TContext> FromPure<TIn, TOut, TContext>(
        Step<TIn, TOut> step,
        string? log = null)
        => ContextualStepDefinition<TIn, TOut, TContext>.FromPure(step, log);

    /// <summary>
    /// Create from context-dependent step factory.
    /// </summary>
    /// <returns></returns>
    public static ContextualStepDefinition<TIn, TOut, TContext> FromContext<TIn, TOut, TContext>(
        Step<TContext, Step<TIn, TOut>> ctxStep)
        => ContextualStepDefinition<TIn, TOut, TContext>.FromContext(ctxStep);

    /// <summary>
    /// Identity contextual step.
    /// </summary>
    /// <returns></returns>
    public static ContextualStepDefinition<TIn, TIn, TContext> Identity<TIn, TContext>(string? log = null)
        => LiftPure<TIn, TIn, TContext>(x => x, log);
}
