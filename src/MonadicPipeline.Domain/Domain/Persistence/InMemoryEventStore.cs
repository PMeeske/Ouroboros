// <copyright file="InMemoryEventStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Persistence;

using System.Collections.Concurrent;
using LangChainPipeline.Domain.Events;

/// <summary>
/// In-memory implementation of event store for development and testing.
/// </summary>
public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, EventStream> streams = new();

    /// <inheritdoc/>
    public Task<long> AppendEventsAsync(
        string branchId,
        IEnumerable<PipelineEvent> events,
        long expectedVersion = -1,
        CancellationToken cancellationToken = default)
    {
        var eventsList = events.ToList();
        if (!eventsList.Any())
        {
            return Task.FromResult(this.GetCurrentVersion(branchId));
        }

        var stream = this.streams.GetOrAdd(branchId, _ => new EventStream(branchId));

        lock (stream.Lock)
        {
            // Optimistic concurrency check
            if (expectedVersion != -1 && stream.Version != expectedVersion)
            {
                throw new ConcurrencyException(branchId, expectedVersion, stream.Version);
            }

            foreach (var evt in eventsList)
            {
                stream.Version++;
                stream.Events.Add(new VersionedEvent(evt, stream.Version));
            }

            return Task.FromResult(stream.Version);
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PipelineEvent>> GetEventsAsync(
        string branchId,
        long fromVersion = 0,
        CancellationToken cancellationToken = default)
    {
        if (!this.streams.TryGetValue(branchId, out var stream))
        {
            return Task.FromResult<IReadOnlyList<PipelineEvent>>(Array.Empty<PipelineEvent>());
        }

        lock (stream.Lock)
        {
            var events = stream.Events
                .Where(ve => ve.Version >= fromVersion)
                .Select(ve => ve.Event)
                .ToList();

            return Task.FromResult<IReadOnlyList<PipelineEvent>>(events);
        }
    }

    /// <inheritdoc/>
    public Task<long> GetVersionAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var version = this.GetCurrentVersion(branchId);
        return Task.FromResult(version);
    }

    /// <inheritdoc/>
    public Task<bool> BranchExistsAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var exists = this.streams.ContainsKey(branchId);
        return Task.FromResult(exists);
    }

    /// <inheritdoc/>
    public Task DeleteBranchAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        this.streams.TryRemove(branchId, out _);
        return Task.CompletedTask;
    }

    private long GetCurrentVersion(string branchId)
    {
        return this.streams.TryGetValue(branchId, out var stream) ? stream.Version : -1;
    }

    private class EventStream
    {
        public string BranchId { get; }

        public List<VersionedEvent> Events { get; } = new();

        public long Version { get; set; } = -1;

        public object Lock { get; } = new();

        public EventStream(string branchId)
        {
            this.BranchId = branchId;
        }
    }

    private class VersionedEvent
    {
        public PipelineEvent Event { get; }

        public long Version { get; }

        public VersionedEvent(PipelineEvent evt, long version)
        {
            this.Event = evt;
            this.Version = version;
        }
    }
}
