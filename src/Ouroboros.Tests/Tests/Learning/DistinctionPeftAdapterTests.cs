// <copyright file="DistinctionPeftAdapterTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Ouroboros.Tests.Mocks;
using Xunit;

/// <summary>
/// Unit tests for DistinctionPeftAdapter.
/// </summary>
[Trait("Category", "Unit")]
public class DistinctionPeftAdapterTests
{
    private readonly MockPeftIntegration _peft;
    private readonly InMemoryDistinctionStorage _storage;
    private readonly MockEmbeddingModel _embeddingModel;
    private readonly DistinctionPeftAdapter _adapter;

    public DistinctionPeftAdapterTests()
    {
        _peft = new MockPeftIntegration(NullLogger<MockPeftIntegration>.Instance);
        _storage = new InMemoryDistinctionStorage(NullLogger<InMemoryDistinctionStorage>.Instance);
        _embeddingModel = new MockEmbeddingModel();
        _adapter = new DistinctionPeftAdapter(
            _peft,
            _storage,
            _embeddingModel,
            "test-model",
            NullLogger<DistinctionPeftAdapter>.Instance);
    }

    [Fact]
    public async Task LearnDistinctionAsync_WithValidObservation_ReturnsSuccess()
    {
        // Arrange
        var observation = Observation.Now("This is a test distinction");
        var stage = DreamStage.Distinction;
        var state = DistinctionState.Initial();

        // Act
        var result = await _adapter.LearnDistinctionAsync(observation, stage, state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Value.Should().NotBe(Guid.Empty);
        result.Value.Circumstance.Should().Be(observation.Content);
        result.Value.LearnedAtStage.Should().Be(stage);
        result.Value.Fitness.Should().Be(0.5); // Initial fitness
    }

    [Fact]
    public async Task LearnDistinctionAsync_StoresWeightsInStorage()
    {
        // Arrange
        var observation = Observation.Now("Test observation");
        var stage = DreamStage.SubjectEmerges;
        var state = DistinctionState.Initial();

        // Act
        var result = await _adapter.LearnDistinctionAsync(observation, stage, state);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var storedResult = await _storage.GetDistinctionWeightsAsync(result.Value.Id);
        storedResult.IsSuccess.Should().BeTrue();
        storedResult.Value.Id.Should().Be(result.Value.Id);
    }

    [Theory]
    [InlineData(DreamStage.Void)]
    [InlineData(DreamStage.Distinction)]
    [InlineData(DreamStage.SubjectEmerges)]
    [InlineData(DreamStage.WorldCrystallizes)]
    [InlineData(DreamStage.Recognition)]
    public async Task LearnDistinctionAsync_WorksForAllStages(DreamStage stage)
    {
        // Arrange
        var observation = Observation.Now($"Observation at stage {stage}");
        var state = DistinctionState.Initial();

        // Act
        var result = await _adapter.LearnDistinctionAsync(observation, stage, state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LearnedAtStage.Should().Be(stage);
    }

    [Fact]
    public async Task DissolveDistinctionAsync_WithValidId_ReturnsSuccess()
    {
        // Arrange
        var observation = Observation.Now("Distinction to dissolve");
        var stage = DreamStage.Distinction;
        var state = DistinctionState.Initial();

        var learnResult = await _adapter.LearnDistinctionAsync(observation, stage, state);
        var distinctionId = learnResult.Value.Id;

        // Act
        var result = await _adapter.DissolveDistinctionAsync(distinctionId, state);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify deletion from storage
        var getResult = await _storage.GetDistinctionWeightsAsync(distinctionId);
        getResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DissolveDistinctionAsync_WithNonexistentId_ReturnsFailure()
    {
        // Arrange
        var nonexistentId = DistinctionId.NewId();
        var state = DistinctionState.Initial();

        // Act
        var result = await _adapter.DissolveDistinctionAsync(nonexistentId, state);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RecognizeAsync_WithMultipleDistinctions_ReturnsSuccess()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var distinctionIds = new List<DistinctionId>();

        // Create multiple distinctions
        for (int i = 0; i < 3; i++)
        {
            var observation = Observation.Now($"Distinction {i}");
            var learnResult = await _adapter.LearnDistinctionAsync(
                observation,
                DreamStage.Distinction,
                state);
            distinctionIds.Add(learnResult.Value.Id);
        }

        // Act
        var result = await _adapter.RecognizeAsync(distinctionIds, "Recognition circumstance");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LearnedAtStage.Should().Be(DreamStage.Recognition);
        result.Value.Fitness.Should().Be(1.0); // Recognition has high fitness
    }

    [Fact]
    public async Task RecognizeAsync_WithNoDistinctions_ReturnsFailure()
    {
        // Arrange
        var emptyList = new List<DistinctionId>();

        // Act
        var result = await _adapter.RecognizeAsync(emptyList, "Test");

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
