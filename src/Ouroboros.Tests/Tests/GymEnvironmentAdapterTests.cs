// <copyright file="GymEnvironmentAdapterTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for GymEnvironmentAdapter.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GymEnvironmentAdapterTests
{
    private readonly GymEnvironmentAdapter adapter;

    public GymEnvironmentAdapterTests()
    {
        this.adapter = new GymEnvironmentAdapter(
            environmentName: "CartPole-v1",
            observationSpaceSize: 4,
            actionSpaceSize: 2,
            isContinuousAction: true,
            logger: NullLogger<GymEnvironmentAdapter>.Instance);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldSucceed()
    {
        // Assert
        this.adapter.EnvironmentName.Should().Be("CartPole-v1");
        this.adapter.ObservationSpaceSize.Should().Be(4);
        this.adapter.ActionSpaceSize.Should().Be(2);
        this.adapter.IsContinuousAction.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyName_ShouldThrow()
    {
        // Act
        Action act = () => new GymEnvironmentAdapter(
            string.Empty,
            4,
            2,
            true,
            NullLogger<GymEnvironmentAdapter>.Instance);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithInvalidObservationSpaceSize_ShouldThrow()
    {
        // Act
        Action act = () => new GymEnvironmentAdapter(
            "Test",
            0,
            2,
            true,
            NullLogger<GymEnvironmentAdapter>.Instance);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ResetAsync_FirstTime_ShouldReturnObservation()
    {
        // Act
        var result = await this.adapter.ResetAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Length.Should().Be(4);
    }

    [Fact]
    public async Task StepAsync_WithoutReset_ShouldReturnFailure()
    {
        // Arrange
        var action = new float[] { 0.5f, 0.5f };

        // Act
        var result = await this.adapter.StepAsync(action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not initialized");
    }

    [Fact]
    public async Task StepAsync_AfterReset_ShouldReturnStepResult()
    {
        // Arrange
        await this.adapter.ResetAsync();
        var action = new float[] { 0.5f, 0.5f };

        // Act
        var result = await this.adapter.StepAsync(action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Observation.Length.Should().Be(4);
        result.Value.Info.Should().ContainKey("step");
    }

    [Fact]
    public async Task StepAsync_WithNullAction_ShouldReturnFailure()
    {
        // Arrange
        await this.adapter.ResetAsync();

        // Act
        var result = await this.adapter.StepAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expected action size");
    }

    [Fact]
    public async Task StepAsync_WithWrongActionSize_ShouldReturnFailure()
    {
        // Arrange
        await this.adapter.ResetAsync();
        var action = new float[] { 0.5f }; // Wrong size

        // Act
        var result = await this.adapter.StepAsync(action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expected action size");
    }

    [Fact]
    public async Task StepAsync_AfterManySteps_ShouldEventuallyTerminate()
    {
        // Arrange
        await this.adapter.ResetAsync();
        var action = new float[] { 1.0f, 1.0f };

        // Act - take many steps
        GymStepResult? lastResult = null;
        for (int i = 0; i < 150; i++)
        {
            var result = await this.adapter.StepAsync(action);
            if (result.IsSuccess)
            {
                lastResult = result.Value;
                if (lastResult.Done)
                {
                    break;
                }
            }
        }

        // Assert
        lastResult.Should().NotBeNull();
        lastResult!.Done.Should().BeTrue("Episode should terminate after 100 steps");
    }

    [Fact]
    public async Task CloseAsync_AfterReset_ShouldSucceed()
    {
        // Arrange
        await this.adapter.ResetAsync();

        // Act
        await this.adapter.CloseAsync();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task CloseAsync_WithoutReset_ShouldSucceed()
    {
        // Act
        await this.adapter.CloseAsync();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task StepAsync_AfterClose_ShouldReturnFailure()
    {
        // Arrange
        await this.adapter.ResetAsync();
        await this.adapter.CloseAsync();
        var action = new float[] { 0.5f, 0.5f };

        // Act
        var result = await this.adapter.StepAsync(action);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleReset_ShouldSucceed()
    {
        // Act
        var result1 = await this.adapter.ResetAsync();
        var result2 = await this.adapter.ResetAsync();

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
    }
}
