// <copyright file="ToolCall.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents a request to execute a tool with specific arguments.
/// Immutable record for thread-safe tool invocation tracking.
/// </summary>
public sealed record ToolCall
{
    /// <summary>
    /// Gets the name of the tool to execute.
    /// </summary>
    public string ToolName { get; init; }

    /// <summary>
    /// Gets the arguments to pass to the tool (JSON or string format).
    /// </summary>
    public string Arguments { get; init; }

    /// <summary>
    /// Gets the confidence score of the LLM that requested this tool (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets optional metadata about the tool call.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }

    /// <summary>
    /// Gets the unique identifier for this tool call.
    /// </summary>
    public string CallId { get; init; }

    /// <summary>
    /// Gets the timestamp when the tool call was requested.
    /// </summary>
    public DateTime RequestedAt { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCall"/> class.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="arguments">The arguments for the tool.</param>
    /// <param name="confidence">The confidence score (default 1.0).</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="callId">Optional call ID (auto-generated if not provided).</param>
    /// <param name="requestedAt">Optional timestamp (uses current UTC time if not provided).</param>
    public ToolCall(
        string toolName,
        string arguments,
        double confidence = 1.0,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? callId = null,
        DateTime? requestedAt = null)
    {
        this.ToolName = toolName;
        this.Arguments = arguments;
        this.Confidence = confidence;
        this.Metadata = metadata ?? new Dictionary<string, string>();
        this.CallId = callId ?? Guid.NewGuid().ToString();
        this.RequestedAt = requestedAt ?? DateTime.UtcNow;
    }
}
