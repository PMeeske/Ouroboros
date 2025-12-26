// <copyright file="ToolBinder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Planning;

/// <summary>
/// Binds MeTTa tool expressions to executable C# pipeline steps.
/// Compiles symbolic tool chains into runnable pipelines.
/// </summary>
public sealed class ToolBinder
{
    private readonly ToolRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolBinder"/> class.
    /// </summary>
    /// <param name="registry">The tool registry containing available tools.</param>
    public ToolBinder(ToolRegistry registry)
    {
        this._registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Binds a tool chain to an executable pipeline.
    /// </summary>
    /// <param name="chain">The tool chain from MeTTa planning.</param>
    /// <returns>A Result containing the executable step or an error.</returns>
    public Result<Step<string, string>, string> Bind(ToolChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (chain.IsEmpty)
        {
            return Result<Step<string, string>, string>.Failure("Cannot bind empty tool chain");
        }

        // Validate all tools exist
        foreach (string toolName in chain.Tools)
        {
            ITool? tool = this._registry.Get(toolName);
            if (tool == null)
            {
                return Result<Step<string, string>, string>.Failure(
                    $"Tool not found in registry: {toolName}");
            }
        }

        // Compose the pipeline
        Step<string, string> pipeline = this.CreatePipeline(chain.Tools);
        return Result<Step<string, string>, string>.Success(pipeline);
    }

    /// <summary>
    /// Binds a tool chain to an executable pipeline with Result error handling.
    /// </summary>
    /// <param name="chain">The tool chain from MeTTa planning.</param>
    /// <returns>A Result containing the executable step or an error.</returns>
    public Result<KleisliResult<string, string, string>, string> BindSafe(ToolChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (chain.IsEmpty)
        {
            return Result<KleisliResult<string, string, string>, string>.Failure(
                "Cannot bind empty tool chain");
        }

        // Validate all tools exist
        foreach (string toolName in chain.Tools)
        {
            ITool? tool = this._registry.Get(toolName);
            if (tool == null)
            {
                return Result<KleisliResult<string, string, string>, string>.Failure(
                    $"Tool not found in registry: {toolName}");
            }
        }

        // Compose the safe pipeline
        KleisliResult<string, string, string> pipeline = this.CreateSafePipeline(chain.Tools);
        return Result<KleisliResult<string, string, string>, string>.Success(pipeline);
    }

    /// <summary>
    /// Creates a pipeline from a sequence of tool names.
    /// </summary>
    private Step<string, string> CreatePipeline(IReadOnlyList<string> toolNames)
    {
        return async input =>
        {
            string current = input;

            foreach (string toolName in toolNames)
            {
                ITool? tool = this._registry.Get(toolName);
                if (tool == null)
                {
                    throw new InvalidOperationException($"Tool not found: {toolName}");
                }

                Result<string, string> result = await tool.InvokeAsync(current);
                
                if (result.IsFailure)
                {
                    throw new InvalidOperationException($"Tool {toolName} failed: {result.Error}");
                }

                current = result.Value;
            }

            return current;
        };
    }

    /// <summary>
    /// Creates a safe pipeline with Result error handling.
    /// </summary>
    private KleisliResult<string, string, string> CreateSafePipeline(IReadOnlyList<string> toolNames)
    {
        return async input =>
        {
            string current = input;

            foreach (string toolName in toolNames)
            {
                ITool? tool = this._registry.Get(toolName);
                if (tool == null)
                {
                    return Result<string, string>.Failure($"Tool not found: {toolName}");
                }

                Result<string, string> result = await tool.InvokeAsync(current);
                
                if (result.IsFailure)
                {
                    return Result<string, string>.Failure($"Tool {toolName} failed: {result.Error}");
                }

                current = result.Value;
            }

            return Result<string, string>.Success(current);
        };
    }

    /// <summary>
    /// Creates a pipeline with progress reporting.
    /// </summary>
    /// <param name="chain">The tool chain.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <returns>A Result containing the executable step or an error.</returns>
    public Result<Step<string, string>, string> BindWithProgress(
        ToolChain chain,
        IProgress<(string ToolName, int Index, int Total)>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (chain.IsEmpty)
        {
            return Result<Step<string, string>, string>.Failure("Cannot bind empty tool chain");
        }

        foreach (string toolName in chain.Tools)
        {
            if (this._registry.Get(toolName) == null)
            {
                return Result<Step<string, string>, string>.Failure(
                    $"Tool not found in registry: {toolName}");
            }
        }

        Step<string, string> pipeline = async input =>
        {
            string current = input;
            int total = chain.Tools.Count;

            for (int i = 0; i < chain.Tools.Count; i++)
            {
                string toolName = chain.Tools[i];
                progress?.Report((toolName, i + 1, total));

                ITool? tool = this._registry.Get(toolName);
                Result<string, string> result = await tool!.InvokeAsync(current);
                
                if (result.IsFailure)
                {
                    throw new InvalidOperationException($"Tool {toolName} failed: {result.Error}");
                }

                current = result.Value;
            }

            return current;
        };

        return Result<Step<string, string>, string>.Success(pipeline);
    }
}

/// <summary>
/// Extension methods for integrating MeTTa planning with tool binding.
/// </summary>
public static class ToolBinderExtensions
{
    /// <summary>
    /// Plans and binds a tool chain in one operation.
    /// </summary>
    /// <param name="planner">The MeTTa planner.</param>
    /// <param name="binder">The tool binder.</param>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the executable pipeline or an error.</returns>
    public static async Task<Result<Step<string, string>, string>> PlanAndBindAsync(
        this MeTTaPlanner planner,
        ToolBinder binder,
        MeTTaType startType,
        MeTTaType endType,
        CancellationToken ct = default)
    {
        Result<ToolChain, string> planResult = await planner.PlanAsync(startType, endType, ct);

        if (planResult.IsFailure)
        {
            return Result<Step<string, string>, string>.Failure(planResult.Error);
        }

        return binder.Bind(planResult.Value);
    }

    /// <summary>
    /// Plans, binds, and executes a tool chain.
    /// </summary>
    /// <param name="planner">The MeTTa planner.</param>
    /// <param name="binder">The tool binder.</param>
    /// <param name="input">The input to process.</param>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the output or an error.</returns>
    public static async Task<Result<string, string>> PlanBindAndExecuteAsync(
        this MeTTaPlanner planner,
        ToolBinder binder,
        string input,
        MeTTaType startType,
        MeTTaType endType,
        CancellationToken ct = default)
    {
        Result<Step<string, string>, string> bindResult = 
            await planner.PlanAndBindAsync(binder, startType, endType, ct);

        if (bindResult.IsFailure)
        {
            return Result<string, string>.Failure(bindResult.Error);
        }

        try
        {
            string result = await bindResult.Value(input);
            return Result<string, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Execution failed: {ex.Message}");
        }
    }
}
