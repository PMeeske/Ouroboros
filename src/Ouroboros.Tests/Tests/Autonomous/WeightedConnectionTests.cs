// <copyright file="WeightedConnectionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Autonomous;

using FluentAssertions;
using Ouroboros.Domain.Autonomous;
using Xunit;

/// <summary>
/// Comprehensive tests for the WeightedConnection class.
/// Tests Hebbian learning, weight clamping, and activation tracking.
/// </summary>
[Trait("Category", "Unit")]
public class WeightedConnectionTests
{
    [Fact]
    public void Constructor_WithValidWeight_ClampsToRange()
    {
        // Arrange & Act
        var connection = new WeightedConnection("source", "target", 1.5);

        // Assert
        connection.Weight.Should().Be(1.0);
    }

    [Fact]
    public void Constructor_WithNegativeWeight_ClampsToRange()
    {
        // Arrange & Act
        var connection = new WeightedConnection("source", "target", -1.5);

        // Assert
        connection.Weight.Should().Be(-1.0);
    }

    [Fact]
    public void Constructor_WithWeightInRange_PreservesValue()
    {
        // Arrange & Act
        var connection = new WeightedConnection("source", "target", 0.5);

        // Assert
        connection.Weight.Should().Be(0.5);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange & Act
        var connection = new WeightedConnection("neuron1", "neuron2", 0.7, 0.02);

        // Assert
        connection.SourceNeuronId.Should().Be("neuron1");
        connection.TargetNeuronId.Should().Be("neuron2");
        connection.Weight.Should().Be(0.7);
        connection.PlasticityRate.Should().Be(0.02);
        connection.IsFrozen.Should().BeFalse();
        connection.ActivationCount.Should().Be(0);
    }

    [Fact]
    public void HebbianUpdate_WithCoActivation_StrengthensConnection()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", 0.5, 0.1);
        var initialWeight = connection.Weight;

        // Act
        connection.HebbianUpdate(sourceActive: true, targetActive: true);

        // Assert
        connection.Weight.Should().BeGreaterThan(initialWeight);
    }

    [Fact]
    public void HebbianUpdate_WithoutCoActivation_WeakensConnection()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", 0.5, 0.1);
        var initialWeight = connection.Weight;

        // Act
        connection.HebbianUpdate(sourceActive: true, targetActive: false);

        // Assert
        connection.Weight.Should().BeLessThan(initialWeight);
    }

    [Fact]
    public void HebbianUpdate_WithNoSourceActivation_DoesNotChange()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", 0.5, 0.1);
        var initialWeight = connection.Weight;

        // Act
        connection.HebbianUpdate(sourceActive: false, targetActive: true);

        // Assert
        connection.Weight.Should().Be(initialWeight);
    }

    [Fact]
    public void HebbianUpdate_WhenFrozen_DoesNotUpdate()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", 0.5, 0.1)
        {
            IsFrozen = true,
        };
        var initialWeight = connection.Weight;

        // Act
        connection.HebbianUpdate(sourceActive: true, targetActive: true);

        // Assert
        connection.Weight.Should().Be(initialWeight);
    }

    [Fact]
    public void HebbianUpdate_WithMultipleUpdates_StaysWithinBounds()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", 0.9, 0.1);

        // Act - multiple strengthening updates
        for (int i = 0; i < 20; i++)
        {
            connection.HebbianUpdate(sourceActive: true, targetActive: true);
        }

        // Assert
        connection.Weight.Should().BeLessThanOrEqualTo(1.0);
        connection.Weight.Should().BeGreaterThanOrEqualTo(-1.0);
    }

    [Fact]
    public void HebbianUpdate_NegativeWeightWithCoActivation_MoveTowardPositive()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", -0.5, 0.1);
        var initialWeight = connection.Weight;

        // Act
        connection.HebbianUpdate(sourceActive: true, targetActive: true);

        // Assert
        connection.Weight.Should().BeGreaterThan(initialWeight);
    }

    [Fact]
    public void RecordActivation_IncrementsCount()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target");

        // Act
        connection.RecordActivation();
        connection.RecordActivation();
        connection.RecordActivation();

        // Assert
        connection.ActivationCount.Should().Be(3);
    }

    [Fact]
    public void RecordActivation_UpdatesLastActivationTime()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target");
        var timeBefore = DateTimeOffset.UtcNow;

        // Act
        connection.RecordActivation();

        // Assert
        connection.LastActivation.Should().BeOnOrAfter(timeBefore);
    }

    [Fact]
    public void HebbianUpdate_WeakeningWithNegativeWeight_StaysWithinBounds()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", -0.9, 0.1);

        // Act - multiple weakening updates
        for (int i = 0; i < 20; i++)
        {
            connection.HebbianUpdate(sourceActive: true, targetActive: false);
        }

        // Assert
        connection.Weight.Should().BeGreaterThanOrEqualTo(-1.0);
        connection.Weight.Should().BeLessThanOrEqualTo(1.0);
    }

    [Theory]
    [InlineData(0.0, 0.01)]
    [InlineData(0.5, 0.05)]
    [InlineData(-0.3, 0.02)]
    public void Constructor_WithVariousWeights_WorksCorrectly(double weight, double plasticityRate)
    {
        // Arrange & Act
        var connection = new WeightedConnection("source", "target", weight, plasticityRate);

        // Assert
        connection.Weight.Should().Be(weight);
        connection.PlasticityRate.Should().Be(plasticityRate);
    }

    [Fact]
    public void HebbianUpdate_ZeroWeight_CanIncrease()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", 0.0, 0.1);

        // Act
        connection.HebbianUpdate(sourceActive: true, targetActive: true);

        // Assert
        connection.Weight.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void HebbianUpdate_BothInactive_NoChange()
    {
        // Arrange
        var connection = new WeightedConnection("source", "target", 0.5, 0.1);
        var initialWeight = connection.Weight;

        // Act
        connection.HebbianUpdate(sourceActive: false, targetActive: false);

        // Assert
        connection.Weight.Should().Be(initialWeight);
    }
}
