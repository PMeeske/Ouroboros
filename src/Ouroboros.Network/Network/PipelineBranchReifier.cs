// <copyright file="PipelineBranchReifier.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Pipeline.Branches;

namespace LangChainPipeline.Network;

/// <summary>
/// Bridge that reifies PipelineBranch events into MerkleDag nodes and transitions.
/// Enables automatic tracking of Step execution in the emergent network state.
/// </summary>
public sealed class PipelineBranchReifier
{
    private readonly MerkleDag dag;
    private readonly NetworkStateProjector projector;
    private readonly Dictionary<Guid, Guid> eventToNodeMapping;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBranchReifier"/> class.
    /// </summary>
    /// <param name="dag">The MerkleDag to populate.</param>
    /// <param name="projector">The network state projector.</param>
    public PipelineBranchReifier(MerkleDag dag, NetworkStateProjector projector)
    {
        this.dag = dag ?? throw new ArgumentNullException(nameof(dag));
        this.projector = projector ?? throw new ArgumentNullException(nameof(projector));
        this.eventToNodeMapping = new Dictionary<Guid, Guid>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBranchReifier"/> class with new DAG.
    /// </summary>
    public PipelineBranchReifier()
        : this(new MerkleDag(), new NetworkStateProjector(new MerkleDag()))
    {
        // Re-initialize with shared DAG
        var sharedDag = new MerkleDag();
        var sharedProjector = new NetworkStateProjector(sharedDag);
        this.dag = sharedDag;
        this.projector = sharedProjector;
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
    /// Gets the mapping from PipelineEvent IDs to MonadNode IDs.
    /// </summary>
    public IReadOnlyDictionary<Guid, Guid> EventToNodeMapping => this.eventToNodeMapping;

    /// <summary>
    /// Reifies an entire PipelineBranch into the MerkleDag.
    /// Creates nodes for each event and transitions between sequential reasoning steps and step executions.
    /// </summary>
    /// <param name="branch">The pipeline branch to reify.</param>
    /// <returns>A Result containing the created nodes count or an error.</returns>
    public Result<ReificationResult> ReifyBranch(PipelineBranch branch)
    {
        if (branch == null)
        {
            return Result<ReificationResult>.Failure("Branch cannot be null");
        }

        var nodesCreated = 0;
        var transitionsCreated = 0;
        MonadNode? previousReasoningNode = null;
        ReasoningStep? previousReasoningStep = null;
        MonadNode? previousStepNode = null;
        StepExecutionEvent? previousStepExec = null;

        foreach (var evt in branch.Events)
        {
            // Determine parent node - prefer step execution chain, fall back to reasoning chain
            var parentNodeId = previousStepNode?.Id ?? previousReasoningNode?.Id;

            var nodeResult = this.ReifyEvent(evt, parentNodeId);
            if (!nodeResult.IsSuccess)
            {
                return Result<ReificationResult>.Failure($"Failed to reify event {evt.Id}: {nodeResult.Error}");
            }

            nodesCreated++;
            var currentNode = nodeResult.Value;

            // Create transition for sequential step executions
            if (evt is StepExecutionEvent currentStepExec && previousStepNode != null && previousStepExec != null)
            {
                var transitionResult = this.CreateStepTransition(
                    previousStepNode,
                    currentNode,
                    previousStepExec,
                    currentStepExec);

                if (transitionResult.IsSuccess)
                {
                    transitionsCreated++;
                }
            }

            // Create transition for sequential reasoning steps
            if (evt is ReasoningStep currentReasoning && previousReasoningNode != null && previousReasoningStep != null)
            {
                var transitionResult = this.CreateTransition(
                    previousReasoningNode,
                    currentNode,
                    previousReasoningStep,
                    currentReasoning);

                if (transitionResult.IsSuccess)
                {
                    transitionsCreated++;
                }
            }

            // Track step executions for chaining
            if (evt is StepExecutionEvent stepExec)
            {
                previousStepNode = currentNode;
                previousStepExec = stepExec;
            }

            // Track reasoning steps for chaining
            if (evt is ReasoningStep reasoning)
            {
                previousReasoningNode = currentNode;
                previousReasoningStep = reasoning;
            }
        }

        return Result<ReificationResult>.Success(new ReificationResult(
            branch.Name,
            nodesCreated,
            transitionsCreated,
            this.dag.NodeCount,
            this.dag.EdgeCount));
    }

    /// <summary>
    /// Reifies a single pipeline event into a MonadNode.
    /// </summary>
    /// <param name="evt">The event to reify.</param>
    /// <param name="parentNodeId">Optional parent node ID for DAG linking.</param>
    /// <returns>A Result containing the created node or an error.</returns>
    public Result<MonadNode> ReifyEvent(PipelineEvent evt, Guid? parentNodeId = null)
    {
        if (evt == null)
        {
            return Result<MonadNode>.Failure("Event cannot be null");
        }

        // Check if already reified
        if (this.eventToNodeMapping.ContainsKey(evt.Id))
        {
            var existingNodeId = this.eventToNodeMapping[evt.Id];
            var existingNode = this.dag.GetNode(existingNodeId);
            if (existingNode.HasValue)
            {
                return Result<MonadNode>.Success(existingNode.Value!);
            }
        }

        MonadNode node = evt switch
        {
            StepExecutionEvent stepExec => this.CreateStepExecutionNode(stepExec, parentNodeId),
            ReasoningStep reasoning => this.CreateReasoningNode(reasoning, parentNodeId),
            IngestBatch ingest => this.CreateIngestNode(ingest, parentNodeId),
            _ => this.CreateGenericEventNode(evt, parentNodeId),
        };

        var addResult = this.dag.AddNode(node);
        if (!addResult.IsSuccess)
        {
            return Result<MonadNode>.Failure($"Failed to add node to DAG: {addResult.Error}");
        }

        this.eventToNodeMapping[evt.Id] = node.Id;
        return Result<MonadNode>.Success(node);
    }

    /// <summary>
    /// Incrementally reifies new events from a branch that may have been partially processed.
    /// </summary>
    /// <param name="branch">The branch with potentially new events.</param>
    /// <returns>A Result containing the count of newly reified events.</returns>
    public Result<int> ReifyNewEvents(PipelineBranch branch)
    {
        if (branch == null)
        {
            return Result<int>.Failure("Branch cannot be null");
        }

        var newEventsCount = 0;
        MonadNode? lastKnownNode = null;
        ReasoningStep? lastKnownReasoningStep = null;

        // Find the last known node
        foreach (var evt in branch.Events)
        {
            if (this.eventToNodeMapping.TryGetValue(evt.Id, out var nodeId))
            {
                var node = this.dag.GetNode(nodeId);
                if (node.HasValue && evt is ReasoningStep reasoning)
                {
                    lastKnownNode = node.Value;
                    lastKnownReasoningStep = reasoning;
                }
            }
        }

        // Reify only new events
        foreach (var evt in branch.Events)
        {
            if (this.eventToNodeMapping.ContainsKey(evt.Id))
            {
                continue; // Already reified
            }

            var nodeResult = this.ReifyEvent(evt, lastKnownNode?.Id);
            if (!nodeResult.IsSuccess)
            {
                return Result<int>.Failure($"Failed to reify event: {nodeResult.Error}");
            }

            newEventsCount++;

            // Create transition if applicable
            if (evt is ReasoningStep currentReasoning && lastKnownNode != null && lastKnownReasoningStep != null)
            {
                this.CreateTransition(lastKnownNode, nodeResult.Value, lastKnownReasoningStep, currentReasoning);
            }

            if (evt is ReasoningStep reasoning)
            {
                lastKnownNode = nodeResult.Value;
                lastKnownReasoningStep = reasoning;
            }
        }

        return Result<int>.Success(newEventsCount);
    }

    /// <summary>
    /// Creates a snapshot of the current network state.
    /// </summary>
    /// <param name="branchName">Optional branch name to include in metadata.</param>
    /// <returns>The created global network state snapshot.</returns>
    public GlobalNetworkState CreateSnapshot(string? branchName = null)
    {
        var metadata = branchName != null
            ? ImmutableDictionary<string, string>.Empty.Add("branch", branchName)
            : null;

        return this.projector.CreateSnapshot(metadata);
    }

    private MonadNode CreateReasoningNode(ReasoningStep reasoning, Guid? parentNodeId)
    {
        var parentIds = parentNodeId.HasValue
            ? ImmutableArray.Create(parentNodeId.Value)
            : ImmutableArray<Guid>.Empty;

        // Include tool calls in the payload
        var payload = new
        {
            State = reasoning.State,
            Prompt = reasoning.Prompt,
            ToolCalls = reasoning.ToolCalls?.Select(t => new
            {
                t.ToolName,
                t.Arguments,
                t.Output,
            }).ToList(),
        };

        return MonadNode.FromPayload(
            reasoning.State.Kind,
            payload,
            parentIds);
    }

    private MonadNode CreateIngestNode(IngestBatch ingest, Guid? parentNodeId)
    {
        var parentIds = parentNodeId.HasValue
            ? ImmutableArray.Create(parentNodeId.Value)
            : ImmutableArray<Guid>.Empty;

        var payload = new
        {
            ingest.Source,
            DocumentIds = ingest.Ids,
            Count = ingest.Ids.Count,
        };

        return MonadNode.FromPayload("Ingest", payload, parentIds);
    }

    private MonadNode CreateGenericEventNode(PipelineEvent evt, Guid? parentNodeId)
    {
        var parentIds = parentNodeId.HasValue
            ? ImmutableArray.Create(parentNodeId.Value)
            : ImmutableArray<Guid>.Empty;

        return MonadNode.FromPayload(evt.Kind, new { EventId = evt.Id, evt.Timestamp }, parentIds);
    }

    private MonadNode CreateStepExecutionNode(StepExecutionEvent stepExec, Guid? parentNodeId)
    {
        var parentIds = parentNodeId.HasValue
            ? ImmutableArray.Create(parentNodeId.Value)
            : ImmutableArray<Guid>.Empty;

        // Create rich payload with step token synopsis
        var payload = new StepExecutionPayload(
            TokenName: stepExec.TokenName,
            Aliases: stepExec.Aliases,
            SourceClass: stepExec.SourceClass,
            Description: stepExec.Description,
            Arguments: stepExec.Arguments,
            Synopsis: stepExec.GetSynopsis(),
            DurationMs: stepExec.DurationMs,
            Success: stepExec.Success,
            Error: stepExec.Error,
            ExecutedAt: stepExec.Timestamp);

        return MonadNode.FromPayload($"Step:{stepExec.TokenName}", payload, parentIds);
    }

    private Result<TransitionEdge> CreateTransition(
        MonadNode fromNode,
        MonadNode toNode,
        ReasoningStep fromStep,
        ReasoningStep toStep)
    {
        var operationSpec = new
        {
            FromKind = fromStep.State.Kind,
            ToKind = toStep.State.Kind,
            Prompt = toStep.Prompt,
            HasToolCalls = toStep.ToolCalls?.Any() ?? false,
        };

        // Calculate duration if timestamps are available
        var durationMs = (long)(toStep.Timestamp - fromStep.Timestamp).TotalMilliseconds;

        var edge = TransitionEdge.CreateSimple(
            fromNode.Id,
            toNode.Id,
            $"{fromStep.State.Kind}To{toStep.State.Kind}",
            operationSpec,
            confidence: null, // Could be extracted from state if available
            durationMs: durationMs > 0 ? durationMs : null);

        return this.dag.AddEdge(edge);
    }

    private Result<TransitionEdge> CreateStepTransition(
        MonadNode fromNode,
        MonadNode toNode,
        StepExecutionEvent fromStep,
        StepExecutionEvent toStep)
    {
        var operationSpec = new
        {
            FromToken = fromStep.TokenName,
            ToToken = toStep.TokenName,
            ToDescription = toStep.Description,
            ToArguments = toStep.Arguments,
        };

        var edge = TransitionEdge.CreateSimple(
            fromNode.Id,
            toNode.Id,
            $"{fromStep.TokenName}→{toStep.TokenName}",
            operationSpec,
            confidence: toStep.Success ? 1.0 : 0.0,
            durationMs: toStep.DurationMs);

        return this.dag.AddEdge(edge);
    }
}

/// <summary>
/// Payload for a step execution node in the MerkleDag.
/// Contains full synopsis of the pipeline token and its execution.
/// </summary>
/// <param name="TokenName">Primary pipeline token name.</param>
/// <param name="Aliases">Alternative names for this token.</param>
/// <param name="SourceClass">Class containing this step.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Arguments">Arguments passed to the step.</param>
/// <param name="Synopsis">Formatted execution synopsis.</param>
/// <param name="DurationMs">Execution duration in milliseconds.</param>
/// <param name="Success">Whether execution succeeded.</param>
/// <param name="Error">Error message if failed.</param>
/// <param name="ExecutedAt">When the step was executed.</param>
public sealed record StepExecutionPayload(
    string TokenName,
    string[] Aliases,
    string SourceClass,
    string Description,
    string? Arguments,
    string Synopsis,
    long? DurationMs,
    bool Success,
    string? Error,
    DateTime ExecutedAt);

/// <summary>
/// Result of a branch reification operation.
/// </summary>
/// <param name="BranchName">Name of the reified branch.</param>
/// <param name="NodesCreated">Number of nodes created.</param>
/// <param name="TransitionsCreated">Number of transitions created.</param>
/// <param name="TotalNodes">Total nodes in the DAG.</param>
/// <param name="TotalTransitions">Total transitions in the DAG.</param>
public sealed record ReificationResult(
    string BranchName,
    int NodesCreated,
    int TransitionsCreated,
    int TotalNodes,
    int TotalTransitions);
