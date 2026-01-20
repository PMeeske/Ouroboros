// <copyright file="InMemoryDistinctionWeightsRepositoryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Xunit;

/// <summary>
/// Unit tests for InMemoryDistinctionWeightsRepository.
/// </summary>
public sealed class InMemoryDistinctionWeightsRepositoryTests
{
    private readonly InMemoryDistinctionWeightsRepository _repository;

    public InMemoryDistinctionWeightsRepositoryTests()
    {
        _repository = new InMemoryDistinctionWeightsRepository(NullLogger<InMemoryDistinctionWeightsRepository>.Instance);
    }

    [Fact]
    public async Task StoreAndRetrieve_RoundTrips()
    {
        // Arrange
        var id = new DistinctionId(Guid.NewGuid());
        var weights = CreateTestWeights(id);

        // Act
        var storeResult = await _repository.StoreDistinctionWeightsAsync(id, weights);
        var getResult = await _repository.GetDistinctionWeightsAsync(id);

        // Assert
        storeResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Id.Should().Be(id);
        getResult.Value.Fitness.Should().Be(weights.Fitness);
    }

    [Fact]
    public async Task GetDistinctionWeightsAsync_WithNonexistentId_ReturnsFailure()
    {
        // Arrange
        var id = new DistinctionId(Guid.NewGuid());

        // Act
        var result = await _repository.GetDistinctionWeightsAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDistinctionWeightsAsync_RemovesEntry()
    {
        // Arrange
        var id = new DistinctionId(Guid.NewGuid());
        var weights = CreateTestWeights(id);
        await _repository.StoreDistinctionWeightsAsync(id, weights);

        // Act
        var deleteResult = await _repository.DeleteDistinctionWeightsAsync(id);
        var getResult = await _repository.GetDistinctionWeightsAsync(id);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();
        getResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateFitnessAsync_UpdatesFitnessValue()
    {
        // Arrange
        var id = new DistinctionId(Guid.NewGuid());
        var weights = CreateTestWeights(id, fitness: 0.5);
        await _repository.StoreDistinctionWeightsAsync(id, weights);

        // Act
        var updateResult = await _repository.UpdateFitnessAsync(id, 0.9);
        var getResult = await _repository.GetDistinctionWeightsAsync(id);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Fitness.Should().Be(0.9);
    }

    [Fact]
    public async Task FindSimilarDistinctionsAsync_ReturnsSimilarItems()
    {
        // Arrange
        var id1 = new DistinctionId(Guid.NewGuid());
        var id2 = new DistinctionId(Guid.NewGuid());
        var weights1 = CreateTestWeights(id1, embedding: new float[] { 1.0f, 0.0f, 0.0f });
        var weights2 = CreateTestWeights(id2, embedding: new float[] { 0.9f, 0.1f, 0.0f });

        await _repository.StoreDistinctionWeightsAsync(id1, weights1);
        await _repository.StoreDistinctionWeightsAsync(id2, weights2);

        // Act
        var result = await _repository.FindSimilarDistinctionsAsync(new float[] { 1.0f, 0.0f, 0.0f }, topK: 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.First().Id.Should().Be(id1); // Most similar
    }

    private static DistinctionWeights CreateTestWeights(
        DistinctionId id,
        double fitness = 0.85,
        float[]? embedding = null)
    {
        return new DistinctionWeights(
            Id: id,
            Embedding: embedding ?? new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f },
            DissolutionMask: new float[] { 0.01f, 0.02f, 0.03f, 0.04f, 0.05f },
            RecognitionTransform: new float[] { 1.1f, 1.2f, 1.3f, 1.4f, 1.5f },
            LearnedAtStage: DreamStage.Distinction,
            Fitness: fitness,
            Circumstance: "test_circumstance",
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);
    }
}
