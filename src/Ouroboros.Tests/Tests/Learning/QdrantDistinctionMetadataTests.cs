// <copyright file="QdrantDistinctionMetadataTests.cs" company="PlaceholderCompany">
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
/// Unit tests for QdrantDistinctionMetadataStorage.
/// Note: These tests require a running Qdrant instance.
/// They are marked as Integration tests to avoid CI failures.
/// </summary>
[Trait("Category", "Integration")]
public class QdrantDistinctionMetadataTests
{
    private const string QdrantConnectionString = "http://localhost:6333";
    private readonly QdrantDistinctionMetadataStorage _storage;

    public QdrantDistinctionMetadataTests()
    {
        _storage = new QdrantDistinctionMetadataStorage(
            QdrantConnectionString,
            NullLogger<QdrantDistinctionMetadataStorage>.Instance);
    }

    [Fact(Skip = "Requires Qdrant server")]
    public async Task StoreMetadataAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id);
        var storagePath = $"/test/path/{id}.distinction.bin";

        // Act
        var result = await _storage.StoreMetadataAsync(weights, storagePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact(Skip = "Requires Qdrant server")]
    public async Task GetByIdAsync_WithExistingId_ReturnsMetadata()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id);
        var storagePath = $"/test/path/{id}.distinction.bin";

        var storeResult = await _storage.StoreMetadataAsync(weights, storagePath);
        storeResult.IsSuccess.Should().BeTrue();

        // Act
        var result = await _storage.GetByIdAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.Circumstance.Should().Be(weights.Circumstance);
        result.Value.StoragePath.Should().Be(storagePath);
        result.Value.Fitness.Should().Be(weights.Fitness);
    }

    [Fact(Skip = "Requires Qdrant server")]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsFailure()
    {
        // Arrange
        var nonExistentId = DistinctionId.NewId();

        // Act
        var result = await _storage.GetByIdAsync(nonExistentId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact(Skip = "Requires Qdrant server")]
    public async Task MarkDissolvedAsync_WithExistingId_RemovesFromQdrant()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id);
        var storagePath = $"/test/path/{id}.distinction.bin";

        await _storage.StoreMetadataAsync(weights, storagePath);

        // Act
        var result = await _storage.MarkDissolvedAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify it's been removed
        var getResult = await _storage.GetByIdAsync(id);
        getResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SearchSimilarAsync_NotYetImplemented_ReturnsEmptyList()
    {
        // Arrange
        var query = "test query";

        // Act
        var result = await _storage.SearchSimilarAsync(query, topK: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    private DistinctionWeights CreateTestWeights(DistinctionId id)
    {
        return new DistinctionWeights(
            Id: id,
            Embedding: new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f },
            DissolutionMask: new float[] { 0.01f, 0.02f, 0.03f, 0.04f, 0.05f },
            RecognitionTransform: new float[] { 1.1f, 1.2f, 1.3f, 1.4f, 1.5f },
            LearnedAtStage: DreamStage.Distinction,
            Fitness: 0.85,
            Circumstance: "test_circumstance",
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);
    }
}
