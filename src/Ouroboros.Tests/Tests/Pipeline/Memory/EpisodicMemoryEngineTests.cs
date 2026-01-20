// <copyright file="EpisodicMemoryEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Pipeline.Memory;

using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Memory;
using Xunit;

/// <summary>
/// Unit tests for EpisodicMemoryEngine.
/// Tests construction, validation, and Result monad patterns.
/// Integration tests with real Qdrant are in separate test class.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodicMemoryEngineTests
{
    [Fact]
    public void Constructor_WithConnectionString_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var embedding = new TestEmbeddingModel();

        // Act
        var engine = new EpisodicMemoryEngine(connectionString, embedding);

        // Assert
        engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        string? connectionString = null;
        var embedding = new TestEmbeddingModel();

        // Act
        Action act = () => new EpisodicMemoryEngine(connectionString!, embedding);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var connectionString = string.Empty;
        var embedding = new TestEmbeddingModel();

        // Act
        Action act = () => new EpisodicMemoryEngine(connectionString, embedding);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithCustomCollectionName_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var embedding = new TestEmbeddingModel();
        var customCollection = "custom_episodes";

        // Act
        var engine = new EpisodicMemoryEngine(connectionString, embedding, customCollection);

        // Assert
        engine.Should().NotBeNull();
    }

    /// <summary>
    /// Test implementation of IEmbeddingModel.
    /// </summary>
    private class TestEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            // Return a dummy embedding vector
            var embedding = new float[768];
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = 0.1f;
            }

            return Task.FromResult(embedding);
        }
    }
}

/// <summary>
/// Unit tests for Episode record type.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodeTests
{
    [Fact]
    public void Episode_ShouldBeImmutable()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var goal = "Test goal";
        var branch = CreateTestBranch();
        var outcome = Outcome.Successful("success", TimeSpan.FromSeconds(1));
        var successScore = 0.9;
        var lessons = ImmutableList<string>.Empty.Add("Lesson 1");
        var context = ImmutableDictionary<string, object>.Empty;
        var embedding = new float[768];

        // Act
        var episode = new Episode(id, timestamp, goal, branch, outcome, successScore, lessons, context, embedding);

        // Assert
        episode.Id.Should().Be(id);
        episode.Timestamp.Should().Be(timestamp);
        episode.Goal.Should().Be(goal);
        episode.SuccessScore.Should().Be(successScore);
        episode.LessonsLearned.Should().HaveCount(1);
    }

    [Fact]
    public void Episode_WithModification_ShouldCreateNewInstance()
    {
        // Arrange
        var episode1 = CreateTestEpisode();

        // Act
        var episode2 = episode1 with { SuccessScore = 0.5 };

        // Assert
        episode1.SuccessScore.Should().Be(0.9);
        episode2.SuccessScore.Should().Be(0.5);
        episode1.Id.Should().Be(episode2.Id); // Same ID
    }

    private static Episode CreateTestEpisode()
    {
        return new Episode(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "Test goal",
            CreateTestBranch(),
            Outcome.Successful("success", TimeSpan.FromSeconds(1)),
            0.9,
            ImmutableList<string>.Empty,
            ImmutableDictionary<string, object>.Empty,
            new float[768]);
    }

    private static PipelineBranch CreateTestBranch()
    {
        var store = new TrackedVectorStore();
        var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch("test", store, dataSource);
    }
}

/// <summary>
/// Unit tests for Outcome record type.
/// </summary>
[Trait("Category", "Unit")]
public class OutcomeTests
{
    [Fact]
    public void Outcome_Successful_ShouldCreateSuccessfulOutcome()
    {
        // Act
        var outcome = Outcome.Successful("output", TimeSpan.FromSeconds(5));

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.Output.Should().Be("output");
        outcome.Duration.Should().Be(TimeSpan.FromSeconds(5));
        outcome.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Outcome_Failed_ShouldCreateFailedOutcome()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var outcome = Outcome.Failed("partial output", TimeSpan.FromSeconds(3), errors);

        // Assert
        outcome.Success.Should().BeFalse();
        outcome.Output.Should().Be("partial output");
        outcome.Duration.Should().Be(TimeSpan.FromSeconds(3));
        outcome.Errors.Should().HaveCount(2);
        outcome.Errors.Should().Contain("Error 1");
    }

    [Fact]
    public void Outcome_ShouldBeImmutable()
    {
        // Arrange
        var outcome1 = Outcome.Successful("output", TimeSpan.FromSeconds(1));

        // Act
        var outcome2 = outcome1 with { Success = false };

        // Assert
        outcome1.Success.Should().BeTrue();
        outcome2.Success.Should().BeFalse();
    }
}

/// <summary>
/// Unit tests for ExecutionContext record type.
/// </summary>
[Trait("Category", "Unit")]
public class ExecutionContextTests
{
    [Fact]
    public void ExecutionContext_WithGoal_ShouldCreateContext()
    {
        // Act
        var context = ExecutionContext.WithGoal("test goal");

        // Assert
        context.Goal.Should().Be("test goal");
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void ExecutionContext_WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var context = ExecutionContext.WithGoal("test goal");

        // Act
        var updated = context.WithMetadata("key1", "value1");

        // Assert
        context.Metadata.Should().BeEmpty(); // Original unchanged
        updated.Metadata.Should().ContainKey("key1");
        updated.Metadata["key1"].Should().Be("value1");
    }

    [Fact]
    public void ExecutionContext_WithMultipleMetadata_ShouldAccumulate()
    {
        // Arrange
        var context = ExecutionContext.WithGoal("test goal");

        // Act
        var updated = context
            .WithMetadata("key1", "value1")
            .WithMetadata("key2", 42)
            .WithMetadata("key3", true);

        // Assert
        updated.Metadata.Should().HaveCount(3);
        updated.Metadata["key1"].Should().Be("value1");
        updated.Metadata["key2"].Should().Be(42);
        updated.Metadata["key3"].Should().Be(true);
    }
}

/// <summary>
/// Unit tests for EpisodeId record type.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodeIdTests
{
    [Fact]
    public void EpisodeId_ShouldStoreGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var episodeId = new EpisodeId(guid);

        // Assert
        episodeId.Value.Should().Be(guid);
    }

    [Fact]
    public void EpisodeId_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new EpisodeId(guid);
        var id2 = new EpisodeId(guid);
        var id3 = new EpisodeId(Guid.NewGuid());

        // Assert
        id1.Should().Be(id2); // Same GUID
        id1.Should().NotBe(id3); // Different GUID
    }
}

/// <summary>
/// Unit tests for ConsolidationStrategy enum.
/// </summary>
[Trait("Category", "Unit")]
public class ConsolidationStrategyTests
{
    [Fact]
    public void ConsolidationStrategy_ShouldHaveAllExpectedValues()
    {
        // Arrange & Act
        var strategies = Enum.GetValues<ConsolidationStrategy>();

        // Assert
        strategies.Should().Contain(ConsolidationStrategy.Compress);
        strategies.Should().Contain(ConsolidationStrategy.Abstract);
        strategies.Should().Contain(ConsolidationStrategy.Prune);
        strategies.Should().Contain(ConsolidationStrategy.Hierarchical);
        strategies.Should().HaveCount(4);
    }
}
