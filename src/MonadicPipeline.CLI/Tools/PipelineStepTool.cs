// <copyright file="PipelineStepTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

using LangChainPipeline.CLI;

/// <summary>
/// A tool that wraps a CLI pipeline step, allowing the LLM to invoke pipeline operations.
/// This enables meta-AI capabilities where the pipeline can reason about and modify its own execution.
/// The tool maintains a reference to the pipeline state to enable true self-modification.
/// </summary>
public sealed class PipelineStepTool : ITool
{
    private readonly string stepName;
    private readonly Func<string?, Step<CliPipelineState, CliPipelineState>> stepFactory;
    private CliPipelineState? pipelineState;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string? JsonSchema { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineStepTool"/> class.
    /// </summary>
    /// <param name="stepName">The name of the pipeline step.</param>
    /// <param name="description">Description of what the step does.</param>
    /// <param name="stepFactory">Factory function that creates a step given optional args.</param>
    public PipelineStepTool(string stepName, string description, Func<string?, Step<CliPipelineState, CliPipelineState>> stepFactory)
    {
        this.stepName = stepName ?? throw new ArgumentNullException(nameof(stepName));
        this.stepFactory = stepFactory ?? throw new ArgumentNullException(nameof(stepFactory));

        this.Name = $"run_{stepName.ToLowerInvariant()}";
        this.Description = description;

        // Simple schema for pipeline steps - they can accept optional string arguments
        this.JsonSchema = """
        {
            "type": "object",
            "properties": {
                "args": {
                    "type": "string",
                    "description": "Optional arguments for the pipeline step"
                }
            }
        }
        """;
    }

    /// <summary>
    /// Sets the pipeline state that this tool will operate on.
    /// This allows the tool to execute steps that modify the pipeline state.
    /// </summary>
    /// <param name="state">The current pipeline state.</param>
    public void SetPipelineState(CliPipelineState state)
    {
        this.pipelineState = state;
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (this.pipelineState == null)
        {
            return Result<string, string>.Failure("Pipeline state not initialized for this tool");
        }

        try
        {
            // Parse input to extract args if provided in JSON format
            string? args = null;
            if (!string.IsNullOrWhiteSpace(input))
            {
                try
                {
                    var json = ToolJson.Deserialize<Dictionary<string, string>>(input);
                    if (json != null && json.TryGetValue("args", out var argsValue))
                    {
                        args = argsValue;
                    }
                }
                catch
                {
                    // If not JSON, treat entire input as args
                    args = input;
                }
            }

            // Execute the pipeline step
            var step = this.stepFactory(args);
            var newState = await step(this.pipelineState);

            // Update the pipeline state reference
            this.pipelineState.Branch = newState.Branch;
            this.pipelineState.Output = newState.Output;
            this.pipelineState.Context = newState.Context;
            this.pipelineState.Prompt = newState.Prompt;
            this.pipelineState.Query = newState.Query;
            this.pipelineState.Topic = newState.Topic;

            // Return result description
            var resultMessage = string.IsNullOrWhiteSpace(newState.Output)
                ? $"Executed pipeline step '{this.stepName}' successfully"
                : $"Executed '{this.stepName}': {(newState.Output.Length > 200 ? newState.Output.Substring(0, 200) + "..." : newState.Output)}";

            return Result<string, string>.Success(resultMessage);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Pipeline step '{this.stepName}' failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a pipeline step tool from a step name using the StepRegistry.
    /// </summary>
    /// <param name="stepName">The name/token of the step.</param>
    /// <param name="description">Description of the step.</param>
    /// <returns>A new PipelineStepTool or null if step not found.</returns>
    public static PipelineStepTool? FromStepName(string stepName, string description)
    {
        // Create a factory that resolves the step from the registry
        return new PipelineStepTool(
            stepName,
            description,
            args =>
            {
                if (StepRegistry.TryResolve(stepName, args, out var step) && step != null)
                {
                    return step;
                }

                // Return no-op step if not found
                return s => Task.FromResult(s);
            });
    }
}
