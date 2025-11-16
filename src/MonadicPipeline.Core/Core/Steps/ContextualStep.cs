// <copyright file="ContextualStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Steps;

/// <summary>
/// A contextual step that depends on context and produces logs (Reader + Writer pattern).
/// </summary>
/// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
public delegate Task<(TOut result, List<string> logs)> ContextualStep<in TIn, TOut, in TContext>(TIn input, TContext context);

/// <summary>
/// Static factory methods for contextual steps.
/// </summary>
public static class ContextualStep
{
    /// <summary>
    /// Lift a pure function into a contextual step.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TOut, TContext> LiftPure<TIn, TOut, TContext>(
        Func<TIn, TOut> func,
        string? log = null)
        => async (input, context) =>
        {
            await Task.Yield();
            TOut? result = func(input);
            List<string> logs = log != null ? [log] : new List<string>();
            return (result, logs);
        };

    /// <summary>
    /// Create from pure Step.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TOut, TContext> FromPure<TIn, TOut, TContext>(
        Step<TIn, TOut> step,
        string? log = null)
        => async (input, context) =>
        {
            TOut? result = await step(input);
            List<string> logs = log != null ? [log] : new List<string>();
            return (result, logs);
        };

    /// <summary>
    /// Create from context-dependent step factory.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TOut, TContext> FromContext<TIn, TOut, TContext>(
        Step<TContext, Step<TIn, TOut>> contextStep)
        => async (input, context) =>
        {
            Step<TIn, TOut> innerStep = await contextStep(context);
            TOut? result = await innerStep(input);
            return (result, []);
        };

    /// <summary>
    /// Identity contextual step.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TIn, TContext> Identity<TIn, TContext>(string? log = null)
        => LiftPure<TIn, TIn, TContext>(x => x, log);
}

/// <summary>
/// Extension methods for contextual step composition.
/// </summary>
public static class ContextualStepExtensions
{
    /// <summary>
    /// Kleisli composition for contextual steps.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TOut, TContext> Then<TIn, TMid, TOut, TContext>(
        this ContextualStep<TIn, TMid, TContext> first,
        ContextualStep<TMid, TOut, TContext> second)
        => async (input, context) =>
        {
            (TMid? midResult, List<string>? firstLogs) = await first(input, context);
            (TOut? finalResult, List<string>? secondLogs) = await second(midResult, context);

            List<string> combinedLogs = new List<string>();
            combinedLogs.AddRange(firstLogs);
            combinedLogs.AddRange(secondLogs);

            return (finalResult, combinedLogs);
        };

    /// <summary>
    /// Map over the result while preserving context and logs.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TOut, TContext> Map<TIn, TMid, TOut, TContext>(
        this ContextualStep<TIn, TMid, TContext> step,
        Func<TMid, TOut> mapper,
        string? log = null)
        => async (input, context) =>
        {
            (TMid? midResult, List<string>? logs) = await step(input, context);
            TOut? finalResult = mapper(midResult);

            if (log != null)
            {
                logs.Add(log);
            }

            return (finalResult, logs);
        };

    /// <summary>
    /// Add logging to a contextual step.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TOut, TContext> WithLog<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        string logMessage)
        => async (input, context) =>
        {
            (TOut? result, List<string>? logs) = await step(input, context);
            logs.Add(logMessage);
            return (result, logs);
        };

    /// <summary>
    /// Add conditional logging based on result.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, TOut, TContext> WithConditionalLog<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        Func<TOut, string?> logFunction)
        => async (input, context) =>
        {
            (TOut? result, List<string>? logs) = await step(input, context);
            string? conditionalLog = logFunction(result);
            if (conditionalLog != null)
            {
                logs.Add(conditionalLog);
            }

            return (result, logs);
        };

    /// <summary>
    /// Forget the context and collapse to a pure step.
    /// </summary>
    /// <returns></returns>
    public static Step<TIn, (TOut result, List<string> logs)> Forget<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        TContext context)
        => input => step(input, context);

    /// <summary>
    /// Extract just the result, discarding logs and context.
    /// </summary>
    /// <returns></returns>
    public static Step<TIn, TOut> ForgetAll<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        TContext context)
        => async input =>
        {
            (TOut result, List<string> _) = await step(input, context);
            return result;
        };

    /// <summary>
    /// Convert to Result-based contextual step for error handling.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, Result<TOut, Exception>, TContext> TryStep<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step)
        => async (input, context) =>
        {
            try
            {
                (TOut result, List<string> logs) = await step(input, context);
                return (Result<TOut, Exception>.Success(result), logs);
            }
            catch (Exception ex)
            {
                return (Result<TOut, Exception>.Failure(ex), [$"Error: {ex.Message}"]);
            }
        };

    /// <summary>
    /// Convert to Option-based contextual step.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<TIn, Option<TOut>, TContext> TryOption<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        Func<TOut, bool> predicate)
        => async (input, context) =>
        {
            try
            {
                (TOut result, List<string> logs) = await step(input, context);
                Option<TOut> option = predicate(result) ? Option<TOut>.Some(result) : Option<TOut>.None();
                return (option, logs);
            }
            catch (Exception ex)
            {
                List<string> logs = new List<string> { $"Exception converted to None: {ex.Message}" };
                return (Option<TOut>.None(), logs);
            }
        };
}
