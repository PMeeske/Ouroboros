// <copyright file="EasyPipelineResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Easy;

/// <summary>
/// Represents the result of an easy pipeline execution.
/// </summary>
/// <param name="Success">Indicates whether the pipeline executed successfully.</param>
/// <param name="Output">The final output text from the pipeline.</param>
/// <param name="Branch">The pipeline branch with full execution history.</param>
/// <param name="Error">Error message if the pipeline failed, null otherwise.</param>
public record EasyPipelineResult(
    bool Success,
    string Output,
    PipelineBranch Branch,
    string? Error)
{
    /// <summary>
    /// Gets the reasoning steps from the pipeline execution.
    /// </summary>
    /// <returns>A collection of reasoning steps.</returns>
    public IEnumerable<ReasoningStep> GetReasoningSteps() =>
        Branch.Events.OfType<ReasoningStep>();

    /// <summary>
    /// Gets the final reasoning state from the pipeline.
    /// </summary>
    /// <returns>The final reasoning state, or null if no reasoning was performed.</returns>
    public ReasoningState? GetFinalState() =>
        Branch.Events
            .OfType<ReasoningStep>()
            .Select(e => e.State)
            .LastOrDefault();

    /// <summary>
    /// Gets all tool executions from the pipeline execution.
    /// </summary>
    /// <returns>A collection of tool executions.</returns>
    public IEnumerable<ToolExecution> GetToolExecutions() =>
        Branch.Events
            .OfType<ReasoningStep>()
            .SelectMany(e => e.ToolCalls ?? Enumerable.Empty<ToolExecution>());

    /// <summary>
    /// Formats the result as a readable string.
    /// </summary>
    /// <returns>A formatted string representation of the result.</returns>
    public override string ToString()
    {
        if (!Success)
            return $"Pipeline failed: {Error}";

        var sb = new StringBuilder();
        sb.AppendLine("Pipeline executed successfully");
        sb.AppendLine($"Steps executed: {Branch.Events.Count}");
        sb.AppendLine($"Output: {Output}");
        
        return sb.ToString();
    }
}
