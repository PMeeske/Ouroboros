// <copyright file="GlobalProjectionService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Core.Monads;

namespace LangChainPipeline.Pipeline.Branches;

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
/// Global projection service for managing epoch snapshots and metrics.
/// Provides a unified view of the system's evolutionary state.
/// </summary>
public sealed class GlobalProjectionService
{
    private readonly List<EpochSnapshot> _epochs = [];
    private long _nextEpochNumber = 1;

    /// <summary>
    /// Gets all epochs stored in the service.
    /// </summary>
    public IReadOnlyList<EpochSnapshot> Epochs => _epochs.AsReadOnly();

    /// <summary>
    /// Creates a new epoch snapshot from a collection of pipeline branches.
    /// </summary>
    /// <param name="branches">The branches to include in the epoch.</param>
    /// <param name="metadata">Optional metadata to attach to the epoch.</param>
    /// <returns>A Result containing the created epoch snapshot.</returns>
    public async Task<Result<EpochSnapshot>> CreateEpochAsync(
        IEnumerable<PipelineBranch> branches,
        Dictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(branches);

        try
        {
            var branchList = branches.ToList();
            var snapshots = new List<BranchSnapshot>();

            foreach (var branch in branchList)
            {
                var snapshot = await BranchSnapshot.Capture(branch);
                snapshots.Add(snapshot);
            }

            var epoch = new EpochSnapshot
            {
                EpochId = Guid.NewGuid(),
                EpochNumber = _nextEpochNumber++,
                CreatedAt = DateTime.UtcNow,
                Branches = snapshots.AsReadOnly(),
                Metadata = (metadata as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>()
            };

            _epochs.Add(epoch);

            return Result<EpochSnapshot>.Success(epoch);
        }
        catch (Exception ex)
        {
            return Result<EpochSnapshot>.Failure($"Failed to create epoch snapshot: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves an epoch by its number.
    /// </summary>
    /// <param name="epochNumber">The epoch number to retrieve.</param>
    /// <returns>A Result containing the epoch if found.</returns>
    public Result<EpochSnapshot> GetEpoch(long epochNumber)
    {
        var epoch = _epochs.FirstOrDefault(e => e.EpochNumber == epochNumber);
        return epoch is not null
            ? Result<EpochSnapshot>.Success(epoch)
            : Result<EpochSnapshot>.Failure($"Epoch {epochNumber} not found");
    }

    /// <summary>
    /// Retrieves the most recent epoch.
    /// </summary>
    /// <returns>A Result containing the latest epoch if any exist.</returns>
    public Result<EpochSnapshot> GetLatestEpoch()
    {
        var latest = _epochs.OrderByDescending(e => e.EpochNumber).FirstOrDefault();
        return latest is not null
            ? Result<EpochSnapshot>.Success(latest)
            : Result<EpochSnapshot>.Failure("No epochs available");
    }

    /// <summary>
    /// Computes metrics for the current state of the projection service.
    /// </summary>
    /// <returns>A Result containing the computed metrics.</returns>
    public Result<ProjectionMetrics> GetMetrics()
    {
        try
        {
            var totalBranches = _epochs.SelectMany(e => e.Branches).DistinctBy(b => b.Name).Count();
            var totalEvents = _epochs.SelectMany(e => e.Branches).Sum(b => b.Events.Count);
            var lastEpoch = _epochs.OrderByDescending(e => e.EpochNumber).FirstOrDefault();

            var metrics = new ProjectionMetrics
            {
                TotalEpochs = _epochs.Count,
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
    /// Clears all epochs from the service.
    /// Useful for testing or resetting the system state.
    /// </summary>
    public void Clear()
    {
        _epochs.Clear();
        _nextEpochNumber = 1;
    }

    /// <summary>
    /// Gets epochs within a specific time range.
    /// </summary>
    /// <param name="start">The start of the time range (inclusive).</param>
    /// <param name="end">The end of the time range (inclusive).</param>
    /// <returns>A list of epochs within the specified range.</returns>
    public IReadOnlyList<EpochSnapshot> GetEpochsInRange(DateTime start, DateTime end)
    {
        return _epochs
            .Where(e => e.CreatedAt >= start && e.CreatedAt <= end)
            .OrderBy(e => e.EpochNumber)
            .ToList()
            .AsReadOnly();
    }
}
