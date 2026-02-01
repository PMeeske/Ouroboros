// <copyright file="GlobalProjectionService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Represents an epoch snapshot of the global system state.
/// </summary>
public sealed record EpochSnapshot
{
    /// <summary>
    /// Gets the unique identifier for this epoch.
    /// </summary>
    public required Guid EpochId { get; init; }

    /// <summary>
    /// Gets the epoch number (incremental).
    /// </summary>
    public required long EpochNumber { get; init; }

    /// <summary>
    /// Gets the timestamp when this epoch was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the collection of branch snapshots in this epoch.
    /// </summary>
    public required IReadOnlyList<BranchSnapshot> Branches { get; init; }

    /// <summary>
    /// Gets the metadata associated with this epoch.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the hash of this epoch for integrity verification.
    /// </summary>
    public string? Hash { get; init; }
}

/// <summary>
/// Metrics for the global projection service.
/// </summary>
public sealed record ProjectionMetrics
{
    /// <summary>
    /// Gets the total number of epochs created.
    /// </summary>
    public required long TotalEpochs { get; init; }

    /// <summary>
    /// Gets the total number of branches tracked.
    /// </summary>
    public required int TotalBranches { get; init; }

    /// <summary>
    /// Gets the total number of events across all branches.
    /// </summary>
    public required long TotalEvents { get; init; }

    /// <summary>
    /// Gets the timestamp of the most recent epoch.
    /// </summary>
    public DateTime? LastEpochAt { get; init; }

    /// <summary>
    /// Gets the average events per branch.
    /// </summary>
    public double AverageEventsPerBranch => TotalBranches > 0
        ? (double)TotalEvents / TotalBranches
        : 0.0;

    /// <summary>
    /// Gets additional custom metrics.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomMetrics { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Global projection service for managing epoch snapshots and metrics using immutable event sourcing.
/// Refactored to follow the PipelineBranch event sourcing pattern - all state is tracked through events.
/// This class now provides static methods that work with PipelineBranch instead of maintaining mutable state.
/// </summary>
public static class GlobalProjectionService
{
    /// <summary>
    /// Creates a new epoch snapshot arrow that captures branches and returns updated branch with epoch event.
    /// </summary>
    /// <param name="branches">The branches to include in the epoch.</param>
    /// <param name="metadata">Optional metadata to attach to the epoch.</param>
    /// <returns>A step that adds an epoch event to the tracking branch.</returns>
    public static Step<PipelineBranch, PipelineBranch> CreateEpochArrow(
        IEnumerable<PipelineBranch> branches,
        Dictionary<string, object>? metadata = null) =>
        async trackingBranch =>
        {
            ArgumentNullException.ThrowIfNull(branches);

            var branchList = branches.ToList();
            var snapshots = new List<BranchSnapshot>();

            foreach (var branch in branchList)
            {
                var snapshot = await BranchSnapshot.Capture(branch);
                snapshots.Add(snapshot);
            }

            // Determine next epoch number from existing epochs in tracking branch
            long epochNumber = GetNextEpochNumber(trackingBranch);

            var epoch = new EpochSnapshot
            {
                EpochId = Guid.NewGuid(),
                EpochNumber = epochNumber,
                CreatedAt = DateTime.UtcNow,
                Branches = snapshots.AsReadOnly(),
                Metadata = (metadata as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>()
            };

            var epochEvent = EpochCreatedEvent.FromEpoch(epoch);
            return trackingBranch.WithEvent(epochEvent);
        };

    /// <summary>
    /// Creates a new epoch snapshot from a collection of pipeline branches (async version).
    /// Returns both the created epoch and the updated tracking branch.
    /// </summary>
    /// <param name="trackingBranch">The branch used to track epochs.</param>
    /// <param name="branches">The branches to include in the epoch.</param>
    /// <param name="metadata">Optional metadata to attach to the epoch.</param>
    /// <returns>A Result containing the created epoch and updated tracking branch.</returns>
    public static async Task<Result<(EpochSnapshot Epoch, PipelineBranch UpdatedBranch)>> CreateEpochAsync(
        PipelineBranch trackingBranch,
        IEnumerable<PipelineBranch> branches,
        Dictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(trackingBranch);

        try
        {
            var arrow = CreateEpochArrow(branches, metadata);
            var updatedBranch = await arrow(trackingBranch);
            var epoch = GetLatestEpoch(updatedBranch).Value;
            return Result<(EpochSnapshot, PipelineBranch)>.Success((epoch, updatedBranch));
        }
        catch (Exception ex)
        {
            return Result<(EpochSnapshot, PipelineBranch)>.Failure($"Failed to create epoch snapshot: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all epochs from a tracking branch's event stream.
    /// </summary>
    /// <param name="trackingBranch">The branch containing epoch events.</param>
    /// <returns>A read-only list of epoch snapshots.</returns>
    public static IReadOnlyList<EpochSnapshot> GetEpochs(PipelineBranch trackingBranch)
    {
        ArgumentNullException.ThrowIfNull(trackingBranch);
        
        return trackingBranch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Retrieves an epoch by its number from a tracking branch.
    /// </summary>
    /// <param name="trackingBranch">The branch containing epoch events.</param>
    /// <param name="epochNumber">The epoch number to retrieve.</param>
    /// <returns>A Result containing the epoch if found.</returns>
    public static Result<EpochSnapshot> GetEpoch(PipelineBranch trackingBranch, long epochNumber)
    {
        ArgumentNullException.ThrowIfNull(trackingBranch);
        
        var epoch = trackingBranch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .FirstOrDefault(e => e.EpochNumber == epochNumber);

        return epoch is not null
            ? Result<EpochSnapshot>.Success(epoch)
            : Result<EpochSnapshot>.Failure($"Epoch {epochNumber} not found");
    }

    /// <summary>
    /// Retrieves the most recent epoch from a tracking branch.
    /// </summary>
    /// <param name="trackingBranch">The branch containing epoch events.</param>
    /// <returns>A Result containing the latest epoch if any exist.</returns>
    public static Result<EpochSnapshot> GetLatestEpoch(PipelineBranch trackingBranch)
    {
        ArgumentNullException.ThrowIfNull(trackingBranch);
        
        var latest = trackingBranch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .OrderByDescending(e => e.EpochNumber)
            .FirstOrDefault();

        return latest is not null
            ? Result<EpochSnapshot>.Success(latest)
            : Result<EpochSnapshot>.Failure("No epochs available");
    }

    /// <summary>
    /// Computes metrics for epochs in a tracking branch.
    /// </summary>
    /// <param name="trackingBranch">The branch containing epoch events.</param>
    /// <returns>A Result containing the computed metrics.</returns>
    public static Result<ProjectionMetrics> GetMetrics(PipelineBranch trackingBranch)
    {
        ArgumentNullException.ThrowIfNull(trackingBranch);
        
        try
        {
            var epochs = GetEpochs(trackingBranch);
            var totalBranches = epochs.SelectMany(e => e.Branches).DistinctBy(b => b.Name).Count();
            var totalEvents = epochs.SelectMany(e => e.Branches).Sum(b => b.Events.Count);
            var lastEpoch = epochs.OrderByDescending(e => e.EpochNumber).FirstOrDefault();

            var metrics = new ProjectionMetrics
            {
                TotalEpochs = epochs.Count,
                TotalBranches = totalBranches,
                TotalEvents = totalEvents,
                LastEpochAt = lastEpoch?.CreatedAt
            };

            return Result<ProjectionMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            return Result<ProjectionMetrics>.Failure($"Failed to compute metrics: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets epochs within a specific time range from a tracking branch.
    /// </summary>
    /// <param name="trackingBranch">The branch containing epoch events.</param>
    /// <param name="start">The start of the time range (inclusive).</param>
    /// <param name="end">The end of the time range (inclusive).</param>
    /// <returns>A list of epochs within the specified range.</returns>
    public static IReadOnlyList<EpochSnapshot> GetEpochsInRange(
        PipelineBranch trackingBranch,
        DateTime start,
        DateTime end)
    {
        ArgumentNullException.ThrowIfNull(trackingBranch);
        
        return trackingBranch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .Where(e => e.CreatedAt >= start && e.CreatedAt <= end)
            .OrderBy(e => e.EpochNumber)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Helper method to determine the next epoch number from existing epochs in a branch.
    /// </summary>
    private static long GetNextEpochNumber(PipelineBranch trackingBranch)
    {
        var maxEpochNumber = trackingBranch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch.EpochNumber)
            .DefaultIfEmpty(0)
            .Max();

        return maxEpochNumber + 1;
    }
}
