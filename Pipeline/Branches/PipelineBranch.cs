using LangChain.DocumentLoaders;

namespace LangChainPipeline.Pipeline.Branches;

public sealed class PipelineBranch(string name, TrackedVectorStore store, DataSource source)
{
    internal readonly List<PipelineEvent> EventsInternal = new();
    public string Name { get; } = name;
    public TrackedVectorStore Store { get; } = store;
    public IReadOnlyList<PipelineEvent> Events => EventsInternal;
    public DataSource Source { get; } = source;

    /// <summary>
    /// Creates a new immutable PipelineBranch instance with an additional reasoning step.
    /// This is the functional programming approach - it returns a new instance rather than mutating state.
    /// </summary>
    /// <param name="state">The reasoning state to add.</param>
    /// <param name="prompt">The prompt used for reasoning.</param>
    /// <param name="tools">Optional tool executions.</param>
    /// <returns>A new PipelineBranch instance with the reasoning step added.</returns>
    public PipelineBranch WithReasoning(ReasoningState state, string prompt, List<ToolExecution>? tools = null)
    {
        var newBranch = new PipelineBranch(Name, Store, Source);
        newBranch.EventsInternal.AddRange(EventsInternal);
        newBranch.EventsInternal.Add(new ReasoningStep(Guid.NewGuid(), state.Kind, state, DateTime.UtcNow, prompt, tools));
        return newBranch;
    }

    /// <summary>
    /// Creates a new immutable PipelineBranch instance with an additional ingest event.
    /// This is the functional programming approach - it returns a new instance rather than mutating state.
    /// </summary>
    /// <param name="sourceString">The source string for the ingestion.</param>
    /// <param name="ids">The document IDs that were ingested.</param>
    /// <returns>A new PipelineBranch instance with the ingest event added.</returns>
    public PipelineBranch WithIngestEvent(string sourceString, IEnumerable<string> ids)
    {
        var newBranch = new PipelineBranch(Name, Store, Source);
        newBranch.EventsInternal.AddRange(EventsInternal);
        newBranch.EventsInternal.Add(new IngestBatch(Guid.NewGuid(), sourceString, ids.ToList(), DateTime.UtcNow));
        return newBranch;
    }

    [Obsolete("Use WithReasoning() for immutable updates. This method will be removed in a future version.")]
    public void AddReasoning(ReasoningState state, string prompt, List<ToolExecution>? tools = null)
    {
        // This is intentionally not implemented to force migration to functional style
        throw new InvalidOperationException(
            "AddReasoning is obsolete. Use WithReasoning() to get a new immutable instance instead of mutating state.");
    }

    /// <summary>
    /// Legacy compatibility method for existing code that expects void AddIngestEvent.
    /// This method mutates the current instance and should be avoided in new code.
    /// Use WithIngestEvent() for functional style.
    /// </summary>
    [Obsolete("Use WithIngestEvent() for immutable updates. This method will be removed in a future version.")]
    public void AddIngestEvent(string sourceString, IEnumerable<string> ids)
    {
        // This is intentionally not implemented to force migration to functional style
        throw new InvalidOperationException(
            "AddIngestEvent is obsolete. Use WithIngestEvent() to get a new immutable instance instead of mutating state.");
    }

    public PipelineBranch Fork(string newName, TrackedVectorStore newStore)
    {
        PipelineBranch fork = new PipelineBranch(newName, newStore, Source);
        fork.EventsInternal.AddRange(EventsInternal);
        return fork;
    }
}
