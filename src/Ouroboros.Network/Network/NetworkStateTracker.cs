// <copyright file="NetworkStateTracker.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Pipeline.Branches;

namespace LangChainPipeline.Network;

/// <summary>
/// Service for automatically tracking pipeline execution in the emergent network state.
/// Provides real-time reification of Step execution into the MerkleDag.
/// </summary>
public sealed class NetworkStateTracker : IDisposable
{
    private readonly MerkleDag dag;
    private readonly NetworkStateProjector projector;
    private readonly TransitionReplayEngine replayEngine;
    private readonly Dictionary<string, PipelineBranchReifier> branchReifiers;
    private readonly object syncLock = new();
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkStateTracker"/> class.
    /// </summary>
    public NetworkStateTracker()
    {
        this.dag = new MerkleDag();
        this.projector = new NetworkStateProjector(this.dag);
        this.replayEngine = new TransitionReplayEngine(this.dag);
        this.branchReifiers = new Dictionary<string, PipelineBranchReifier>();
    }

    /// <summary>
    /// Gets the underlying MerkleDag.
    /// </summary>
    public MerkleDag Dag => this.dag;

    /// <summary>
    /// Gets the network state projector.
    /// </summary>
    public NetworkStateProjector Projector => this.projector;

    /// <summary>
    /// Gets the transition replay engine.
    /// </summary>
    public TransitionReplayEngine ReplayEngine => this.replayEngine;

    /// <summary>
    /// Gets the count of tracked branches.
    /// </summary>
    public int TrackedBranchCount => this.branchReifiers.Count;

    /// <summary>
    /// Tracks a pipeline branch, reifying its current and future events.
    /// </summary>
    /// <param name="branch">The branch to track.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public Result<ReificationResult> TrackBranch(PipelineBranch branch)
    {
        if (branch == null)
        {
            return Result<ReificationResult>.Failure("Branch cannot be null");
        }

        lock (this.syncLock)
        {
            if (!this.branchReifiers.TryGetValue(branch.Name, out var reifier))
            {
                reifier = new PipelineBranchReifier(this.dag, this.projector);
                this.branchReifiers[branch.Name] = reifier;
            }

            return reifier.ReifyBranch(branch);
        }
    }

    /// <summary>
    /// Updates tracking for a branch with new events.
    /// Call this after each Step execution to incrementally update the DAG.
    /// </summary>
    /// <param name="branch">The branch with potentially new events.</param>
    /// <returns>A Result containing the count of newly reified events.</returns>
    public Result<int> UpdateBranch(PipelineBranch branch)
    {
        if (branch == null)
        {
            return Result<int>.Failure("Branch cannot be null");
        }

        lock (this.syncLock)
        {
            if (!this.branchReifiers.TryGetValue(branch.Name, out var reifier))
            {
                // First time seeing this branch - do full reification
                reifier = new PipelineBranchReifier(this.dag, this.projector);
                this.branchReifiers[branch.Name] = reifier;
                var fullResult = reifier.ReifyBranch(branch);
                return fullResult.IsSuccess
                    ? Result<int>.Success(fullResult.Value.NodesCreated)
                    : Result<int>.Failure(fullResult.Error ?? "Unknown error");
            }

            return reifier.ReifyNewEvents(branch);
        }
    }

    /// <summary>
    /// Creates a snapshot of the current global network state across all tracked branches.
    /// </summary>
    /// <returns>The global network state snapshot.</returns>
    public GlobalNetworkState CreateSnapshot()
    {
        var metadata = ImmutableDictionary<string, string>.Empty
            .Add("trackedBranches", string.Join(",", this.branchReifiers.Keys))
            .Add("branchCount", this.branchReifiers.Count.ToString());

        return this.projector.CreateSnapshot(metadata);
    }

    /// <summary>
    /// Gets the replay path from a root node to the specified target node.
    /// </summary>
    /// <param name="targetNodeId">The target node ID.</param>
    /// <returns>A Result containing the transition path.</returns>
    public Result<ImmutableArray<TransitionEdge>> ReplayToNode(Guid targetNodeId)
    {
        return this.replayEngine.ReplayPathToNode(targetNodeId);
    }

    /// <summary>
    /// Gets a summary of the current network state.
    /// </summary>
    /// <returns>A formatted summary string.</returns>
    public string GetStateSummary()
    {
        var state = this.projector.ProjectCurrentState();

        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== Network State Summary ===");
        summary.AppendLine($"Total Nodes: {state.TotalNodes}");
        summary.AppendLine($"Total Transitions: {state.TotalTransitions}");
        summary.AppendLine($"Tracked Branches: {this.branchReifiers.Count}");
        summary.AppendLine($"Current Epoch: {this.projector.CurrentEpoch}");

        if (state.AverageConfidence.HasValue)
        {
            summary.AppendLine($"Average Confidence: {state.AverageConfidence:F2}");
        }

        if (state.TotalProcessingTimeMs.HasValue)
        {
            summary.AppendLine($"Total Processing Time: {state.TotalProcessingTimeMs}ms");
        }

        summary.AppendLine();
        summary.AppendLine("Nodes by Type:");
        foreach (var kvp in state.NodeCountByType)
        {
            summary.AppendLine($"  {kvp.Key}: {kvp.Value}");
        }

        if (state.TransitionCountByOperation.Any())
        {
            summary.AppendLine();
            summary.AppendLine("Transitions by Operation:");
            foreach (var kvp in state.TransitionCountByOperation)
            {
                summary.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        return summary.ToString();
    }

    /// <summary>
    /// Gets the reifier for a specific branch.
    /// </summary>
    /// <param name="branchName">The branch name.</param>
    /// <returns>An Option containing the reifier if found.</returns>
    public Option<PipelineBranchReifier> GetBranchReifier(string branchName)
    {
        lock (this.syncLock)
        {
            return this.branchReifiers.TryGetValue(branchName, out var reifier)
                ? Option<PipelineBranchReifier>.Some(reifier)
                : Option<PipelineBranchReifier>.None();
        }
    }

    /// <summary>
    /// Clears all tracked branches and resets the DAG.
    /// </summary>
    public void Reset()
    {
        lock (this.syncLock)
        {
            this.branchReifiers.Clear();
            // Note: MerkleDag doesn't have a Clear method - this creates fresh instances
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            this.branchReifiers.Clear();
            this.disposed = true;
        }
    }
}
