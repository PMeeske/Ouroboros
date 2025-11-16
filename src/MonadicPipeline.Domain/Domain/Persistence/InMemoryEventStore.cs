#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Collections.Concurrent;
using LangChainPipeline.Domain.Events;

namespace LangChainPipeline.Domain.Persistence;

/// <summary>
/// In-memory implementation of event store for development and testing.
/// </summary>
public class InMemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, EventStream> _streams = new();

    /// <inheritdoc/>
    public Task<long> AppendEventsAsync(
        string branchId,
        IEnumerable<PipelineEvent> events,
        long expectedVersion = -1,
        CancellationToken cancellationToken = default)
    {
        List<PipelineEvent> eventsList = events.ToList();
        if (!eventsList.Any())
        {
            return Task.FromResult(GetCurrentVersion(branchId));
        }

        EventStream stream = _streams.GetOrAdd(branchId, _ => new EventStream(branchId));

        lock (stream.Lock)
        {
            // Optimistic concurrency check
            if (expectedVersion != -1 && stream.Version != expectedVersion)
            {
                throw new ConcurrencyException(branchId, expectedVersion, stream.Version);
            }

            foreach (PipelineEvent? evt in eventsList)
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
        if (!_streams.TryGetValue(branchId, out EventStream? stream))
        {
            return Task.FromResult<IReadOnlyList<PipelineEvent>>(Array.Empty<PipelineEvent>());
        }

        lock (stream.Lock)
        {
            List<PipelineEvent> events = stream.Events
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
        long version = GetCurrentVersion(branchId);
        return Task.FromResult(version);
    }

    /// <inheritdoc/>
    public Task<bool> BranchExistsAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        bool exists = _streams.ContainsKey(branchId);
        return Task.FromResult(exists);
    }

    /// <inheritdoc/>
    public Task DeleteBranchAsync(
        string branchId,
        CancellationToken cancellationToken = default)
    {
        _streams.TryRemove(branchId, out _);
        return Task.CompletedTask;
    }

    private long GetCurrentVersion(string branchId)
    {
        return _streams.TryGetValue(branchId, out EventStream? stream) ? stream.Version : -1;
    }

    private class EventStream
    {
        public string BranchId { get; }
        public List<VersionedEvent> Events { get; } = new();
        public long Version { get; set; } = -1;
        public object Lock { get; } = new();

        public EventStream(string branchId)
        {
            BranchId = branchId;
        }
    }

    private class VersionedEvent
    {
        public PipelineEvent Event { get; }
        public long Version { get; }

        public VersionedEvent(PipelineEvent evt, long version)
        {
            Event = evt;
            Version = version;
        }
    }
}
