// <copyright file="PipelineBranchExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Network;

/// <summary>
/// Extension methods for integrating PipelineBranch with the Merkle-DAG network state.
/// </summary>
public static class PipelineBranchExtensions
{
    /// <summary>
    /// Converts a PipelineBranch to a MerkleDag, reifying all events as nodes and transitions.
    /// </summary>
    /// <param name="branch">The pipeline branch to convert.</param>
    /// <returns>A Result containing the populated MerkleDag or an error.</returns>
    public static Result<MerkleDag> ToMerkleDag(this PipelineBranch branch)
    {
        if (branch == null)
        {
            return Result<MerkleDag>.Failure("Branch cannot be null");
        }

        var reifier = new PipelineBranchReifier();
        var result = reifier.ReifyBranch(branch);

        return result.IsSuccess
            ? Result<MerkleDag>.Success(reifier.Dag)
            : Result<MerkleDag>.Failure(result.Error ?? "Unknown error during reification");
    }

    /// <summary>
    /// Creates a PipelineBranchReifier for incremental reification of branch events.
    /// </summary>
    /// <param name="branch">The pipeline branch to start tracking.</param>
    /// <returns>A reifier that can be used for incremental updates.</returns>
    public static PipelineBranchReifier CreateReifier(this PipelineBranch branch)
    {
        var reifier = new PipelineBranchReifier();
        reifier.ReifyBranch(branch);
        return reifier;
    }

    /// <summary>
    /// Gets the most recent ReasoningStep from a branch as a MonadNode.
    /// </summary>
    /// <param name="branch">The pipeline branch.</param>
    /// <returns>An Option containing the MonadNode or None if no reasoning steps exist.</returns>
    public static Option<MonadNode> GetLatestReasoningNode(this PipelineBranch branch)
    {
        var lastReasoning = branch.Events.OfType<ReasoningStep>().LastOrDefault();
        if (lastReasoning == null)
        {
            return Option<MonadNode>.None();
        }

        return Option<MonadNode>.Some(MonadNode.FromReasoningState(lastReasoning.State));
    }

    /// <summary>
    /// Gets summary statistics for a branch's reasoning chain.
    /// </summary>
    /// <param name="branch">The pipeline branch.</param>
    /// <returns>A summary of the branch's reasoning steps.</returns>
    public static BranchReasoningSummary GetReasoningSummary(this PipelineBranch branch)
    {
        var reasoningSteps = branch.Events.OfType<ReasoningStep>().ToList();

        var stepsByKind = reasoningSteps
            .GroupBy(s => s.State.Kind)
            .ToImmutableDictionary(g => g.Key, g => g.Count());

        var totalToolCalls = reasoningSteps
            .Sum(s => s.ToolCalls?.Count ?? 0);

        var firstTimestamp = reasoningSteps.FirstOrDefault()?.Timestamp;
        var lastTimestamp = reasoningSteps.LastOrDefault()?.Timestamp;

        var totalDuration = firstTimestamp.HasValue && lastTimestamp.HasValue
            ? (lastTimestamp.Value - firstTimestamp.Value)
            : TimeSpan.Zero;

        return new BranchReasoningSummary(
            branch.Name,
            reasoningSteps.Count,
            stepsByKind,
            totalToolCalls,
            totalDuration);
    }

    /// <summary>
    /// Projects the current state of a branch into a GlobalNetworkState snapshot.
    /// </summary>
    /// <param name="branch">The pipeline branch.</param>
    /// <param name="metadata">Optional metadata to include.</param>
    /// <returns>A Result containing the projected state or an error.</returns>
    public static Result<GlobalNetworkState> ProjectNetworkState(
        this PipelineBranch branch,
        ImmutableDictionary<string, string>? metadata = null)
    {
        var dagResult = branch.ToMerkleDag();
        if (!dagResult.IsSuccess)
        {
            return Result<GlobalNetworkState>.Failure(dagResult.Error ?? "Failed to create DAG");
        }

        var projector = new NetworkStateProjector(dagResult.Value);
        var combinedMetadata = metadata ?? ImmutableDictionary<string, string>.Empty;
        combinedMetadata = combinedMetadata.Add("branch", branch.Name);

        return Result<GlobalNetworkState>.Success(projector.ProjectCurrentState(combinedMetadata));
    }
}

/// <summary>
/// Summary of reasoning steps in a pipeline branch.
/// </summary>
/// <param name="BranchName">Name of the branch.</param>
/// <param name="TotalSteps">Total number of reasoning steps.</param>
/// <param name="StepsByKind">Count of steps by kind (Draft, Critique, etc.).</param>
/// <param name="TotalToolCalls">Total number of tool calls across all steps.</param>
/// <param name="TotalDuration">Duration from first to last step.</param>
public sealed record BranchReasoningSummary(
    string BranchName,
    int TotalSteps,
    ImmutableDictionary<string, int> StepsByKind,
    int TotalToolCalls,
    TimeSpan TotalDuration);
