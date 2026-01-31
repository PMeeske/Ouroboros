// <copyright file="WorldModelTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Tests.WorldModel;

/// <summary>
/// Unit tests for WorldModel implementation.
/// Tests state prediction, learning, planning, and trajectory simulation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorldModelTests
{
    private readonly Ouroboros.Agent.WorldModel.WorldModel worldModel;

    public WorldModelTests()
    {
        this.worldModel = new Ouroboros.Agent.WorldModel.WorldModel(
            maxMemorySize: 1000,
            kNeighbors: 5,
            logger: NullLogger<Ouroboros.Agent.WorldModel.WorldModel>.Instance);
    }

    [Fact]
    public async Task PredictAsync_WithNoExperience_ShouldReturnFailure()
    {
        // Arrange
        var state = CreateTestState(new Vector3(0, 0, 0));
        var action = EmbodiedAction.Move(Vector3.UnitX);

        // Act
        var result = await this.worldModel.PredictAsync(state, action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No experience available");
    }

    [Fact]
    public async Task PredictAsync_WithNullState_ShouldReturnFailure()
    {
        // Arrange
        SensorState? state = null;
        var action = EmbodiedAction.Move(Vector3.UnitX);

        // Act
        var result = await this.worldModel.PredictAsync(state!, action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task PredictAsync_WithNullAction_ShouldReturnFailure()
    {
        // Arrange
        var state = CreateTestState(new Vector3(0, 0, 0));
        EmbodiedAction? action = null;

        // Act
        var result = await this.worldModel.PredictAsync(state, action!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task UpdateFromExperienceAsync_WithValidTransitions_ShouldSucceed()
    {
        // Arrange
        var transitions = new List<EmbodiedTransition>
        {
            CreateTransition(Vector3.Zero, Vector3.UnitX, new Vector3(1, 0, 0), 1.0),
            CreateTransition(new Vector3(1, 0, 0), Vector3.UnitX, new Vector3(2, 0, 0), 1.0),
        };

        // Act
        var result = await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateFromExperienceAsync_WithNullTransitions_ShouldReturnFailure()
    {
        // Arrange
        IReadOnlyList<EmbodiedTransition>? transitions = null;

        // Act
        var result = await this.worldModel.UpdateFromExperienceAsync(transitions!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task UpdateFromExperienceAsync_WithEmptyTransitions_ShouldReturnFailure()
    {
        // Arrange
        var transitions = new List<EmbodiedTransition>();

        // Act
        var result = await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task PredictAsync_AfterLearning_ShouldPredictNextState()
    {
        // Arrange - Learn that moving forward increases X position
        var transitions = new List<EmbodiedTransition>
        {
            CreateTransition(Vector3.Zero, Vector3.UnitX, new Vector3(1, 0, 0), 1.0),
            CreateTransition(new Vector3(1, 0, 0), Vector3.UnitX, new Vector3(2, 0, 0), 1.0),
            CreateTransition(new Vector3(2, 0, 0), Vector3.UnitX, new Vector3(3, 0, 0), 1.0),
        };

        await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Act - Predict from similar state
        var currentState = CreateTestState(new Vector3(0.1f, 0, 0));
        var action = EmbodiedAction.Move(Vector3.UnitX);
        var result = await this.worldModel.PredictAsync(currentState, action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Position.X.Should().BeGreaterThan(0.1f);
        result.Value.Confidence.Should().BeGreaterThan(0);
        result.Value.Reward.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetUncertaintyAsync_WithNoExperience_ShouldReturnHighUncertainty()
    {
        // Arrange
        var state = CreateTestState(Vector3.Zero);
        var action = EmbodiedAction.Move(Vector3.UnitX);

        // Act
        var uncertainty = await this.worldModel.GetUncertaintyAsync(state, action);

        // Assert
        uncertainty.Should().Be(1.0);
    }

    [Fact]
    public async Task GetUncertaintyAsync_WithSimilarExperience_ShouldReturnLowerUncertainty()
    {
        // Arrange
        var transitions = Enumerable.Range(0, 10)
            .Select(i => CreateTransition(
                new Vector3(i, 0, 0),
                Vector3.UnitX,
                new Vector3(i + 1, 0, 0),
                1.0))
            .ToList();

        await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Act
        var state = CreateTestState(new Vector3(5, 0, 0));
        var action = EmbodiedAction.Move(Vector3.UnitX);
        var uncertainty = await this.worldModel.GetUncertaintyAsync(state, action);

        // Assert
        uncertainty.Should().BeLessThan(1.0);
        uncertainty.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public async Task PlanWithModelAsync_WithNoExperience_ShouldReturnFailure()
    {
        // Arrange
        var state = CreateTestState(Vector3.Zero);
        var goal = "Reach target";

        // Act
        var result = await this.worldModel.PlanWithModelAsync(state, goal, horizon: 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No experience");
    }

    [Fact]
    public async Task PlanWithModelAsync_WithNullState_ShouldReturnFailure()
    {
        // Arrange
        SensorState? state = null;
        var goal = "Reach target";

        // Act
        var result = await this.worldModel.PlanWithModelAsync(state!, goal, horizon: 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task PlanWithModelAsync_WithEmptyGoal_ShouldReturnFailure()
    {
        // Arrange
        var state = CreateTestState(Vector3.Zero);
        var goal = string.Empty;

        // Act
        var result = await this.worldModel.PlanWithModelAsync(state, goal, horizon: 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal");
    }

    [Fact]
    public async Task PlanWithModelAsync_WithInvalidHorizon_ShouldReturnFailure()
    {
        // Arrange
        var state = CreateTestState(Vector3.Zero);
        var goal = "Reach target";

        // Act
        var result = await this.worldModel.PlanWithModelAsync(state, goal, horizon: 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Horizon");
    }

    [Fact]
    public async Task PlanWithModelAsync_AfterLearning_ShouldGeneratePlan()
    {
        // Arrange
        var transitions = Enumerable.Range(0, 20)
            .Select(i => CreateTransition(
                new Vector3(i, 0, 0),
                Vector3.UnitX,
                new Vector3(i + 1, 0, 0),
                1.0))
            .ToList();

        await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Act
        var state = CreateTestState(Vector3.Zero);
        var result = await this.worldModel.PlanWithModelAsync(state, "Move forward", horizon: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SimulateTrajectoryAsync_WithNullState_ShouldReturnFailure()
    {
        // Arrange
        SensorState? state = null;
        var actions = new List<EmbodiedAction> { EmbodiedAction.Move(Vector3.UnitX) };

        // Act
        var result = await this.worldModel.SimulateTrajectoryAsync(state!, actions);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task SimulateTrajectoryAsync_WithNullActions_ShouldReturnFailure()
    {
        // Arrange
        var state = CreateTestState(Vector3.Zero);
        IReadOnlyList<EmbodiedAction>? actions = null;

        // Act
        var result = await this.worldModel.SimulateTrajectoryAsync(state, actions!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task SimulateTrajectoryAsync_WithEmptyActions_ShouldReturnFailure()
    {
        // Arrange
        var state = CreateTestState(Vector3.Zero);
        var actions = new List<EmbodiedAction>();

        // Act
        var result = await this.worldModel.SimulateTrajectoryAsync(state, actions);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task SimulateTrajectoryAsync_AfterLearning_ShouldSimulateFullTrajectory()
    {
        // Arrange
        var transitions = Enumerable.Range(0, 20)
            .Select(i => CreateTransition(
                new Vector3(i, 0, 0),
                Vector3.UnitX,
                new Vector3(i + 1, 0, 0),
                1.0,
                terminal: i == 19))
            .ToList();

        await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Act
        var state = CreateTestState(Vector3.Zero);
        var actions = Enumerable.Range(0, 5)
            .Select(_ => EmbodiedAction.Move(Vector3.UnitX))
            .ToList();

        var result = await this.worldModel.SimulateTrajectoryAsync(state, actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().BeGreaterThan(0);
        result.Value.Count.Should().BeLessThanOrEqualTo(actions.Count);

        // Each predicted state should have metadata
        foreach (var predicted in result.Value)
        {
            predicted.Metadata.Should().NotBeNull();
            predicted.Metadata.Should().ContainKey("neighbors_count");
            predicted.Confidence.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public async Task SimulateTrajectoryAsync_ShouldStopAtTerminalState()
    {
        // Arrange
        var transitions = new List<EmbodiedTransition>
        {
            CreateTransition(Vector3.Zero, Vector3.UnitX, new Vector3(1, 0, 0), 1.0, terminal: false),
            CreateTransition(new Vector3(1, 0, 0), Vector3.UnitX, new Vector3(2, 0, 0), 1.0, terminal: true),
        };

        await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Act
        var state = CreateTestState(Vector3.Zero);
        var actions = Enumerable.Range(0, 5)
            .Select(_ => EmbodiedAction.Move(Vector3.UnitX))
            .ToList();

        var result = await this.worldModel.SimulateTrajectoryAsync(state, actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().BeLessThan(actions.Count);
        result.Value.Last().Terminal.Should().BeTrue();
    }

    [Fact]
    public async Task WorldModel_ShouldMaintainMemoryLimit()
    {
        // Arrange
        var smallModel = new Ouroboros.Agent.WorldModel.WorldModel(maxMemorySize: 10, kNeighbors: 3);

        var transitions = Enumerable.Range(0, 20)
            .Select(i => CreateTransition(
                new Vector3(i, 0, 0),
                Vector3.UnitX,
                new Vector3(i + 1, 0, 0),
                1.0))
            .ToList();

        // Act
        await smallModel.UpdateFromExperienceAsync(transitions);

        // Add more to verify FIFO behavior
        var moreTransitions = Enumerable.Range(20, 5)
            .Select(i => CreateTransition(
                new Vector3(i, 0, 0),
                Vector3.UnitX,
                new Vector3(i + 1, 0, 0),
                1.0))
            .ToList();

        var result = await smallModel.UpdateFromExperienceAsync(moreTransitions);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Model should still be able to predict (memory maintained at limit)
        var state = CreateTestState(new Vector3(20, 0, 0));
        var action = EmbodiedAction.Move(Vector3.UnitX);
        var predictResult = await smallModel.PredictAsync(state, action);

        predictResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetUncertaintyAsync_WithNullInputs_ShouldReturnMaximumUncertainty()
    {
        // Act
        var uncertainty1 = await this.worldModel.GetUncertaintyAsync(null!, EmbodiedAction.NoOp());
        var uncertainty2 = await this.worldModel.GetUncertaintyAsync(CreateTestState(Vector3.Zero), null!);

        // Assert
        uncertainty1.Should().Be(1.0);
        uncertainty2.Should().Be(1.0);
    }

    [Fact]
    public async Task PredictAsync_WithDiverseExperience_ShouldAggregateNeighbors()
    {
        // Arrange - Multiple similar transitions with varying outcomes
        var transitions = new List<EmbodiedTransition>
        {
            CreateTransition(Vector3.Zero, Vector3.UnitX, new Vector3(1.0f, 0, 0), 1.0),
            CreateTransition(new Vector3(0.1f, 0, 0), Vector3.UnitX, new Vector3(1.1f, 0, 0), 1.1),
            CreateTransition(new Vector3(0.2f, 0, 0), Vector3.UnitX, new Vector3(1.2f, 0, 0), 0.9),
            CreateTransition(new Vector3(-0.1f, 0, 0), Vector3.UnitX, new Vector3(0.9f, 0, 0), 1.0),
        };

        await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Act
        var state = CreateTestState(Vector3.Zero);
        var action = EmbodiedAction.Move(Vector3.UnitX);
        var result = await this.worldModel.PredictAsync(state, action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Reward.Should().BeApproximately(1.0, 0.2); // Average of rewards
        result.Value.State.Position.X.Should().BeGreaterThan(0.8f);
        result.Value.Metadata["neighbors_count"].Should().Be(4);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task PlanWithModelAsync_WithDifferentHorizons_ShouldRespectHorizon(int horizon)
    {
        // Arrange
        var transitions = Enumerable.Range(0, 30)
            .Select(i => CreateTransition(
                new Vector3(i, 0, 0),
                Vector3.UnitX,
                new Vector3(i + 1, 0, 0),
                1.0))
            .ToList();

        await this.worldModel.UpdateFromExperienceAsync(transitions);

        // Act
        var state = CreateTestState(Vector3.Zero);
        var result = await this.worldModel.PlanWithModelAsync(state, "Move forward", horizon);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().BeLessThanOrEqualTo(horizon);
    }

    private static SensorState CreateTestState(Vector3 position)
    {
        return new SensorState(
            Position: position,
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);
    }

    private static EmbodiedTransition CreateTransition(
        Vector3 fromPos,
        Vector3 movement,
        Vector3 toPos,
        double reward,
        bool terminal = false)
    {
        var stateBefore = CreateTestState(fromPos);
        var stateAfter = CreateTestState(toPos);
        var action = EmbodiedAction.Move(movement);

        return new EmbodiedTransition(
            StateBefore: stateBefore,
            Action: action,
            StateAfter: stateAfter,
            Reward: reward,
            Terminal: terminal);
    }
}
