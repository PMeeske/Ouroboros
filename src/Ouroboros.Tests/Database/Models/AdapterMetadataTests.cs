// <copyright file="AdapterMetadataTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.Models;

using FluentAssertions;
using Ouroboros.Core.Learning;
using Xunit;

/// <summary>
/// Unit tests for AdapterMetadata record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class AdapterMetadataTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsMetadata()
    {
        // Arrange
        var id = AdapterId.NewId();
        var taskName = "summarization";
        var config = AdapterConfig.Default();
        var blobPath = "/storage/adapter.bin";

        // Act
        var metadata = AdapterMetadata.Create(id, taskName, config, blobPath);

        // Assert
        metadata.Id.Should().Be(id);
        metadata.TaskName.Should().Be(taskName);
        metadata.Config.Should().Be(config);
        metadata.BlobStoragePath.Should().Be(blobPath);
        metadata.TrainingExampleCount.Should().Be(0);
        metadata.PerformanceScore.Should().BeNull();
    }

    [Fact]
    public void Create_SetsCreatedAtAndLastTrainedAt_ToCurrentTime()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path");

        var afterCreate = DateTime.UtcNow.AddSeconds(1);

        // Assert
        metadata.CreatedAt.Should().BeAfter(beforeCreate);
        metadata.CreatedAt.Should().BeBefore(afterCreate);
        metadata.LastTrainedAt.Should().Be(metadata.CreatedAt);
    }

    [Fact]
    public void Create_WithDifferentConfigs_PreservesConfiguration()
    {
        // Arrange
        var lowRankConfig = AdapterConfig.LowRank();
        var highRankConfig = AdapterConfig.HighRank();

        // Act
        var lowRankMeta = AdapterMetadata.Create(AdapterId.NewId(), "task", lowRankConfig, "/path");
        var highRankMeta = AdapterMetadata.Create(AdapterId.NewId(), "task", highRankConfig, "/path");

        // Assert
        lowRankMeta.Config.Rank.Should().Be(4);
        highRankMeta.Config.Rank.Should().Be(16);
    }

    #endregion

    #region WithTraining Method Tests

    [Fact]
    public void WithTraining_AddsExampleCount_Cumulatively()
    {
        // Arrange
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path");

        // Act
        var afterFirstTraining = metadata.WithTraining(100);
        var afterSecondTraining = afterFirstTraining.WithTraining(50);

        // Assert
        metadata.TrainingExampleCount.Should().Be(0);
        afterFirstTraining.TrainingExampleCount.Should().Be(100);
        afterSecondTraining.TrainingExampleCount.Should().Be(150);
    }

    [Fact]
    public void WithTraining_WithPerformanceScore_SetsScore()
    {
        // Arrange
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path");

        // Act
        var trained = metadata.WithTraining(100, performanceScore: 0.85);

        // Assert
        trained.PerformanceScore.Should().Be(0.85);
    }

    [Fact]
    public void WithTraining_WithoutPerformanceScore_PreservesPrevious()
    {
        // Arrange
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path")
            .WithTraining(50, 0.75);

        // Act
        var retrainedWithoutScore = metadata.WithTraining(25);

        // Assert
        retrainedWithoutScore.PerformanceScore.Should().Be(0.75);
    }

    [Fact]
    public void WithTraining_UpdatesLastTrainedAt()
    {
        // Arrange
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path");
        var originalTrainedAt = metadata.LastTrainedAt;

        // Act - wait a tiny bit to ensure time difference
        Thread.Sleep(10);
        var trained = metadata.WithTraining(100);

        // Assert
        trained.LastTrainedAt.Should().BeAfter(originalTrainedAt);
    }

    [Fact]
    public void WithTraining_PreservesOtherProperties()
    {
        // Arrange
        var id = AdapterId.NewId();
        var taskName = "preserve-task";
        var config = AdapterConfig.HighRank();
        var blobPath = "/preserve/path.bin";
        var metadata = AdapterMetadata.Create(id, taskName, config, blobPath);

        // Act
        var trained = metadata.WithTraining(100, 0.9);

        // Assert
        trained.Id.Should().Be(id);
        trained.TaskName.Should().Be(taskName);
        trained.Config.Should().Be(config);
        trained.BlobStoragePath.Should().Be(blobPath);
        trained.CreatedAt.Should().Be(metadata.CreatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10000)]
    public void WithTraining_WithDifferentExampleCounts_AccumulatesCorrectly(int exampleCount)
    {
        // Arrange
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path");

        // Act
        var trained = metadata.WithTraining(exampleCount);

        // Assert
        trained.TrainingExampleCount.Should().Be(exampleCount);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void WithTraining_WithDifferentPerformanceScores_SetsCorrectly(double score)
    {
        // Arrange
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path");

        // Act
        var trained = metadata.WithTraining(100, score);

        // Assert
        trained.PerformanceScore.Should().Be(score);
    }

    #endregion

    #region Record Immutability Tests

    [Fact]
    public void WithTraining_ReturnsNewInstance_DoesNotModifyOriginal()
    {
        // Arrange
        var original = AdapterMetadata.Create(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path");

        // Act
        var trained = original.WithTraining(100, 0.9);

        // Assert
        original.TrainingExampleCount.Should().Be(0);
        original.PerformanceScore.Should().BeNull();
        trained.TrainingExampleCount.Should().Be(100);
        trained.PerformanceScore.Should().Be(0.9);
    }

    [Fact]
    public void WithExpression_CreatesNewRecordWithModifiedProperty()
    {
        // Arrange
        var original = AdapterMetadata.Create(
            AdapterId.NewId(),
            "original-task",
            AdapterConfig.Default(),
            "/path");

        // Act
        var modified = original with { TaskName = "modified-task" };

        // Assert
        original.TaskName.Should().Be("original-task");
        modified.TaskName.Should().Be("modified-task");
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var id = AdapterId.NewId();
        var config = AdapterConfig.Default();
        var createdAt = DateTime.UtcNow;
        var trainedAt = DateTime.UtcNow;

        var metadata1 = new AdapterMetadata(
            id, "task", config, "/path", createdAt, trainedAt, 100, 0.9);
        var metadata2 = new AdapterMetadata(
            id, "task", config, "/path", createdAt, trainedAt, 100, 0.9);

        // Assert
        metadata1.Should().Be(metadata2);
    }

    [Fact]
    public void Equality_DifferentIds_AreNotEqual()
    {
        // Arrange
        var config = AdapterConfig.Default();
        var createdAt = DateTime.UtcNow;
        var trainedAt = DateTime.UtcNow;

        var metadata1 = new AdapterMetadata(
            AdapterId.NewId(), "task", config, "/path", createdAt, trainedAt, 100, 0.9);
        var metadata2 = new AdapterMetadata(
            AdapterId.NewId(), "task", config, "/path", createdAt, trainedAt, 100, 0.9);

        // Assert
        metadata1.Should().NotBe(metadata2);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = AdapterId.NewId();
        var taskName = "full-constructor-task";
        var config = AdapterConfig.HighRank();
        var blobPath = "/full/path.bin";
        var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var trainedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var exampleCount = 500;
        var score = 0.95;

        // Act
        var metadata = new AdapterMetadata(
            id, taskName, config, blobPath, createdAt, trainedAt, exampleCount, score);

        // Assert
        metadata.Id.Should().Be(id);
        metadata.TaskName.Should().Be(taskName);
        metadata.Config.Should().Be(config);
        metadata.BlobStoragePath.Should().Be(blobPath);
        metadata.CreatedAt.Should().Be(createdAt);
        metadata.LastTrainedAt.Should().Be(trainedAt);
        metadata.TrainingExampleCount.Should().Be(exampleCount);
        metadata.PerformanceScore.Should().Be(score);
    }

    [Fact]
    public void Constructor_WithNullPerformanceScore_DefaultsToNull()
    {
        // Arrange & Act
        var metadata = new AdapterMetadata(
            AdapterId.NewId(),
            "task",
            AdapterConfig.Default(),
            "/path",
            DateTime.UtcNow,
            DateTime.UtcNow,
            0);

        // Assert
        metadata.PerformanceScore.Should().BeNull();
    }

    #endregion

    #region Multiple Training Sessions Tests

    [Fact]
    public void MultipleTrainingSessions_AccumulateExamplesAndUpdateScore()
    {
        // Arrange
        var metadata = AdapterMetadata.Create(
            AdapterId.NewId(),
            "progressive-training",
            AdapterConfig.Default(),
            "/path");

        // Act - simulate multiple training sessions
        metadata = metadata.WithTraining(100, 0.6);  // Initial training
        metadata = metadata.WithTraining(50, 0.7);   // Improvement
        metadata = metadata.WithTraining(75, 0.8);   // Further improvement
        metadata = metadata.WithTraining(25, 0.85);  // Fine-tuning

        // Assert
        metadata.TrainingExampleCount.Should().Be(250);
        metadata.PerformanceScore.Should().Be(0.85);
    }

    #endregion
}
