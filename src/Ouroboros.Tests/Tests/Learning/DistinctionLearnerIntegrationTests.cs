// <copyright file="DistinctionLearnerIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Domain.Learning;
using Ouroboros.Tests.Mocks;
using Xunit;

/// <summary>
/// Integration tests for DistinctionLearner.
/// Tests the full learning cycle through dream stages.
/// </summary>
[Trait("Category", "Integration")]
public class DistinctionLearnerIntegrationTests
{
    private readonly DistinctionLearner _learner;
    private readonly InMemoryDistinctionStorage _storage;

    public DistinctionLearnerIntegrationTests()
    {
        var peft = new MockPeftIntegration(NullLogger<MockPeftIntegration>.Instance);
        _storage = new InMemoryDistinctionStorage(NullLogger<InMemoryDistinctionStorage>.Instance);
        var embeddingModel = new MockEmbeddingModel();
        var adapter = new DistinctionPeftAdapter(
            peft,
            _storage,
            embeddingModel,
            "test-model",
            NullLogger<DistinctionPeftAdapter>.Instance);
        _learner = new DistinctionLearner(
            adapter,
            _storage,
            NullLogger<DistinctionLearner>.Instance);
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_WithValidObservation_UpdatesState()
    {
        // Arrange
        var state = DistinctionState.Initial();
        var observation = Observation.Now("First distinction");
        var stage = DreamStage.Distinction;

        // Act
        var result = await _learner.UpdateFromDistinctionAsync(state, observation, stage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveDistinctions.Should().Contain(observation.Content);
        result.Value.CurrentStage.Should().Be(stage);
        result.Value.CycleCount.Should().BeGreaterThan(state.CycleCount);
    }

    [Fact]
    public async Task UpdateFromDistinctionAsync_MultipleTimes_AccumulatesDistinctions()
    {
        // Arrange
        var state = DistinctionState.Initial();

        // Act
        var result1 = await _learner.UpdateFromDistinctionAsync(
            state,
            Observation.Now("Distinction 1"),
            DreamStage.Distinction);

        var result2 = await _learner.UpdateFromDistinctionAsync(
            result1.Value,
            Observation.Now("Distinction 2"),
            DreamStage.SubjectEmerges);

        var result3 = await _learner.UpdateFromDistinctionAsync(
            result2.Value,
            Observation.Now("Distinction 3"),
            DreamStage.WorldCrystallizes);

        // Assert
        result3.IsSuccess.Should().BeTrue();
        result3.Value.ActiveDistinctions.Should().HaveCount(3);
        result3.Value.DistinctionFitness.Should().HaveCount(3);
    }

    [Fact]
    public async Task RecognizeAsync_AfterLearning_UpdatesStateToRecognition()
    {
        // Arrange
        var state = DistinctionState.Initial();

        // Add some distinctions
        var result1 = await _learner.UpdateFromDistinctionAsync(
            state,
            Observation.Now("Test distinction"),
            DreamStage.Distinction);

        // Act
        var recognitionResult = await _learner.RecognizeAsync(
            result1.Value,
            "Recognition moment");

        // Assert
        recognitionResult.IsSuccess.Should().BeTrue();
        recognitionResult.Value.CurrentStage.Should().Be(DreamStage.Recognition);
        recognitionResult.Value.EpistemicCertainty.Should().Be(Form.Mark);
    }

    [Fact]
    public async Task RecognizeAsync_WithNoDistinctions_ReturnsFailure()
    {
        // Arrange
        var state = DistinctionState.Initial();

        // Act
        var result = await _learner.RecognizeAsync(state, "Test");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DissolveAsync_RemovesLowFitnessDistinctions()
    {
        // Arrange
        var state = DistinctionState.Initial();

        // Add distinctions with different fitness scores
        var result1 = await _learner.UpdateFromDistinctionAsync(
            state,
            Observation.Now("High fitness distinction"),
            DreamStage.Distinction);

        // Manually update fitness to be below threshold
        var updatedState = result1.Value with
        {
            DistinctionFitness = result1.Value.DistinctionFitness
                .SetItem("High fitness distinction", 0.2) // Below default threshold of 0.3
        };

        // Act
        var dissolveResult = await _learner.DissolveAsync(updatedState, 0.3);

        // Assert
        dissolveResult.IsSuccess.Should().BeTrue();
        dissolveResult.Value.CurrentStage.Should().Be(DreamStage.Dissolution);
        dissolveResult.Value.ActiveDistinctions.Should().BeEmpty();
    }

    [Fact]
    public async Task DissolveAsync_KeepsHighFitnessDistinctions()
    {
        // Arrange
        var state = DistinctionState.Initial();

        var result1 = await _learner.UpdateFromDistinctionAsync(
            state,
            Observation.Now("High fitness distinction"),
            DreamStage.Distinction);

        // Manually set high fitness
        var updatedState = result1.Value with
        {
            DistinctionFitness = result1.Value.DistinctionFitness
                .SetItem("High fitness distinction", 0.8)
        };

        // Act
        var dissolveResult = await _learner.DissolveAsync(updatedState, 0.3);

        // Assert
        dissolveResult.IsSuccess.Should().BeTrue();
        dissolveResult.Value.ActiveDistinctions.Should().Contain("High fitness distinction");
    }

    [Fact]
    public async Task FullDreamCycle_ThroughAllStages_WorksCorrectly()
    {
        // Arrange - Start in Void
        var state = DistinctionState.Initial();

        // Act & Assert - Progress through stages
        var distinction = await _learner.UpdateFromDistinctionAsync(
            state,
            Observation.Now("First cut"),
            DreamStage.Distinction);
        distinction.IsSuccess.Should().BeTrue();

        var subjectEmerges = await _learner.UpdateFromDistinctionAsync(
            distinction.Value,
            Observation.Now("I notice"),
            DreamStage.SubjectEmerges);
        subjectEmerges.IsSuccess.Should().BeTrue();

        var worldCrystallizes = await _learner.UpdateFromDistinctionAsync(
            subjectEmerges.Value,
            Observation.Now("Objects appear"),
            DreamStage.WorldCrystallizes);
        worldCrystallizes.IsSuccess.Should().BeTrue();

        var recognition = await _learner.RecognizeAsync(
            worldCrystallizes.Value,
            "I am the distinction");
        recognition.IsSuccess.Should().BeTrue();
        recognition.Value.CurrentStage.Should().Be(DreamStage.Recognition);

        var dissolution = await _learner.DissolveAsync(recognition.Value, 0.0);
        dissolution.IsSuccess.Should().BeTrue();
        dissolution.Value.CurrentStage.Should().Be(DreamStage.Dissolution);

        // Verify cycle completed
        dissolution.Value.ActiveDistinctions.Should().BeEmpty();
    }
}
