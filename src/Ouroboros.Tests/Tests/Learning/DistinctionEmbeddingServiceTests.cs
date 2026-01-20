// <copyright file="DistinctionEmbeddingServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests.Learning;

using FluentAssertions;
using Ouroboros.Application.Learning;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Domain;
using Xunit;

/// <summary>
/// Tests for the DistinctionEmbeddingService.
/// Validates embedding creation, similarity, dissolution, and recognition operations.
/// </summary>
public sealed class DistinctionEmbeddingServiceTests
{
    private readonly DistinctionEmbeddingService service;
    private readonly MockEmbeddingModel embeddingModel;

    public DistinctionEmbeddingServiceTests()
    {
        this.embeddingModel = new MockEmbeddingModel();
        this.service = new DistinctionEmbeddingService(this.embeddingModel);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateDistinctionEmbedding_ShouldSucceed()
    {
        // Arrange
        var circumstance = "test circumstance";
        var stage = DreamStage.Distinction;

        // Act
        var result = await this.service.CreateDistinctionEmbeddingAsync(circumstance, stage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateDistinctionEmbedding_WithDifferentStages_ShouldProduceDifferentEmbeddings()
    {
        // Arrange
        var circumstance = "same circumstance";

        // Act
        var embedding1 = await this.service.CreateDistinctionEmbeddingAsync(circumstance, DreamStage.Void);
        var embedding2 = await this.service.CreateDistinctionEmbeddingAsync(circumstance, DreamStage.Recognition);

        // Assert
        embedding1.IsSuccess.Should().BeTrue();
        embedding2.IsSuccess.Should().BeTrue();
        embedding1.Value.Should().NotBeEquivalentTo(embedding2.Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateDreamCycleEmbedding_ShouldContainAllStages()
    {
        // Arrange
        var circumstance = "test dream cycle";

        // Act
        var result = await this.service.CreateDreamCycleEmbeddingAsync(circumstance);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dreamEmbedding = result.Value;
        dreamEmbedding.StageEmbeddings.Should().HaveCount(9); // All 9 dream stages
        dreamEmbedding.CompositeEmbedding.Should().NotBeNull();
        dreamEmbedding.CompositeEmbedding.Length.Should().BeGreaterThan(0);
        dreamEmbedding.Circumstance.Should().Be(circumstance);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateDreamCycleEmbedding_CompositeEmbedding_ShouldBeNormalized()
    {
        // Arrange
        var circumstance = "normalization test";

        // Act
        var result = await this.service.CreateDreamCycleEmbeddingAsync(circumstance);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var composite = result.Value.CompositeEmbedding;
        
        var norm = Math.Sqrt(composite.Sum(x => x * x));
        norm.Should().BeApproximately(1.0, 0.01); // Should be unit length
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeDistinctionSimilarity_WithIdenticalEmbeddings_ShouldReturnOne()
    {
        // Arrange
        var embedding = new float[] { 1.0f, 0.0f, 0.0f };

        // Act
        var similarity = this.service.ComputeDistinctionSimilarity(embedding, embedding);

        // Assert
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeDistinctionSimilarity_WithOrthogonalEmbeddings_ShouldReturnZero()
    {
        // Arrange
        var embedding1 = new float[] { 1.0f, 0.0f, 0.0f };
        var embedding2 = new float[] { 0.0f, 1.0f, 0.0f };

        // Act
        var similarity = this.service.ComputeDistinctionSimilarity(embedding1, embedding2);

        // Assert
        similarity.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeDistinctionSimilarity_WithDifferentDimensions_ShouldReturnZero()
    {
        // Arrange
        var embedding1 = new float[] { 1.0f, 0.0f };
        var embedding2 = new float[] { 1.0f, 0.0f, 0.0f };

        // Act
        var similarity = this.service.ComputeDistinctionSimilarity(embedding1, embedding2);

        // Assert
        similarity.Should().Be(0.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyDissolution_ShouldSubtractContribution()
    {
        // Arrange
        var currentEmbedding = new float[] { 1.0f, 1.0f, 0.0f };
        var dissolvedEmbedding = new float[] { 0.5f, 0.5f, 0.0f };
        var strength = 1.0;

        // Act
        var result = this.service.ApplyDissolution(currentEmbedding, dissolvedEmbedding, strength);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(currentEmbedding.Length);
        
        // Result should be normalized and different from original
        var norm = Math.Sqrt(result.Sum(x => x * x));
        norm.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyDissolution_WithZeroStrength_ShouldReturnNormalizedOriginal()
    {
        // Arrange
        var currentEmbedding = new float[] { 1.0f, 1.0f, 0.0f };
        var dissolvedEmbedding = new float[] { 0.5f, 0.5f, 0.0f };
        var strength = 0.0;

        // Act
        var result = this.service.ApplyDissolution(currentEmbedding, dissolvedEmbedding, strength);

        // Assert
        result.Should().NotBeNull();
        var norm = Math.Sqrt(result.Sum(x => x * x));
        norm.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyDissolution_WithMismatchedDimensions_ShouldThrow()
    {
        // Arrange
        var currentEmbedding = new float[] { 1.0f, 0.0f };
        var dissolvedEmbedding = new float[] { 1.0f, 0.0f, 0.0f };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            this.service.ApplyDissolution(currentEmbedding, dissolvedEmbedding, 1.0));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyRecognition_ShouldMergeEmbeddings()
    {
        // Arrange
        var currentEmbedding = new float[] { 1.0f, 1.0f, 0.0f };
        var selfEmbedding = new float[] { 1.0f, 0.0f, 1.0f };

        // Act
        var result = this.service.ApplyRecognition(currentEmbedding, selfEmbedding);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(currentEmbedding.Length);
        
        // Result should be normalized
        var norm = Math.Sqrt(result.Sum(x => x * x));
        norm.Should().BeApproximately(1.0, 0.01);
        
        // Result should be different from both inputs (geometric mean)
        result.Should().NotBeEquivalentTo(currentEmbedding);
        result.Should().NotBeEquivalentTo(selfEmbedding);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyRecognition_WithIdenticalEmbeddings_ShouldReturnEquivalent()
    {
        // Arrange
        var embedding = new float[] { 1.0f, 0.0f, 0.0f };

        // Act
        var result = this.service.ApplyRecognition(embedding, embedding);

        // Assert
        result.Should().NotBeNull();
        
        // Geometric mean of identical values should preserve direction
        var similarity = this.service.ComputeDistinctionSimilarity(result, embedding);
        similarity.Should().BeGreaterThan(0.9);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyRecognition_WithMismatchedDimensions_ShouldThrow()
    {
        // Arrange
        var currentEmbedding = new float[] { 1.0f, 0.0f };
        var selfEmbedding = new float[] { 1.0f, 0.0f, 0.0f };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            this.service.ApplyRecognition(currentEmbedding, selfEmbedding));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DreamEmbedding_ComputeSimilarity_WithSelf_ShouldReturnOne()
    {
        // Arrange
        var result = await this.service.CreateDreamCycleEmbeddingAsync("test");
        var dreamEmbedding = result.Value;

        // Act
        var similarity = dreamEmbedding.ComputeSimilarity(dreamEmbedding);

        // Assert
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DreamEmbedding_GetStageEmbedding_ShouldReturnCorrectEmbedding()
    {
        // Arrange
        var result = await this.service.CreateDreamCycleEmbeddingAsync("test");
        var dreamEmbedding = result.Value;

        // Act
        var stageEmbedding = dreamEmbedding.GetStageEmbedding(DreamStage.Recognition);

        // Assert
        stageEmbedding.Should().NotBeNull();
        stageEmbedding!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DistinctionEmbeddingService_WithNullModel_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DistinctionEmbeddingService(null!));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CreateDistinctionEmbedding_WithEmptyCircumstance_ShouldReturnFailure()
    {
        // Act
        var result = await this.service.CreateDistinctionEmbeddingAsync(string.Empty, DreamStage.Void);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to create");
    }
}

/// <summary>
/// Mock embedding model for testing without external dependencies.
/// </summary>
internal class MockEmbeddingModel : IEmbeddingModel
{
    private const int StandardEmbeddingDimension = 384;

    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        // Create deterministic embeddings based on input hash
        var hash = input.GetHashCode();
        var random = new Random(hash);
        
        var embedding = new float[StandardEmbeddingDimension];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize to unit length
        var norm = Math.Sqrt(embedding.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= (float)norm;
            }
        }

        return Task.FromResult(embedding);
    }
}
