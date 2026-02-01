// <copyright file="GlobalProjectionArrows.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Events;

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Provides arrow functions for global projection operations using immutable event sourcing.
/// Refactored from GlobalProjectionService to follow the PipelineBranch event sourcing pattern.
/// </summary>
public static class GlobalProjectionArrows
{
    /// <summary>
    /// Creates an epoch snapshot arrow that captures the current branch state and related branches.
    /// Returns a new branch with an EpochCreatedEvent added to its event stream.
    /// </summary>
    /// <param name="relatedBranches">Additional branches to include in the epoch snapshot.</param>
    /// <param name="metadata">Optional metadata to attach to the epoch.</param>
    /// <returns>A step that adds an epoch event to the branch.</returns>
    public static Step<PipelineBranch, PipelineBranch> CreateEpochArrow(
        IEnumerable<PipelineBranch>? relatedBranches = null,
        Dictionary<string, object>? metadata = null) =>
        async branch =>
        {
            // Collect all branches to include in the epoch
            var branches = new List<PipelineBranch> { branch };
            if (relatedBranches != null)
            {
                branches.AddRange(relatedBranches);
            }

            // Capture snapshots of all branches
            var snapshots = new List<BranchSnapshot>();
            // Use Select for functional-style mapping
            var snapshotTasks = branches.Select(b => BranchSnapshot.Capture(b));
            snapshots.AddRange(await Task.WhenAll(snapshotTasks));

            // Determine epoch number from existing epochs in the branch
            long epochNumber = GetNextEpochNumber(branch);

            // Create the epoch snapshot
            var epoch = new EpochSnapshot
            {
                EpochId = Guid.NewGuid(),
                EpochNumber = epochNumber,
                CreatedAt = DateTime.UtcNow,
                Branches = snapshots.AsReadOnly(),
                Metadata = (metadata as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>()
            };

            // Create the epoch event
            var epochEvent = EpochCreatedEvent.FromEpoch(epoch);

            // Return new branch with epoch event added
            return branch.WithEvent(epochEvent);
        };

    /// <summary>
    /// Creates a Result-safe epoch snapshot arrow.
    /// </summary>
    /// <param name="relatedBranches">Additional branches to include in the epoch snapshot.</param>
    /// <param name="metadata">Optional metadata to attach to the epoch.</param>
    /// <returns>A Kleisli arrow that safely creates an epoch.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeCreateEpochArrow(
        IEnumerable<PipelineBranch>? relatedBranches = null,
        Dictionary<string, object>? metadata = null) =>
        async branch =>
        {
            try
            {
                var result = await CreateEpochArrow(relatedBranches, metadata)(branch);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Epoch creation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Gets all epochs from a branch's event stream.
    /// </summary>
    /// <param name="branch">The branch to extract epochs from.</param>
    /// <returns>A read-only list of epoch snapshots.</returns>
    public static IReadOnlyList<EpochSnapshot> GetEpochs(PipelineBranch branch)
    {
        return branch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a specific epoch by its number from a branch's event stream.
    /// </summary>
    /// <param name="branch">The branch to search.</param>
    /// <param name="epochNumber">The epoch number to find.</param>
    /// <returns>A Result containing the epoch if found.</returns>
    public static Result<EpochSnapshot> GetEpoch(PipelineBranch branch, long epochNumber)
    {
        var epoch = branch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .FirstOrDefault(e => e.EpochNumber == epochNumber);

        return epoch is not null
            ? Result<EpochSnapshot>.Success(epoch)
            : Result<EpochSnapshot>.Failure($"Epoch {epochNumber} not found");
    }

    /// <summary>
    /// Gets the most recent epoch from a branch's event stream.
    /// </summary>
    /// <param name="branch">The branch to search.</param>
    /// <returns>A Result containing the latest epoch if any exist.</returns>
    public static Result<EpochSnapshot> GetLatestEpoch(PipelineBranch branch)
    {
        var latest = branch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .OrderByDescending(e => e.EpochNumber)
            .FirstOrDefault();

        return latest is not null
            ? Result<EpochSnapshot>.Success(latest)
            : Result<EpochSnapshot>.Failure("No epochs available");
    }

    /// <summary>
    /// Computes metrics for epochs in a branch's event stream.
    /// </summary>
    /// <param name="branch">The branch to analyze.</param>
    /// <returns>A Result containing the computed metrics.</returns>
    public static Result<ProjectionMetrics> GetMetrics(PipelineBranch branch)
    {
        // Delegate to the centralized metrics computation to avoid duplication.
        return GlobalProjectionService.GetMetrics(branch);
    }

    /// <summary>
    /// Gets epochs within a specific time range from a branch's event stream.
    /// </summary>
    /// <param name="branch">The branch to search.</param>
    /// <param name="start">The start of the time range (inclusive).</param>
    /// <param name="end">The end of the time range (inclusive).</param>
    /// <returns>A list of epochs within the specified range.</returns>
    public static IReadOnlyList<EpochSnapshot> GetEpochsInRange(
        PipelineBranch branch,
        DateTime start,
        DateTime end)
    {
        return branch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch)
            .Where(e => e.CreatedAt >= start && e.CreatedAt <= end)
            .OrderBy(e => e.EpochNumber)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Helper method to determine the next epoch number from existing epochs in the branch.
    /// </summary>
    private static long GetNextEpochNumber(PipelineBranch branch)
    {
        var maxEpochNumber = branch.Events
            .OfType<EpochCreatedEvent>()
            .Select(e => e.Epoch.EpochNumber)
            .DefaultIfEmpty(0)
            .Max();

        return maxEpochNumber + 1;
    }
}
