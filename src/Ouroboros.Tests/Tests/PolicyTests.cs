// <copyright file="PolicyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for RL policies.
/// </summary>
public class PolicyTests
{
    [Fact]
    public async Task EpsilonGreedyPolicy_SelectAction_ShouldReturnValidAction()
    {
        // Arrange
        var policy = new EpsilonGreedyPolicy(epsilon: 0.1, seed: 42);
        var state = new EnvironmentState(
            new Dictionary<string, object> { ["x"] = 0, ["y"] = 0 });
        var actions = new List<EnvironmentAction>
        {
            new("UP"),
            new("DOWN"),
            new("LEFT"),
            new("RIGHT"),
        };

        // Act
        var result = await policy.SelectActionAsync(state, actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        actions.Should().Contain(result.Value);
    }

    [Fact]
    public async Task EpsilonGreedyPolicy_Update_ShouldLearnFromReward()
    {
        // Arrange
        var policy = new EpsilonGreedyPolicy(epsilon: 0.0, seed: 42); // No exploration
        var state = new EnvironmentState(
            new Dictionary<string, object> { ["x"] = 0 });
        var actions = new List<EnvironmentAction>
        {
            new("ACTION_A"),
            new("ACTION_B"),
        };

        // Act - Give positive reward to ACTION_A multiple times
        for (var i = 0; i < 10; i++)
        {
            var observation = new Observation(state, 10.0, false);
            await policy.UpdateAsync(state, actions[0], observation);
        }

        // Give negative reward to ACTION_B
        for (var i = 0; i < 10; i++)
        {
            var observation = new Observation(state, -5.0, false);
            await policy.UpdateAsync(state, actions[1], observation);
        }

        // Select action multiple times - should consistently pick ACTION_A
        var selectedActions = new List<EnvironmentAction>();
        for (var i = 0; i < 5; i++)
        {
            var result = await policy.SelectActionAsync(state, actions);
            selectedActions.Add(result.Value);
        }

        // Assert - With epsilon=0, should always pick best action (ACTION_A)
        selectedActions.Should().OnlyContain(a => a.ActionType == "ACTION_A");
    }

    [Fact]
    public async Task BanditPolicy_SelectAction_ShouldExploreUntriedActions()
    {
        // Arrange
        var policy = new BanditPolicy();
        var state = new EnvironmentState(new Dictionary<string, object>());
        var actions = new List<EnvironmentAction>
        {
            new("ACTION_1"),
            new("ACTION_2"),
            new("ACTION_3"),
        };

        // Act - Select actions multiple times and update with neutral rewards
        var selectedActions = new HashSet<string>();
        for (var i = 0; i < 15; i++)
        {
            var result = await policy.SelectActionAsync(state, actions);
            selectedActions.Add(result.Value.ActionType);

            // Give neutral reward to encourage exploration
            var observation = new Observation(state, 1.0, false);
            await policy.UpdateAsync(state, result.Value, observation);
        }

        // Assert - Should try all actions (UCB prioritizes untried actions)
        selectedActions.Should().HaveCount(3);
    }

    [Fact]
    public async Task BanditPolicy_Update_ShouldConvergeTobestAction()
    {
        // Arrange
        var policy = new BanditPolicy();
        var state = new EnvironmentState(new Dictionary<string, object>());
        var actions = new List<EnvironmentAction>
        {
            new("GOOD_ACTION"),
            new("BAD_ACTION"),
        };

        // Act - Simulate many trials with different rewards
        for (var i = 0; i < 100; i++)
        {
            var selectedAction = await policy.SelectActionAsync(state, actions);

            // Give reward based on action
            var reward = selectedAction.Value.ActionType == "GOOD_ACTION" ? 10.0 : 1.0;
            var observation = new Observation(state, reward, false);

            await policy.UpdateAsync(state, selectedAction.Value, observation);
        }

        // Now select action 10 times - should mostly pick GOOD_ACTION
        var finalSelections = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var result = await policy.SelectActionAsync(state, actions);
            finalSelections.Add(result.Value.ActionType);
        }

        // Assert - Should prefer GOOD_ACTION more than 70% of the time
        var goodActionCount = finalSelections.Count(a => a == "GOOD_ACTION");
        goodActionCount.Should().BeGreaterThan(7);
    }

    [Fact]
    public async Task Policy_WithNoActions_ShouldReturnFailure()
    {
        // Arrange
        var policy = new EpsilonGreedyPolicy();
        var state = new EnvironmentState(new Dictionary<string, object>());
        var emptyActions = new List<EnvironmentAction>();

        // Act
        var result = await policy.SelectActionAsync(state, emptyActions);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No available actions");
    }

    [Fact]
    public void EpsilonGreedyPolicy_InvalidEpsilon_ShouldThrow()
    {
        // Act & Assert
        Action act1 = () => new EpsilonGreedyPolicy(epsilon: -0.1);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        Action act2 = () => new EpsilonGreedyPolicy(epsilon: 1.1);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }
}
