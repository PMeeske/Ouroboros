// <copyright file="MetaLearningEngineTestsTest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.MetaLearning;
using Xunit;

namespace Ouroboros.Tests.UnitTests.MetaLearning;

/// <summary>
/// Tests for SynthesisTask implementation.
/// Validates task creation, properties, and behavior.
/// </summary>
[Trait("Category", "Unit")]
public class SynthesisTaskTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsSynthesisTask()
    {
        // Arrange
        var name = "TestTask";
        var domain = "TestDomain";
        var trainingExamples = new List<Example>
        {
            Example.Create("input1", "output1"),
            Example.Create("input2", "output2"),
        };
        var validationExamples = new List<Example>
        {
            Example.Create("val1", "valOut1"),
        };

        // Act
        var task = SynthesisTask.Create(name, domain, trainingExamples, validationExamples);

        // Assert
        task.Should().NotBeNull();
        task.Name.Should().Be(name);
        task.Domain.Should().Be(domain);
        task.TrainingExamples.Should().BeEquivalentTo(trainingExamples);
        task.ValidationExamples.Should().BeEquivalentTo(validationExamples);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var domain = "TestDomain";
        var trainingExamples = new List<Example> { Example.Create("input", "output") };
        var validationExamples = new List<Example> { Example.Create("val", "valOut") };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SynthesisTask.Create(name, domain, trainingExamples, validationExamples));
    }

    [Fact]
    public void Create_WithNullTrainingExamples_ThrowsArgumentNullException()
    {
        // Arrange
        var name = "TestTask";
        var domain = "TestDomain";
        List<Example> trainingExamples = null!;
        var validationExamples = new List<Example> { Example.Create("val", "valOut") };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SynthesisTask.Create(name, domain, trainingExamples, validationExamples));
    }

    [Fact]
    public void Create_WithEmptyTrainingExamples_ThrowsArgumentException()
    {
        // Arrange
        var name = "TestTask";
        var domain = "TestDomain";
        var trainingExamples = new List<Example>();
        var validationExamples = new List<Example> { Example.Create("val", "valOut") };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SynthesisTask.Create(name, domain, trainingExamples, validationExamples));
    }

    [Fact]
    public void Create_WithNullValidationExamples_ThrowsArgumentNullException()
    {
        // Arrange
        var name = "TestTask";
        var domain = "TestDomain";
        var trainingExamples = new List<Example> { Example.Create("input", "output") };
        List<Example> validationExamples = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SynthesisTask.Create(name, domain, trainingExamples, validationExamples));
    }

    [Fact]
    public void Create_WithEmptyValidationExamples_ThrowsArgumentException()
    {
        // Arrange
        var name = "TestTask";
        var domain = "TestDomain";
        var trainingExamples = new List<Example> { Example.Create("input", "output") };
        var validationExamples = new List<Example>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SynthesisTask.Create(name, domain, trainingExamples, validationExamples));
    }

    [Fact]
    public void SynthesisTask_IsImmutable_AfterCreation()
    {
        // Arrange
        var trainingExamples = new List<Example> { Example.Create("input", "output") };
        var validationExamples = new List<Example> { Example.Create("val", "valOut") };
        var task = SynthesisTask.Create("TestTask", "TestDomain", trainingExamples, validationExamples);

        // Act: Try to modify the lists (this should not affect the task if immutable)
        trainingExamples.Add(Example.Create("new", "new"));
        validationExamples.Add(Example.Create("newVal", "newValOut"));

        // Assert: Task should retain original examples
        task.TrainingExamples.Should().HaveCount(1);
        task.ValidationExamples.Should().HaveCount(1);
    }
}
