// <copyright file="MonadicToolExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

/// <summary>
/// Extensions to integrate tools with monadic operations.
/// </summary>
public static class MonadicToolExtensions
{
    /// <summary>
    /// Convert tool to Step for pipeline integration.
    /// </summary>
    /// <returns></returns>
    public static Step<string, Result<string, string>> ToStep(this ITool tool)
        => async input => await tool.InvokeAsync(input);

    /// <summary>
    /// Convert tool to Kleisli arrow.
    /// </summary>
    /// <returns></returns>
    public static KleisliResult<string, string, string> ToKleisli(this ITool tool)
        => async input => await tool.InvokeAsync(input);

    /// <summary>
    /// Chain tools together monadically.
    /// </summary>
    /// <returns></returns>
    public static Step<string, Result<string, string>> Then(this ITool first, ITool second)
    {
        return async input =>
        {
            Result<string, string> firstResult = await first.InvokeAsync(input);
            return await firstResult.Match(
                success => second.InvokeAsync(success),
                failure => Task.FromResult(Result<string, string>.Failure(failure)));
        };
    }

    /// <summary>
    /// Try multiple tools until one succeeds.
    /// </summary>
    /// <returns></returns>
    public static Step<string, Result<string, string>> OrElse(this ITool first, ITool fallback)
    {
        return async input =>
        {
            Result<string, string> firstResult = await first.InvokeAsync(input);
            return firstResult.IsSuccess
                ? firstResult
                : await fallback.InvokeAsync(input);
        };
    }

    /// <summary>
    /// Map tool result.
    /// </summary>
    /// <returns></returns>
    public static Step<string, Result<TOut, string>> Map<TOut>(this ITool tool, Func<string, TOut> mapper)
    {
        return async input =>
        {
            Result<string, string> result = await tool.InvokeAsync(input);
            return result.Map(mapper);
        };
    }

    /// <summary>
    /// Execute tool with contextual step integration.
    /// </summary>
    /// <returns></returns>
    public static ContextualStep<string, Result<string, string>, TContext> ToContextual<TContext>(
        this ITool tool,
        string? logMessage = null)
    {
        return async (input, context) =>
        {
            Result<string, string> result = await tool.InvokeAsync(input);
            string log = logMessage ?? $"Tool '{tool.Name}' executed";
            return (result, [log]);
        };
    }
}

/// <summary>
/// Monadic tool builder for creating sophisticated tool compositions.
/// </summary>
public static class ToolBuilder
{
    /// <summary>
    /// Create a tool that chains multiple operations.
    /// </summary>
    /// <returns></returns>
    public static ITool Chain(string name, string description, params ITool[] tools)
    {
        return new DelegateTool(name, description, async (input, ct) =>
        {
            Result<string, string> result = Result<string, string>.Success(input);

            foreach (ITool tool in tools)
            {
                if (ct.IsCancellationRequested)
                {
                    return Result<string, string>.Failure("Operation cancelled");
                }

                result = await result.Match(
                    async success => await tool.InvokeAsync(success, ct),
                    failure => Task.FromResult(Result<string, string>.Failure(failure)));

                if (result.IsFailure)
                {
                    break;
                }
            }

            return result;
        });
    }

    /// <summary>
    /// Create a tool that tries multiple tools in sequence.
    /// </summary>
    /// <returns></returns>
    public static ITool FirstSuccess(string name, string description, params ITool[] tools)
    {
        return new DelegateTool(name, description, async (input, ct) =>
        {
            foreach (ITool tool in tools)
            {
                if (ct.IsCancellationRequested)
                {
                    return Result<string, string>.Failure("Operation cancelled");
                }

                Result<string, string> result = await tool.InvokeAsync(input, ct);
                if (result.IsSuccess)
                {
                    return result;
                }
            }

            return Result<string, string>.Failure("All tools failed");
        });
    }

    /// <summary>
    /// Create a conditional tool that selects based on input.
    /// </summary>
    /// <returns></returns>
    public static ITool Conditional(string name, string description,
        Func<string, ITool> selector)
    {
        return new DelegateTool(name, description, async (input, ct) =>
        {
            try
            {
                ITool selectedTool = selector(input);
                return await selectedTool.InvokeAsync(input, ct);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Tool selection failed: {ex.Message}");
            }
        });
    }
}
