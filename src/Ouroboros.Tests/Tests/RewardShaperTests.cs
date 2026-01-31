// <copyright file="RewardShaperTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for RewardShaper.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RewardShaperTests
{
    private readonly RewardShaper shaper;

    public RewardShaperTests()
    {
        this.shaper = new RewardShaper(NullLogger<RewardShaper>.Instance);
    }

    [Fact]
    public void ShapeReward_WithImprovementTowardGoal_ShouldIncreaseReward()
    {
        // Arrange
        var previousState = new SensorState(
            Position: new Vector3(10, 0, 0),
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var currentState = new SensorState(
            Position: new Vector3(5, 0, 0), // Closer to origin
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var action = EmbodiedAction.NoOp();

        // Act
        var shapedReward = this.shaper.ShapeReward(0.0, previousState, action, currentState);

        // Assert
        shapedReward.Should().BeGreaterThan(0.0, "Moving toward goal should increase reward");
    }

    [Fact]
    public void ShapeReward_WithMovementAwayFromGoal_ShouldDecreaseReward()
    {
        // Arrange
        var previousState = new SensorState(
            Position: new Vector3(5, 0, 0),
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var currentState = new SensorState(
            Position: new Vector3(10, 0, 0), // Farther from origin
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var action = EmbodiedAction.NoOp();

        // Act
        var shapedReward = this.shaper.ShapeReward(0.0, previousState, action, currentState);

        // Assert
        shapedReward.Should().BeLessThan(0.0, "Moving away from goal should decrease reward");
    }

    [Fact]
    public void ShapeReward_WithHighVelocity_ShouldPenalize()
    {
        // Arrange
        var previousState = new SensorState(
            Position: Vector3.Zero,
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var currentState = new SensorState(
            Position: Vector3.Zero,
            Rotation: Quaternion.Identity,
            Velocity: new Vector3(10, 0, 0), // High velocity
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var action = EmbodiedAction.NoOp();

        // Act
        var shapedReward = this.shaper.ShapeReward(1.0, previousState, action, currentState);

        // Assert
        shapedReward.Should().BeLessThan(1.0, "High velocity should penalize reward");
    }

    [Fact]
    public void ShapeReward_WithNullStates_ShouldReturnRawReward()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var shapedReward = this.shaper.ShapeReward(1.0, null!, action, null!);

        // Assert
        shapedReward.Should().Be(1.0, "Should return raw reward when states are null");
    }

    [Fact]
    public async Task ComputeCuriosityRewardAsync_WithNovelState_ShouldReturnBonus()
    {
        // Arrange
        var state = new SensorState(
            Position: new Vector3(5, 0, 0),
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var action = EmbodiedAction.NoOp();

        // Act
        var curiosityReward = await this.shaper.ComputeCuriosityRewardAsync(state, action);

        // Assert
        curiosityReward.Should().BeGreaterThan(0.0, "Novel state should give curiosity bonus");
    }

    [Fact]
    public async Task ComputeCuriosityRewardAsync_WithRevisitedState_ShouldReturnZero()
    {
        // Arrange
        var state = new SensorState(
            Position: new Vector3(5, 0, 0),
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var action = EmbodiedAction.NoOp();

        // Visit state first time
        await this.shaper.ComputeCuriosityRewardAsync(state, action);

        // Act - revisit same state
        var curiosityReward = await this.shaper.ComputeCuriosityRewardAsync(state, action);

        // Assert
        curiosityReward.Should().Be(0.0, "Revisited state should not give curiosity bonus");
    }

    [Fact]
    public async Task ComputeCuriosityRewardAsync_WithNullState_ShouldReturnZero()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var curiosityReward = await this.shaper.ComputeCuriosityRewardAsync(null!, action);

        // Assert
        curiosityReward.Should().Be(0.0);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(0.0, 1.0, 0.0)]
    [InlineData(0.0, 0.0, 1.0)]
    public async Task ComputeCuriosityRewardAsync_WithDifferentPositions_ShouldTrackSeparately(
        float x,
        float y,
        float z)
    {
        // Arrange
        var state = new SensorState(
            Position: new Vector3(x, y, z),
            Rotation: Quaternion.Identity,
            Velocity: Vector3.Zero,
            VisualObservation: Array.Empty<float>(),
            ProprioceptiveState: Array.Empty<float>(),
            CustomSensors: new Dictionary<string, float>(),
            Timestamp: DateTime.UtcNow);

        var action = EmbodiedAction.NoOp();

        // Act
        var curiosityReward = await this.shaper.ComputeCuriosityRewardAsync(state, action);

        // Assert
        curiosityReward.Should().BeGreaterThan(0.0, "Each unique position should be novel initially");
    }
}
