// <copyright file="StepExecutionEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Events;

/// <summary>
/// Event capturing the execution of a pipeline step with its token metadata.
/// Enables reification of step synopses into the Merkle-DAG network state.
/// </summary>
/// <param name="Id">Unique identifier for this event.</param>
/// <param name="TokenName">The primary pipeline token name (e.g., "MottoChat").</param>
/// <param name="Aliases">Alternative names for this token (e.g., "MeTTaChat").</param>
/// <param name="SourceClass">The class containing this step (e.g., "MeTTaCliSteps").</param>
/// <param name="Description">Human-readable description of what the step does.</param>
/// <param name="Arguments">The arguments passed to the step, if any.</param>
/// <param name="Timestamp">When the step execution started.</param>
/// <param name="DurationMs">Duration of the step execution in milliseconds.</param>
/// <param name="Success">Whether the step completed successfully.</param>
/// <param name="Error">Error message if the step failed.</param>
public sealed record StepExecutionEvent(
    Guid Id,
    string TokenName,
    string[] Aliases,
    string SourceClass,
    string Description,
    string? Arguments,
    DateTime Timestamp,
    long? DurationMs = null,
    bool Success = true,
    string? Error = null) : PipelineEvent(Id, "StepExecution", Timestamp)
{
    /// <summary>
    /// Creates a StepExecutionEvent from a started step execution.
    /// </summary>
    public static StepExecutionEvent Start(
        string tokenName,
        string[] aliases,
        string sourceClass,
        string description,
        string? arguments = null)
    {
        return new StepExecutionEvent(
            Guid.NewGuid(),
            tokenName,
            aliases,
            sourceClass,
            description,
            arguments,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Returns a new event with completion information.
    /// </summary>
    public StepExecutionEvent WithCompletion(long durationMs, bool success = true, string? error = null)
    {
        return this with
        {
            DurationMs = durationMs,
            Success = success,
            Error = error
        };
    }

    /// <summary>
    /// Gets a formatted synopsis of this step execution.
    /// </summary>
    public string GetSynopsis()
    {
        var args = !string.IsNullOrEmpty(Arguments) ? $"({Arguments})" : "";
        var status = Success ? "✓" : $"✗ {Error}";
        var duration = DurationMs.HasValue ? $" [{DurationMs}ms]" : "";
        return $"{TokenName}{args}{duration} {status}";
    }
}
