// <copyright file="NetworkStateProjectorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Xunit;

/// <summary>
/// Tests for the NetworkStateProjector implementation.
/// Validates global state projection and snapshot functionality.
/// </summary>
[Trait("Category", "Unit")]
public class NetworkStateProjectorTests
{
    [Fact]
    public void ProjectCurrentState_WithEmptyDag_ReturnsEmptyState()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.Should().NotBeNull();
        state.TotalNodes.Should().Be(0);
        state.TotalTransitions.Should().Be(0);
        state.Epoch.Should().Be(0);
    }

    [Fact]
    public void ProjectCurrentState_CountsNodesByType()
    {
        // Arrange
        var dag = new MerkleDag();
        dag.AddNode(MonadNode.FromReasoningState(new Draft("Draft1")));
        dag.AddNode(MonadNode.FromReasoningState(new Draft("Draft2")));
        dag.AddNode(MonadNode.FromReasoningState(new Critique("Critique1")));

        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.TotalNodes.Should().Be(3);
        state.NodeCountByType["Draft"].Should().Be(2);
        state.NodeCountByType["Critique"].Should().Be(1);
    }

    [Fact]
    public void ProjectCurrentState_CalculatesAverageConfidence()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Draft"));
        var node2 = MonadNode.FromReasoningState(new Critique("Critique"));
        dag.AddNode(node1);
        dag.AddNode(node2);

        var edge1 = TransitionEdge.CreateSimple(
            node1.Id, node2.Id, "Test", new { }, confidence: 0.8);
        var edge2 = TransitionEdge.CreateSimple(
            node1.Id, node2.Id, "Test2", new { }, confidence: 0.6);

        var edge1WithId = new TransitionEdge(
            Guid.NewGuid(),
            edge1.InputIds,
            edge1.OutputId,
            edge1.OperationName,
            edge1.OperationSpecJson,
            edge1.CreatedAt,
            0.8);

        var edge2WithId = new TransitionEdge(
            Guid.NewGuid(),
            edge2.InputIds,
            edge2.OutputId,
            edge2.OperationName,
            edge2.OperationSpecJson,
            edge2.CreatedAt,
            0.6);

        dag.AddEdge(edge1WithId);
        dag.AddEdge(edge2WithId);

        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.AverageConfidence.Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public void CreateSnapshot_IncrementsEpoch()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Act
        var snapshot1 = projector.CreateSnapshot();
        var snapshot2 = projector.CreateSnapshot();

        // Assert
        snapshot1.Epoch.Should().Be(0);
        snapshot2.Epoch.Should().Be(1);
        projector.CurrentEpoch.Should().Be(2);
    }

    [Fact]
    public void GetSnapshot_RetrievesCorrectSnapshot()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        var snapshot = projector.CreateSnapshot();

        // Act
        var retrieved = projector.GetSnapshot(0);

        // Assert
        retrieved.HasValue.Should().BeTrue();
        retrieved.Value!.Epoch.Should().Be(0);
    }

    [Fact]
    public void GetLatestSnapshot_ReturnsNewestSnapshot()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        projector.CreateSnapshot();
        projector.CreateSnapshot();
        var latest = projector.CreateSnapshot();

        // Act
        var retrieved = projector.GetLatestSnapshot();

        // Assert
        retrieved.HasValue.Should().BeTrue();
        retrieved.Value!.Epoch.Should().Be(latest.Epoch);
    }

    [Fact]
    public void ComputeDelta_CalculatesCorrectDifference()
    {
        // Arrange
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        var snapshot1 = projector.CreateSnapshot();

        dag.AddNode(MonadNode.FromReasoningState(new Draft("New")));
        var snapshot2 = projector.CreateSnapshot();

        // Act
        var deltaResult = projector.ComputeDelta(snapshot1.Epoch, snapshot2.Epoch);

        // Assert
        deltaResult.IsSuccess.Should().BeTrue();
        deltaResult.Value.NodeDelta.Should().Be(1);
        deltaResult.Value.TransitionDelta.Should().Be(0);
    }

    [Fact]
    public void ProjectCurrentState_IdentifiesRootAndLeafNodes()
    {
        // Arrange
        var dag = new MerkleDag();
        var root = MonadNode.FromReasoningState(new Draft("Root"));
        var middle = MonadNode.FromReasoningState(
            new Critique("Middle"),
            ImmutableArray.Create(root.Id));
        var leaf = MonadNode.FromReasoningState(
            new FinalSpec("Leaf"),
            ImmutableArray.Create(middle.Id));

        dag.AddNode(root);
        dag.AddNode(middle);
        dag.AddNode(leaf);

        var projector = new NetworkStateProjector(dag);

        // Act
        var state = projector.ProjectCurrentState();

        // Assert
        state.RootNodeIds.Should().Contain(root.Id);
        state.LeafNodeIds.Should().Contain(leaf.Id);
    }
}
