// <copyright file="DistinctionLearnerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.DistinctionLearning;
using Xunit;

/// <summary>
/// Tests for the DistinctionLearner implementation.
/// Validates distinction learning from consciousness dream cycles.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DistinctionLearnerTests
{
    private readonly Mock<IDistinctionWeightStorage> _mockStorage;
    private readonly Mock<ILogger<DistinctionLearner>> _mockLogger;
    private readonly DistinctionLearner _sut;

    public DistinctionLearnerTests()
    {
        _mockStorage = new Mock<IDistinctionWeightStorage>();
        _mockLogger = new Mock<ILogger<DistinctionLearner>>();
        _sut = new DistinctionLearner(_mockStorage.Object, _mockLogger.Object);
    }

    #region UpdateFromDistinctionAsync Tests

    [Fact]
    public async Task UpdateFromDistinctionAsync_WithValidObservation_ReturnsSuccess()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var observation = new Observation(
            Content: "test content",
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.5,
            Context: new Dictionary<string, object>());

        // Act
        var result = await _sut.UpdateFromDistinctionAsync(state, observation, "Recognition");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().HaveCount(1);
        result.Value.ActiveDistinctions[0].Content.Should().Be("test content");
        result.Value.ActiveDistinctions[0].LearnedAtStage.Should().Be("Recognition");
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_CalculatesFitnessCorrectly()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var observation = new Observation(
            Content: new string('x', 100), // Exactly 100 chars for contentFactor = 1.0
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.8,
            Context: new Dictionary<string, object>());

        // Act
        var result = await _sut.UpdateFromDistinctionAsync(state, observation, "Distinction");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions[0].Fitness.Should().BeGreaterThan(0.0);
        result.Value.ActiveDistinctions[0].Fitness.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_WithRecognitionStage_BoostsFitness()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var observation = new Observation(
            Content: "test",
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.5,
            Context: new Dictionary<string, object>());

        // Act
        var resultRecognition = await _sut.UpdateFromDistinctionAsync(state, observation, "Recognition");
        var resultOther = await _sut.UpdateFromDistinctionAsync(state, observation, "Distinction");

        // Assert
        resultRecognition.IsSuccess.Should().BeTrue();
        resultOther.IsSuccess.Should().BeTrue();
        resultRecognition.Value.ActiveDistinctions[0].Fitness
            .Should().BeGreaterThan(resultOther.Value.ActiveDistinctions[0].Fitness);
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_UpdatesCertaintyCorrectly()
    {
        // Arrange
        var state = DistinctionState.Initial().WithCertainty(0.4);
        var observation = new Observation(
            Content: "test",
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.6,
            Context: new Dictionary<string, object>());

        // Act
        var result = await _sut.UpdateFromDistinctionAsync(state, observation, "Distinction");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpistemicCertainty.Should().BeGreaterThan(state.EpistemicCertainty);
        result.Value.EpistemicCertainty.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_WithShortContent_CalculatesLowerFitness()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var shortObservation = new Observation(
            Content: "x",
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.5,
            Context: new Dictionary<string, object>());
        var longObservation = new Observation(
            Content: new string('x', 200),
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.5,
            Context: new Dictionary<string, object>());

        // Act
        var shortResult = await _sut.UpdateFromDistinctionAsync(state, shortObservation, "Distinction");
        var longResult = await _sut.UpdateFromDistinctionAsync(state, longObservation, "Distinction");

        // Assert
        shortResult.IsSuccess.Should().BeTrue();
        longResult.IsSuccess.Should().BeTrue();
        shortResult.Value.ActiveDistinctions[0].Fitness
            .Should().BeLessThan(longResult.Value.ActiveDistinctions[0].Fitness);
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_PreservesStateImmutability()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var observation = new Observation(
            Content: "test",
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.5,
            Context: new Dictionary<string, object>());

        // Act
        var result = await _sut.UpdateFromDistinctionAsync(state, observation, "Distinction");

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.ActiveDistinctions.Should().BeEmpty("original state should be immutable");
        result.Value.ActiveDistinctions.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    public async Task UpdateFromDistinctionAsync_WithVariousCertainties_HandlesCorrectly(
        double prior, double current)
    {
        // Arrange
        var state = DistinctionState.Initial().WithCertainty(current);
        var observation = new Observation(
            Content: "test",
            Timestamp: DateTime.UtcNow,
            PriorCertainty: prior,
            Context: new Dictionary<string, object>());

        // Act
        var result = await _sut.UpdateFromDistinctionAsync(state, observation, "Distinction");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpistemicCertainty.Should().BeInRange(0.0, 1.0);
    }

    #endregion

    #region RecognizeAsync Tests

    [Fact]
    public async Task RecognizeAsync_BoostsEpistemicCertainty()
    {
        // Arrange
        var state = DistinctionState.Initial().WithCertainty(0.5);
        var originalCertainty = state.EpistemicCertainty;

        // Act
        var result = await _sut.RecognizeAsync(state, "test circumstance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpistemicCertainty.Should().BeGreaterThan(originalCertainty);
        result.Value.EpistemicCertainty.Should().Be(0.6); // 0.5 + 0.1 boost
    }

    [Fact]
    public async Task RecognizeAsync_ClampsMaxCertaintyToOne()
    {
        // Arrange
        var state = DistinctionState.Initial().WithCertainty(0.95);

        // Act
        var result = await _sut.RecognizeAsync(state, "test circumstance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpistemicCertainty.Should().Be(1.0, "certainty should be clamped to 1.0");
    }

    [Fact]
    public async Task RecognizeAsync_IncrementsCycleCount()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var originalCycle = state.CycleCount;

        // Act
        var result = await _sut.RecognizeAsync(state, "test circumstance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CycleCount.Should().Be(originalCycle + 1);
    }

    [Fact]
    public async Task RecognizeAsync_UpdatesLastUpdatedTimestamp()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var originalTimestamp = state.LastUpdated;
        await Task.Delay(10); // Ensure time passes

        // Act
        var result = await _sut.RecognizeAsync(state, "test circumstance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LastUpdated.Should().BeAfter(originalTimestamp);
    }

    [Fact]
    public async Task RecognizeAsync_PreservesActiveDistinctions()
    {
        // Arrange
        var distinction = new ActiveDistinction(
            Id: "test-id",
            Content: "test content",
            Fitness: 0.8,
            LearnedAt: DateTime.UtcNow,
            LearnedAtStage: "Distinction");
        var state = DistinctionState.Initial().WithDistinction(distinction);

        // Act
        var result = await _sut.RecognizeAsync(state, "test circumstance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().HaveCount(1);
        result.Value.ActiveDistinctions[0].Should().Be(distinction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("simple test")]
    [InlineData("complex circumstance with multiple words")]
    public async Task RecognizeAsync_WithVariousCircumstances_Succeeds(string circumstance)
    {
        // Arrange
        var state = DistinctionState.Initial();

        // Act
        var result = await _sut.RecognizeAsync(state, circumstance);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region DissolveAsync Tests

    [Fact]
    public async Task DissolveAsync_WithFitnessThreshold_DissolvesLowFitnessDistinctions()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.2, "Distinction", DateTime.UtcNow, false, 100),
            new("id2", "path2.weights", 0.8, "Recognition", DateTime.UtcNow, false, 100),
            new("id3", "path3.weights", 0.1, "Void", DateTime.UtcNow, false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, string>.Success(Unit.Value));

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path1.weights", It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path3.weights", It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path2.weights", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DissolveAsync_WithOldestFirst_DissolvesOldestDistinctions()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.8, "Distinction", DateTime.UtcNow.AddDays(-10), false, 100),
            new("id2", "path2.weights", 0.9, "Recognition", DateTime.UtcNow.AddDays(-5), false, 100),
            new("id3", "path3.weights", 0.7, "Void", DateTime.UtcNow.AddDays(-20), false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, string>.Success(Unit.Value));

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.OldestFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockStorage.Verify(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
            Times.AtLeast(2));
    }

    [Fact]
    public async Task DissolveAsync_WithAll_DissolvesAllNonDissolvedDistinctions()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.8, "Distinction", DateTime.UtcNow, false, 100),
            new("id2", "path2.weights", 0.9, "Recognition", DateTime.UtcNow, true, 100),
            new("id3", "path3.weights", 0.7, "Void", DateTime.UtcNow, false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, string>.Success(Unit.Value));

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.All);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path1.weights", It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path3.weights", It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path2.weights", It.IsAny<CancellationToken>()), Times.Never,
            "already dissolved distinctions should not be dissolved again");
    }

    [Fact]
    public async Task DissolveAsync_WhenStorageListFails_ReturnsFailure()
    {
        // Arrange
        var state = DistinctionState.Initial();
        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Failure("Storage error"));

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to list weights");
    }

    [Fact]
    public async Task DissolveAsync_ContinuesOnIndividualFailures()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.2, "Distinction", DateTime.UtcNow, false, 100),
            new("id2", "path2.weights", 0.1, "Void", DateTime.UtcNow, false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync("path1.weights", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Failure("Dissolution failed")));
        _mockStorage.Setup(s => s.DissolveWeightsAsync("path2.weights", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, string>.Success(Unit.Value));

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);

        // Assert
        result.IsSuccess.Should().BeTrue("should succeed overall even if some dissolutions fail");
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path1.weights", It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path2.weights", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DissolveAsync_WithEmptyWeightsList_Succeeds()
    {
        // Arrange
        var state = DistinctionState.Initial();
        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(new List<DistinctionWeightMetadata>()));

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockStorage.Verify(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task DissolveAsync_SkipsAlreadyDissolvedDistinctions()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights.dissolved", 0.2, "Distinction", DateTime.UtcNow, true, 100),
            new("id2", "path2.weights", 0.1, "Void", DateTime.UtcNow, false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Unit, string>.Success(Unit.Value));

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path1.weights.dissolved", It.IsAny<CancellationToken>()), 
            Times.Never, "already dissolved distinctions should be skipped");
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path2.weights", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Constructor_WithNullStorage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DistinctionLearner(null!));
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var observation = new Observation(
            Content: "test",
            Timestamp: DateTime.UtcNow,
            PriorCertainty: 0.5,
            Context: new Dictionary<string, object>());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _sut.UpdateFromDistinctionAsync(state, observation, "Distinction", cts.Token));
    }

    [Fact]
    public async Task RecognizeAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _sut.RecognizeAsync(state, "test", cts.Token));
    }

    [Fact]
    public async Task DissolveAsync_WithCancellation_HandlesGracefully()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var cts = new CancellationTokenSource();
        
        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _sut.DissolveAsync(state, DissolutionStrategy.All, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue("should fail when cancellation occurs");
    }

    #endregion
}
