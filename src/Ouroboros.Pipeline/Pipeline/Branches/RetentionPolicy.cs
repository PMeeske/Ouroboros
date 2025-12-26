// <copyright file="RetentionPolicy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Defines retention policies for pipeline branch snapshots.
/// Supports time-based and count-based retention strategies.
/// </summary>
public sealed record RetentionPolicy
{
    /// <summary>
    /// Gets or sets the maximum age of snapshots to retain.
    /// Snapshots older than this will be marked for deletion.
    /// </summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of snapshots to retain per branch.
    /// When exceeded, oldest snapshots are marked for deletion.
    /// </summary>
    public int? MaxCount { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to keep at least one snapshot per branch regardless of age or count.
    /// </summary>
    public bool KeepAtLeastOne { get; init; } = true;

    /// <summary>
    /// Creates a retention policy based on maximum age.
    /// </summary>
    /// <param name="maxAge">Maximum age of snapshots to retain.</param>
    /// <returns>A new retention policy instance.</returns>
    public static RetentionPolicy ByAge(TimeSpan maxAge) => new() { MaxAge = maxAge };

    /// <summary>
    /// Creates a retention policy based on maximum count.
    /// </summary>
    /// <param name="maxCount">Maximum number of snapshots to retain.</param>
    /// <returns>A new retention policy instance.</returns>
    public static RetentionPolicy ByCount(int maxCount) => new() { MaxCount = maxCount };

    /// <summary>
    /// Creates a retention policy that combines age and count constraints.
    /// </summary>
    /// <param name="maxAge">Maximum age of snapshots to retain.</param>
    /// <param name="maxCount">Maximum number of snapshots to retain.</param>
    /// <returns>A new retention policy instance.</returns>
    public static RetentionPolicy Combined(TimeSpan maxAge, int maxCount) => new()
    {
        MaxAge = maxAge,
        MaxCount = maxCount
    };

    /// <summary>
    /// Creates a permissive retention policy that keeps everything.
    /// </summary>
    /// <returns>A new retention policy instance that retains all snapshots.</returns>
    public static RetentionPolicy KeepAll() => new();
}

/// <summary>
/// Represents a snapshot with metadata for retention evaluation.
/// </summary>
public sealed record SnapshotMetadata
{
    /// <summary>
    /// Gets the snapshot identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the branch name.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Gets the timestamp when the snapshot was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the snapshot hash for integrity verification.
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Gets the size of the snapshot in bytes.
    /// </summary>
    public long SizeBytes { get; init; }
}

/// <summary>
/// Result of a retention plan evaluation.
/// </summary>
public sealed record RetentionPlan
{
    /// <summary>
    /// Gets the snapshots to keep.
    /// </summary>
    public required IReadOnlyList<SnapshotMetadata> ToKeep { get; init; }

    /// <summary>
    /// Gets the snapshots to delete.
    /// </summary>
    public required IReadOnlyList<SnapshotMetadata> ToDelete { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a dry run (no actual deletions will occur).
    /// </summary>
    public bool IsDryRun { get; init; } = true;

    /// <summary>
    /// Gets a summary of the retention plan.
    /// </summary>
    /// <returns>A human-readable summary string.</returns>
    public string GetSummary() =>
        $"Retention Plan ({(IsDryRun ? "DRY RUN" : "LIVE")}): " +
        $"Keep {ToKeep.Count} snapshots, Delete {ToDelete.Count} snapshots";
}

/// <summary>
/// Evaluates retention policies and generates retention plans.
/// </summary>
public static class RetentionEvaluator
{
    /// <summary>
    /// Evaluates a retention policy against a collection of snapshots.
    /// </summary>
    /// <param name="snapshots">The snapshots to evaluate.</param>
    /// <param name="policy">The retention policy to apply.</param>
    /// <param name="dryRun">Whether this is a dry run (default: true).</param>
    /// <returns>A retention plan indicating which snapshots to keep and delete.</returns>
    public static RetentionPlan Evaluate(
        IEnumerable<SnapshotMetadata> snapshots,
        RetentionPolicy policy,
        bool dryRun = true)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(policy);

        var snapshotList = snapshots.ToList();
        var now = DateTime.UtcNow;

        // Group by branch for per-branch evaluation
        var byBranch = snapshotList.GroupBy(s => s.BranchName);
        var toKeep = new List<SnapshotMetadata>();
        var toDelete = new List<SnapshotMetadata>();

        foreach (var branchGroup in byBranch)
        {
            var branchSnapshots = branchGroup.OrderByDescending(s => s.CreatedAt).ToList();

            // Apply age filter if specified
            var afterAgeFilter = policy.MaxAge.HasValue
                ? branchSnapshots.Where(s => now - s.CreatedAt <= policy.MaxAge.Value).ToList()
                : branchSnapshots;

            // Apply count filter if specified
            var afterCountFilter = policy.MaxCount.HasValue
                ? afterAgeFilter.Take(policy.MaxCount.Value).ToList()
                : afterAgeFilter;

            // Ensure at least one snapshot is kept if required
            if (policy.KeepAtLeastOne && afterCountFilter.Count == 0 && branchSnapshots.Count > 0)
            {
                afterCountFilter = [branchSnapshots.First()];
            }

            toKeep.AddRange(afterCountFilter);
            toDelete.AddRange(branchSnapshots.Except(afterCountFilter));
        }

        return new RetentionPlan
        {
            ToKeep = toKeep.AsReadOnly(),
            ToDelete = toDelete.AsReadOnly(),
            IsDryRun = dryRun
        };
    }
}
