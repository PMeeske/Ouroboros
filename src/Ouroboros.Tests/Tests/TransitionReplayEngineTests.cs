// <copyright file="TransitionReplayEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Xunit;

/// <summary>
/// Tests for the TransitionReplayEngine implementation.
/// Validates replay and query functionality.
/// </summary>
[Trait("Category", "Unit")]
public class TransitionReplayEngineTests
{
    [Fact]
    public void ReplayPathToNode_WithValidPath_ReturnsCorrectSequence()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Node1"));
        var node2 = MonadNode.FromReasoningState(new Critique("Node2"));
        var node3 = MonadNode.FromReasoningState(new FinalSpec("Node3"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        var edge1 = TransitionEdge.CreateSimple(node1.Id, node2.Id, "Op1", new { });
        var edge2 = TransitionEdge.CreateSimple(node2.Id, node3.Id, "Op2", new { });
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        var engine = new TransitionReplayEngine(dag);

        // Act
        var result = engine.ReplayPathToNode(node3.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].OperationName.Should().Be("Op1");
        result.Value[1].OperationName.Should().Be("Op2");
    }

    [Fact]
    public void ReplayPathToNode_WithNonexistentNode_ReturnsError()
    {
        // Arrange
        var dag = new MerkleDag();
        var engine = new TransitionReplayEngine(dag);

        // Act
        var result = engine.ReplayPathToNode(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void GetNodeHistory_ReturnsOrderedTransitions()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("Start"));
        var node2 = MonadNode.FromReasoningState(new Critique("Middle"));
        var node3 = MonadNode.FromReasoningState(new FinalSpec("End"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        var edge1 = TransitionEdge.CreateSimple(node1.Id, node2.Id, "Step1", new { });
        var edge2 = TransitionEdge.CreateSimple(node2.Id, node3.Id, "Step2", new { });
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        var engine = new TransitionReplayEngine(dag);

        // Act
        var history = engine.GetNodeHistory(node3.Id);

        // Assert
        history.Should().HaveCount(2);
        history[0].OperationName.Should().Be("Step1");
        history[1].OperationName.Should().Be("Step2");
    }

    [Fact]
    public void QueryTransitions_FiltersCorrectly()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("N1"));
        var node2 = MonadNode.FromReasoningState(new Critique("N2"));
        var node3 = MonadNode.FromReasoningState(new FinalSpec("N3"));

        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);

        var edge1 = TransitionEdge.CreateSimple(
            node1.Id, node2.Id, "TestOp", new { }, confidence: 0.9);
        var edge2 = TransitionEdge.CreateSimple(
            node2.Id, node3.Id, "OtherOp", new { }, confidence: 0.5);
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        var engine = new TransitionReplayEngine(dag);

        // Act
        var highConfidence = engine.QueryTransitions(e => e.Confidence > 0.7).ToList();

        // Assert
        highConfidence.Should().HaveCount(1);
        highConfidence[0].OperationName.Should().Be("TestOp");
    }

    [Fact]
    public void QueryNodes_FiltersCorrectly()
    {
        // Arrange
        var dag = new MerkleDag();
        var draft1 = MonadNode.FromReasoningState(new Draft("Draft1"));
        var draft2 = MonadNode.FromReasoningState(new Draft("Draft2"));
        var critique = MonadNode.FromReasoningState(new Critique("Critique1"));

        dag.AddNode(draft1);
        dag.AddNode(draft2);
        dag.AddNode(critique);

        var engine = new TransitionReplayEngine(dag);

        // Act
        var drafts = engine.QueryNodes(n => n.TypeName == "Draft").ToList();

        // Assert
        drafts.Should().HaveCount(2);
        drafts.All(n => n.TypeName == "Draft").Should().BeTrue();
    }

    [Fact]
    public void GetTransitionsInTimeRange_FiltersCorrectly()
    {
        // Arrange
        var dag = new MerkleDag();
        var node1 = MonadNode.FromReasoningState(new Draft("N1"));
        var node2 = MonadNode.FromReasoningState(new Critique("N2"));

        dag.AddNode(node1);
        dag.AddNode(node2);

        var now = DateTimeOffset.UtcNow;
        var edge = TransitionEdge.CreateSimple(node1.Id, node2.Id, "Op", new { });
        dag.AddEdge(edge);

        var engine = new TransitionReplayEngine(dag);

        // Act
        var transitions = engine.GetTransitionsInTimeRange(
            now.AddMinutes(-1),
            now.AddMinutes(1)).ToList();

        // Assert
        transitions.Should().HaveCount(1);
    }
}
