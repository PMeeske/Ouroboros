// <copyright file="WorldModelEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MetaAI;

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Xunit;

/// <summary>
/// Comprehensive tests for WorldModelEngine implementation.
/// Tests model learning, prediction, planning, evaluation, and synthetic experience generation.
/// </summary>
[Trait("Category", "Unit")]
public class WorldModelEngineTests
{
    private readonly WorldModelEngine engine;

    public WorldModelEngineTests()
    {
        this.engine = new WorldModelEngine(seed: 42);
    }

    #region Helper Methods

    private static List<Transition> CreateTestTransitions(int count = 10, int embeddingSize = 8)
    {
        var random = new Random(42);
        var transitions = new List<Transition>();

        for (int i = 0; i < count; i++)
        {
            var prevState = CreateRandomState(random, embeddingSize);
            var action = CreateRandomAction(random);
            var nextState = CreateRandomState(random, embeddingSize);
            var reward = random.NextDouble() * 10 - 5; // -5 to 5
            var terminal = random.NextDouble() < 0.1; // 10% terminal

            transitions.Add(new Transition(prevState, action, nextState, reward, terminal));
        }

        return transitions;
    }

    private static State CreateRandomState(Random random, int embeddingSize)
    {
        var features = new Dictionary<string, object>
        {
            ["x"] = random.NextDouble() * 100,
            ["y"] = random.NextDouble() * 100,
        };

        var embedding = new float[embeddingSize];
        for (int i = 0; i < embeddingSize; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return new State(features, embedding);
    }

    private static Action CreateRandomAction(Random random)
    {
        var actions = new[] { "move_forward", "move_backward", "turn_left", "turn_right" };
        var name = actions[random.Next(actions.Length)];
        return new Action(name, new Dictionary<string, object> { ["speed"] = random.NextDouble() });
    }

    #endregion

    #region LearnModelAsync Tests

    [Fact]
    public async Task LearnModelAsync_WithValidTransitions_ReturnsSuccessfulModel()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 50);

        // Act
        var result = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Domain.Should().Be("learned");
        result.Value.TransitionModel.Should().NotBeNull();
        result.Value.RewardModel.Should().NotBeNull();
        result.Value.TerminalModel.Should().NotBeNull();
    }

    [Fact]
    public async Task LearnModelAsync_WithEmptyTransitions_ReturnsFailure()
    {
        // Arrange
        var transitions = new List<Transition>();

        // Act
        var result = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task LearnModelAsync_WithNullTransitions_ReturnsFailure()
    {
        // Act
        var result = await this.engine.LearnModelAsync(null!, ModelArchitecture.MLP);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task LearnModelAsync_WithInconsistentEmbeddings_ReturnsFailure()
    {
        // Arrange
        var transitions = new List<Transition>
        {
            CreateTestTransitions(1, embeddingSize: 8)[0],
            CreateTestTransitions(1, embeddingSize: 16)[0],
        };

        // Act
        var result = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Inconsistent");
    }

    [Fact]
    public async Task LearnModelAsync_StoresHyperparameters()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 20);

        // Act
        var result = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Hyperparameters.Should().ContainKey("architecture");
        result.Value.Hyperparameters.Should().ContainKey("embedding_size");
        result.Value.Hyperparameters.Should().ContainKey("training_samples");
        result.Value.Hyperparameters["training_samples"].Should().Be(20);
    }

    #endregion

    #region PredictNextStateAsync Tests

    [Fact]
    public async Task PredictNextStateAsync_WithValidInputs_ReturnsState()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 30);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var state = transitions[0].PreviousState;
        var action = transitions[0].ActionTaken;

        // Act
        var result = await this.engine.PredictNextStateAsync(state, action, model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Embedding.Should().HaveCount(state.Embedding.Length);
    }

    [Fact]
    public async Task PredictNextStateAsync_WithNullState_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var action = CreateRandomAction(new Random());

        // Act
        var result = await this.engine.PredictNextStateAsync(null!, action, model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("state cannot be null");
    }

    [Fact]
    public async Task PredictNextStateAsync_WithNullAction_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var state = transitions[0].PreviousState;

        // Act
        var result = await this.engine.PredictNextStateAsync(state, null!, model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Action cannot be null");
    }

    [Fact]
    public async Task PredictNextStateAsync_WithNullModel_ReturnsFailure()
    {
        // Arrange
        var state = CreateRandomState(new Random(), 8);
        var action = CreateRandomAction(new Random());

        // Act
        var result = await this.engine.PredictNextStateAsync(state, action, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Model cannot be null");
    }

    #endregion

    #region PlanInImaginationAsync Tests

    [Fact]
    public async Task PlanInImaginationAsync_WithValidInputs_ReturnsPlan()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 50);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var initialState = transitions[0].PreviousState;

        // Act
        var result = await this.engine.PlanInImaginationAsync(
            initialState,
            "reach the goal",
            model,
            lookaheadDepth: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Actions.Should().NotBeEmpty();
        result.Value.Actions.Count.Should().BeLessThanOrEqualTo(5);
        result.Value.Description.Should().Contain("goal");
        result.Value.Confidence.Should().BeGreaterThanOrEqualTo(0);
        result.Value.Confidence.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task PlanInImaginationAsync_WithNullState_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;

        // Act
        var result = await this.engine.PlanInImaginationAsync(null!, "goal", model, 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("state cannot be null");
    }

    [Fact]
    public async Task PlanInImaginationAsync_WithEmptyGoal_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var state = transitions[0].PreviousState;

        // Act
        var result = await this.engine.PlanInImaginationAsync(state, string.Empty, model, 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal cannot be empty");
    }

    [Fact]
    public async Task PlanInImaginationAsync_WithZeroDepth_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var state = transitions[0].PreviousState;

        // Act
        var result = await this.engine.PlanInImaginationAsync(state, "goal", model, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be positive");
    }

    [Fact]
    public async Task PlanInImaginationAsync_WithNegativeDepth_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var state = transitions[0].PreviousState;

        // Act
        var result = await this.engine.PlanInImaginationAsync(state, "goal", model, -5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be positive");
    }

    #endregion

    #region EvaluateModelAsync Tests

    [Fact]
    public async Task EvaluateModelAsync_WithValidInputs_ReturnsQualityMetrics()
    {
        // Arrange
        var trainTransitions = CreateTestTransitions(count: 100);
        var testTransitions = CreateTestTransitions(count: 20);
        var modelResult = await this.engine.LearnModelAsync(trainTransitions, ModelArchitecture.MLP);
        var model = modelResult.Value;

        // Act
        var result = await this.engine.EvaluateModelAsync(model, testTransitions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PredictionAccuracy.Should().BeGreaterThan(0);
        result.Value.PredictionAccuracy.Should().BeLessThanOrEqualTo(1);
        result.Value.RewardCorrelation.Should().BeGreaterThan(0);
        result.Value.RewardCorrelation.Should().BeLessThanOrEqualTo(1);
        result.Value.TerminalAccuracy.Should().BeGreaterThanOrEqualTo(0);
        result.Value.TerminalAccuracy.Should().BeLessThanOrEqualTo(1);
        result.Value.TestSamples.Should().Be(20);
    }

    [Fact]
    public async Task EvaluateModelAsync_WithNullModel_ReturnsFailure()
    {
        // Arrange
        var testTransitions = CreateTestTransitions(count: 10);

        // Act
        var result = await this.engine.EvaluateModelAsync(null!, testTransitions);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Model cannot be null");
    }

    [Fact]
    public async Task EvaluateModelAsync_WithEmptyTestSet_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var emptyTestSet = new List<Transition>();

        // Act
        var result = await this.engine.EvaluateModelAsync(model, emptyTestSet);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task EvaluateModelAsync_WithNullTestSet_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;

        // Act
        var result = await this.engine.EvaluateModelAsync(model, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    #endregion

    #region GenerateSyntheticExperienceAsync Tests

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_WithValidInputs_ReturnsTransitions()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 50);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var startState = transitions[0].PreviousState;

        // Act
        var result = await this.engine.GenerateSyntheticExperienceAsync(
            model,
            startState,
            trajectoryLength: 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().NotBeEmpty();
        result.Value.Count.Should().BeLessThanOrEqualTo(10);

        // Verify transition structure
        foreach (var transition in result.Value)
        {
            transition.PreviousState.Should().NotBeNull();
            transition.ActionTaken.Should().NotBeNull();
            transition.NextState.Should().NotBeNull();
            transition.NextState.Embedding.Should().HaveCount(startState.Embedding.Length);
        }
    }

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_TerminatesOnTerminalState()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 50);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var startState = transitions[0].PreviousState;

        // Act
        var result = await this.engine.GenerateSyntheticExperienceAsync(
            model,
            startState,
            trajectoryLength: 100);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // If trajectory ended early, last transition should be terminal (or reached max length)
        if (result.Value.Count < 100 && result.Value.Any())
        {
            result.Value.Last().Terminal.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_WithNullModel_ReturnsFailure()
    {
        // Arrange
        var state = CreateRandomState(new Random(), 8);

        // Act
        var result = await this.engine.GenerateSyntheticExperienceAsync(null!, state, 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Model cannot be null");
    }

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_WithNullStartState_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;

        // Act
        var result = await this.engine.GenerateSyntheticExperienceAsync(model, null!, 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Start state cannot be null");
    }

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_WithZeroLength_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var state = transitions[0].PreviousState;

        // Act
        var result = await this.engine.GenerateSyntheticExperienceAsync(model, state, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be positive");
    }

    [Fact]
    public async Task GenerateSyntheticExperienceAsync_WithNegativeLength_ReturnsFailure()
    {
        // Arrange
        var transitions = CreateTestTransitions(count: 10);
        var modelResult = await this.engine.LearnModelAsync(transitions, ModelArchitecture.MLP);
        var model = modelResult.Value;
        var state = transitions[0].PreviousState;

        // Act
        var result = await this.engine.GenerateSyntheticExperienceAsync(model, state, -5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be positive");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullWorkflow_LearnEvaluatePlanGenerate_WorksEndToEnd()
    {
        // Arrange
        var allTransitions = CreateTestTransitions(count: 100);
        var trainTransitions = allTransitions.Take(80).ToList();
        var testTransitions = allTransitions.Skip(80).ToList();

        // Act & Assert - Learn
        var learnResult = await this.engine.LearnModelAsync(trainTransitions, ModelArchitecture.MLP);
        learnResult.IsSuccess.Should().BeTrue();
        var model = learnResult.Value;

        // Act & Assert - Evaluate
        var evalResult = await this.engine.EvaluateModelAsync(model, testTransitions);
        evalResult.IsSuccess.Should().BeTrue();
        evalResult.Value.TestSamples.Should().Be(20);

        // Act & Assert - Plan
        var planResult = await this.engine.PlanInImaginationAsync(
            trainTransitions[0].PreviousState,
            "maximize reward",
            model,
            5);
        planResult.IsSuccess.Should().BeTrue();
        planResult.Value.Actions.Should().NotBeEmpty();

        // Act & Assert - Generate
        var generateResult = await this.engine.GenerateSyntheticExperienceAsync(
            model,
            trainTransitions[0].PreviousState,
            10);
        generateResult.IsSuccess.Should().BeTrue();
        generateResult.Value.Should().NotBeEmpty();
    }

    #endregion
}
