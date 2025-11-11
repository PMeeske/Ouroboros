// <copyright file="IEventStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Persistence;

using LangChainPipeline.Domain.Events;

/// <summary>
/// Interface for persisting and retrieving pipeline events (event sourcing).
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends events to the event stream for a specific branch.
    /// </summary>
    /// <param name="branchId">The branch identifier.</param>
    /// <param name="events">Events to append.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency control (-1 for any version).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new version after appending events.</returns>
    Task<long> AppendEventsAsync(
        string branchId,
        IEnumerable<PipelineEvent> events,
        long expectedVersion = -1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all events for a specific branch.
    /// </summary>
    /// <param name="branchId">The branch identifier.</param>
    /// <param name="fromVersion">Start from this version (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sequence of events in order.</returns>
    Task<IReadOnlyList<PipelineEvent>> GetEventsAsync(
        string branchId,
        long fromVersion = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version for a branch.
    /// </summary>
    /// <param name="branchId">The branch identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current version, or -1 if branch doesn't exist.</returns>
    Task<long> GetVersionAsync(
        string branchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a branch exists in the store.
    /// </summary>
    /// <param name="branchId">The branch identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if branch exists.</returns>
    Task<bool> BranchExistsAsync(
        string branchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all events for a branch (use with caution).
    /// </summary>
    /// <param name="branchId">The branch identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task DeleteBranchAsync(
        string branchId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when optimistic concurrency check fails.
/// </summary>
public class ConcurrencyException : Exception
{
    /// <summary>
    /// Gets expected version.
    /// </summary>
    public long ExpectedVersion { get; }

    /// <summary>
    /// Gets actual version.
    /// </summary>
    public long ActualVersion { get; }

    /// <summary>
    /// Gets branch ID where concurrency conflict occurred.
    /// </summary>
    public string BranchId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
    /// Initializes a concurrency exception.
    /// </summary>
    public ConcurrencyException(string branchId, long expectedVersion, long actualVersion)
        : base($"Concurrency conflict for branch '{branchId}': expected version {expectedVersion}, actual version {actualVersion}")
    {
        this.BranchId = branchId;
        this.ExpectedVersion = expectedVersion;
        this.ActualVersion = actualVersion;
    }

    public ConcurrencyException() : base()
    {
    }

    public ConcurrencyException(string? message) : base(message)
    {
    }

    public ConcurrencyException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
