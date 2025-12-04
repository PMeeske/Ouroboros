#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Collections.Immutable;
using LangChain.DocumentLoaders;
using LangChainPipeline.Domain.Vectors;

namespace LangChainPipeline.Pipeline.Branches;

/// <summary>
/// Immutable representation of a pipeline execution branch.
/// Follows functional programming principles with pure operations returning new instances.
/// </summary>
public sealed record PipelineBranch
{
    private readonly ImmutableList<PipelineEvent> _events;

    /// <summary>
    /// The name of this branch.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The vector store associated with this branch (IOC-injectable).
    /// </summary>
    public IVectorStore Store { get; }

    /// <summary>
    /// The data source for this branch.
    /// </summary>
    public DataSource Source { get; }

    /// <summary>
    /// Immutable list of events in this branch.
    /// </summary>
    public IReadOnlyList<PipelineEvent> Events => _events;

    /// <summary>
    /// Creates a new PipelineBranch instance.
    /// </summary>
    public PipelineBranch(string name, IVectorStore store, DataSource source) : this(name, store, source, ImmutableList<PipelineEvent>.Empty)
    {
    }

    /// <summary>
    /// Factory method to create a PipelineBranch with existing events.
    /// Used for deserialization and replay scenarios.
    /// </summary>
    /// <param name="name">The name of the branch</param>
    /// <param name="store">The vector store</param>
    /// <param name="source">The data source</param>
    /// <param name="events">The existing events to initialize with</param>
    /// <returns>A new PipelineBranch with the specified events</returns>
    public static PipelineBranch WithEvents(string name, IVectorStore store, DataSource source, IEnumerable<PipelineEvent> events)
    {
        return new PipelineBranch(name, store, source, events.ToImmutableList());
    }

    /// <summary>
    /// Internal constructor for creating branches with existing events.
    /// </summary>
    private PipelineBranch(string name, IVectorStore store, DataSource source, ImmutableList<PipelineEvent> events)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <summary>
    /// Pure functional operation that returns a new branch with the reasoning event added.
    /// Follows monadic principles by returning a new immutable instance.
    /// </summary>
    /// <param name="state">The reasoning state to add</param>
    /// <param name="prompt">The prompt used for reasoning</param>
    /// <param name="tools">Optional tool executions</param>
    /// <returns>A new PipelineBranch with the reasoning event added</returns>
    public PipelineBranch WithReasoning(ReasoningState state, string prompt, List<ToolExecution>? tools = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(prompt);

        ReasoningStep newEvent = new ReasoningStep(Guid.NewGuid(), state.Kind, state, DateTime.UtcNow, prompt, tools);
        return new PipelineBranch(Name, Store, Source, _events.Add(newEvent));
    }

    /// <summary>
    /// Pure functional operation that returns a new branch with the ingest event added.
    /// </summary>
    /// <param name="sourceString">The source identifier</param>
    /// <param name="ids">The document IDs that were ingested</param>
    /// <returns>A new PipelineBranch with the ingest event added</returns>
    public PipelineBranch WithIngestEvent(string sourceString, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(sourceString);
        ArgumentNullException.ThrowIfNull(ids);

        IngestBatch newEvent = new IngestBatch(Guid.NewGuid(), sourceString, ids.ToList(), DateTime.UtcNow);
        return new PipelineBranch(Name, Store, Source, _events.Add(newEvent));
    }

    /// <summary>
    /// Returns a new branch with a different data source while preserving events and store.
    /// </summary>
    /// <param name="source">The new data source.</param>
    /// <returns>A new <see cref="PipelineBranch"/> with the updated source.</returns>
    public PipelineBranch WithSource(DataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new PipelineBranch(Name, Store, source, _events);
    }

    /// <summary>
    /// Creates a new branch (fork) with a different name and store, copying all events.
    /// This is a pure functional operation that doesn't modify the original branch.
    /// </summary>
    /// <param name="newName">The name for the forked branch</param>
    /// <param name="newStore">The vector store for the forked branch</param>
    /// <returns>A new PipelineBranch that is a fork of this one</returns>
    public PipelineBranch Fork(string newName, IVectorStore newStore)
    {
        ArgumentNullException.ThrowIfNull(newName);
        ArgumentNullException.ThrowIfNull(newStore);

        return new PipelineBranch(newName, newStore, Source, _events);
    }
}
