using FluentAssertions;
using LangChain.DocumentLoaders;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Comprehensive tests for GlobalProjectionService (refactored to immutable event sourcing).
/// Tests focus on epoch management, metrics computation, and Result monad patterns.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GlobalProjectionServiceTests
{
    #region CreateEpochAsync Tests

    [Fact]
    public async Task CreateEpochAsync_WithValidBranches_ShouldSucceed()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var (epoch, updatedBranch) = result.Value;
        epoch.EpochNumber.Should().Be(1);
        epoch.Branches.Should().HaveCount(1);
        epoch.Branches[0].Name.Should().Be("branch1");
        updatedBranch.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateEpochAsync_WithMultipleBranches_ShouldCaptureAll()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[]
        {
            CreateTestBranch("branch1"),
            CreateTestBranch("branch2"),
            CreateTestBranch("branch3")
        };

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Epoch.Branches.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEpochAsync_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        var metadata = new Dictionary<string, object>
        {
            ["version"] = "1.0",
            ["author"] = "test"
        };

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var epoch = result.Value.Epoch;
        epoch.Metadata.Should().ContainKey("version");
        epoch.Metadata.Should().ContainKey("author");
        epoch.Metadata["version"].Should().Be("1.0");
    }

    [Fact]
    public async Task CreateEpochAsync_ShouldIncrementEpochNumber()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };

        // Act
        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);
        var result3 = await GlobalProjectionService.CreateEpochAsync(result2.Value.UpdatedBranch, branches);

        // Assert
        result1.Value.Epoch.EpochNumber.Should().Be(1);
        result2.Value.Epoch.EpochNumber.Should().Be(2);
        result3.Value.Epoch.EpochNumber.Should().Be(3);
    }

    [Fact]
    public async Task CreateEpochAsync_ShouldSetUniqueEpochIds()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };

        // Act
        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);

        // Assert
        result1.Value.Epoch.EpochId.Should().NotBe(result2.Value.Epoch.EpochId);
    }

    [Fact]
    public async Task CreateEpochAsync_ShouldSetCreatedAt()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        var beforeCreate = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        var afterCreate = DateTime.UtcNow.AddSeconds(1);

        // Assert
        result.Value.Epoch.CreatedAt.Should().BeAfter(beforeCreate);
        result.Value.Epoch.CreatedAt.Should().BeBefore(afterCreate);
    }

    [Fact]
    public async Task CreateEpochAsync_WithNullBranches_ShouldReturnFailure()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to create epoch snapshot");
    }

    [Fact]
    public async Task CreateEpochAsync_WithEmptyBranches_ShouldSucceed()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = Array.Empty<PipelineBranch>();

        // Act
        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Epoch.Branches.Should().BeEmpty();
    }

    #endregion

    #region GetEpoch Tests

    [Fact]
    public async Task GetEpoch_WithExistingEpoch_ShouldReturnSuccess()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        var created = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);

        // Act
        var result = GlobalProjectionService.GetEpoch(created.Value.UpdatedBranch, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochNumber.Should().Be(1);
    }

    [Fact]
    public void GetEpoch_WithNonExistentEpoch_ShouldReturnFailure()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");

        // Act
        var result = GlobalProjectionService.GetEpoch(trackingBranch, 999);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetEpoch_WithMultipleEpochs_ShouldReturnCorrectOne()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        
        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);
        var result3 = await GlobalProjectionService.CreateEpochAsync(result2.Value.UpdatedBranch, branches);

        // Act
        var epochResult = GlobalProjectionService.GetEpoch(result3.Value.UpdatedBranch, 2);

        // Assert
        epochResult.IsSuccess.Should().BeTrue();
        epochResult.Value.EpochNumber.Should().Be(2);
    }

    #endregion

    #region GetLatestEpoch Tests

    [Fact]
    public async Task GetLatestEpoch_WithEpochs_ShouldReturnMostRecent()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        
        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);
        var result3 = await GlobalProjectionService.CreateEpochAsync(result2.Value.UpdatedBranch, branches);

        // Act
        var latest = GlobalProjectionService.GetLatestEpoch(result3.Value.UpdatedBranch);

        // Assert
        latest.IsSuccess.Should().BeTrue();
        latest.Value.EpochNumber.Should().Be(3);
    }

    [Fact]
    public void GetLatestEpoch_WithNoEpochs_ShouldReturnFailure()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");

        // Act
        var result = GlobalProjectionService.GetLatestEpoch(trackingBranch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No epochs available");
    }

    #endregion

    #region GetMetrics Tests

    [Fact]
    public void GetMetrics_WithNoEpochs_ShouldReturnZeroMetrics()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");

        // Act
        var result = GlobalProjectionService.GetMetrics(trackingBranch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;
        metrics.TotalEpochs.Should().Be(0);
        metrics.TotalBranches.Should().Be(0);
        metrics.TotalEvents.Should().Be(0);
        metrics.LastEpochAt.Should().BeNull();
    }

    [Fact]
    public async Task GetMetrics_WithEpochs_ShouldComputeCorrectMetrics()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branch1 = CreateTestBranch("branch1");
        var branch2 = CreateTestBranch("branch2");
        
        // Add reasoning events to branches
        branch1 = branch1.WithReasoning(new Draft("test1"), "prompt1");
        branch2 = branch2.WithReasoning(new Draft("test2"), "prompt2");
        branch2 = branch2.WithReasoning(new Draft("test3"), "prompt3");

        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, new[] { branch1, branch2 });
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, new[] { branch1 });

        // Act
        var metricsResult = GlobalProjectionService.GetMetrics(result2.Value.UpdatedBranch);

        // Assert
        metricsResult.IsSuccess.Should().BeTrue();
        var metrics = metricsResult.Value;
        metrics.TotalEpochs.Should().Be(2);
        metrics.TotalBranches.Should().Be(2); // Distinct branches
        metrics.TotalEvents.Should().Be(4); // 1 + 2 from first epoch, 1 from second epoch
        metrics.LastEpochAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMetrics_ShouldComputeAverageEventsPerBranch()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branch1 = CreateTestBranch("branch1");
        var branch2 = CreateTestBranch("branch2");
        
        branch1 = branch1.WithReasoning(new Draft("test1"), "prompt1");
        branch1 = branch1.WithReasoning(new Draft("test2"), "prompt2");
        branch2 = branch2.WithReasoning(new Draft("test3"), "prompt3");

        var result = await GlobalProjectionService.CreateEpochAsync(trackingBranch, new[] { branch1, branch2 });

        // Act
        var metricsResult = GlobalProjectionService.GetMetrics(result.Value.UpdatedBranch);

        // Assert
        metricsResult.IsSuccess.Should().BeTrue();
        metricsResult.Value.AverageEventsPerBranch.Should().BeApproximately(1.5, 0.01); // 3 events / 2 branches
    }

    #endregion

    #region GetEpochsInRange Tests

    [Fact]
    public async Task GetEpochsInRange_ShouldReturnEpochsWithinRange()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        
        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        await Task.Delay(10); // Small delay to ensure different timestamps
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);
        await Task.Delay(10);
        var result3 = await GlobalProjectionService.CreateEpochAsync(result2.Value.UpdatedBranch, branches);

        var epoch1 = GlobalProjectionService.GetEpoch(result3.Value.UpdatedBranch, 1).Value;
        var epoch2 = GlobalProjectionService.GetEpoch(result3.Value.UpdatedBranch, 2).Value;

        var start = epoch1.CreatedAt.AddMilliseconds(-1);
        var end = epoch2.CreatedAt.AddMilliseconds(1);

        // Act
        var epochs = GlobalProjectionService.GetEpochsInRange(result3.Value.UpdatedBranch, start, end);

        // Assert
        epochs.Should().HaveCount(2);
        epochs[0].EpochNumber.Should().Be(1);
        epochs[1].EpochNumber.Should().Be(2);
    }

    [Fact]
    public void GetEpochsInRange_WithNoEpochsInRange_ShouldReturnEmpty()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var start = DateTime.UtcNow.AddDays(-10);
        var end = DateTime.UtcNow.AddDays(-5);

        // Act
        var epochs = GlobalProjectionService.GetEpochsInRange(trackingBranch, start, end);

        // Assert
        epochs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEpochsInRange_ShouldReturnEpochsInOrder()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        
        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);
        var result3 = await GlobalProjectionService.CreateEpochAsync(result2.Value.UpdatedBranch, branches);

        var start = DateTime.UtcNow.AddMinutes(-1);
        var end = DateTime.UtcNow.AddMinutes(1);

        // Act
        var epochs = GlobalProjectionService.GetEpochsInRange(result3.Value.UpdatedBranch, start, end);

        // Assert
        epochs.Should().HaveCount(3);
        epochs[0].EpochNumber.Should().Be(1);
        epochs[1].EpochNumber.Should().Be(2);
        epochs[2].EpochNumber.Should().Be(3);
    }

    #endregion

    #region GetEpochs Tests

    [Fact]
    public async Task GetEpochs_ShouldReturnAllEpochs()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");
        var branches = new[] { CreateTestBranch("branch1") };
        
        var result1 = await GlobalProjectionService.CreateEpochAsync(trackingBranch, branches);
        var result2 = await GlobalProjectionService.CreateEpochAsync(result1.Value.UpdatedBranch, branches);

        // Act
        var epochs = GlobalProjectionService.GetEpochs(result2.Value.UpdatedBranch);

        // Assert
        epochs.Should().HaveCount(2);
    }

    [Fact]
    public void GetEpochs_ShouldReturnReadOnlyList()
    {
        // Arrange
        var trackingBranch = CreateTestBranch("tracking");

        // Act
        var epochs = GlobalProjectionService.GetEpochs(trackingBranch);

        // Assert
        epochs.Should().BeAssignableTo<IReadOnlyList<EpochSnapshot>>();
    }

    #endregion

    #region Helper Methods

    private static PipelineBranch CreateTestBranch(string name)
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        return new PipelineBranch(name, store, source);
    }

    #endregion
}
