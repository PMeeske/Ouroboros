// <copyright file="GridWorldEnvironmentTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.Environment;
using Ouroboros.Examples.Environments;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for GridWorldEnvironment.
/// </summary>
public class GridWorldEnvironmentTests
{
    [Fact]
    public async Task GetStateAsync_ShouldReturnInitialState()
    {
        // Arrange
        var env = new GridWorldEnvironment(5, 5);

        // Act
        var result = await env.GetStateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var state = result.Value;
        state.StateData.Should().ContainKey("agent_x");
        state.StateData.Should().ContainKey("agent_y");
        state.StateData["agent_x"].Should().Be(0);
        state.StateData["agent_y"].Should().Be(0);
    }

    [Fact]
    public async Task GetAvailableActionsAsync_ShouldReturnFourActions()
    {
        // Arrange
        var env = new GridWorldEnvironment(5, 5);

        // Act
        var result = await env.GetAvailableActionsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
        result.Value.Select(a => a.ActionType).Should()
            .BeEquivalentTo(new[] { "UP", "DOWN", "LEFT", "RIGHT" });
    }

    [Fact]
    public async Task ExecuteActionAsync_MoveRight_ShouldUpdatePosition()
    {
        // Arrange
        var env = new GridWorldEnvironment(5, 5);
        var action = new EnvironmentAction("RIGHT");

        // Act
        var result = await env.ExecuteActionAsync(action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var observation = result.Value;
        observation.State.StateData["agent_x"].Should().Be(1);
        observation.State.StateData["agent_y"].Should().Be(0);
        observation.Reward.Should().Be(-1.0); // Step penalty
        observation.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteActionAsync_MoveOutOfBounds_ShouldStayInPlace()
    {
        // Arrange
        var env = new GridWorldEnvironment(5, 5);
        var action = new EnvironmentAction("LEFT"); // Already at (0,0)

        // Act
        var result = await env.ExecuteActionAsync(action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var observation = result.Value;
        observation.State.StateData["agent_x"].Should().Be(0);
        observation.State.StateData["agent_y"].Should().Be(0);
    }

    [Fact]
    public async Task ExecuteActionAsync_ReachGoal_ShouldReturnLargeReward()
    {
        // Arrange - Create small grid where goal is one step away
        var env = new GridWorldEnvironment(2, 2);

        // Act - Move right then down to reach goal at (1,1)
        var result1 = await env.ExecuteActionAsync(new EnvironmentAction("RIGHT"));
        var result2 = await env.ExecuteActionAsync(new EnvironmentAction("DOWN"));

        // Assert
        result2.IsSuccess.Should().BeTrue();
        var observation = result2.Value;
        observation.State.StateData["agent_x"].Should().Be(1);
        observation.State.StateData["agent_y"].Should().Be(1);
        observation.Reward.Should().Be(100.0);
        observation.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public async Task ResetAsync_ShouldReturnToInitialState()
    {
        // Arrange
        var env = new GridWorldEnvironment(5, 5);
        await env.ExecuteActionAsync(new EnvironmentAction("RIGHT"));
        await env.ExecuteActionAsync(new EnvironmentAction("DOWN"));

        // Act
        var result = await env.ResetAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StateData["agent_x"].Should().Be(0);
        result.Value.StateData["agent_y"].Should().Be(0);
    }

    [Fact]
    public async Task ExecuteActionAsync_InvalidAction_ShouldReturnFailure()
    {
        // Arrange
        var env = new GridWorldEnvironment(5, 5);
        var action = new EnvironmentAction("INVALID");

        // Act
        var result = await env.ExecuteActionAsync(action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid action");
    }
}
