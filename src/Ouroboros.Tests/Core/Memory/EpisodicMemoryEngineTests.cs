// <copyright file="EpisodicMemoryEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Core.Memory;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Core.Memory;

/// <summary>
/// Unit tests for the EpisodicMemoryEngine following Ouroboros testing patterns.
/// </summary>
public class EpisodicMemoryEngineTests
{
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ILogger<EpisodicMemoryEngine>> _mockLogger;
    private readonly EpisodicMemoryEngine _memoryEngine;

    public EpisodicMemoryEngineTests()
    {
        _mockVectorStore = new Mock<IVectorStore>();
        _mockLogger = new Mock<ILogger<EpisodicMemoryEngine>>();
        _memoryEngine = new EpisodicMemoryEngine(_mockVectorStore.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StoreEpisodeAsync_ValidInput_StoresEpisodeSuccessfully()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();
        var context = new ExecutionContext("test", new Dictionary<string, object>());
        var outcome = new Outcome(true, "Success", TimeSpan.FromSeconds(5), new List<string>());
        var metadata = new Dictionary<string, object> { ["test_key"] = "test_value" };

        _mockVectorStore.Setup(vs => vs.AddAsync(
            It.IsAny<IEnumerable<Vector>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _memoryEngine.StoreEpisodeAsync(branch, context, outcome, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        _mockVectorStore.Verify(vs => vs.AddAsync(
            It.Is<IEnumerable<Vector>>(vectors => vectors.Any()), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task StoreEpisodeAsync_VectorStoreFails_ReturnsFailure()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();
        var context = new ExecutionContext("test", new Dictionary<string, object>());
        var outcome = new Outcome(true, "Success", TimeSpan.Zero, new List<string>());
        var metadata = new Dictionary<string, object>();

        _mockVectorStore.Setup(vs => vs.AddAsync(
            It.IsAny<IEnumerable<Vector>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage failed"));

        // Act
        var result = await _memoryEngine.StoreEpisodeAsync(branch, context, outcome, metadata);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to store episode");
    }

    [Fact]
    public async Task RetrieveSimilarEpisodesAsync_ValidQuery_RetrievesEpisodes()
    {
        // Arrange
        var query = "test query";
        var testDocuments = CreateTestDocuments();

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(testDocuments);

        // Act
        var result = await _memoryEngine.RetrieveSimilarEpisodesAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2); // Two documents with sufficient similarity
        _mockVectorStore.Verify(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task RetrieveSimilarEpisodesAsync_LowSimilarity_FiltersResults()
    {
        // Arrange
        var query = "test query";
        var testDocuments = CreateLowSimilarityDocuments();

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(testDocuments);

        // Act
        var result = await _memoryEngine.RetrieveSimilarEpisodesAsync(query, minSimilarity: 0.8);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty(); // No documents meet the higher threshold
    }

    [Fact]
    public async Task RetrieveSimilarEpisodesAsync_VectorStoreFails_ReturnsFailure()
    {
        // Arrange
        var query = "test query";

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Search failed"));

        // Act
        var result = await _memoryEngine.RetrieveSimilarEpisodesAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to retrieve episodes");
    }

    [Fact]
    public async Task ConsolidateMemoriesAsync_ValidStrategy_ReturnsSuccess()
    {
        // Arrange
        var olderThan = TimeSpan.FromDays(7);
        var strategy = ConsolidationStrategy.Compress;

        // Act
        var result = await _memoryEngine.ConsolidateMemoriesAsync(olderThan, strategy);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(ConsolidationStrategy.Compress)]
    [InlineData(ConsolidationStrategy.Abstract)]
    [InlineData(ConsolidationStrategy.Prune)]
    [InlineData(ConsolidationStrategy.Hierarchical)]
    public async Task ConsolidateMemoriesAsync_DifferentStrategies_AllSucceed(ConsolidationStrategy strategy)
    {
        // Arrange
        var olderThan = TimeSpan.FromDays(1);

        // Act
        var result = await _memoryEngine.ConsolidateMemoriesAsync(olderThan, strategy);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConsolidateMemoriesAsync_InvalidStrategy_ReturnsFailure()
    {
        // Arrange
        var olderThan = TimeSpan.FromDays(7);
        var invalidStrategy = (ConsolidationStrategy)999; // Invalid enum value

        // Act
        var result = await _memoryEngine.ConsolidateMemoriesAsync(olderThan, invalidStrategy);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanWithExperienceAsync_RelevantEpisodes_CreatesPlan()
    {
        // Arrange
        var goal = "test goal";
        var episodes = CreateTestEpisodes();

        // Act
        var result = await _memoryEngine.PlanWithExperienceAsync(goal, episodes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Goal.Should().Be(goal);
        result.Value.Steps.Should().NotBeEmpty();
        result.Value.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task PlanWithExperienceAsync_EmptyEpisodes_ReturnsFailure()
    {
        // Arrange
        var goal = "test goal";
        var emptyEpisodes = new List<Episode>();

        // Act
        var result = await _memoryEngine.PlanWithExperienceAsync(goal, emptyEpisodes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No relevant episodes");
    }

    [Fact]
    public async Task PlanWithExperienceAsync_HighSuccessEpisodes_IncreasesConfidence()
    {
        // Arrange
        var goal = "test goal";
        var highSuccessEpisodes = CreateHighSuccessEpisodes();

        // Act
        var result = await _memoryEngine.PlanWithExperienceAsync(goal, highSuccessEpisodes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void ExtractGoalFromBranch_WithReasoningEvents_ExtractsGoal()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();

        // Act
        var goal = EpisodicMemoryExtensions.ExtractGoalFromReasoning(branch);

        // Assert
        goal.Should().Be("test reasoning prompt");
    }

    [Fact]
    public void ExtractGoalFromBranchInfo_NamedBranch_UsesBranchName()
    {
        // Arrange
        var branch = new PipelineBranch("test-branch-name", Mock.Of<IVectorStore>(), Mock.Of<DataSource>());

        // Act
        var goal = EpisodicMemoryExtensions.ExtractGoalFromBranchInfo(branch);

        // Assert
        goal.Should().Be("test-branch-name");
    }

    [Fact]
    public void ExtractGoalFromBranchInfo_UnnamedBranch_UsesReasoning()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();

        // Act
        var goal = EpisodicMemoryExtensions.ExtractGoalFromBranchInfo(branch);

        // Assert
        goal.Should().Be("test reasoning prompt");
    }

    [Fact]
    public void Episode_Creation_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var episode = CreateTestEpisode();

        // Assert
        episode.Id.Should().NotBeNull();
        episode.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        episode.Goal.Should().Be("test goal");
        episode.SuccessScore.Should().BeInRange(0.0, 1.0);
        episode.LessonsLearned.Should().NotBeEmpty();
    }

    [Fact]
    public void Outcome_Creation_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var outcome = new Outcome(true, "test output", TimeSpan.FromSeconds(10), new List<string>());

        // Assert
        outcome.Success.Should().BeTrue();
        outcome.Output.Should().Be("test output");
        outcome.Duration.Should().Be(TimeSpan.FromSeconds(10));
        outcome.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ExecutionContext_ToDictionary_IncludesAllProperties()
    {
        // Arrange
        var context = new ExecutionContext("test-env", new Dictionary<string, object>
        {
            ["param1"] = "value1",
            ["param2"] = 42
        });

        // Act
        var dict = context.ToDictionary();

        // Assert
        dict.Should().ContainKey("environment");
        dict.Should().ContainKey("param1");
        dict.Should().ContainKey("param2");
        dict["environment"].Should().Be("test-env");
    }

    #region Test Data Setup

    private PipelineBranch CreateTestPipelineBranch()
    {
        var store = Mock.Of<IVectorStore>();
        var source = Mock.Of<DataSource>();
        var branch = new PipelineBranch("test-branch", store, source);

        // Add a reasoning event
        var reasoningEvent = new ReasoningStep(
            Guid.NewGuid(), 
            "reasoning", 
            new object(), 
            DateTime.UtcNow, 
            "test reasoning prompt", 
            null);

        return branch.WithEvent(reasoningEvent);
    }

    private IReadOnlyCollection<Document> CreateTestDocuments()
    {
        return new[]
        {
            new Document(
                "Test episode 1",
                new Dictionary<string, object>
                {
                    ["similarity_score"] = 0.9,
                    ["episode_data"] = "{\"goal\":\"test goal 1\",\"success_score\":0.8}"
                }),
            new Document(
                "Test episode 2", 
                new Dictionary<string, object>
                {
                    ["similarity_score"] = 0.85,
                    ["episode_data"] = "{\"goal\":\"test goal 2\",\"success_score\":0.9}"
                })
        };
    }

    private IReadOnlyCollection<Document> CreateLowSimilarityDocuments()
    {
        return new[]
        {
            new Document(
                "Low similarity episode",
                new Dictionary<string, object>
                {
                    ["similarity_score"] = 0.6,
                    ["episode_data"] = "{\"goal\":\"test goal\",\"success_score\":0.5}"
                })
        };
    }

    private List<Episode> CreateTestEpisodes()
    {
        return new List<Episode>
        {
            new Episode(
                EpisodeId.NewId(),
                DateTime.UtcNow.AddDays(-1),
                "test goal 1",
                CreateTestPipelineBranch(),
                new Outcome(true, "success", TimeSpan.FromSeconds(5), new List<string>()),
                0.8,
                new List<string> { "lesson 1" },
                new Dictionary<string, object>(),
                Array.Empty<float>()),
            new Episode(
                EpisodeId.NewId(),
                DateTime.UtcNow.AddDays(-2),
                "test goal 2", 
                CreateTestPipelineBranch(),
                new Outcome(true, "success", TimeSpan.FromSeconds(3), new List<string>()),
                0.9,
                new List<string> { "lesson 2" },
                new Dictionary<string, object>(),
                Array.Empty<float>())
        };
    }

    private List<Episode> CreateHighSuccessEpisodes()
    {
        return new List<Episode>
        {
            new Episode(
                EpisodeId.NewId(),
                DateTime.UtcNow.AddDays(-1),
                "high success goal",
                CreateTestPipelineBranch(),
                new Outcome(true, "excellent result", TimeSpan.FromSeconds(2), new List<string>()),
                0.95,
                new List<string> { "great lesson" },
                new Dictionary<string, object>(),
                Array.Empty<float>()),
            new Episode(
                EpisodeId.NewId(),
                DateTime.UtcNow.AddDays(-2),
                "high success goal 2",
                CreateTestPipelineBranch(),
                new Outcome(true, "perfect result", TimeSpan.FromSeconds(1), new List<string>()),
                0.98,
                new List<string> { "excellent lesson" },
                new Dictionary<string, object>(),
                Array.Empty<float>())
        };
    }

    private Episode CreateTestEpisode()
    {
        return new Episode(
            EpisodeId.NewId(),
            DateTime.UtcNow,
            "test goal",
            CreateTestPipelineBranch(),
            new Outcome(true, "test output", TimeSpan.FromSeconds(5), new List<string>()),
            0.85,
            new List<string> { "test lesson 1", "test lesson 2" },
            new Dictionary<string, object> { ["test"] = "data" },
            Array.Empty<float>());
    }

    #endregion
}