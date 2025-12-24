// <copyright file="ToolResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Represents the result of a tool execution.
/// Immutable record with execution details.
/// </summary>
public sealed record ToolResult
{
    /// <summary>
    /// Gets the output from the tool execution.
    /// </summary>
    public string Output { get; init; }

    /// <summary>
    /// Gets the original tool call that produced this result.
    /// </summary>
    public ToolCall ToolCall { get; init; }

    /// <summary>
    /// Gets the execution status.
    /// </summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the timestamp when execution completed.
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Gets the execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets optional error information if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolResult"/> class.
    /// </summary>
    /// <param name="output">The tool output.</param>
    /// <param name="toolCall">The original tool call.</param>
    /// <param name="status">The execution status.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="errorMessage">Optional error message.</param>
    /// <param name="completedAt">Optional completion timestamp.</param>
    public ToolResult(
        string output,
        ToolCall toolCall,
        ExecutionStatus status,
        TimeSpan duration,
        string? errorMessage = null,
        DateTime? completedAt = null)
    {
        this.Output = output;
        this.ToolCall = toolCall;
        this.Status = status;
        this.Duration = duration;
        this.ErrorMessage = errorMessage;
        this.CompletedAt = completedAt ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    /// <param name="output">The tool output.</param>
    /// <param name="toolCall">The original tool call.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A successful tool result.</returns>
    public static ToolResult Success(string output, ToolCall toolCall, TimeSpan duration)
    {
        return new ToolResult(output, toolCall, ExecutionStatus.Success, duration);
    }

    /// <summary>
    /// Creates a failed tool result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="toolCall">The original tool call.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A failed tool result.</returns>
    public static ToolResult Failure(string errorMessage, ToolCall toolCall, TimeSpan duration)
    {
        return new ToolResult(string.Empty, toolCall, ExecutionStatus.Failed, duration, errorMessage);
    }
}

/// <summary>
/// Represents the execution status of a tool.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Execution succeeded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Execution failed.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Execution was blocked by safety checks.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// Execution requires human approval.
    /// </summary>
    PendingApproval = 3
}
