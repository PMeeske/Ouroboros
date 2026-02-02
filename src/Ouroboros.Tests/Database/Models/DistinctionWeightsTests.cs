// <copyright file="DistinctionWeightsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.Models;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Learning;
using Xunit;

/// <summary>
/// Unit tests for DistinctionWeights record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class DistinctionWeightsTests
{
    #region Helper Methods

    private static DistinctionWeights CreateTestWeights(
        DistinctionId? id = null,
        double fitness = 0.5,
        float[]? embedding = null,
        string circumstance = "test-circumstance")
    {
        return new DistinctionWeights(
            Id: id ?? DistinctionId.NewId(),
            Embedding: embedding ?? new float[] { 0.1f, 0.2f, 0.3f },
            DissolutionMask: new float[] { 0.01f, 0.02f, 0.03f },
            RecognitionTransform: new float[] { 1.0f, 1.1f, 1.2f },
            LearnedAtStage: DreamStage.Distinction,
            Fitness: fitness,
            Circumstance: circumstance,
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);
    }

    #endregion

    #region Constructor / Record Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesRecord()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var embedding = new float[] { 0.5f, 0.6f };
        var dissolutionMask = new float[] { 0.1f, 0.2f };
        var recognitionTransform = new float[] { 1.0f, 2.0f };
        var createdAt = DateTime.UtcNow;

        // Act
        var weights = new DistinctionWeights(
            id, embedding, dissolutionMask, recognitionTransform,
            DreamStage.Recognition, 0.75, "test", createdAt, null);

        // Assert
        weights.Id.Should().Be(id);
        weights.Embedding.Should().BeEquivalentTo(embedding);
        weights.DissolutionMask.Should().BeEquivalentTo(dissolutionMask);
        weights.RecognitionTransform.Should().BeEquivalentTo(recognitionTransform);
        weights.LearnedAtStage.Should().Be(DreamStage.Recognition);
        weights.Fitness.Should().Be(0.75);
        weights.Circumstance.Should().Be("test");
        weights.CreatedAt.Should().Be(createdAt);
        weights.LastUpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var createdAt = DateTime.UtcNow;
        var embedding = new float[] { 0.1f };
        var mask = new float[] { 0.1f };
        var transform = new float[] { 0.1f };

        var weights1 = new DistinctionWeights(id, embedding, mask, transform, DreamStage.Distinction, 0.5, "test", createdAt, null);
        var weights2 = new DistinctionWeights(id, embedding, mask, transform, DreamStage.Distinction, 0.5, "test", createdAt, null);

        // Assert - records with same values should be equal
        weights1.Should().Be(weights2);
    }

    [Theory]
    [InlineData(DreamStage.Distinction)]
    [InlineData(DreamStage.Dissolution)]
    [InlineData(DreamStage.Recognition)]
    public void Constructor_WithDifferentDreamStages_SetsCorrectly(DreamStage stage)
    {
        // Act
        var weights = new DistinctionWeights(
            DistinctionId.NewId(),
            new float[] { 0.1f },
            new float[] { 0.1f },
            new float[] { 0.1f },
            stage,
            0.5,
            "test",
            DateTime.UtcNow,
            null);

        // Assert
        weights.LearnedAtStage.Should().Be(stage);
    }

    #endregion

    #region UpdateFitness Tests

    [Fact]
    public void UpdateFitness_WithCorrectPrediction_IncreasesFitness()
    {
        // Arrange
        var weights = CreateTestWeights(fitness: 0.5);

        // Act
        var updated = weights.UpdateFitness(correct: true);

        // Assert
        updated.Fitness.Should().BeGreaterThan(0.5);
        updated.LastUpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateFitness_WithIncorrectPrediction_DecreasesFitness()
    {
        // Arrange
        var weights = CreateTestWeights(fitness: 0.5);

        // Act
        var updated = weights.UpdateFitness(correct: false);

        // Assert
        updated.Fitness.Should().BeLessThan(0.5);
        updated.LastUpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateFitness_WithDefaultAlpha_UsesExponentialMovingAverage()
    {
        // Arrange - default alpha = 0.3
        var weights = CreateTestWeights(fitness: 0.5);

        // Act - correct prediction: newScore=1.0, updated = 0.3*1.0 + 0.7*0.5 = 0.65
        var updated = weights.UpdateFitness(correct: true);

        // Assert
        updated.Fitness.Should().BeApproximately(0.65, 0.001);
    }

    [Fact]
    public void UpdateFitness_WithCustomAlpha_AppliesCorrectly()
    {
        // Arrange
        var weights = CreateTestWeights(fitness: 0.5);

        // Act - alpha=0.5: newScore=1.0, updated = 0.5*1.0 + 0.5*0.5 = 0.75
        var updated = weights.UpdateFitness(correct: true, alpha: 0.5);

        // Assert
        updated.Fitness.Should().BeApproximately(0.75, 0.001);
    }

    [Fact]
    public void UpdateFitness_PreservesOtherProperties()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id: id, fitness: 0.5, circumstance: "preserve-me");

        // Act
        var updated = weights.UpdateFitness(correct: true);

        // Assert
        updated.Id.Should().Be(id);
        updated.Circumstance.Should().Be("preserve-me");
        updated.Embedding.Should().BeEquivalentTo(weights.Embedding);
        updated.LearnedAtStage.Should().Be(weights.LearnedAtStage);
    }

    [Fact]
    public void UpdateFitness_MultipleUpdates_AccumulatesCorrectly()
    {
        // Arrange
        var weights = CreateTestWeights(fitness: 0.5);

        // Act - multiple correct predictions should increase fitness
        var updated = weights
            .UpdateFitness(correct: true)
            .UpdateFitness(correct: true)
            .UpdateFitness(correct: true);

        // Assert - fitness should have increased significantly
        updated.Fitness.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void UpdateFitness_AlternatingResults_StabilizesAround50Percent()
    {
        // Arrange
        var weights = CreateTestWeights(fitness: 0.5);

        // Act - alternating correct/incorrect should stabilize around 0.5
        for (int i = 0; i < 20; i++)
        {
            weights = weights.UpdateFitness(correct: i % 2 == 0);
        }

        // Assert - should be close to 0.5
        weights.Fitness.Should().BeApproximately(0.5, 0.1);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    public void UpdateFitness_WithDifferentAlphaValues_StaysInBounds(double alpha)
    {
        // Arrange
        var weights = CreateTestWeights(fitness: 0.5);

        // Act
        var updatedCorrect = weights.UpdateFitness(correct: true, alpha);
        var updatedIncorrect = weights.UpdateFitness(correct: false, alpha);

        // Assert - fitness should always be between 0 and 1
        updatedCorrect.Fitness.Should().BeInRange(0.0, 1.0);
        updatedIncorrect.Fitness.Should().BeInRange(0.0, 1.0);
    }

    #endregion

    #region ShouldDissolve Tests

    [Theory]
    [InlineData(0.1, 0.3, true)]   // Below threshold
    [InlineData(0.29, 0.3, true)]  // Just below threshold
    [InlineData(0.3, 0.3, false)]  // At threshold
    [InlineData(0.31, 0.3, false)] // Just above threshold
    [InlineData(0.5, 0.3, false)]  // Well above threshold
    [InlineData(0.9, 0.3, false)]  // High fitness
    public void ShouldDissolve_WithDifferentFitnessAndThreshold_ReturnsCorrectly(
        double fitness, double threshold, bool expectedShouldDissolve)
    {
        // Arrange
        var weights = CreateTestWeights(fitness: fitness);

        // Act
        var shouldDissolve = weights.ShouldDissolve(threshold);

        // Assert
        shouldDissolve.Should().Be(expectedShouldDissolve);
    }

    [Fact]
    public void ShouldDissolve_WithDefaultThreshold_Uses0Point3()
    {
        // Arrange
        var lowFitness = CreateTestWeights(fitness: 0.2);
        var highFitness = CreateTestWeights(fitness: 0.5);

        // Act & Assert
        lowFitness.ShouldDissolve().Should().BeTrue("0.2 < 0.3 default threshold");
        highFitness.ShouldDissolve().Should().BeFalse("0.5 >= 0.3 default threshold");
    }

    [Fact]
    public void ShouldDissolve_AfterMultipleIncorrectPredictions_ReturnsTrue()
    {
        // Arrange
        var weights = CreateTestWeights(fitness: 0.5);

        // Act - multiple incorrect predictions
        for (int i = 0; i < 10; i++)
        {
            weights = weights.UpdateFitness(correct: false);
        }

        // Assert
        weights.ShouldDissolve().Should().BeTrue();
        weights.Fitness.Should().BeLessThan(0.3);
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void UpdateFitness_ReturnsNewInstance_DoesNotModifyOriginal()
    {
        // Arrange
        var original = CreateTestWeights(fitness: 0.5);
        var originalFitness = original.Fitness;

        // Act
        var updated = original.UpdateFitness(correct: true);

        // Assert
        original.Fitness.Should().Be(originalFitness);
        updated.Should().NotBeSameAs(original);
        updated.Fitness.Should().NotBe(originalFitness);
    }

    [Fact]
    public void WithExpression_CreatesNewRecordWithModifiedProperty()
    {
        // Arrange
        var original = CreateTestWeights(fitness: 0.5);

        // Act
        var modified = original with { Fitness = 0.9 };

        // Assert
        original.Fitness.Should().Be(0.5);
        modified.Fitness.Should().Be(0.9);
        modified.Id.Should().Be(original.Id);
    }

    #endregion
}
