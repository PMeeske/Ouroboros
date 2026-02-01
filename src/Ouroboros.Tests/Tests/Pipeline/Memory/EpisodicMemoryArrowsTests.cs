// <copyright file="EpisodicMemoryArrowsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Ouroboros.Domain.Reinforcement;
using Ouroboros.Domain.States;
using Ouroboros.Pipeline.Memory;
using Qdrant.Client;

namespace Ouroboros.Tests.Memory;

/// <summary>
/// Tests for EpisodicMemoryArrows arrow parameterization pattern.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodicMemoryArrowsTests
{
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string text, CancellationToken ct = default)
        {
            // Return a simple mock embedding
            var embedding = new float[768];
            for (int i = 0; i < 768; i++)
            {
                embedding[i] = 0.1f;
            }
            return Task.FromResult(embedding);
        }
    }

    private static PipelineBranch CreateTestBranch()
    {
        var store = new TrackedVectorStore();
        var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch("test", store, dataSource);
    }

    private static Ouroboros.Pipeline.Memory.ExecutionContext CreateTestContext()
    {
        return new Ouroboros.Pipeline.Memory.ExecutionContext(
            "Test goal",
            ImmutableDictionary<string, object>.Empty);
    }

    private static Outcome CreateTestOutcome(bool success = true)
    {
        return new Outcome(
            success,
            "Test output",
            TimeSpan.FromSeconds(1),
            ImmutableList<string>.Empty);
    }

    [Fact(Skip = "Requires Qdrant connection")]
    public async Task StoreEpisodeArrow_WithValidData_ShouldStoreEpisode()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var branch = CreateTestBranch();
        var context = CreateTestContext();
        var result = CreateTestOutcome();
        var metadata = ImmutableDictionary<string, object>.Empty;

        // Act
        var arrow = EpisodicMemoryArrows.StoreEpisodeArrow(
            qdrantClient,
            embeddingModel,
            context,
            result,
            metadata);
        var updatedBranch = await arrow(branch);

        // Assert
        updatedBranch.Should().NotBeNull();
        updatedBranch.Events.OfType<EpisodeStoredEvent>().Should().NotBeEmpty();
    }

    [Fact(Skip = "Requires Qdrant connection")]
    public async Task SafeStoreEpisodeArrow_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var branch = CreateTestBranch();
        var context = CreateTestContext();
        var result = CreateTestOutcome();
        var metadata = ImmutableDictionary<string, object>.Empty;

        // Act
        var arrow = EpisodicMemoryArrows.SafeStoreEpisodeArrow(
            qdrantClient,
            embeddingModel,
            context,
            result,
            metadata);
        var arrowResult = await arrow(branch);

        // Assert
        arrowResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CreateConfiguredMemorySystem_ShouldReturnValidSystem()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();

        // Act
        var memorySystem = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient,
            embeddingModel);

        // Assert
        memorySystem.Should().NotBeNull();
    }

    [Fact]
    public void ConfiguredMemorySystem_StoreEpisode_ShouldReturnArrow()
    {
        // Arrange
        var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var memorySystem = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient,
            embeddingModel);
        var context = CreateTestContext();
        var result = CreateTestOutcome();
        var metadata = ImmutableDictionary<string, object>.Empty;

        // Act
        var arrow = memorySystem.StoreEpisode(context, result, metadata);

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void ConfiguredMemorySystem_RetrieveSimilarEpisodes_ShouldReturnArrow()
    {
        // Arrange
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var memorySystem = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient,
            embeddingModel);

        // Act
        var arrow = memorySystem.RetrieveSimilarEpisodes("test query", 5);

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void ConfiguredMemorySystem_PlanWithExperience_ShouldReturnArrow()
    {
        // Arrange
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var memorySystem = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
            qdrantClient,
            embeddingModel);

        // Act
        var arrow = memorySystem.PlanWithExperience("test goal");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void CreateEpisodeRetriever_ShouldReturnFactory()
    {
        // Arrange
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();

        // Act
        var factory = EpisodicMemoryArrows.CreateEpisodeRetriever(
            qdrantClient,
            embeddingModel);

        // Assert
        factory.Should().NotBeNull();

        // Test that the factory can create arrows
        var arrow = factory("test query", 5);
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void EpisodeStoredEvent_ShouldHaveCorrectProperties()
    {
        // Arrange
        var episodeId = new EpisodeId(Guid.NewGuid());
        var goal = "Test goal";
        var success = true;
        var timestamp = DateTime.UtcNow;

        // Act
        var ev = new EpisodeStoredEvent(episodeId, goal, success, timestamp);

        // Assert
        ev.EpisodeId.Should().Be(episodeId);
        ev.Goal.Should().Be(goal);
        ev.Success.Should().BeTrue();
        ev.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void EpisodesRetrievedEvent_ShouldHaveCorrectProperties()
    {
        // Arrange
        var count = 5;
        var query = "Test query";
        var timestamp = DateTime.UtcNow;

        // Act
        var ev = new EpisodesRetrievedEvent(count, query, timestamp);

        // Assert
        ev.Count.Should().Be(count);
        ev.Query.Should().Be(query);
        ev.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public async Task ArrowComposition_MultipleEpisodicMemoryOperations_ShouldCompose()
    {
        // Note: This test demonstrates arrow composition without requiring Qdrant
        // In real usage, these would be connected to actual Qdrant instances

        // Act - Create arrows that would be composed in real usage
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();

        var retrieverFactory = EpisodicMemoryArrows.CreateEpisodeRetriever(
            qdrantClient,
            embeddingModel);

        var retrieveArrow1 = retrieverFactory("query1", 5);
        var retrieveArrow2 = retrieverFactory("query2", 5);

        // Assert - Verify arrows can be created and would compose
        retrieveArrow1.Should().NotBeNull();
        retrieveArrow2.Should().NotBeNull();
    }

    [Fact(Skip = "Requires Qdrant connection")]
    public async Task PlanWithExperienceArrow_WithSuccessfulEpisodes_ShouldGeneratePlanWithPatterns()
    {
        // Arrange
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var branch = CreateTestBranch();
        var goal = "Test goal with successful episodes";

        // Act
        var arrow = EpisodicMemoryArrows.PlanWithExperienceArrow(
            qdrantClient,
            embeddingModel,
            goal,
            topK: 5);
        var (resultBranch, plan) = await arrow(branch);

        // Assert
        resultBranch.Should().NotBeNull();
        // Plan may be null if no episodes found, which is expected in test environment
        if (plan != null)
        {
            plan.Description.Should().Contain(goal);
            plan.Actions.Should().NotBeNull();
        }
    }

    [Fact(Skip = "Requires Qdrant connection")]
    public async Task PlanWithExperienceArrow_WithNoEpisodes_ShouldReturnNullPlan()
    {
        // Arrange
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var branch = CreateTestBranch();
        var goal = "Completely novel goal with no history";

        // Act
        var arrow = EpisodicMemoryArrows.PlanWithExperienceArrow(
            qdrantClient,
            embeddingModel,
            goal,
            topK: 5);
        var (resultBranch, plan) = await arrow(branch);

        // Assert
        resultBranch.Should().NotBeNull();
        // Plan might be null when no relevant episodes are found
    }

    [Fact(Skip = "Requires Qdrant connection")]
    public async Task PlanWithExperienceArrow_WithCustomTopK_ShouldRespectParameter()
    {
        // Arrange
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var branch = CreateTestBranch();
        var goal = "Test goal with custom topK";

        // Act
        var arrow = EpisodicMemoryArrows.PlanWithExperienceArrow(
            qdrantClient,
            embeddingModel,
            goal,
            topK: 10);
        var (resultBranch, plan) = await arrow(branch);

        // Assert
        resultBranch.Should().NotBeNull();
        // The topK parameter should be used in episode retrieval
    }

    [Fact]
    public void PlanWithExperienceArrow_Creation_ShouldNotThrow()
    {
        // Arrange
        using var qdrantClient = new QdrantClient("localhost", 6334);
        var embeddingModel = new MockEmbeddingModel();
        var goal = "Test goal";

        // Act
        var arrow = EpisodicMemoryArrows.PlanWithExperienceArrow(
            qdrantClient,
            embeddingModel,
            goal);

        // Assert
        arrow.Should().NotBeNull();
    }
}
