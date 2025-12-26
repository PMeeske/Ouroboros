using FluentAssertions;
using Ouroboros.Pipeline.Branches;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Comprehensive tests for retention policies and evaluator.
/// Tests focus on age-based, count-based, and combined retention strategies.
/// </summary>
public sealed class RetentionPolicyTests
{
    #region RetentionPolicy Factory Tests

    [Fact]
    public void ByAge_ShouldCreatePolicyWithMaxAge()
    {
        // Arrange
        var maxAge = TimeSpan.FromDays(30);

        // Act
        var policy = RetentionPolicy.ByAge(maxAge);

        // Assert
        policy.MaxAge.Should().Be(maxAge);
        policy.MaxCount.Should().BeNull();
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void ByCount_ShouldCreatePolicyWithMaxCount()
    {
        // Arrange
        var maxCount = 10;

        // Act
        var policy = RetentionPolicy.ByCount(maxCount);

        // Assert
        policy.MaxCount.Should().Be(maxCount);
        policy.MaxAge.Should().BeNull();
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void Combined_ShouldCreatePolicyWithBothConstraints()
    {
        // Arrange
        var maxAge = TimeSpan.FromDays(30);
        var maxCount = 10;

        // Act
        var policy = RetentionPolicy.Combined(maxAge, maxCount);

        // Assert
        policy.MaxAge.Should().Be(maxAge);
        policy.MaxCount.Should().Be(maxCount);
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void KeepAll_ShouldCreatePermissivePolicy()
    {
        // Act
        var policy = RetentionPolicy.KeepAll();

        // Assert
        policy.MaxAge.Should().BeNull();
        policy.MaxCount.Should().BeNull();
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    #endregion

    #region RetentionEvaluator - Age-Based Tests

    [Fact]
    public void Evaluate_WithAgePolicy_ShouldKeepRecentSnapshots()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateSnapshot("snap1", "branch1", now.AddDays(-10)),
            CreateSnapshot("snap2", "branch1", now.AddDays(-5)),
            CreateSnapshot("snap3", "branch1", now.AddDays(-1))
        };
        var policy = RetentionPolicy.ByAge(TimeSpan.FromDays(7));

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(2); // snap2 and snap3
        plan.ToDelete.Should().HaveCount(1); // snap1
        plan.IsDryRun.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithAgePolicy_AllOld_ShouldKeepAtLeastOne()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateSnapshot("snap1", "branch1", now.AddDays(-100)),
            CreateSnapshot("snap2", "branch1", now.AddDays(-50)),
            CreateSnapshot("snap3", "branch1", now.AddDays(-30))
        };
        var policy = RetentionPolicy.ByAge(TimeSpan.FromDays(7));

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(1); // Most recent (snap3)
        plan.ToDelete.Should().HaveCount(2); // snap1 and snap2
    }

    #endregion

    #region RetentionEvaluator - Count-Based Tests

    [Fact]
    public void Evaluate_WithCountPolicy_ShouldKeepMostRecent()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateSnapshot("snap1", "branch1", now.AddDays(-5)),
            CreateSnapshot("snap2", "branch1", now.AddDays(-4)),
            CreateSnapshot("snap3", "branch1", now.AddDays(-3)),
            CreateSnapshot("snap4", "branch1", now.AddDays(-2)),
            CreateSnapshot("snap5", "branch1", now.AddDays(-1))
        };
        var policy = RetentionPolicy.ByCount(3);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(3); // snap3, snap4, snap5
        plan.ToDelete.Should().HaveCount(2); // snap1, snap2
    }

    [Fact]
    public void Evaluate_WithCountPolicy_LessThanMax_ShouldKeepAll()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateSnapshot("snap1", "branch1", now.AddDays(-2)),
            CreateSnapshot("snap2", "branch1", now.AddDays(-1))
        };
        var policy = RetentionPolicy.ByCount(5);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(2);
        plan.ToDelete.Should().BeEmpty();
    }

    #endregion

    #region RetentionEvaluator - Combined Policy Tests

    [Fact]
    public void Evaluate_WithCombinedPolicy_ShouldApplyBothConstraints()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateSnapshot("snap1", "branch1", now.AddDays(-20)), // Old, excluded by age
            CreateSnapshot("snap2", "branch1", now.AddDays(-15)), // Old, excluded by age
            CreateSnapshot("snap3", "branch1", now.AddDays(-5)),  // Recent
            CreateSnapshot("snap4", "branch1", now.AddDays(-4)),  // Recent
            CreateSnapshot("snap5", "branch1", now.AddDays(-3)),  // Recent
            CreateSnapshot("snap6", "branch1", now.AddDays(-2)),  // Recent
            CreateSnapshot("snap7", "branch1", now.AddDays(-1))   // Recent
        };
        var policy = RetentionPolicy.Combined(TimeSpan.FromDays(10), 3);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(3); // snap5, snap6, snap7 (3 most recent within age limit)
        plan.ToDelete.Should().HaveCount(4);
    }

    #endregion

    #region RetentionEvaluator - Multi-Branch Tests

    [Fact]
    public void Evaluate_WithMultipleBranches_ShouldApplyPolicyPerBranch()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateSnapshot("snap1", "branch1", now.AddDays(-5)),
            CreateSnapshot("snap2", "branch1", now.AddDays(-1)),
            CreateSnapshot("snap3", "branch2", now.AddDays(-5)),
            CreateSnapshot("snap4", "branch2", now.AddDays(-1))
        };
        var policy = RetentionPolicy.ByCount(1);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(2); // One per branch (snap2, snap4)
        plan.ToDelete.Should().HaveCount(2); // snap1, snap3
    }

    [Fact]
    public void Evaluate_WithKeepAll_ShouldKeepAllSnapshots()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateSnapshot("snap1", "branch1", now.AddDays(-100)),
            CreateSnapshot("snap2", "branch1", now.AddDays(-50)),
            CreateSnapshot("snap3", "branch1", now.AddDays(-1))
        };
        var policy = RetentionPolicy.KeepAll();

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(3);
        plan.ToDelete.Should().BeEmpty();
    }

    #endregion

    #region RetentionEvaluator - Dry Run Tests

    [Fact]
    public void Evaluate_WithDryRunTrue_ShouldMarkAsDryRun()
    {
        // Arrange
        var snapshots = new[] { CreateSnapshot("snap1", "branch1", DateTime.UtcNow) };
        var policy = RetentionPolicy.KeepAll();

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy, dryRun: true);

        // Assert
        plan.IsDryRun.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithDryRunFalse_ShouldMarkAsLive()
    {
        // Arrange
        var snapshots = new[] { CreateSnapshot("snap1", "branch1", DateTime.UtcNow) };
        var policy = RetentionPolicy.KeepAll();

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy, dryRun: false);

        // Assert
        plan.IsDryRun.Should().BeFalse();
    }

    #endregion

    #region RetentionPlan Tests

    [Fact]
    public void GetSummary_ShouldReturnHumanReadableString()
    {
        // Arrange
        var plan = new RetentionPlan
        {
            ToKeep = new[] { CreateSnapshot("s1", "b1", DateTime.UtcNow) },
            ToDelete = new[] { CreateSnapshot("s2", "b1", DateTime.UtcNow) },
            IsDryRun = true
        };

        // Act
        var summary = plan.GetSummary();

        // Assert
        summary.Should().Contain("DRY RUN");
        summary.Should().Contain("Keep 1");
        summary.Should().Contain("Delete 1");
    }

    [Fact]
    public void GetSummary_WhenLive_ShouldIndicateLiveRun()
    {
        // Arrange
        var plan = new RetentionPlan
        {
            ToKeep = Array.Empty<SnapshotMetadata>(),
            ToDelete = Array.Empty<SnapshotMetadata>(),
            IsDryRun = false
        };

        // Act
        var summary = plan.GetSummary();

        // Assert
        summary.Should().Contain("LIVE");
        summary.Should().NotContain("DRY RUN");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Evaluate_WithEmptySnapshots_ShouldReturnEmptyPlan()
    {
        // Arrange
        var snapshots = Array.Empty<SnapshotMetadata>();
        var policy = RetentionPolicy.ByCount(5);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().BeEmpty();
        plan.ToDelete.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WithNullSnapshots_ShouldThrowArgumentNullException()
    {
        // Arrange
        var policy = RetentionPolicy.KeepAll();

        // Act
        Action act = () => RetentionEvaluator.Evaluate(null!, policy);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Evaluate_WithNullPolicy_ShouldThrowArgumentNullException()
    {
        // Arrange
        var snapshots = new[] { CreateSnapshot("s1", "b1", DateTime.UtcNow) };

        // Act
        Action act = () => RetentionEvaluator.Evaluate(snapshots, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Helper Methods

    private static SnapshotMetadata CreateSnapshot(string id, string branchName, DateTime createdAt)
    {
        return new SnapshotMetadata
        {
            Id = id,
            BranchName = branchName,
            CreatedAt = createdAt,
            Hash = "fakehash",
            SizeBytes = 1024
        };
    }

    #endregion
}
