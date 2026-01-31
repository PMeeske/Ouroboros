// <copyright file="RLAgentTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for RLAgent.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RLAgentTests
{
    private readonly RLAgent agent;

    public RLAgentTests()
    {
        this.agent = new RLAgent(
            stateSpaceSize: 8,
            actionSpaceSize: 2,
            logger: NullLogger<RLAgent>.Instance);
    }

    [Fact]
    public async Task SelectActionAsync_WithValidState_ShouldReturnAction()
    {
        // Arrange
        var state = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };

        // Act
        var result = await this.agent.SelectActionAsync(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Length.Should().Be(2);
    }

    [Fact]
    public async Task SelectActionAsync_WithNullState_ShouldReturnFailure()
    {
        // Act
        var result = await this.agent.SelectActionAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task SelectActionAsync_WithEmptyState_ShouldReturnFailure()
    {
        // Act
        var result = await this.agent.SelectActionAsync(Array.Empty<float>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public async Task SelectActionAsync_WithWrongStateSize_ShouldReturnFailure()
    {
        // Arrange
        var state = new float[] { 0.1f, 0.2f }; // Wrong size

        // Act
        var result = await this.agent.SelectActionAsync(state);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expected state size");
    }

    [Fact]
    public async Task SelectActionAsync_InTrainingMode_ShouldExplore()
    {
        // Arrange
        var state = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
        var actions = new List<float[]>();

        // Act - multiple selections to check for exploration
        for (int i = 0; i < 10; i++)
        {
            var result = await this.agent.SelectActionAsync(state, training: true);
            result.IsSuccess.Should().BeTrue();
            actions.Add(result.Value);
        }

        // Assert - should have some variation due to exploration
        var uniqueActions = actions.Select(a => string.Join(",", a)).Distinct().Count();
        uniqueActions.Should().BeGreaterThan(1, "Training mode should explore");
    }

    [Fact]
    public async Task UpdatePolicyAsync_WithValidBatch_ShouldReturnMetrics()
    {
        // Arrange
        var batch = new List<Transition>
        {
            new Transition(
                State: new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f },
                Action: new float[] { 0.5f, 0.5f },
                Reward: 1.0f,
                NextState: new float[] { 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f },
                Done: false),
        };

        // Act
        var result = await this.agent.UpdatePolicyAsync(batch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.PolicyLoss.Should().BeGreaterThanOrEqualTo(0);
        result.Value.ValueLoss.Should().BeGreaterThanOrEqualTo(0);
        result.Value.StepsProcessed.Should().Be(batch.Count);
    }

    [Fact]
    public async Task UpdatePolicyAsync_WithNullBatch_ShouldReturnFailure()
    {
        // Act
        var result = await this.agent.UpdatePolicyAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task UpdatePolicyAsync_WithEmptyBatch_ShouldReturnFailure()
    {
        // Act
        var result = await this.agent.UpdatePolicyAsync(new List<Transition>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public async Task SaveCheckpointAsync_WithValidPath_ShouldSucceed()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"checkpoint_{Guid.NewGuid()}.json");

        try
        {
            // Act
            var result = await this.agent.SaveCheckpointAsync(path);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(path);
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveCheckpointAsync_WithNullPath_ShouldReturnFailure()
    {
        // Act
        var result = await this.agent.SaveCheckpointAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task LoadCheckpointAsync_WithExistingFile_ShouldSucceed()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"checkpoint_{Guid.NewGuid()}.json");

        try
        {
            await this.agent.SaveCheckpointAsync(path);

            // Act
            var result = await this.agent.LoadCheckpointAsync(path);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task LoadCheckpointAsync_WithNonExistentFile_ShouldReturnFailure()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.json");

        // Act
        var result = await this.agent.LoadCheckpointAsync(path);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void Constructor_WithInvalidStateSpaceSize_ShouldThrow()
    {
        // Act
        Action act = () => new RLAgent(
            stateSpaceSize: 0,
            actionSpaceSize: 2,
            logger: NullLogger<RLAgent>.Instance);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithInvalidActionSpaceSize_ShouldThrow()
    {
        // Act
        Action act = () => new RLAgent(
            stateSpaceSize: 8,
            actionSpaceSize: 0,
            logger: NullLogger<RLAgent>.Instance);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
