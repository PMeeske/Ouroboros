using LangChain.DocumentLoaders;

namespace LangChainPipeline.Pipeline.Branches;

public sealed class PipelineBranch(string name, TrackedVectorStore store, DataSource source)
{
    internal readonly List<PipelineEvent> EventsInternal = new();
    public string Name { get; } = name;
    public TrackedVectorStore Store { get; } = store;
    public IReadOnlyList<PipelineEvent> Events => EventsInternal;
    public DataSource Source { get; } = source;

    public void AddReasoning(ReasoningState state, string prompt, List<ToolExecution>? tools = null) =>
        EventsInternal.Add(new ReasoningStep(Guid.NewGuid(), state.Kind, state, DateTime.UtcNow, prompt, tools));

    public void AddIngestEvent(string sourceString, IEnumerable<string> ids) =>
        EventsInternal.Add(new IngestBatch(Guid.NewGuid(), sourceString, ids.ToList(), DateTime.UtcNow));

    public PipelineBranch Fork(string newName, TrackedVectorStore newStore)
    {
        PipelineBranch fork = new PipelineBranch(newName, newStore, Source);
        fork.EventsInternal.AddRange(EventsInternal);
        return fork;
    }
}
