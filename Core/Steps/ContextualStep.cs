// ==========================================================
// Contextual Step System - Reader/Writer Monad Integration
// Extends our existing monadic operations with contextual dependencies
// ==========================================================

namespace LangChainPipeline.Core.Steps;

/// <summary>
/// A contextual step that depends on context and produces logs (Reader + Writer pattern)
/// </summary>
public delegate Task<(TOut result, List<string> logs)> ContextualStep<TIn, TOut, TContext>(TIn input, TContext context);

/// <summary>
/// Static factory methods for contextual steps
/// </summary>
public static class ContextualStep
{
    /// <summary>
    /// Lift a pure function into a contextual step
    /// </summary>
    public static ContextualStep<TIn, TOut, TContext> LiftPure<TIn, TOut, TContext>(
        Func<TIn, TOut> func, 
        string? log = null)
        => async (input, context) =>
        {
            await Task.Yield();
            var result = func(input);
            var logs = log != null ? new List<string> { log } : new List<string>();
            return (result, logs);
        };

    /// <summary>
    /// Create from pure Step
    /// </summary>
    public static ContextualStep<TIn, TOut, TContext> FromPure<TIn, TOut, TContext>(
        Step<TIn, TOut> step, 
        string? log = null)
        => async (input, context) =>
        {
            var result = await step(input);
            var logs = log != null ? new List<string> { log } : new List<string>();
            return (result, logs);
        };

    /// <summary>
    /// Create from context-dependent step factory
    /// </summary>
    public static ContextualStep<TIn, TOut, TContext> FromContext<TIn, TOut, TContext>(
        Step<TContext, Step<TIn, TOut>> contextStep)
        => async (input, context) =>
        {
            var innerStep = await contextStep(context);
            var result = await innerStep(input);
            return (result, new List<string>());
        };

    /// <summary>
    /// Identity contextual step
    /// </summary>
    public static ContextualStep<TIn, TIn, TContext> Identity<TIn, TContext>(string? log = null)
        => LiftPure<TIn, TIn, TContext>(x => x, log);
}

/// <summary>
/// Extension methods for contextual step composition
/// </summary>
public static class ContextualStepExtensions
{
    /// <summary>
    /// Kleisli composition for contextual steps
    /// </summary>
    public static ContextualStep<TIn, TOut, TContext> Then<TIn, TMid, TOut, TContext>(
        this ContextualStep<TIn, TMid, TContext> first,
        ContextualStep<TMid, TOut, TContext> second)
        => async (input, context) =>
        {
            var (midResult, firstLogs) = await first(input, context);
            var (finalResult, secondLogs) = await second(midResult, context);
            
            var combinedLogs = new List<string>();
            combinedLogs.AddRange(firstLogs);
            combinedLogs.AddRange(secondLogs);
            
            return (finalResult, combinedLogs);
        };

    /// <summary>
    /// Map over the result while preserving context and logs
    /// </summary>
    public static ContextualStep<TIn, TOut, TContext> Map<TIn, TMid, TOut, TContext>(
        this ContextualStep<TIn, TMid, TContext> step,
        Func<TMid, TOut> mapper,
        string? log = null)
        => async (input, context) =>
        {
            var (midResult, logs) = await step(input, context);
            var finalResult = mapper(midResult);
            
            if (log != null)
                logs.Add(log);
            
            return (finalResult, logs);
        };

    /// <summary>
    /// Add logging to a contextual step
    /// </summary>
    public static ContextualStep<TIn, TOut, TContext> WithLog<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        string logMessage)
        => async (input, context) =>
        {
            var (result, logs) = await step(input, context);
            logs.Add(logMessage);
            return (result, logs);
        };

    /// <summary>
    /// Add conditional logging based on result
    /// </summary>
    public static ContextualStep<TIn, TOut, TContext> WithConditionalLog<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        Func<TOut, string?> logFunction)
        => async (input, context) =>
        {
            var (result, logs) = await step(input, context);
            var conditionalLog = logFunction(result);
            if (conditionalLog != null)
                logs.Add(conditionalLog);
            return (result, logs);
        };

    /// <summary>
    /// Forget the context and collapse to a pure step
    /// </summary>
    public static Step<TIn, (TOut result, List<string> logs)> Forget<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        TContext context)
        => input => step(input, context);

    /// <summary>
    /// Extract just the result, discarding logs and context
    /// </summary>
    public static Step<TIn, TOut> ForgetAll<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        TContext context)
        => async input =>
        {
            var (result, _) = await step(input, context);
            return result;
        };

    /// <summary>
    /// Convert to Result-based contextual step for error handling
    /// </summary>
    public static ContextualStep<TIn, Result<TOut, Exception>, TContext> TryStep<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step)
        => async (input, context) =>
        {
            try
            {
                var (result, logs) = await step(input, context);
                return (Result<TOut, Exception>.Success(result), logs);
            }
            catch (Exception ex)
            {
                return (Result<TOut, Exception>.Failure(ex), new List<string> { $"Error: {ex.Message}" });
            }
        };

    /// <summary>
    /// Convert to Option-based contextual step
    /// </summary>
    public static ContextualStep<TIn, Option<TOut>, TContext> TryOption<TIn, TOut, TContext>(
        this ContextualStep<TIn, TOut, TContext> step,
        Func<TOut, bool> predicate)
        => async (input, context) =>
        {
            try
            {
                var (result, logs) = await step(input, context);
                var option = predicate(result) ? Option<TOut>.Some(result) : Option<TOut>.None();
                return (option, logs);
            }
            catch (Exception ex)
            {
                var logs = new List<string> { $"Exception converted to None: {ex.Message}" };
                return (Option<TOut>.None(), logs);
            }
        };
}
