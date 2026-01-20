// <copyright file="MetaLearningDomainTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.MetaLearning;
using Xunit;

namespace Ouroboros.Tests.MetaLearning;

/// <summary>
/// Tests for Meta-Learning domain types.
/// Validates domain model correctness and behavior.
/// </summary>
[Trait("Category", "Unit")]
public class MetaLearningDomainTests
{
    [Fact]
    public void Example_Create_ShouldSetProperties()
    {
        // Arrange & Act
        var example = Example.Create("test input", "test output");

        // Assert
        example.Input.Should().Be("test input");
        example.Output.Should().Be("test output");
        example.Metadata.Should().BeNull();
    }

    [Fact]
    public void Example_WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var example = Example.Create("input", "output");

        // Act
        var withMetadata = example.WithMetadata("difficulty", "hard");

        // Assert
        withMetadata.Metadata.Should().ContainKey("difficulty");
        withMetadata.Metadata!["difficulty"].Should().Be("hard");
    }

    [Fact]
    public void SynthesisTask_Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var trainExamples = new List<Example>
        {
            Example.Create("input1", "output1"),
            Example.Create("input2", "output2"),
        };
        var valExamples = new List<Example>
        {
            Example.Create("val1", "valOut1"),
        };

        // Act
        var task = SynthesisTask.Create("TestTask", "TestDomain", trainExamples, valExamples);

        // Assert
        task.Name.Should().Be("TestTask");
        task.Domain.Should().Be("TestDomain");
        task.TrainingExamples.Should().HaveCount(2);
        task.ValidationExamples.Should().HaveCount(1);
        task.TotalExamples.Should().Be(3);
    }

    [Fact]
    public void SynthesisTask_SplitExamples_ShouldSplitCorrectly()
    {
        // Arrange
        var examples = Enumerable.Range(0, 10)
            .Select(i => Example.Create($"input{i}", $"output{i}"))
            .ToList();

        // Act
        var (training, validation) = SynthesisTask.SplitExamples(examples, trainingSplit: 0.7);

        // Assert
        training.Should().HaveCount(7);
        validation.Should().HaveCount(3);
    }

    [Fact]
    public void SynthesisTask_SplitExamples_WithInvalidSplit_ThrowsException()
    {
        // Arrange
        var examples = new List<Example> { Example.Create("input", "output") };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SynthesisTask.SplitExamples(examples, trainingSplit: 1.5));
    }

    [Fact]
    public void TaskDistribution_Uniform_ShouldSampleUniformly()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 5)
            .Select(i => CreateTestTask($"Task{i}"))
            .ToList();
        var distribution = TaskDistribution.Uniform(tasks);
        var random = new Random(42);

        // Act
        var samples = Enumerable.Range(0, 100)
            .Select(_ => distribution.Sample(random))
            .ToList();

        // Assert
        samples.Should().HaveCount(100);
        samples.Select(t => t.Name).Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void TaskDistribution_Weighted_ShouldSampleAccordingToWeights()
    {
        // Arrange
        var task1 = CreateTestTask("Task1");
        var task2 = CreateTestTask("Task2");
        var weightedTasks = new Dictionary<SynthesisTask, double>
        {
            [task1] = 0.9,
            [task2] = 0.1,
        };
        var distribution = TaskDistribution.Weighted(weightedTasks);
        var random = new Random(42);

        // Act
        var samples = Enumerable.Range(0, 100)
            .Select(_ => distribution.Sample(random))
            .ToList();

        // Assert
        var task1Count = samples.Count(t => t.Name == "Task1");
        var task2Count = samples.Count(t => t.Name == "Task2");
        task1Count.Should().BeGreaterThan(task2Count);
    }

    [Fact]
    public void TaskDistribution_SampleBatch_ShouldReturnCorrectCount()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 3)
            .Select(i => CreateTestTask($"Task{i}"))
            .ToList();
        var distribution = TaskDistribution.Uniform(tasks);
        var random = new Random(42);

        // Act
        var batch = distribution.SampleBatch(10, random);

        // Assert
        batch.Should().HaveCount(10);
    }

    [Fact]
    public void TaskFamily_Create_ShouldSplitTasksCorrectly()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(i => CreateTestTask($"Task{i}"))
            .ToList();

        // Act
        var family = TaskFamily.Create("TestDomain", tasks, validationSplit: 0.2);

        // Assert
        family.Domain.Should().Be("TestDomain");
        family.TrainingTasks.Should().HaveCount(8);
        family.ValidationTasks.Should().HaveCount(2);
        family.TotalTasks.Should().Be(10);
    }

    [Fact]
    public void TaskFamily_SampleTrainingBatch_ShouldReturnTasks()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 5)
            .Select(i => CreateTestTask($"Task{i}"))
            .ToList();
        var family = TaskFamily.Create("TestDomain", tasks);
        var random = new Random(42);

        // Act
        var batch = family.SampleTrainingBatch(3, random);

        // Assert
        batch.Should().HaveCount(3);
    }

    [Fact]
    public void TaskEmbedding_CosineSimilarity_WithIdenticalVectors_ReturnsOne()
    {
        // Arrange
        var vector = new float[] { 1.0f, 0.0f, 0.0f };
        var embedding1 = new TaskEmbedding(vector, new Dictionary<string, double>(), "Test");
        var embedding2 = new TaskEmbedding(vector, new Dictionary<string, double>(), "Test");

        // Act
        var similarity = embedding1.CosineSimilarity(embedding2);

        // Assert
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void TaskEmbedding_CosineSimilarity_WithOrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var vector1 = new float[] { 1.0f, 0.0f, 0.0f };
        var vector2 = new float[] { 0.0f, 1.0f, 0.0f };
        var embedding1 = new TaskEmbedding(vector1, new Dictionary<string, double>(), "Test1");
        var embedding2 = new TaskEmbedding(vector2, new Dictionary<string, double>(), "Test2");

        // Act
        var similarity = embedding1.CosineSimilarity(embedding2);

        // Assert
        similarity.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void TaskEmbedding_EuclideanDistance_WithIdenticalVectors_ReturnsZero()
    {
        // Arrange
        var vector = new float[] { 1.0f, 2.0f, 3.0f };
        var embedding1 = new TaskEmbedding(vector, new Dictionary<string, double>(), "Test");
        var embedding2 = new TaskEmbedding(vector, new Dictionary<string, double>(), "Test");

        // Act
        var distance = embedding1.EuclideanDistance(embedding2);

        // Assert
        distance.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void TaskEmbedding_FromCharacteristics_ShouldCreateEmbedding()
    {
        // Arrange
        var characteristics = new Dictionary<string, double>
        {
            ["complexity"] = 0.5,
            ["length"] = 100.0,
        };

        // Act
        var embedding = TaskEmbedding.FromCharacteristics(characteristics, "Test");

        // Assert
        embedding.Dimension.Should().Be(2);
        embedding.Characteristics.Should().BeEquivalentTo(characteristics);
    }

    [Fact]
    public void MetaLearningConfig_DefaultMAML_ShouldHaveCorrectValues()
    {
        // Act
        var config = MetaLearningConfig.DefaultMAML;

        // Assert
        config.Algorithm.Should().Be(MetaAlgorithm.MAML);
        config.InnerLearningRate.Should().BeGreaterThan(0);
        config.OuterLearningRate.Should().BeGreaterThan(0);
        config.InnerSteps.Should().BeGreaterThan(0);
        config.TaskBatchSize.Should().BeGreaterThan(0);
        config.MetaIterations.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MetaLearningConfig_DefaultReptile_ShouldHaveCorrectValues()
    {
        // Act
        var config = MetaLearningConfig.DefaultReptile;

        // Assert
        config.Algorithm.Should().Be(MetaAlgorithm.Reptile);
        config.InnerSteps.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AdaptedModel_IsSuccessful_ShouldEvaluateCorrectly()
    {
        // Arrange
        var model = new Ouroboros.Agent.MetaLearning.SimpleModel((input, _) => input, null);
        var adaptedModel = AdaptedModel.Create(model, 5, 0.85, TimeSpan.FromSeconds(1));

        // Act & Assert
        adaptedModel.IsSuccessful(0.8).Should().BeTrue();
        adaptedModel.IsSuccessful(0.9).Should().BeFalse();
    }

    [Fact]
    public void AdaptedModel_StepsPerSecond_ShouldCalculateCorrectly()
    {
        // Arrange
        var model = new Ouroboros.Agent.MetaLearning.SimpleModel((input, _) => input, null);
        var adaptedModel = AdaptedModel.Create(model, 10, 0.9, TimeSpan.FromSeconds(2));

        // Act
        var stepsPerSecond = adaptedModel.StepsPerSecond;

        // Assert
        stepsPerSecond.Should().BeApproximately(5.0, 0.1);
    }

    [Fact]
    public void MetaModel_Create_ShouldSetProperties()
    {
        // Arrange
        var innerModel = new Ouroboros.Agent.MetaLearning.SimpleModel((input, _) => input, null);
        var config = MetaLearningConfig.DefaultMAML;
        var metaParams = new Dictionary<string, object> { ["test"] = 1.0 };

        // Act
        var metaModel = MetaModel.Create(innerModel, config, metaParams);

        // Assert
        metaModel.InnerModel.Should().Be(innerModel);
        metaModel.Config.Should().Be(config);
        metaModel.MetaParameters.Should().ContainKey("test");
    }

    [Fact]
    public void MetaModel_WithMetaParameter_ShouldUpdateParameter()
    {
        // Arrange
        var innerModel = new Ouroboros.Agent.MetaLearning.SimpleModel((input, _) => input, null);
        var config = MetaLearningConfig.DefaultMAML;
        var metaParams = new Dictionary<string, object> { ["test"] = 1.0 };
        var metaModel = MetaModel.Create(innerModel, config, metaParams);

        // Act
        var updated = metaModel.WithMetaParameter("test", 2.0);

        // Assert
        updated.GetMetaParameter("test").Should().Be(2.0);
        metaModel.GetMetaParameter("test").Should().Be(1.0); // Original unchanged
    }

    private static SynthesisTask CreateTestTask(string name)
    {
        var trainExamples = new List<Example>
        {
            Example.Create($"{name}_input1", $"{name}_output1"),
            Example.Create($"{name}_input2", $"{name}_output2"),
        };
        var valExamples = new List<Example>
        {
            Example.Create($"{name}_val", $"{name}_valOut"),
        };

        return SynthesisTask.Create(name, "TestDomain", trainExamples, valExamples);
    }
}
