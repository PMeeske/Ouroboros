// <copyright file="PipelineBranch.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Branches;

using System.Collections.Immutable;
using LangChain.DocumentLoaders;

/// <summary>
/// Immutable representation of a pipeline execution branch.
/// Follows functional programming principles with pure operations returning new instances.
/// </summary>
public sealed record PipelineBranch
{
    private readonly ImmutableList<PipelineEvent> events;

    /// <summary>
    /// Gets the name of this branch.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the vector store associated with this branch.
    /// </summary>
    public TrackedVectorStore Store { get; }

    /// <summary>
    /// Gets the data source for this branch.
    /// </summary>
    public DataSource Source { get; }

    /// <summary>
    /// Gets immutable list of events in this branch.
    /// </summary>
    public IReadOnlyList<PipelineEvent> Events => this.events;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBranch"/> class.
    /// Creates a new PipelineBranch instance.
    /// </summary>
    public PipelineBranch(string name, TrackedVectorStore store, DataSource source)
        : this(name, store, source, ImmutableList<PipelineEvent>.Empty)
    {
    }

    /// <summary>
    /// Factory method to create a PipelineBranch with existing events.
    /// Used for deserialization and replay scenarios.
    /// </summary>
    /// <param name="name">The name of the branch.</param>
    /// <param name="store">The vector store.</param>
    /// <param name="source">The data source.</param>
    /// <param name="events">The existing events to initialize with.</param>
    /// <returns>A new PipelineBranch with the specified events.</returns>
    public static PipelineBranch WithEvents(string name, TrackedVectorStore store, DataSource source, IEnumerable<PipelineEvent> events)
    {
        return new PipelineBranch(name, store, source, events.ToImmutableList());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineBranch"/> class.
    /// Internal constructor for creating branches with existing events.
    /// </summary>
    private PipelineBranch(string name, TrackedVectorStore store, DataSource source, ImmutableList<PipelineEvent> events)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.Store = store ?? throw new ArgumentNullException(nameof(store));
        this.Source = source ?? throw new ArgumentNullException(nameof(source));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <summary>
    /// Pure functional operation that returns a new branch with the reasoning event added.
    /// Follows monadic principles by returning a new immutable instance.
    /// </summary>
    /// <param name="state">The reasoning state to add.</param>
    /// <param name="prompt">The prompt used for reasoning.</param>
    /// <param name="tools">Optional tool executions.</param>
    /// <returns>A new PipelineBranch with the reasoning event added.</returns>
    public PipelineBranch WithReasoning(ReasoningState state, string prompt, List<ToolExecution>? tools = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(prompt);

        var newEvent = new ReasoningStep(Guid.NewGuid(), state.Kind, state, DateTime.UtcNow, prompt, tools);
        return new PipelineBranch(this.Name, this.Store, this.Source, this.events.Add(newEvent));
    }

    /// <summary>
    /// Pure functional operation that returns a new branch with the ingest event added.
    /// </summary>
    /// <param name="sourceString">The source identifier.</param>
    /// <param name="ids">The document IDs that were ingested.</param>
    /// <returns>A new PipelineBranch with the ingest event added.</returns>
    public PipelineBranch WithIngestEvent(string sourceString, IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(sourceString);
        ArgumentNullException.ThrowIfNull(ids);

        var newEvent = new IngestBatch(Guid.NewGuid(), sourceString, ids.ToList(), DateTime.UtcNow);
        return new PipelineBranch(this.Name, this.Store, this.Source, this.events.Add(newEvent));
    }

    /// <summary>
    /// Returns a new branch with a different data source while preserving events and store.
    /// </summary>
    /// <param name="source">The new data source.</param>
    /// <returns>A new <see cref="PipelineBranch"/> with the updated source.</returns>
    public PipelineBranch WithSource(DataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new PipelineBranch(this.Name, this.Store, source, this.events);
    }

    /// <summary>
    /// Creates a new branch (fork) with a different name and store, copying all events.
    /// This is a pure functional operation that doesn't modify the original branch.
    /// </summary>
    /// <param name="newName">The name for the forked branch.</param>
    /// <param name="newStore">The vector store for the forked branch.</param>
    /// <returns>A new PipelineBranch that is a fork of this one.</returns>
    public PipelineBranch Fork(string newName, TrackedVectorStore newStore)
    {
        ArgumentNullException.ThrowIfNull(newName);
        ArgumentNullException.ThrowIfNull(newStore);

        return new PipelineBranch(newName, newStore, this.Source, this.events);
    }
}
