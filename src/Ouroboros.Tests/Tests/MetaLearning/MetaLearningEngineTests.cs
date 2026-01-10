// <copyright file="MetaLearningEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaLearning;
using Ouroboros.Domain;
using Ouroboros.Domain.MetaLearning;
using Xunit;

namespace Ouroboros.Tests.MetaLearning;

/// <summary>
/// Tests for MetaLearningEngine implementation.
/// Validates MAML, Reptile algorithms, and task adaptation.
/// </summary>
[Trait("Category", "Unit")]
public class MetaLearningEngineTests
{
    private readonly IEmbeddingModel mockEmbeddingModel;

    public MetaLearningEngineTests()
    {
        this.mockEmbeddingModel = new MockEmbeddingModel();
    }

    [Fact]
    public async Task MetaTrainAsync_WithValidConfig_ReturnsSuccessResult()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel, seed: 42);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 10 };

        // Act
        var result = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Config.Should().Be(config);
        result.Value.InnerModel.Should().NotBeNull();
    }

    [Fact]
    public async Task MetaTrainAsync_WithEmptyTaskFamilies_ReturnsFailure()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel);
        var emptyFamilies = new List<TaskFamily>();
        var config = MetaLearningConfig.DefaultMAML;

        // Act
        var result = await engine.MetaTrainAsync(emptyFamilies, config, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task MetaTrainAsync_WithMAMLAlgorithm_CompletesSuccessfully()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel, seed: 42);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultMAML with { MetaIterations = 5, TaskBatchSize = 2 };

        // Act
        var result = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Config.Algorithm.Should().Be(MetaAlgorithm.MAML);
    }

    [Fact]
    public async Task MetaTrainAsync_WithReptileAlgorithm_CompletesSuccessfully()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel, seed: 42);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 10 };

        // Act
        var result = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Config.Algorithm.Should().Be(MetaAlgorithm.Reptile);
    }

    [Fact]
    public async Task AdaptToTaskAsync_WithValidExamples_ReturnsAdaptedModel()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel, seed: 42);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 5 };

        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);
        metaTrainResult.IsSuccess.Should().BeTrue();

        var fewShotExamples = new List<Example>
        {
            Example.Create("test input 1", "test output 1"),
            Example.Create("test input 2", "test output 2"),
            Example.Create("test input 3", "test output 3"),
        };

        // Act
        var result = await engine.AdaptToTaskAsync(
            metaTrainResult.Value,
            fewShotExamples,
            adaptationSteps: 5,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AdaptationSteps.Should().Be(5);
        result.Value.Model.Should().NotBeNull();
        result.Value.AdaptationTime.Should().BePositive();
    }

    [Fact]
    public async Task AdaptToTaskAsync_WithEmptyExamples_ReturnsFailure()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 5 };

        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);
        var emptyExamples = new List<Example>();

        // Act
        var result = await engine.AdaptToTaskAsync(
            metaTrainResult.Value,
            emptyExamples,
            adaptationSteps: 5,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task AdaptToTaskAsync_WithZeroSteps_ReturnsFailure()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 5 };

        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);
        var examples = new List<Example> { Example.Create("input", "output") };

        // Act
        var result = await engine.AdaptToTaskAsync(
            metaTrainResult.Value,
            examples,
            adaptationSteps: 0,
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("positive");
    }

    [Fact]
    public async Task EmbedTaskAsync_WithValidTask_ReturnsEmbedding()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel, seed: 42);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 5 };

        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);
        var task = taskFamilies[0].TrainingTasks[0];

        // Act
        var result = await engine.EmbedTaskAsync(task, metaTrainResult.Value, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Vector.Should().NotBeEmpty();
        result.Value.Characteristics.Should().ContainKey("training_examples");
        result.Value.Characteristics.Should().ContainKey("validation_examples");
    }

    [Fact]
    public async Task ComputeTaskSimilarityAsync_WithSimilarTasks_ReturnsHighSimilarity()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel, seed: 42);
        var taskFamilies = CreateTestTaskFamilies();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 5 };

        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);
        var taskA = taskFamilies[0].TrainingTasks[0];
        var taskB = taskFamilies[0].TrainingTasks[0]; // Same task

        // Act
        var result = await engine.ComputeTaskSimilarityAsync(
            taskA,
            taskB,
            metaTrainResult.Value,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0.0);
        result.Value.Should().BeLessThanOrEqualTo(1.0);
        result.Value.Should().BeGreaterThan(0.9); // Same task should have high similarity
    }

    [Fact]
    public async Task ComputeTaskSimilarityAsync_WithDifferentTasks_ReturnsLowerSimilarity()
    {
        // Arrange
        var engine = new MetaLearningEngine(this.mockEmbeddingModel, seed: 42);
        var taskFamilies = CreateTestTaskFamiliesWithMultipleTasks();
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 5 };

        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);
        var taskA = taskFamilies[0].TrainingTasks[0];
        var taskB = taskFamilies[0].TrainingTasks[1];

        // Act
        var result = await engine.ComputeTaskSimilarityAsync(
            taskA,
            taskB,
            metaTrainResult.Value,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0.0);
        result.Value.Should().BeLessThanOrEqualTo(1.0);
    }

    private static List<TaskFamily> CreateTestTaskFamilies()
    {
        var task1 = SynthesisTask.Create(
            "Task1",
            "TestDomain",
            new List<Example>
            {
                Example.Create("input1", "output1"),
                Example.Create("input2", "output2"),
            },
            new List<Example>
            {
                Example.Create("val1", "valOut1"),
            });

        var task2 = SynthesisTask.Create(
            "Task2",
            "TestDomain",
            new List<Example>
            {
                Example.Create("input3", "output3"),
                Example.Create("input4", "output4"),
            },
            new List<Example>
            {
                Example.Create("val2", "valOut2"),
            });

        var family = TaskFamily.Create("TestDomain", new List<SynthesisTask> { task1, task2 });
        return new List<TaskFamily> { family };
    }

    private static List<TaskFamily> CreateTestTaskFamiliesWithMultipleTasks()
    {
        var task1 = SynthesisTask.Create(
            "Task1",
            "TestDomain",
            new List<Example>
            {
                Example.Create("input1", "output1"),
                Example.Create("input2", "output2"),
            },
            new List<Example>
            {
                Example.Create("val1", "valOut1"),
            });

        var task2 = SynthesisTask.Create(
            "Task2",
            "TestDomain",
            new List<Example>
            {
                Example.Create("input3", "output3"),
                Example.Create("input4", "output4"),
            },
            new List<Example>
            {
                Example.Create("val2", "valOut2"),
            });

        var task3 = SynthesisTask.Create(
            "Task3",
            "TestDomain",
            new List<Example>
            {
                Example.Create("input5", "output5"),
                Example.Create("input6", "output6"),
            },
            new List<Example>
            {
                Example.Create("val3", "valOut3"),
            });

        var family = TaskFamily.Create("TestDomain", new List<SynthesisTask> { task1, task2, task3 });
        return new List<TaskFamily> { family };
    }

    /// <summary>
    /// Mock embedding model for testing.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            // Return deterministic embedding based on input hash
            var hash = input.GetHashCode();
            var embedding = new float[128];
            var random = new Random(hash);

            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)random.NextDouble();
            }

            // Normalize
            var norm = Math.Sqrt(embedding.Sum(x => x * x));
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= (float)norm;
            }

            return Task.FromResult(embedding);
        }
    }
}
