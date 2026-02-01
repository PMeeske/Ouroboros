// <copyright file="EmbodiedAgentTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Ethics;
using Ouroboros.Domain.Embodied;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for EmbodiedAgent.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EmbodiedAgentTests
{
    private readonly IEnvironmentManager environmentManager;
    private readonly IEthicsFramework ethics;
    private readonly EmbodiedAgent agent;

    public EmbodiedAgentTests()
    {
        this.environmentManager = new EnvironmentManager(NullLogger<EnvironmentManager>.Instance);
        this.ethics = EthicsFrameworkFactory.CreateDefault();
        this.agent = new EmbodiedAgent(this.environmentManager, this.ethics, NullLogger<EmbodiedAgent>.Instance);
    }

    [Fact]
    public async Task InitializeInEnvironmentAsync_WithValidConfig_ShouldSucceed()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string> { "Move", "Rotate" },
            Type: EnvironmentType.Unity);



        // Act
        var result = await this.agent.InitializeInEnvironmentAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Ouroboros.Domain.Reinforcement.Unit.Value);
    }

    [Fact]
    public async Task InitializeInEnvironmentAsync_WhenEnvironmentCreationFails_ShouldReturnFailure()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        // Use invalid config to trigger failure
        config = new EnvironmentConfig(
            SceneName: string.Empty, // Empty scene name will cause failure
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        // Act
        var result = await this.agent.InitializeInEnvironmentAsync(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to create environment");
    }

    [Fact]
    public async Task PerceiveAsync_WhenNotInitialized_ShouldReturnFailure()
    {
        // Act
        var result = await this.agent.PerceiveAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not initialized");
    }

    [Fact]
    public async Task PerceiveAsync_WhenInitialized_ShouldReturnSensorState()
    {
        // Arrange
        await this.InitializeAgentAsync();

        // Act
        var result = await this.agent.PerceiveAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Position.Should().NotBeNull();
        result.Value.Rotation.Should().NotBeNull();
    }

    [Fact]
    public async Task ActAsync_WhenNotInitialized_ShouldReturnFailure()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var result = await this.agent.ActAsync(action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not initialized");
    }

    [Fact]
    public async Task ActAsync_WhenInitialized_ShouldReturnActionResult()
    {
        // Arrange
        await this.InitializeAgentAsync();
        var action = EmbodiedAction.Move(new Vector3(1f, 0f, 0f), "MoveForward");

        // Act
        var result = await this.agent.ActAsync(action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Success.Should().BeTrue();
        result.Value.ResultingState.Should().NotBeNull();
    }

    [Fact]
    public async Task LearnFromExperienceAsync_WithValidTransitions_ShouldSucceed()
    {
        // Arrange
        var transitions = new List<EmbodiedTransition>
        {
            new(
                StateBefore: SensorState.Default(),
                Action: EmbodiedAction.NoOp(),
                StateAfter: SensorState.Default(),
                Reward: 1.0,
                Terminal: false),
        };

        // Act
        var result = await this.agent.LearnFromExperienceAsync(transitions);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LearnFromExperienceAsync_WithEmptyTransitions_ShouldSucceed()
    {
        // Arrange
        var transitions = new List<EmbodiedTransition>();

        // Act
        var result = await this.agent.LearnFromExperienceAsync(transitions);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PlanEmbodiedAsync_WithValidGoal_ShouldReturnPlan()
    {
        // Arrange
        var goal = "Navigate to target location";
        var currentState = SensorState.Default();

        // Act
        var result = await this.agent.PlanEmbodiedAsync(goal, currentState);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Goal.Should().Be(goal);
        result.Value.Actions.Should().NotBeEmpty();
        result.Value.Confidence.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task PlanEmbodiedAsync_WithMultipleGoals_ShouldGenerateDistinctPlans()
    {
        // Arrange
        var goal1 = "Move forward";
        var goal2 = "Turn around";
        var state = SensorState.Default();

        // Act
        var result1 = await this.agent.PlanEmbodiedAsync(goal1, state);
        var result2 = await this.agent.PlanEmbodiedAsync(goal2, state);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Goal.Should().NotBe(result2.Value.Goal);
    }

    private async Task InitializeAgentAsync()
    {
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        await this.agent.InitializeInEnvironmentAsync(config);
    }
}
