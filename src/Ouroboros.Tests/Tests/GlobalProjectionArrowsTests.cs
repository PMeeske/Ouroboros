using FluentAssertions;
using LangChain.DocumentLoaders;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for GlobalProjectionArrows - the immutable event-sourced version of GlobalProjectionService.
/// Verifies that epoch management works correctly using PipelineBranch event sourcing.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GlobalProjectionArrowsTests
{
    #region CreateEpochArrow Tests

    [Fact]
    public async Task CreateEpochArrow_WithValidBranch_ShouldAddEpochEvent()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().HaveCount(1);
        result.Events.First().Should().BeOfType<EpochCreatedEvent>();
        
        var epochEvent = (EpochCreatedEvent)result.Events.First();
        epochEvent.Epoch.EpochNumber.Should().Be(1);
        epochEvent.Epoch.Branches.Should().HaveCount(1);
        epochEvent.Epoch.Branches[0].Name.Should().Be("test-branch");
    }

    [Fact]
    public async Task CreateEpochArrow_WithRelatedBranches_ShouldCaptureAll()
    {
        // Arrange
        var branch = CreateTestBranch("branch1");
        var relatedBranches = new[]
        {
            CreateTestBranch("branch2"),
            CreateTestBranch("branch3")
        };
        var arrow = GlobalProjectionArrows.CreateEpochArrow(relatedBranches);

        // Act
        var result = await arrow(branch);

        // Assert
        var epochEvent = (EpochCreatedEvent)result.Events.First();
        epochEvent.Epoch.Branches.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEpochArrow_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var metadata = new Dictionary<string, object>
        {
            ["version"] = "1.0",
            ["author"] = "test"
        };
        var arrow = GlobalProjectionArrows.CreateEpochArrow(metadata: metadata);

        // Act
        var result = await arrow(branch);

        // Assert
        var epochEvent = (EpochCreatedEvent)result.Events.First();
        epochEvent.Epoch.Metadata.Should().ContainKey("version");
        epochEvent.Epoch.Metadata.Should().ContainKey("author");
        epochEvent.Epoch.Metadata["version"].Should().Be("1.0");
    }

    [Fact]
    public async Task CreateEpochArrow_Multiple_ShouldIncrementEpochNumber()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();

        // Act
        var result1 = await arrow(branch);
        var result2 = await arrow(result1);
        var result3 = await arrow(result2);

        // Assert
        var epoch1 = ((EpochCreatedEvent)result1.Events.First()).Epoch;
        var epoch2 = ((EpochCreatedEvent)result2.Events.Last()).Epoch;
        var epoch3 = ((EpochCreatedEvent)result3.Events.Last()).Epoch;

        epoch1.EpochNumber.Should().Be(1);
        epoch2.EpochNumber.Should().Be(2);
        epoch3.EpochNumber.Should().Be(3);
    }

    [Fact]
    public async Task CreateEpochArrow_ShouldSetUniqueEpochIds()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();

        // Act
        var result1 = await arrow(branch);
        var result2 = await arrow(result1);

        // Assert
        var epoch1 = ((EpochCreatedEvent)result1.Events.First()).Epoch;
        var epoch2 = ((EpochCreatedEvent)result2.Events.Last()).Epoch;

        epoch1.EpochId.Should().NotBe(epoch2.EpochId);
    }

    #endregion

    #region SafeCreateEpochArrow Tests

    [Fact]
    public async Task SafeCreateEpochArrow_WithValidBranch_ShouldSucceed()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.SafeCreateEpochArrow();

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().HaveCount(1);
    }

    #endregion

    #region GetEpochs Tests

    [Fact]
    public async Task GetEpochs_WithMultipleEpochs_ShouldReturnAll()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        
        var result1 = await arrow(branch);
        var result2 = await arrow(result1);
        var result3 = await arrow(result2);

        // Act
        var epochs = GlobalProjectionArrows.GetEpochs(result3);

        // Assert
        epochs.Should().HaveCount(3);
        epochs[0].EpochNumber.Should().Be(1);
        epochs[1].EpochNumber.Should().Be(2);
        epochs[2].EpochNumber.Should().Be(3);
    }

    [Fact]
    public void GetEpochs_WithNoEpochs_ShouldReturnEmpty()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");

        // Act
        var epochs = GlobalProjectionArrows.GetEpochs(branch);

        // Assert
        epochs.Should().BeEmpty();
    }

    #endregion

    #region GetEpoch Tests

    [Fact]
    public async Task GetEpoch_WithExistingEpoch_ShouldReturnSuccess()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        var result = await arrow(branch);

        // Act
        var epochResult = GlobalProjectionArrows.GetEpoch(result, 1);

        // Assert
        epochResult.IsSuccess.Should().BeTrue();
        epochResult.Value.EpochNumber.Should().Be(1);
    }

    [Fact]
    public void GetEpoch_WithNonExistentEpoch_ShouldReturnFailure()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");

        // Act
        var result = GlobalProjectionArrows.GetEpoch(branch, 999);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetEpoch_WithMultipleEpochs_ShouldReturnCorrectOne()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        
        var result1 = await arrow(branch);
        var result2 = await arrow(result1);
        var result3 = await arrow(result2);

        // Act
        var epochResult = GlobalProjectionArrows.GetEpoch(result3, 2);

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
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        
        var result1 = await arrow(branch);
        var result2 = await arrow(result1);
        var result3 = await arrow(result2);

        // Act
        var latestResult = GlobalProjectionArrows.GetLatestEpoch(result3);

        // Assert
        latestResult.IsSuccess.Should().BeTrue();
        latestResult.Value.EpochNumber.Should().Be(3);
    }

    [Fact]
    public void GetLatestEpoch_WithNoEpochs_ShouldReturnFailure()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");

        // Act
        var result = GlobalProjectionArrows.GetLatestEpoch(branch);

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
        var branch = CreateTestBranch("test-branch");

        // Act
        var result = GlobalProjectionArrows.GetMetrics(branch);

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
        var branch1 = CreateTestBranch("branch1");
        var branch2 = CreateTestBranch("branch2");
        
        // Add reasoning events to branches
        branch1 = branch1.WithReasoning(new Draft("test1"), "prompt1");
        branch2 = branch2.WithReasoning(new Draft("test2"), "prompt2");
        branch2 = branch2.WithReasoning(new Draft("test3"), "prompt3");

        var arrow1 = GlobalProjectionArrows.CreateEpochArrow(new[] { branch2 });
        var result1 = await arrow1(branch1);

        var arrow2 = GlobalProjectionArrows.CreateEpochArrow();
        var result2 = await arrow2(result1);

        // Act
        var metricsResult = GlobalProjectionArrows.GetMetrics(result2);

        // Assert
        metricsResult.IsSuccess.Should().BeTrue();
        var metrics = metricsResult.Value;
        metrics.TotalEpochs.Should().Be(2);
        metrics.TotalBranches.Should().Be(2); // Distinct branches
        metrics.TotalEvents.Should().BeGreaterThan(0);
        metrics.LastEpochAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMetrics_ShouldComputeAverageEventsPerBranch()
    {
        // Arrange
        var branch1 = CreateTestBranch("branch1");
        var branch2 = CreateTestBranch("branch2");
        
        branch1 = branch1.WithReasoning(new Draft("test1"), "prompt1");
        branch1 = branch1.WithReasoning(new Draft("test2"), "prompt2");
        branch2 = branch2.WithReasoning(new Draft("test3"), "prompt3");

        var arrow = GlobalProjectionArrows.CreateEpochArrow(new[] { branch2 });
        var result = await arrow(branch1);

        // Act
        var metricsResult = GlobalProjectionArrows.GetMetrics(result);

        // Assert
        metricsResult.IsSuccess.Should().BeTrue();
        metricsResult.Value.AverageEventsPerBranch.Should().BeGreaterThan(0);
    }

    #endregion

    #region GetEpochsInRange Tests

    [Fact]
    public async Task GetEpochsInRange_ShouldReturnEpochsWithinRange()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        
        var result1 = await arrow(branch);
        await Task.Delay(10); // Small delay to ensure different timestamps
        var result2 = await arrow(result1);
        await Task.Delay(10);
        var result3 = await arrow(result2);

        var epoch1 = GlobalProjectionArrows.GetEpoch(result3, 1).Value;
        var epoch2 = GlobalProjectionArrows.GetEpoch(result3, 2).Value;

        var start = epoch1.CreatedAt.AddMilliseconds(-1);
        var end = epoch2.CreatedAt.AddMilliseconds(1);

        // Act
        var epochs = GlobalProjectionArrows.GetEpochsInRange(result3, start, end);

        // Assert
        epochs.Should().HaveCount(2);
        epochs[0].EpochNumber.Should().Be(1);
        epochs[1].EpochNumber.Should().Be(2);
    }

    [Fact]
    public void GetEpochsInRange_WithNoEpochsInRange_ShouldReturnEmpty()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var start = DateTime.UtcNow.AddDays(-10);
        var end = DateTime.UtcNow.AddDays(-5);

        // Act
        var epochs = GlobalProjectionArrows.GetEpochsInRange(branch, start, end);

        // Assert
        epochs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEpochsInRange_ShouldReturnEpochsInOrder()
    {
        // Arrange
        var branch = CreateTestBranch("test-branch");
        var arrow = GlobalProjectionArrows.CreateEpochArrow();
        
        var result1 = await arrow(branch);
        var result2 = await arrow(result1);
        var result3 = await arrow(result2);

        var start = DateTime.UtcNow.AddMinutes(-1);
        var end = DateTime.UtcNow.AddMinutes(1);

        // Act
        var epochs = GlobalProjectionArrows.GetEpochsInRange(result3, start, end);

        // Assert
        epochs.Should().HaveCount(3);
        epochs[0].EpochNumber.Should().Be(1);
        epochs[1].EpochNumber.Should().Be(2);
        epochs[2].EpochNumber.Should().Be(3);
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
