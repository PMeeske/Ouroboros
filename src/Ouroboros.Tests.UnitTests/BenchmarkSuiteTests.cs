// <copyright file="BenchmarkSuiteTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Benchmarks;
using Xunit;

/// <summary>
/// Tests for the BenchmarkSuite implementation.
/// Validates benchmark execution, reporting, and error handling.
/// </summary>
[Trait("Category", "Unit")]
public class BenchmarkSuiteTests
{
    private readonly BenchmarkSuite suite;

    public BenchmarkSuiteTests()
    {
        this.suite = new BenchmarkSuite();
    }

    [Fact]
    public async Task RunARCBenchmarkAsync_WithValidTaskCount_ReturnsSuccessResult()
    {
        // Arrange
        var taskCount = 10;

        // Act
        var result = await this.suite.RunARCBenchmarkAsync(taskCount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkName.Should().Be("ARC-AGI-2");
        result.Value.DetailedResults.Should().HaveCount(taskCount);
        result.Value.OverallScore.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task RunARCBenchmarkAsync_WithZeroTaskCount_ReturnsFailure()
    {
        // Arrange
        var taskCount = 0;

        // Act
        var result = await this.suite.RunARCBenchmarkAsync(taskCount);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Task count must be positive");
    }

    [Fact]
    public async Task RunARCBenchmarkAsync_WithNegativeTaskCount_ReturnsFailure()
    {
        // Arrange
        var taskCount = -5;

        // Act
        var result = await this.suite.RunARCBenchmarkAsync(taskCount);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Task count must be positive");
    }

    [Fact]
    public async Task RunARCBenchmarkAsync_ReportContainsSubScores()
    {
        // Arrange
        var taskCount = 20;

        // Act
        var result = await this.suite.RunARCBenchmarkAsync(taskCount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubScores.Should().NotBeEmpty();
        result.Value.SubScores.Keys.Should().Contain(k => k.Contains("rotation") || k.Contains("scaling"));
    }

    [Fact]
    public async Task RunARCBenchmarkAsync_AllTasksHaveMetadata()
    {
        // Arrange
        var taskCount = 5;

        // Act
        var result = await this.suite.RunARCBenchmarkAsync(taskCount);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DetailedResults.Should().AllSatisfy(task =>
        {
            task.Metadata.Should().ContainKey("difficulty");
            task.Metadata.Should().ContainKey("pattern_type");
        });
    }

    [Fact]
    public async Task RunARCBenchmarkAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await this.suite.RunARCBenchmarkAsync(100, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task RunMMLUBenchmarkAsync_WithValidSubjects_ReturnsSuccessResult()
    {
        // Arrange
        var subjects = new List<string> { "mathematics", "physics", "history" };

        // Act
        var result = await this.suite.RunMMLUBenchmarkAsync(subjects);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkName.Should().Be("MMLU");
        result.Value.DetailedResults.Should().HaveCount(3);
        result.Value.SubScores.Should().ContainKeys(subjects);
    }

    [Fact]
    public async Task RunMMLUBenchmarkAsync_WithEmptySubjectList_ReturnsFailure()
    {
        // Arrange
        var subjects = new List<string>();

        // Act
        var result = await this.suite.RunMMLUBenchmarkAsync(subjects);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Subject list cannot be empty");
    }

    [Fact]
    public async Task RunMMLUBenchmarkAsync_WithNullSubjectList_ReturnsFailure()
    {
        // Act
        var result = await this.suite.RunMMLUBenchmarkAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Subject list cannot be empty");
    }

    [Fact]
    public async Task RunMMLUBenchmarkAsync_ReportContainsPerSubjectScores()
    {
        // Arrange
        var subjects = new List<string> { "mathematics", "computer_science" };

        // Act
        var result = await this.suite.RunMMLUBenchmarkAsync(subjects);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubScores["mathematics"].Should().BeInRange(0.0, 1.0);
        result.Value.SubScores["computer_science"].Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task RunContinualLearningBenchmarkAsync_WithValidSequences_ReturnsSuccessResult()
    {
        // Arrange
        var sequences = new List<TaskSequence>
        {
            new TaskSequence(
                Name: "Sequence1",
                Tasks: new List<LearningTask>
                {
                    new LearningTask("Task1", new List<TrainingExample>(), new List<TestExample>()),
                },
                MeasureRetention: true),
        };

        // Act
        var result = await this.suite.RunContinualLearningBenchmarkAsync(sequences);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkName.Should().Be("Continual Learning");
        result.Value.DetailedResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunContinualLearningBenchmarkAsync_WithEmptySequences_ReturnsFailure()
    {
        // Arrange
        var sequences = new List<TaskSequence>();

        // Act
        var result = await this.suite.RunContinualLearningBenchmarkAsync(sequences);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Task sequences cannot be empty");
    }

    [Fact]
    public async Task RunContinualLearningBenchmarkAsync_ReportContainsRetentionMetrics()
    {
        // Arrange
        var sequences = new List<TaskSequence>
        {
            new TaskSequence(
                Name: "TestSequence",
                Tasks: new List<LearningTask>
                {
                    new LearningTask("Task1", new List<TrainingExample>(), new List<TestExample>()),
                },
                MeasureRetention: true),
        };

        // Act
        var result = await this.suite.RunContinualLearningBenchmarkAsync(sequences);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DetailedResults[0].Metadata.Should().ContainKey("retention_rate");
        result.Value.DetailedResults[0].Metadata.Should().ContainKey("initial_accuracy");
        result.Value.DetailedResults[0].Metadata.Should().ContainKey("final_accuracy");
    }

    [Fact]
    public async Task RunCognitiveBenchmarkAsync_WithReasoningDimension_ReturnsSuccessResult()
    {
        // Act
        var result = await this.suite.RunCognitiveBenchmarkAsync(CognitiveDimension.Reasoning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkName.Should().Contain("Reasoning");
        result.Value.DetailedResults.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunCognitiveBenchmarkAsync_WithPlanningDimension_ReturnsSuccessResult()
    {
        // Act
        var result = await this.suite.RunCognitiveBenchmarkAsync(CognitiveDimension.Planning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkName.Should().Contain("Planning");
    }

    [Fact]
    public async Task RunCognitiveBenchmarkAsync_WithMemoryDimension_ReturnsSuccessResult()
    {
        // Act
        var result = await this.suite.RunCognitiveBenchmarkAsync(CognitiveDimension.Memory);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkName.Should().Contain("Memory");
    }

    [Fact]
    public async Task RunCognitiveBenchmarkAsync_ReportContainsSubCategories()
    {
        // Act
        var result = await this.suite.RunCognitiveBenchmarkAsync(CognitiveDimension.Reasoning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubScores.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunFullEvaluationAsync_ReturnsComprehensiveReport()
    {
        // Act
        var result = await this.suite.RunFullEvaluationAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkResults.Should().NotBeEmpty();
        result.Value.OverallScore.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task RunFullEvaluationAsync_ReportContainsMultipleBenchmarks()
    {
        // Act
        var result = await this.suite.RunFullEvaluationAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BenchmarkResults.Should().ContainKey("ARC-AGI-2");
        result.Value.BenchmarkResults.Should().ContainKey("MMLU");
    }

    [Fact]
    public async Task RunFullEvaluationAsync_ReportContainsStrengthsAndWeaknesses()
    {
        // Act
        var result = await this.suite.RunFullEvaluationAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Strengths.Should().NotBeEmpty();
        result.Value.Weaknesses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunFullEvaluationAsync_ReportContainsRecommendations()
    {
        // Act
        var result = await this.suite.RunFullEvaluationAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunFullEvaluationAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await this.suite.RunFullEvaluationAsync(cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task BenchmarkReport_ContainsTimestamp()
    {
        // Act
        var result = await this.suite.RunARCBenchmarkAsync(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task BenchmarkReport_ContainsDuration()
    {
        // Act
        var result = await this.suite.RunARCBenchmarkAsync(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TaskResult_ContainsRequiredFields()
    {
        // Act
        var result = await this.suite.RunARCBenchmarkAsync(1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var taskResult = result.Value.DetailedResults[0];
        taskResult.TaskId.Should().NotBeNullOrEmpty();
        taskResult.TaskName.Should().NotBeNullOrEmpty();
        taskResult.Score.Should().BeInRange(0.0, 1.0);
        taskResult.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        taskResult.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void TestExample_CanBeCreatedWithValidator()
    {
        // Arrange & Act
        var example = new TestExample(
            Input: "2 + 2",
            ExpectedOutput: "4",
            Validator: (actual, expected) => actual == expected);

        // Assert
        example.Input.Should().Be("2 + 2");
        example.ExpectedOutput.Should().Be("4");
        example.Validator.Should().NotBeNull();
        example.Validator("4", "4").Should().BeTrue();
    }

    [Fact]
    public void TrainingExample_CanBeCreated()
    {
        // Arrange & Act
        var example = new TrainingExample(
            Input: "Example input",
            ExpectedOutput: "Example output");

        // Assert
        example.Input.Should().Be("Example input");
        example.ExpectedOutput.Should().Be("Example output");
    }

    [Fact]
    public void LearningTask_CanBeCreatedWithData()
    {
        // Arrange
        var trainingData = new List<TrainingExample>
        {
            new TrainingExample("input1", "output1"),
        };
        var testData = new List<TestExample>
        {
            new TestExample("input2", "output2", (a, e) => a == e),
        };

        // Act
        var task = new LearningTask("TestTask", trainingData, testData);

        // Assert
        task.Name.Should().Be("TestTask");
        task.TrainingData.Should().HaveCount(1);
        task.TestData.Should().HaveCount(1);
    }

    [Fact]
    public void TaskSequence_CanBeCreatedWithTasks()
    {
        // Arrange
        var tasks = new List<LearningTask>
        {
            new LearningTask("Task1", new List<TrainingExample>(), new List<TestExample>()),
        };

        // Act
        var sequence = new TaskSequence("Sequence1", tasks, true);

        // Assert
        sequence.Name.Should().Be("Sequence1");
        sequence.Tasks.Should().HaveCount(1);
        sequence.MeasureRetention.Should().BeTrue();
    }

    [Fact]
    public void CognitiveDimension_HasAllExpectedValues()
    {
        // Arrange & Act
        var dimensions = Enum.GetValues<CognitiveDimension>();

        // Assert
        dimensions.Should().Contain(CognitiveDimension.Reasoning);
        dimensions.Should().Contain(CognitiveDimension.Planning);
        dimensions.Should().Contain(CognitiveDimension.Learning);
        dimensions.Should().Contain(CognitiveDimension.Memory);
        dimensions.Should().Contain(CognitiveDimension.Generalization);
        dimensions.Should().Contain(CognitiveDimension.Creativity);
        dimensions.Should().Contain(CognitiveDimension.SocialIntelligence);
    }
}
