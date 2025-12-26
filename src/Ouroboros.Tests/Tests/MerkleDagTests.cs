// <copyright file="MerkleDagTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Xunit;

/// <summary>
/// Tests for the MerkleDag implementation.
/// Validates DAG operations, integrity, and traversal.
/// </summary>
public class MerkleDagTests
{
    [Fact]
    public void AddNode_ValidNode_Succeeds()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Test"));

        // Act
        var result = dag.AddNode(node);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dag.NodeCount.Should().Be(1);
    }

    [Fact]
    public void AddNode_DuplicateId_Fails()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Test"));
        dag.AddNode(node);

        // Act
        var result = dag.AddNode(node);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public void AddNode_WithMissingParent_Fails()
    {
        // Arrange
        var dag = new MerkleDag();
        var missingParentId = Guid.NewGuid();
        var node = MonadNode.FromReasoningState(
            new Draft("Test"),
            ImmutableArray.Create(missingParentId));

        // Act
        var result = dag.AddNode(node);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public void AddEdge_ValidEdge_Succeeds()
    {
        // Arrange
        var dag = new MerkleDag();
        var inputNode = MonadNode.FromReasoningState(new Draft("Input"));
        var outputNode = MonadNode.FromReasoningState(new Critique("Output"));
        dag.AddNode(inputNode);
        dag.AddNode(outputNode);

        var edge = TransitionEdge.CreateSimple(
            inputNode.Id,
            outputNode.Id,
            "UseCritique",
            new { Prompt = "Test prompt" });

        // Act
        var result = dag.AddEdge(edge);

        // Assert
        result.IsSuccess.Should().BeTrue();
        dag.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void AddEdge_WithMissingNode_Fails()
    {
        // Arrange
        var dag = new MerkleDag();
        var inputNode = MonadNode.FromReasoningState(new Draft("Input"));
        dag.AddNode(inputNode);

        var edge = TransitionEdge.CreateSimple(
            inputNode.Id,
            Guid.NewGuid(),
            "Test",
            new { });

        // Act
        var result = dag.AddEdge(edge);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public void GetNode_ExistingNode_ReturnsNode()
    {
        // Arrange
        var dag = new MerkleDag();
        var node = MonadNode.FromReasoningState(new Draft("Test"));
        dag.AddNode(node);

        // Act
        var result = dag.GetNode(node.Id);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Id.Should().Be(node.Id);
    }

    [Fact]
    public void GetRootNodes_ReturnsNodesWithNoParents()
    {
        // Arrange
        var dag = new MerkleDag();
        var rootNode = MonadNode.FromReasoningState(new Draft("Root"));
        var childNode = MonadNode.FromReasoningState(
            new Critique("Child"),
            ImmutableArray.Create(rootNode.Id));
        
        dag.AddNode(rootNode);
        dag.AddNode(childNode);

        // Act
        var roots = dag.GetRootNodes().ToList();

        // Assert
        roots.Should().HaveCount(1);
        roots[0].Id.Should().Be(rootNode.Id);
    }

    [Fact]
    public void TopologicalSort_OnAcyclicGraph_Succeeds()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Node1"));
        var node2 = MonadNode.FromReasoningState(new Critique("Node2"));
        var node3 = MonadNode.FromReasoningState(new FinalSpec("Node3"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        // Add edges to create the DAG structure
        var edge1 = TransitionEdge.CreateSimple(node1.Id, node2.Id, "Op1", new { });
        var edge2 = TransitionEdge.CreateSimple(node2.Id, node3.Id, "Op2", new { });
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        // Act
        var result = dag.TopologicalSort();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].Id.Should().Be(node1.Id);
        result.Value[2].Id.Should().Be(node3.Id);
    }

    [Fact]
    public void VerifyIntegrity_OnValidDag_Succeeds()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Node1"));
        var node2 = MonadNode.FromReasoningState(new Critique("Node2"));
        dag.AddNode(node1);
        dag.AddNode(node2);

        var edge = TransitionEdge.CreateSimple(
            node1.Id,
            node2.Id,
            "Test",
            new { });
        dag.AddEdge(edge);

        // Act
        var result = dag.VerifyIntegrity();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void GetNodesByType_FiltersCorrectly()
    {
        // Arrange
        var dag = new MerkleDag();
        var draft1 = MonadNode.FromReasoningState(new Draft("Draft1"));
        var draft2 = MonadNode.FromReasoningState(new Draft("Draft2"));
        var critique = MonadNode.FromReasoningState(new Critique("Critique1"));

        dag.AddNode(draft1);
        dag.AddNode(draft2);
        dag.AddNode(critique);

        // Act
        var drafts = dag.GetNodesByType("Draft").ToList();

        // Assert
        drafts.Should().HaveCount(2);
        drafts.All(n => n.TypeName == "Draft").Should().BeTrue();
    }
}
