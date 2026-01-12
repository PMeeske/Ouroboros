// <copyright file="DistinctionFitnessTrackerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Xunit;

/// <summary>
/// Unit tests for DistinctionFitnessTracker.
/// </summary>
[Trait("Category", "Unit")]
public class DistinctionFitnessTrackerTests
{
    private readonly InMemoryDistinctionStorage _storage;
    private readonly DistinctionFitnessTracker _tracker;

    public DistinctionFitnessTrackerTests()
    {
        _storage = new InMemoryDistinctionStorage(NullLogger<InMemoryDistinctionStorage>.Instance);
        _tracker = new DistinctionFitnessTracker(
            _storage,
            NullLogger<DistinctionFitnessTracker>.Instance);
    }

    [Fact]
    public async Task UpdateFitnessAsync_WithCorrectPrediction_IncreasesFitness()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = new DistinctionWeights(
            id,
            new float[384],
            new float[384],
            new float[384],
            DreamStage.Distinction,
            Fitness: 0.5,
            Circumstance: "Test",
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);

        await _storage.StoreDistinctionWeightsAsync(id, weights);

        // Act
        var result = await _tracker.UpdateFitnessAsync(id, predictionCorrect: true, confidenceScore: 0.9);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedWeights = await _storage.GetDistinctionWeightsAsync(id);
        updatedWeights.Value.Fitness.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task UpdateFitnessAsync_WithIncorrectPrediction_DecreasesFitness()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = new DistinctionWeights(
            id,
            new float[384],
            new float[384],
            new float[384],
            DreamStage.Distinction,
            Fitness: 0.8,
            Circumstance: "Test",
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);

        await _storage.StoreDistinctionWeightsAsync(id, weights);

        // Act
        var result = await _tracker.UpdateFitnessAsync(id, predictionCorrect: false, confidenceScore: 0.3);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedWeights = await _storage.GetDistinctionWeightsAsync(id);
        updatedWeights.Value.Fitness.Should().BeLessThan(0.8);
    }

    [Fact]
    public async Task UpdateFitnessAsync_WithNonexistentId_ReturnsFailure()
    {
        // Arrange
        var nonexistentId = DistinctionId.NewId();

        // Act
        var result = await _tracker.UpdateFitnessAsync(nonexistentId, true, 0.9);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateFitnessAsync_MultipleUpdates_ConvergesToActualPerformance()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = new DistinctionWeights(
            id,
            new float[384],
            new float[384],
            new float[384],
            DreamStage.Distinction,
            Fitness: 0.5,
            Circumstance: "Test",
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);

        await _storage.StoreDistinctionWeightsAsync(id, weights);

        // Act - Simulate consistent correct predictions
        for (int i = 0; i < 10; i++)
        {
            await _tracker.UpdateFitnessAsync(id, predictionCorrect: true, confidenceScore: 0.95);
        }

        // Assert
        var updatedWeights = await _storage.GetDistinctionWeightsAsync(id);
        updatedWeights.Value.Fitness.Should().BeGreaterThan(0.8); // Should converge towards high fitness
    }

    [Fact]
    public async Task GetLowFitnessDistinctionsAsync_ReturnsSuccess()
    {
        // Arrange & Act
        var result = await _tracker.GetLowFitnessDistinctionsAsync(0.3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
}
