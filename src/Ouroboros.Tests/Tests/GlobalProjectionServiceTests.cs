using FluentAssertions;
using LangChain.DocumentLoaders;
using Ouroboros.Pipeline.Branches;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Comprehensive tests for GlobalProjectionService.
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
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };

        // Act
        var result = await service.CreateEpochAsync(branches);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var epoch = result.Value;
        epoch.EpochNumber.Should().Be(1);
        epoch.Branches.Should().HaveCount(1);
        epoch.Branches[0].Name.Should().Be("branch1");
    }

    [Fact]
    public async Task CreateEpochAsync_WithMultipleBranches_ShouldCaptureAll()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[]
        {
            CreateTestBranch("branch1"),
            CreateTestBranch("branch2"),
            CreateTestBranch("branch3")
        };

        // Act
        var result = await service.CreateEpochAsync(branches);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Branches.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEpochAsync_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        var metadata = new Dictionary<string, object>
        {
            ["version"] = "1.0",
            ["author"] = "test"
        };

        // Act
        var result = await service.CreateEpochAsync(branches, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("version");
        result.Value.Metadata.Should().ContainKey("author");
        result.Value.Metadata["version"].Should().Be("1.0");
    }

    [Fact]
    public async Task CreateEpochAsync_ShouldIncrementEpochNumber()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };

        // Act
        var result1 = await service.CreateEpochAsync(branches);
        var result2 = await service.CreateEpochAsync(branches);
        var result3 = await service.CreateEpochAsync(branches);

        // Assert
        result1.Value.EpochNumber.Should().Be(1);
        result2.Value.EpochNumber.Should().Be(2);
        result3.Value.EpochNumber.Should().Be(3);
    }

    [Fact]
    public async Task CreateEpochAsync_ShouldSetUniqueEpochIds()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };

        // Act
        var result1 = await service.CreateEpochAsync(branches);
        var result2 = await service.CreateEpochAsync(branches);

        // Assert
        result1.Value.EpochId.Should().NotBe(result2.Value.EpochId);
    }

    [Fact]
    public async Task CreateEpochAsync_ShouldSetCreatedAt()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        var beforeCreate = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await service.CreateEpochAsync(branches);
        var afterCreate = DateTime.UtcNow.AddSeconds(1);

        // Assert
        result.Value.CreatedAt.Should().BeAfter(beforeCreate);
        result.Value.CreatedAt.Should().BeBefore(afterCreate);
    }

    [Fact]
    public async Task CreateEpochAsync_WithNullBranches_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new GlobalProjectionService();

        // Act
        Func<Task> act = async () => await service.CreateEpochAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEpochAsync_WithEmptyBranches_ShouldSucceed()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = Array.Empty<PipelineBranch>();

        // Act
        var result = await service.CreateEpochAsync(branches);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Branches.Should().BeEmpty();
    }

    #endregion

    #region GetEpoch Tests

    [Fact]
    public async Task GetEpoch_WithExistingEpoch_ShouldReturnSuccess()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        var created = await service.CreateEpochAsync(branches);

        // Act
        var result = service.GetEpoch(created.Value.EpochNumber);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochNumber.Should().Be(created.Value.EpochNumber);
    }

    [Fact]
    public void GetEpoch_WithNonExistentEpoch_ShouldReturnFailure()
    {
        // Arrange
        var service = new GlobalProjectionService();

        // Act
        var result = service.GetEpoch(999);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetEpoch_WithMultipleEpochs_ShouldReturnCorrectOne()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        await service.CreateEpochAsync(branches);
        var epoch2 = await service.CreateEpochAsync(branches);
        await service.CreateEpochAsync(branches);

        // Act
        var result = service.GetEpoch(2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochId.Should().Be(epoch2.Value.EpochId);
    }

    #endregion

    #region GetLatestEpoch Tests

    [Fact]
    public async Task GetLatestEpoch_WithEpochs_ShouldReturnMostRecent()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        await service.CreateEpochAsync(branches);
        await service.CreateEpochAsync(branches);
        var latest = await service.CreateEpochAsync(branches);

        // Act
        var result = service.GetLatestEpoch();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpochId.Should().Be(latest.Value.EpochId);
        result.Value.EpochNumber.Should().Be(3);
    }

    [Fact]
    public void GetLatestEpoch_WithNoEpochs_ShouldReturnFailure()
    {
        // Arrange
        var service = new GlobalProjectionService();

        // Act
        var result = service.GetLatestEpoch();

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
        var service = new GlobalProjectionService();

        // Act
        var result = service.GetMetrics();

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
        var service = new GlobalProjectionService();
        var branch1 = CreateTestBranch("branch1");
        var branch2 = CreateTestBranch("branch2");
        
        // Add reasoning events to branches
        branch1 = branch1.WithReasoning(new Draft("test1"), "prompt1");
        branch2 = branch2.WithReasoning(new Draft("test2"), "prompt2");
        branch2 = branch2.WithReasoning(new Draft("test3"), "prompt3");

        await service.CreateEpochAsync(new[] { branch1, branch2 });
        await service.CreateEpochAsync(new[] { branch1 });

        // Act
        var result = service.GetMetrics();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metrics = result.Value;
        metrics.TotalEpochs.Should().Be(2);
        metrics.TotalBranches.Should().Be(2); // Distinct branches
        metrics.TotalEvents.Should().Be(4); // 1 + 2 from first epoch, 1 from second epoch
        metrics.LastEpochAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMetrics_ShouldComputeAverageEventsPerBranch()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branch1 = CreateTestBranch("branch1");
        var branch2 = CreateTestBranch("branch2");
        
        branch1 = branch1.WithReasoning(new Draft("test1"), "prompt1");
        branch1 = branch1.WithReasoning(new Draft("test2"), "prompt2");
        branch2 = branch2.WithReasoning(new Draft("test3"), "prompt3");

        await service.CreateEpochAsync(new[] { branch1, branch2 });

        // Act
        var result = service.GetMetrics();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AverageEventsPerBranch.Should().BeApproximately(1.5, 0.01); // 3 events / 2 branches
    }

    #endregion

    #region Clear Tests

    [Fact]
    public async Task Clear_ShouldRemoveAllEpochs()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        await service.CreateEpochAsync(branches);
        await service.CreateEpochAsync(branches);

        // Act
        service.Clear();

        // Assert
        service.Epochs.Should().BeEmpty();
        var latestResult = service.GetLatestEpoch();
        latestResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Clear_ShouldResetEpochNumber()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        await service.CreateEpochAsync(branches);
        await service.CreateEpochAsync(branches);

        // Act
        service.Clear();
        var newEpoch = await service.CreateEpochAsync(branches);

        // Assert
        newEpoch.Value.EpochNumber.Should().Be(1);
    }

    #endregion

    #region GetEpochsInRange Tests

    [Fact]
    public async Task GetEpochsInRange_ShouldReturnEpochsWithinRange()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        
        var epoch1 = await service.CreateEpochAsync(branches);
        await Task.Delay(10); // Small delay to ensure different timestamps
        var epoch2 = await service.CreateEpochAsync(branches);
        await Task.Delay(10);
        var epoch3 = await service.CreateEpochAsync(branches);

        var start = epoch1.Value.CreatedAt.AddMilliseconds(-1);
        var end = epoch2.Value.CreatedAt.AddMilliseconds(1);

        // Act
        var epochs = service.GetEpochsInRange(start, end);

        // Assert
        epochs.Should().HaveCount(2);
        epochs[0].EpochNumber.Should().Be(1);
        epochs[1].EpochNumber.Should().Be(2);
    }

    [Fact]
    public void GetEpochsInRange_WithNoEpochsInRange_ShouldReturnEmpty()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var start = DateTime.UtcNow.AddDays(-10);
        var end = DateTime.UtcNow.AddDays(-5);

        // Act
        var epochs = service.GetEpochsInRange(start, end);

        // Assert
        epochs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEpochsInRange_ShouldReturnEpochsInOrder()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        
        await service.CreateEpochAsync(branches);
        await service.CreateEpochAsync(branches);
        await service.CreateEpochAsync(branches);

        var start = DateTime.UtcNow.AddMinutes(-1);
        var end = DateTime.UtcNow.AddMinutes(1);

        // Act
        var epochs = service.GetEpochsInRange(start, end);

        // Assert
        epochs.Should().HaveCount(3);
        epochs[0].EpochNumber.Should().Be(1);
        epochs[1].EpochNumber.Should().Be(2);
        epochs[2].EpochNumber.Should().Be(3);
    }

    #endregion

    #region Epochs Property Tests

    [Fact]
    public async Task Epochs_ShouldReturnAllEpochs()
    {
        // Arrange
        var service = new GlobalProjectionService();
        var branches = new[] { CreateTestBranch("branch1") };
        await service.CreateEpochAsync(branches);
        await service.CreateEpochAsync(branches);

        // Act
        var epochs = service.Epochs;

        // Assert
        epochs.Should().HaveCount(2);
    }

    [Fact]
    public void Epochs_ShouldReturnReadOnlyList()
    {
        // Arrange
        var service = new GlobalProjectionService();

        // Act
        var epochs = service.Epochs;

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
