// <copyright file="ConnectionTopologyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Autonomous;

using FluentAssertions;
using Ouroboros.Domain.Autonomous;
using Xunit;

/// <summary>
/// Comprehensive tests for the ConnectionTopology class.
/// Tests connection management, weight retrieval, and thread-safety.
/// </summary>
[Trait("Category", "Unit")]
public class ConnectionTopologyTests
{
    [Fact]
    public void SetConnection_AndGetWeight_RoundTrip()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        topology.SetConnection("neuron1", "neuron2", 0.8);
        var weight = topology.GetWeight("neuron1", "neuron2");

        // Assert
        weight.Should().Be(0.8);
    }

    [Fact]
    public void GetWeight_NoConnection_ReturnsDefaultOne()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        var weight = topology.GetWeight("neuron1", "neuron2");

        // Assert
        weight.Should().Be(1.0);
    }

    [Fact]
    public void SetConnection_UpdatesExistingConnection()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron2", 0.5);

        // Act
        topology.SetConnection("neuron1", "neuron2", 0.9);
        var weight = topology.GetWeight("neuron1", "neuron2");

        // Assert
        weight.Should().Be(0.9);
    }

    [Fact]
    public void GetConnection_ExistingConnection_ReturnsConnection()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron2", 0.7);

        // Act
        var connection = topology.GetConnection("neuron1", "neuron2");

        // Assert
        connection.Should().NotBeNull();
        connection!.Weight.Should().Be(0.7);
        connection.SourceNeuronId.Should().Be("neuron1");
        connection.TargetNeuronId.Should().Be("neuron2");
    }

    [Fact]
    public void GetConnection_NoConnection_ReturnsNull()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        var connection = topology.GetConnection("neuron1", "neuron2");

        // Assert
        connection.Should().BeNull();
    }

    [Fact]
    public void GetOutgoingConnections_ReturnsCorrectConnections()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron2", 0.5);
        topology.SetConnection("neuron1", "neuron3", 0.6);
        topology.SetConnection("neuron2", "neuron3", 0.7);

        // Act
        var outgoing = topology.GetOutgoingConnections("neuron1").ToList();

        // Assert
        outgoing.Should().HaveCount(2);
        outgoing.Should().Contain(c => c.TargetNeuronId == "neuron2" && Math.Abs(c.Weight - 0.5) < 1e-10);
        outgoing.Should().Contain(c => c.TargetNeuronId == "neuron3" && Math.Abs(c.Weight - 0.6) < 1e-10);
    }

    [Fact]
    public void GetIncomingConnections_ReturnsCorrectConnections()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron3", 0.5);
        topology.SetConnection("neuron2", "neuron3", 0.6);
        topology.SetConnection("neuron3", "neuron4", 0.7);

        // Act
        var incoming = topology.GetIncomingConnections("neuron3").ToList();

        // Assert
        incoming.Should().HaveCount(2);
        incoming.Should().Contain(c => c.SourceNeuronId == "neuron1" && Math.Abs(c.Weight - 0.5) < 1e-10);
        incoming.Should().Contain(c => c.SourceNeuronId == "neuron2" && Math.Abs(c.Weight - 0.6) < 1e-10);
    }

    [Fact]
    public void GetOutgoingConnections_NoConnections_ReturnsEmpty()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        var outgoing = topology.GetOutgoingConnections("neuron1").ToList();

        // Assert
        outgoing.Should().BeEmpty();
    }

    [Fact]
    public void ApplyHebbianUpdate_ExistingConnection_UpdatesWeight()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron2", 0.5, plasticityRate: 0.1);
        var initialWeight = topology.GetWeight("neuron1", "neuron2");

        // Act
        topology.ApplyHebbianUpdate("neuron1", "neuron2", sourceActive: true, targetActive: true);
        var updatedWeight = topology.GetWeight("neuron1", "neuron2");

        // Assert
        updatedWeight.Should().BeGreaterThan(initialWeight);
    }

    [Fact]
    public void ApplyHebbianUpdate_NoConnection_DoesNothing()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act & Assert - should not throw
        topology.ApplyHebbianUpdate("neuron1", "neuron2", sourceActive: true, targetActive: true);
    }

    [Fact]
    public void GetWeightSnapshot_ReturnsAllWeights()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron2", 0.5);
        topology.SetConnection("neuron2", "neuron3", 0.6);
        topology.SetConnection("neuron3", "neuron1", -0.4);

        // Act
        var snapshot = topology.GetWeightSnapshot();

        // Assert
        snapshot.Should().HaveCount(3);
        snapshot[("neuron1", "neuron2")].Should().Be(0.5);
        snapshot[("neuron2", "neuron3")].Should().Be(0.6);
        snapshot[("neuron3", "neuron1")].Should().Be(-0.4);
    }

    [Fact]
    public void AddInhibition_CreatesNegativeWeight()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        topology.AddInhibition("neuron1", "neuron2", -0.7);
        var weight = topology.GetWeight("neuron1", "neuron2");

        // Assert
        weight.Should().Be(-0.7);
    }

    [Fact]
    public void AddInhibition_DefaultStrength_CreatesNegativeWeight()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        topology.AddInhibition("neuron1", "neuron2");
        var weight = topology.GetWeight("neuron1", "neuron2");

        // Assert
        weight.Should().Be(-0.5);
    }

    [Fact]
    public void AddInhibition_ClampsToNegativeRange()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        topology.AddInhibition("neuron1", "neuron2", 0.5); // Positive value should be clamped to 0
        var weight = topology.GetWeight("neuron1", "neuron2");

        // Assert
        weight.Should().Be(0.0);
    }

    [Fact]
    public void ComputeNetInput_CalculatesWeightedSum()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "target", 0.5);
        topology.SetConnection("neuron2", "target", 0.3);
        topology.SetConnection("neuron3", "target", -0.2);

        var activations = new Dictionary<string, double>
        {
            { "neuron1", 1.0 },
            { "neuron2", 0.5 },
            { "neuron3", 0.8 },
        };

        // Act
        var netInput = topology.ComputeNetInput("target", id => activations[id]);

        // Assert
        // Expected: (0.5 * 1.0) + (0.3 * 0.5) + (-0.2 * 0.8) = 0.5 + 0.15 - 0.16 = 0.49
        netInput.Should().BeApproximately(0.49, 0.001);
    }

    [Fact]
    public void ComputeNetInput_NoIncomingConnections_ReturnsZero()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        var netInput = topology.ComputeNetInput("target", id => 1.0);

        // Assert
        netInput.Should().Be(0.0);
    }

    [Fact]
    public void ConcurrentSetConnection_ThreadSafe()
    {
        // Arrange
        var topology = new ConnectionTopology();
        var tasks = new List<Task>();

        // Act - multiple threads setting connections concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    topology.SetConnection($"neuron{index}", $"neuron{j}", 0.5 + (index * 0.01));
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - verify data integrity
        var snapshot = topology.GetWeightSnapshot();
        snapshot.Should().NotBeEmpty();
        snapshot.Values.Should().AllSatisfy(w => w.Should().BeInRange(-1.0, 1.0));
    }

    [Fact]
    public void GetWeightSnapshot_IsReadOnly()
    {
        // Arrange
        var topology = new ConnectionTopology();
        topology.SetConnection("neuron1", "neuron2", 0.5);

        // Act
        var snapshot = topology.GetWeightSnapshot();

        // Assert
        snapshot.Should().BeAssignableTo<IReadOnlyDictionary<(string, string), double>>();
    }

    [Fact]
    public void SetConnection_WithCustomPlasticityRate_WorksCorrectly()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        topology.SetConnection("neuron1", "neuron2", 0.5, plasticityRate: 0.05);
        var connection = topology.GetConnection("neuron1", "neuron2");

        // Assert
        connection.Should().NotBeNull();
        connection!.PlasticityRate.Should().Be(0.05);
    }

    [Fact]
    public void SetConnection_WithExtremeWeights_Clamps()
    {
        // Arrange
        var topology = new ConnectionTopology();

        // Act
        topology.SetConnection("neuron1", "neuron2", 5.0);
        topology.SetConnection("neuron3", "neuron4", -5.0);

        // Assert
        topology.GetWeight("neuron1", "neuron2").Should().Be(1.0);
        topology.GetWeight("neuron3", "neuron4").Should().Be(-1.0);
    }
}
