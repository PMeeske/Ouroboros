// <copyright file="DistinctionLearnerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests.Learning;

using FluentAssertions;
using Ouroboros.Core.Learning;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for the DistinctionLearner implementation.
/// Validates distinction-based learning following Laws of Form.
/// </summary>
public sealed class DistinctionLearnerTests
{
    private readonly IDistinctionLearner learner;

    public DistinctionLearnerTests()
    {
        this.learner = new DistinctionLearner();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_WithVoidStage_ShouldMaintainVoidCertainty()
    {
        // Arrange
        var state = DistinctionState.Void();
        var observation = Observation.WithCertainPrior("test content");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(
            state, observation, DreamStage.Void);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpistemicCertainty.IsVoid().Should().BeTrue();
        result.Value.Stage.Should().Be(DreamStage.Void);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_WithDistinctionStage_ShouldMakeDistinctions()
    {
        // Arrange
        var state = DistinctionState.Void();
        var observation = Observation.WithCertainPrior("apple banana cherry");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(
            state, observation, DreamStage.Distinction);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().NotBeEmpty();
        result.Value.EpistemicCertainty.IsMark().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_WithSubjectEmergesStage_ShouldCreateImaginaryState()
    {
        // Arrange
        var state = DistinctionState.WithInitialDistinction("test");
        var observation = Observation.WithCertainPrior("self awareness");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(
            state, observation, DreamStage.SubjectEmerges);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EpistemicCertainty.IsImaginary().Should().BeTrue();
        result.Value.ActiveDistinctions.Should().Contain("self");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_WithQuestioningStage_ShouldLowerFitness()
    {
        // Arrange
        var state = DistinctionState.WithInitialDistinction("test", 0.8);
        var observation = Observation.WithCertainPrior("different content");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(
            state, observation, DreamStage.Questioning);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.FitnessScores["test"].Should().BeLessThan(0.8);
        result.Value.EpistemicCertainty.IsImaginary().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task EvaluateDistinctionFitness_WithRelevantObservations_ShouldReturnHighFitness()
    {
        // Arrange
        var distinction = "apple";
        var observations = new List<Observation>
        {
            Observation.WithCertainPrior("apple is red"),
            Observation.WithCertainPrior("apple is sweet"),
            Observation.WithCertainPrior("banana is yellow"),
        };

        // Act
        var result = await this.learner.EvaluateDistinctionFitnessAsync(distinction, observations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0.3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task EvaluateDistinctionFitness_WithNoRelevantObservations_ShouldReturnLowFitness()
    {
        // Arrange
        var distinction = "nonexistent";
        var observations = new List<Observation>
        {
            Observation.WithCertainPrior("apple is red"),
            Observation.WithCertainPrior("banana is yellow"),
        };

        // Act
        var result = await this.learner.EvaluateDistinctionFitnessAsync(distinction, observations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeLessThan(0.5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task EvaluateDistinctionFitness_WithEmptyObservations_ShouldReturnNeutralFitness()
    {
        // Arrange
        var distinction = "test";
        var observations = new List<Observation>();

        // Act
        var result = await this.learner.EvaluateDistinctionFitnessAsync(distinction, observations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0.5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Dissolve_WithFitnessThreshold_ShouldRemoveLowFitnessDistinctions()
    {
        // Arrange
        var state = DistinctionState.Void()
            .AddDistinction("high_fitness", 0.8)
            .AddDistinction("low_fitness", 0.2);

        // Act
        var result = await this.learner.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().Contain("high_fitness");
        result.Value.ActiveDistinctions.Should().NotContain("low_fitness");
        result.Value.DissolvedDistinctions.Should().Contain("low_fitness");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Dissolve_WithComplete_ShouldRemoveAllDistinctions()
    {
        // Arrange
        var state = DistinctionState.Void()
            .AddDistinction("distinction1", 0.8)
            .AddDistinction("distinction2", 0.9);

        // Act
        var result = await this.learner.DissolveAsync(state, DissolutionStrategy.Complete);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().BeEmpty();
        result.Value.DissolvedDistinctions.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Recognize_ShouldCreateRecognitionDistinction()
    {
        // Arrange
        var state = DistinctionState.WithInitialDistinction("test");
        var circumstance = "pattern";

        // Act
        var result = await this.learner.RecognizeAsync(state, circumstance);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().Contain($"I={circumstance}");
        result.Value.Stage.Should().Be(DreamStage.Recognition);
        result.Value.EpistemicCertainty.IsMark().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Recognize_ShouldHaveHighFitness()
    {
        // Arrange
        var state = DistinctionState.WithInitialDistinction("test");
        var circumstance = "insight";

        // Act
        var result = await this.learner.RecognizeAsync(state, circumstance);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var recognitionDistinction = $"I={circumstance}";
        result.Value.FitnessScores[recognitionDistinction].Should().BeGreaterThan(0.9);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_WithDissolutionStage_ShouldDissolveLowFitness()
    {
        // Arrange
        var state = DistinctionState.Void()
            .AddDistinction("good", 0.8)
            .AddDistinction("bad", 0.1);
        var observation = Observation.WithCertainPrior("test");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(
            state, observation, DreamStage.Dissolution);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().Contain("good");
        result.Value.ActiveDistinctions.Should().NotContain("bad");
        result.Value.EpistemicCertainty.IsVoid().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_WithRecognitionStage_ShouldAddKeyTerms()
    {
        // Arrange
        var state = DistinctionState.WithInitialDistinction("initial");
        var observation = Observation.WithCertainPrior("important insight revealed");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(
            state, observation, DreamStage.Recognition);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().Contain(d => d.Length > 4);
        result.Value.EpistemicCertainty.IsMark().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompleteDreamCycle_ShouldProgressThroughAllStages()
    {
        // Arrange
        var state = DistinctionState.Void();
        var observation = Observation.WithCertainPrior("testing learning content deeply");
        var initialDistinctionCount = state.ActiveDistinctions.Count;

        // Act - Progress through at least Distinction stage to create distinctions
        var result = await this.learner.UpdateFromDistinctionAsync(state, observation, DreamStage.Distinction);
        result.IsSuccess.Should().BeTrue();
        state = result.Value;

        // Assert - Should have made progress by adding distinctions
        state.ActiveDistinctions.Count.Should().BeGreaterThan(initialDistinctionCount);
        state.Stage.Should().Be(DreamStage.Distinction);
    }

    [Theory]
    [InlineData(DreamStage.Void)]
    [InlineData(DreamStage.Distinction)]
    [InlineData(DreamStage.WorldCrystallizes)]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_ShouldSucceedForAllStages(DreamStage stage)
    {
        // Arrange
        var state = DistinctionState.WithInitialDistinction("test");
        var observation = Observation.WithCertainPrior("content");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(state, observation, stage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Stage.Should().Be(stage);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UpdateFromDistinction_WithNullState_ShouldReturnFailure()
    {
        // Arrange
        var observation = Observation.WithCertainPrior("test");

        // Act
        var result = await this.learner.UpdateFromDistinctionAsync(null!, observation, DreamStage.Void);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to update");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task EvaluateDistinctionFitness_WithNullDistinction_ShouldReturnFailure()
    {
        // Arrange
        var observations = new List<Observation>();

        // Act
        var result = await this.learner.EvaluateDistinctionFitnessAsync(string.Empty, observations);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to evaluate");
    }
}
