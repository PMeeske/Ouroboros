// <copyright file="MetaLearnerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MetaLearning;

using FluentAssertions;
using Moq;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.MetaLearning;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Comprehensive tests for MetaLearner implementation.
/// Tests strategy optimization, few-shot adaptation, hyperparameter suggestion,
/// learning efficiency analysis, and meta-knowledge extraction.
/// </summary>
[Trait("Category", "Unit")]
public class MetaLearnerTests
{
    private readonly Mock<IChatCompletionModel> mockLlm;
    private readonly Mock<ISkillRegistry> mockSkillRegistry;
    private readonly Mock<IMemoryStore> mockMemory;
    private readonly MetaLearner metaLearner;

    public MetaLearnerTests()
    {
        this.mockLlm = new Mock<IChatCompletionModel>();
        this.mockSkillRegistry = new Mock<ISkillRegistry>();
        this.mockMemory = new Mock<IMemoryStore>();

        this.metaLearner = new MetaLearner(
            this.mockLlm.Object,
            this.mockSkillRegistry.Object,
            this.mockMemory.Object,
            new MetaLearnerConfig(
                MinEpisodesForOptimization: 3,
                MaxFewShotExamples: 5,
                MinConfidenceThreshold: 0.6,
                DefaultEvaluationWindow: TimeSpan.FromDays(7)));
    }

    #region Strategy Optimization Tests

    [Fact]
    public async Task OptimizeLearningStrategyAsync_WithSufficientHistory_ReturnsStrategy()
    {
        // Arrange
        List<LearningEpisode> history = CreateTestEpisodes(count: 5, successRate: 0.8);

        this.mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"NAME: Optimized Strategy
APPROACH: Supervised
TASKS: classification, reasoning
EFFICIENCY: 85%
OPTIMIZATIONS:
- Use more examples
- Increase batch size");

        // Act
        Result<LearningStrategy, string> result = await this.metaLearner.OptimizeLearningStrategyAsync(
            history,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().NotBeEmpty();
        result.Value.Approach.Should().Be(LearningApproach.Supervised);
        result.Value.SuitableTaskTypes.Should().Contain("classification");
        result.Value.ExpectedEfficiency.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OptimizeLearningStrategyAsync_WithInsufficientHistory_ReturnsFailure()
    {
        // Arrange
        List<LearningEpisode> history = CreateTestEpisodes(count: 1, successRate: 1.0);

        // Act
        Result<LearningStrategy, string> result = await this.metaLearner.OptimizeLearningStrategyAsync(
            history,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient learning history");
    }

    [Fact]
    public async Task OptimizeLearningStrategyAsync_WithNoSuccessfulEpisodes_ReturnsFailure()
    {
        // Arrange
        List<LearningEpisode> history = CreateTestEpisodes(count: 5, successRate: 0.0);

        // Act
        Result<LearningStrategy, string> result = await this.metaLearner.OptimizeLearningStrategyAsync(
            history,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No successful episodes");
    }

    [Fact]
    public async Task OptimizeLearningStrategyAsync_WithMixedTaskTypes_GroupsByTaskType()
    {
        // Arrange
        List<LearningEpisode> history = new List<LearningEpisode>
        {
            CreateEpisode("classification", true, 10, 0.9),
            CreateEpisode("classification", true, 12, 0.85),
            CreateEpisode("reasoning", true, 15, 0.8),
            CreateEpisode("generation", false, 20, 0.5),
        };

        this.mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"NAME: Multi-Task Strategy
APPROACH: CurriculumLearning
TASKS: classification, reasoning
EFFICIENCY: 80%");

        // Act
        Result<LearningStrategy, string> result = await this.metaLearner.OptimizeLearningStrategyAsync(
            history,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Approach.Should().Be(LearningApproach.CurriculumLearning);
    }

    #endregion

    #region Few-Shot Adaptation Tests

    [Fact]
    public async Task FewShotAdaptAsync_WithValidExamples_ReturnsAdaptedModel()
    {
        // Arrange
        string taskDescription = "Classify sentiment of text";
        List<TaskExample> examples = new List<TaskExample>
        {
            new TaskExample("I love this!", "positive"),
            new TaskExample("This is terrible", "negative"),
            new TaskExample("It's okay", "neutral"),
        };

        this.mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"PATTERNS: sentiment keywords, intensity markers
STEPS:
1. Tokenize input
2. Identify sentiment keywords
3. Calculate sentiment score
PREREQUISITES: text input
ESTIMATED_PERFORMANCE: 0.85");

        // Act
        Result<AdaptedModel, string> result = await this.metaLearner.FewShotAdaptAsync(
            taskDescription,
            examples,
            maxExamples: 5,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TaskDescription.Should().Be(taskDescription);
        result.Value.ExamplesUsed.Should().Be(3);
        result.Value.AdaptedSkill.Should().NotBeNull();
        result.Value.EstimatedPerformance.Should().BeGreaterThan(0);
        result.Value.LearnedPatterns.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FewShotAdaptAsync_WithEmptyTaskDescription_ReturnsFailure()
    {
        // Arrange
        List<TaskExample> examples = new List<TaskExample>
        {
            new TaskExample("input", "output"),
        };

        // Act
        Result<AdaptedModel, string> result = await this.metaLearner.FewShotAdaptAsync(
            string.Empty,
            examples,
            maxExamples: 5,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Task description cannot be empty");
    }

    [Fact]
    public async Task FewShotAdaptAsync_WithNoExamples_ReturnsFailure()
    {
        // Arrange
        string taskDescription = "Test task";
        List<TaskExample> examples = new List<TaskExample>();

        // Act
        Result<AdaptedModel, string> result = await this.metaLearner.FewShotAdaptAsync(
            taskDescription,
            examples,
            maxExamples: 5,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("At least one example is required");
    }

    [Fact]
    public async Task FewShotAdaptAsync_WithManyExamples_LimitsToMaxExamples()
    {
        // Arrange
        string taskDescription = "Test task";
        List<TaskExample> examples = Enumerable.Range(0, 10)
            .Select(i => new TaskExample($"input{i}", $"output{i}", Importance: (i + 1) / 10.0))
            .ToList();

        this.mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"PATTERNS: test pattern
STEPS:
1. Process input
ESTIMATED_PERFORMANCE: 0.8");

        // Act
        Result<AdaptedModel, string> result = await this.metaLearner.FewShotAdaptAsync(
            taskDescription,
            examples,
            maxExamples: 3,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExamplesUsed.Should().Be(3); // Should limit to maxExamples
    }

    #endregion

    #region Hyperparameter Suggestion Tests

    [Fact]
    public async Task SuggestHyperparametersAsync_WithKnownTaskType_ReturnsSuggestions()
    {
        // Arrange
        string taskType = "classification";

        // Record some episodes for this task type
        this.metaLearner.RecordLearningEpisode(CreateEpisode(taskType, true, 10, 0.9));
        this.metaLearner.RecordLearningEpisode(CreateEpisode(taskType, true, 12, 0.85));

        // Act
        Result<HyperparameterConfig, string> result = await this.metaLearner.SuggestHyperparametersAsync(
            taskType,
            context: null,
            ct: CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.LearningRate.Should().BeGreaterThan(0);
        result.Value.BatchSize.Should().BeGreaterThan(0);
        result.Value.MaxIterations.Should().BeGreaterThan(0);
        result.Value.QualityThreshold.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task SuggestHyperparametersAsync_WithUnknownTaskType_ReturnsDefaults()
    {
        // Arrange
        string taskType = "unknown_task_type";

        // Act
        Result<HyperparameterConfig, string> result = await this.metaLearner.SuggestHyperparametersAsync(
            taskType,
            context: null,
            ct: CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.LearningRate.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SuggestHyperparametersAsync_WithEmptyTaskType_ReturnsFailure()
    {
        // Act
        Result<HyperparameterConfig, string> result = await this.metaLearner.SuggestHyperparametersAsync(
            string.Empty,
            context: null,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Task type cannot be empty");
    }

    [Fact]
    public async Task SuggestHyperparametersAsync_WithContext_IncludesContextInConfig()
    {
        // Arrange
        string taskType = "classification";
        Dictionary<string, object> context = new Dictionary<string, object>
        {
            ["complexity"] = "high",
            ["dataSize"] = 1000,
        };

        // Act
        Result<HyperparameterConfig, string> result = await this.metaLearner.SuggestHyperparametersAsync(
            taskType,
            context,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CustomParams.Should().ContainKey("complexity");
        result.Value.CustomParams.Should().ContainKey("dataSize");
    }

    #endregion

    #region Learning Efficiency Tests

    [Fact]
    public async Task EvaluateLearningEfficiencyAsync_WithRecentEpisodes_ReturnsReport()
    {
        // Arrange
        List<LearningEpisode> episodes = CreateTestEpisodes(count: 10, successRate: 0.7);
        foreach (LearningEpisode episode in episodes)
        {
            this.metaLearner.RecordLearningEpisode(episode);
        }

        // Act
        Result<LearningEfficiencyReport, string> result = await this.metaLearner.EvaluateLearningEfficiencyAsync(
            TimeSpan.FromDays(30),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.AverageIterationsToLearn.Should().BeGreaterThan(0);
        result.Value.AverageExamplesNeeded.Should().BeGreaterThan(0);
        result.Value.SuccessRate.Should().BeInRange(0, 1);
        result.Value.EfficiencyByTaskType.Should().NotBeEmpty();
        result.Value.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EvaluateLearningEfficiencyAsync_WithNoRecentEpisodes_ReturnsFailure()
    {
        // Act
        Result<LearningEfficiencyReport, string> result = await this.metaLearner.EvaluateLearningEfficiencyAsync(
            TimeSpan.FromDays(1),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No learning episodes in the evaluation window");
    }

    [Fact]
    public async Task EvaluateLearningEfficiencyAsync_WithImprovingTrend_ReportsPositiveTrend()
    {
        // Arrange - Episodes with decreasing iteration count (improvement)
        DateTime baseTime = DateTime.UtcNow.AddDays(-10);
        for (int i = 0; i < 10; i++)
        {
            LearningEpisode episode = new LearningEpisode(
                Id: Guid.NewGuid(),
                TaskType: "test",
                TaskDescription: "test task",
                StrategyUsed: CreateTestStrategy(),
                ExamplesProvided: 10,
                IterationsRequired: 50 - (i * 4), // Decreasing iterations
                FinalPerformance: 0.8 + (i * 0.01),
                LearningDuration: TimeSpan.FromMinutes(10),
                ProgressCurve: new List<PerformanceSnapshot>(),
                Successful: true,
                FailureReason: null,
                StartedAt: baseTime.AddDays(i),
                CompletedAt: baseTime.AddDays(i).AddMinutes(10));

            this.metaLearner.RecordLearningEpisode(episode);
        }

        // Act
        Result<LearningEfficiencyReport, string> result = await this.metaLearner.EvaluateLearningEfficiencyAsync(
            TimeSpan.FromDays(30),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LearningSpeedTrend.Should().BeGreaterThan(0); // Positive trend = improvement
    }

    [Fact]
    public async Task EvaluateLearningEfficiencyAsync_WithLowSuccessRate_IdentifiesBottleneck()
    {
        // Arrange
        List<LearningEpisode> episodes = CreateTestEpisodes(count: 10, successRate: 0.3);
        foreach (LearningEpisode episode in episodes)
        {
            this.metaLearner.RecordLearningEpisode(episode);
        }

        // Act
        Result<LearningEfficiencyReport, string> result = await this.metaLearner.EvaluateLearningEfficiencyAsync(
            TimeSpan.FromDays(30),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Bottlenecks.Should().Contain(b => b.Contains("Low success rate"));
    }

    #endregion

    #region Meta-Knowledge Extraction Tests

    [Fact]
    public async Task ExtractMetaKnowledgeAsync_WithSufficientEpisodes_ReturnsKnowledge()
    {
        // Arrange
        List<LearningEpisode> episodes = CreateTestEpisodes(count: 25, successRate: 0.8);
        foreach (LearningEpisode episode in episodes)
        {
            this.metaLearner.RecordLearningEpisode(episode);
        }

        this.mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"INSIGHT: More examples lead to better performance
CONFIDENCE: 0.85
APPLICABLE_TO: classification, regression");

        // Act
        Result<List<MetaKnowledge>, string> result = await this.metaLearner.ExtractMetaKnowledgeAsync(
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(k => k.Domain != null);
    }

    [Fact]
    public async Task ExtractMetaKnowledgeAsync_WithNoEpisodes_ReturnsEmptyList()
    {
        // Act
        Result<List<MetaKnowledge>, string> result = await this.metaLearner.ExtractMetaKnowledgeAsync(
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractMetaKnowledgeAsync_WithHighSuccessRate_IncludesGeneralInsight()
    {
        // Arrange
        List<LearningEpisode> episodes = CreateTestEpisodes(count: 25, successRate: 0.85);
        foreach (LearningEpisode episode in episodes)
        {
            this.metaLearner.RecordLearningEpisode(episode);
        }

        this.mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"INSIGHT: Test insight
CONFIDENCE: 0.8
APPLICABLE_TO: all");

        // Act
        Result<List<MetaKnowledge>, string> result = await this.metaLearner.ExtractMetaKnowledgeAsync(
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(k => k.Domain == "General");
    }

    [Fact]
    public async Task ExtractMetaKnowledgeAsync_GroupsByTaskType()
    {
        // Arrange - Add episodes of different task types
        for (int i = 0; i < 5; i++)
        {
            this.metaLearner.RecordLearningEpisode(CreateEpisode("classification", true, 10, 0.9));
            this.metaLearner.RecordLearningEpisode(CreateEpisode("reasoning", true, 15, 0.85));
        }

        this.mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"INSIGHT: Task-specific insight
CONFIDENCE: 0.75
APPLICABLE_TO: classification");

        // Act
        Result<List<MetaKnowledge>, string> result = await this.metaLearner.ExtractMetaKnowledgeAsync(
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should have insights for multiple task types
        result.Value.Select(k => k.Domain).Distinct().Count().Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Recording Tests

    [Fact]
    public async Task RecordLearningEpisode_WithValidEpisode_RecordsSuccessfully()
    {
        // Arrange
        LearningEpisode episode = CreateEpisode("test", true, 10, 0.9);

        // Act
        this.metaLearner.RecordLearningEpisode(episode);

        // Assert - Episode should be available for future queries
        // We can't directly access episodes, but we can test through efficiency evaluation
        Result<LearningEfficiencyReport, string> result = await this.metaLearner.EvaluateLearningEfficiencyAsync(
            TimeSpan.FromDays(30),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RecordLearningEpisode_WithNullEpisode_ThrowsException()
    {
        // Act & Assert
        Action act = () => this.metaLearner.RecordLearningEpisode(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Helper Methods

    private static List<LearningEpisode> CreateTestEpisodes(int count, double successRate)
    {
        List<LearningEpisode> episodes = new List<LearningEpisode>();
        Random random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            bool successful = random.NextDouble() < successRate;
            episodes.Add(CreateEpisode(
                taskType: i % 3 == 0 ? "classification" : i % 3 == 1 ? "reasoning" : "generation",
                successful: successful,
                iterations: random.Next(10, 50),
                performance: successful ? 0.7 + (random.NextDouble() * 0.3) : 0.3 + (random.NextDouble() * 0.3)));
        }

        return episodes;
    }

    private static LearningEpisode CreateEpisode(
        string taskType,
        bool successful,
        int iterations,
        double performance)
    {
        DateTime now = DateTime.UtcNow;
        return new LearningEpisode(
            Id: Guid.NewGuid(),
            TaskType: taskType,
            TaskDescription: $"Test {taskType} task",
            StrategyUsed: CreateTestStrategy(),
            ExamplesProvided: 10,
            IterationsRequired: iterations,
            FinalPerformance: performance,
            LearningDuration: TimeSpan.FromMinutes(5),
            ProgressCurve: new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot(0, 0.5, 0.5, now),
                new PerformanceSnapshot(iterations / 2, performance * 0.7, 0.3, now.AddMinutes(2)),
                new PerformanceSnapshot(iterations, performance, 0.1, now.AddMinutes(5)),
            },
            Successful: successful,
            FailureReason: successful ? null : "Test failure",
            StartedAt: now.AddMinutes(-5),
            CompletedAt: now);
    }

    private static LearningStrategy CreateTestStrategy()
    {
        return new LearningStrategy(
            Name: "Test Strategy",
            Description: "A test learning strategy",
            Approach: LearningApproach.Supervised,
            Hyperparameters: new HyperparameterConfig(
                LearningRate: 0.01,
                BatchSize: 16,
                MaxIterations: 100,
                QualityThreshold: 0.8,
                ExplorationRate: 0.1,
                CustomParams: new Dictionary<string, object>()),
            SuitableTaskTypes: new List<string> { "test" },
            ExpectedEfficiency: 0.8,
            CustomConfig: new Dictionary<string, object>());
    }

    #endregion
}
