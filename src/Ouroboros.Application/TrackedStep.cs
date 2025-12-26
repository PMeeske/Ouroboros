// <copyright file="TrackedStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Diagnostics;
using System.Reflection;
using Ouroboros.Core.Steps;
using Ouroboros.Domain.Events;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Application;

/// <summary>
/// Wraps a pipeline step with execution tracking that records step token metadata.
/// Enables reification of step synopses into the Merkle-DAG network state.
/// </summary>
public static class TrackedStep
{
    /// <summary>
    /// Wraps a step with execution tracking, recording step token metadata in the pipeline branch.
    /// </summary>
    /// <param name="step">The step to wrap.</param>
    /// <param name="tokenName">The primary pipeline token name.</param>
    /// <param name="aliases">Alternative names for this token.</param>
    /// <param name="sourceClass">The class containing this step.</param>
    /// <param name="description">Human-readable description.</param>
    /// <param name="arguments">The arguments passed to the step.</param>
    /// <returns>A wrapped step that records execution metadata.</returns>
    public static Step<CliPipelineState, CliPipelineState> Wrap(
        Step<CliPipelineState, CliPipelineState> step,
        string tokenName,
        string[] aliases,
        string sourceClass,
        string description,
        string? arguments = null)
    {
        return async state =>
        {
            var startEvent = StepExecutionEvent.Start(
                tokenName,
                aliases,
                sourceClass,
                description,
                arguments);

            // Record start of execution
            var trackedBranch = state.Branch.WithEvent(startEvent);
            state = state.WithBranch(trackedBranch);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Execute the actual step
                var result = await step(state);
                stopwatch.Stop();

                // Record successful completion
                var completedEvent = startEvent.WithCompletion(stopwatch.ElapsedMilliseconds, success: true);
                var updatedBranch = UpdateLastStepEvent(result.Branch, startEvent.Id, completedEvent);
                return result.WithBranch(updatedBranch);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Record failure
                var failedEvent = startEvent.WithCompletion(stopwatch.ElapsedMilliseconds, success: false, error: ex.Message);
                var updatedBranch = UpdateLastStepEvent(state.Branch, startEvent.Id, failedEvent);

                // Re-throw but with updated state
                throw new TrackedStepException(ex.Message, ex, state.WithBranch(updatedBranch));
            }
        };
    }

    /// <summary>
    /// Wraps a step using reflection to extract PipelineToken metadata from the calling method.
    /// </summary>
    /// <param name="step">The step to wrap.</param>
    /// <param name="arguments">The arguments passed to the step.</param>
    /// <param name="callerMethod">The method info of the pipeline token method.</param>
    /// <returns>A wrapped step that records execution metadata.</returns>
    public static Step<CliPipelineState, CliPipelineState> WrapWithReflection(
        Step<CliPipelineState, CliPipelineState> step,
        string? arguments = null,
        MethodInfo? callerMethod = null)
    {
        if (callerMethod == null)
        {
            // Fall back to stack trace inspection
            var stackTrace = new StackTrace();
            callerMethod = stackTrace.GetFrame(1)?.GetMethod() as MethodInfo;
        }

        if (callerMethod == null)
        {
            // Can't determine calling method, return unwrapped
            return step;
        }

        var attr = callerMethod.GetCustomAttribute<PipelineTokenAttribute>();
        if (attr == null)
        {
            // No PipelineToken attribute, return unwrapped
            return step;
        }

        var description = callerMethod.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description
            ?? ExtractXmlDocSummary(callerMethod)
            ?? $"Pipeline step from {callerMethod.DeclaringType?.Name}";

        return Wrap(
            step,
            attr.Names.FirstOrDefault() ?? callerMethod.Name,
            attr.Names.Skip(1).ToArray(),
            callerMethod.DeclaringType?.Name ?? "Unknown",
            description,
            arguments);
    }

    /// <summary>
    /// Creates a tracked step directly from PipelineTokenInfo.
    /// </summary>
    public static Step<CliPipelineState, CliPipelineState> FromTokenInfo(
        Step<CliPipelineState, CliPipelineState> step,
        PipelineTokenInfo tokenInfo,
        string? arguments = null)
    {
        return Wrap(
            step,
            tokenInfo.PrimaryName,
            tokenInfo.Aliases,
            tokenInfo.SourceClass,
            tokenInfo.Description,
            arguments);
    }

    private static PipelineBranch UpdateLastStepEvent(PipelineBranch branch, Guid eventId, StepExecutionEvent updatedEvent)
    {
        // Replace the start event with the completed event
        var events = branch.Events.ToList();
        var index = events.FindIndex(e => e.Id == eventId);
        if (index >= 0)
        {
            events[index] = updatedEvent;
            return PipelineBranch.WithEvents(branch.Name, branch.Store, branch.Source, events);
        }
        return branch;
    }

    private static string? ExtractXmlDocSummary(MethodInfo method)
    {
        // Simple extraction - in production, use proper XML doc parsing
        try
        {
            var xmlDoc = method.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.Name == "XmlDocAttribute");
            return null; // Placeholder
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Exception thrown when a tracked step fails, preserving the updated state.
/// </summary>
public class TrackedStepException : Exception
{
    public CliPipelineState State { get; }

    public TrackedStepException(string message, Exception inner, CliPipelineState state)
        : base(message, inner)
    {
        State = state;
    }
}
