// <copyright file="MerkleDagIntegrityTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Ouroboros.Network;
using Xunit;

namespace Ouroboros.Tests.Tests.Safety;

/// <summary>
/// Safety-critical adversarial integrity tests for MerkleDag.
/// Verifies hash verification, cycle detection, and concurrent operations.
/// </summary>
[Trait("Category", "Safety")]
public sealed class MerkleDagIntegrityTests
{
    #region Hash Verification Tests

    [Fact]
    public void VerifyIntegrity_ValidDag_ReturnsTrue()
    {
        // Arrange
        var dag = new MerkleDag();
        
        var node1 = MonadNode.FromPayload("TestType", new { value = "test1" });
        var node2 = MonadNode.FromPayload("TestType", new { value = "test2" }, ImmutableArray.Create(node1.Id));
        
        dag.AddNode(node1);
        dag.AddNode(node2);
        
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(node1.Id),
            node2.Id,
            "TestOperation",
            "{}",
            DateTimeOffset.UtcNow);
        
        dag.AddEdge(edge);

        // Act
        var result = dag.VerifyIntegrity();

        // Assert
        result.IsSuccess.Should().BeTrue("valid DAG should pass integrity check");
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void VerifyIntegrity_CorruptedNodeHash_ReturnsFalse()
    {
        // Arrange
        var dag = new MerkleDag();
        
        var node1 = MonadNode.FromPayload("TestType", new { value = "original" });
        dag.AddNode(node1);
        
        // Tamper with the node's hash using reflection to simulate adversarial corruption
        // Note: This uses reflection because MerkleDag doesn't expose a test API for corruption simulation.
        // Alternative would be [InternalsVisibleTo] with internal test hooks or a dedicated integrity-test API.
        var nodesField = typeof(MerkleDag).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance);
        var nodesDict = nodesField!.GetValue(dag) as Dictionary<Guid, MonadNode>;
        
        // Create a corrupted node with mismatched hash
        var corruptedNode = node1 with { Hash = "0000000000000000000000000000000000000000000000000000000000000000" };
        nodesDict![node1.Id] = corruptedNode;

        // Act
        var result = dag.VerifyIntegrity();

        // Assert
        result.IsSuccess.Should().BeFalse("corrupted node hash should fail integrity check");
        result.Error.Should().ContainEquivalentOf("hash verification failed");
    }

    [Fact]
    public void VerifyIntegrity_CorruptedEdgeHash_ReturnsFalse()
    {
        // Arrange
        var dag = new MerkleDag();
        
        var node1 = MonadNode.FromPayload("TestType", new { value = "test1" });
        var node2 = MonadNode.FromPayload("TestType", new { value = "test2" });
        
        dag.AddNode(node1);
        dag.AddNode(node2);
        
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(node1.Id),
            node2.Id,
            "TestOperation",
            "{}",
            DateTimeOffset.UtcNow);
        
        dag.AddEdge(edge);
        
        // Corrupt the edge hash
        var edgesField = typeof(MerkleDag).GetField("edges", BindingFlags.NonPublic | BindingFlags.Instance);
        var edgesDict = edgesField!.GetValue(dag) as Dictionary<Guid, TransitionEdge>;
        
        var corruptedEdge = edge with { Hash = "0000000000000000000000000000000000000000000000000000000000000000" };
        edgesDict![edge.Id] = corruptedEdge;

        // Act
        var result = dag.VerifyIntegrity();

        // Assert
        result.IsSuccess.Should().BeFalse("corrupted edge hash should fail integrity check");
        result.Error.Should().ContainEquivalentOf("hash verification failed");
    }

    [Fact]
    public void VerifyIntegrity_MissingNode_ReturnsFalse()
    {
        // Arrange
        var dag = new MerkleDag();
        
        var node1 = MonadNode.FromPayload("TestType", new { value = "test1" });
        var node2 = MonadNode.FromPayload("TestType", new { value = "test2" });
        
        // Only add node1, but create edge that references non-existent node2
        var addResult1 = dag.AddNode(node1);
        addResult1.IsSuccess.Should().BeTrue();
        
        var edge = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(node1.Id),
            node2.Id, // This node doesn't exist
            "TestOperation",
            "{}",
            DateTimeOffset.UtcNow);
        
        // Act
        var edgeResult = dag.AddEdge(edge);

        // Assert
        edgeResult.IsSuccess.Should().BeFalse("edge referencing missing node should be rejected");
        edgeResult.Error.Should().ContainEquivalentOf("does not exist");
    }

    [Fact]
    public void VerifyIntegrity_EmptyDag_ReturnsTrue()
    {
        // Arrange
        var dag = new MerkleDag();

        // Act
        var result = dag.VerifyIntegrity();

        // Assert
        result.IsSuccess.Should().BeTrue("empty DAG should be valid");
        result.Value.Should().BeTrue();
    }

    #endregion

    #region Cycle Detection Tests

    [Fact]
    public void TopologicalSort_AcyclicGraph_Succeeds()
    {
        // Arrange
        var dag = new MerkleDag();
        
        var node1 = MonadNode.FromPayload("TestType", new { value = "1" });
        var node2 = MonadNode.FromPayload("TestType", new { value = "2" });
        var node3 = MonadNode.FromPayload("TestType", new { value = "3" });
        
        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);
        
        // Create linear chain: 1 -> 2 -> 3
        var edge1 = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(node1.Id),
            node2.Id,
            "Op1",
            "{}",
            DateTimeOffset.UtcNow);
        
        var edge2 = new TransitionEdge(
            Guid.NewGuid(),
            ImmutableArray.Create(node2.Id),
            node3.Id,
            "Op2",
            "{}",
            DateTimeOffset.UtcNow);
        
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);

        // Act
        var result = dag.TopologicalSort();

        // Assert
        result.IsSuccess.Should().BeTrue("acyclic graph should sort successfully");
        result.Value.Should().HaveCount(3);
        
        // Verify ordering: node1 should come before node2, which should come before node3
        var sortedIds = result.Value.Select(n => n.Id).ToList();
        sortedIds.IndexOf(node1.Id).Should().BeLessThan(sortedIds.IndexOf(node2.Id));
        sortedIds.IndexOf(node2.Id).Should().BeLessThan(sortedIds.IndexOf(node3.Id));
    }

    [Fact]
    public void TopologicalSort_CyclicGraph_ReturnsFailure()
    {
        // Arrange - We cannot directly create cycles through AddEdge because it checks parent relationships
        // But we can test that the cycle detection works by using reflection to inject a cycle
        var dag = new MerkleDag();
        
        var node1 = MonadNode.FromPayload("TestType", new { value = "1" });
        var node2 = MonadNode.FromPayload("TestType", new { value = "2" });
        var node3 = MonadNode.FromPayload("TestType", new { value = "3" });
        
        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);
        
        // Create edges: 1 -> 2 -> 3
        var edge1 = new TransitionEdge(Guid.NewGuid(), ImmutableArray.Create(node1.Id), node2.Id, "Op1", "{}", DateTimeOffset.UtcNow);
        var edge2 = new TransitionEdge(Guid.NewGuid(), ImmutableArray.Create(node2.Id), node3.Id, "Op2", "{}", DateTimeOffset.UtcNow);
        
        dag.AddEdge(edge1);
        dag.AddEdge(edge2);
        
        // Now inject a cycle: 3 -> 1 (using reflection to bypass validation)
        var edge3 = new TransitionEdge(Guid.NewGuid(), ImmutableArray.Create(node3.Id), node1.Id, "Op3", "{}", DateTimeOffset.UtcNow);
        
        var edgesField = typeof(MerkleDag).GetField("edges", BindingFlags.NonPublic | BindingFlags.Instance);
        var nodeToIncomingField = typeof(MerkleDag).GetField("nodeToIncomingEdges", BindingFlags.NonPublic | BindingFlags.Instance);
        var nodeToOutgoingField = typeof(MerkleDag).GetField("nodeToOutgoingEdges", BindingFlags.NonPublic | BindingFlags.Instance);
        
        var edgesDict = edgesField!.GetValue(dag) as Dictionary<Guid, TransitionEdge>;
        var incomingDict = nodeToIncomingField!.GetValue(dag) as Dictionary<Guid, List<Guid>>;
        var outgoingDict = nodeToOutgoingField!.GetValue(dag) as Dictionary<Guid, List<Guid>>;
        
        edgesDict![edge3.Id] = edge3;
        incomingDict![node1.Id].Add(edge3.Id);
        outgoingDict![node3.Id].Add(edge3.Id);

        // Act
        var result = dag.TopologicalSort();

        // Assert
        result.IsSuccess.Should().BeFalse("cyclic graph should fail topological sort");
        result.Error.Should().ContainEquivalentOf("cycle");
    }

    [Fact]
    public void AddNode_WithMultipleParents_IsAccepted()
    {
        // Arrange
        var dag = new MerkleDag();
        
        // Create nodes with parent relationships
        var node1 = MonadNode.FromPayload("TestType", new { value = "1" });
        var node2 = MonadNode.FromPayload("TestType", new { value = "2" }, ImmutableArray.Create(node1.Id));
        var node3 = MonadNode.FromPayload("TestType", new { value = "3" }, ImmutableArray.Create(node2.Id));
        
        dag.AddNode(node1);
        dag.AddNode(node2);
        dag.AddNode(node3);
        
        // Create a node with multiple parents (DAG allows this, not a cycle)
        var node4 = MonadNode.FromPayload("TestType", new { value = "4" }, ImmutableArray.Create(node3.Id, node1.Id));
        
        // Act
        var result = dag.AddNode(node4);
        
        // Assert
        // The DAG structure allows multiple parents (converging paths)
        // This is not a cycle - cycles would be node3 pointing back to node1
        result.IsSuccess.Should().BeTrue("nodes with multiple parents should be accepted");
    }

    #endregion

    #region Content Addressing Tests

    [Fact]
    public void AddNode_SameContent_ProducesSameHash()
    {
        // Arrange
        var content = new { value = "deterministic", number = 42 };
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1000000);
        
        var node1 = new MonadNode(
            Guid.NewGuid(),
            "TestType",
            System.Text.Json.JsonSerializer.Serialize(content),
            timestamp,
            ImmutableArray<Guid>.Empty);
        
        var node2 = new MonadNode(
            Guid.NewGuid(),
            "TestType",
            System.Text.Json.JsonSerializer.Serialize(content),
            timestamp,
            ImmutableArray<Guid>.Empty);

        // Act & Assert
        node1.Hash.Should().Be(node2.Hash, "same content should produce same hash");
    }

    [Fact]
    public void AddNode_DifferentContent_ProducesDifferentHash()
    {
        // Arrange
        var content1 = new { value = "first" };
        var content2 = new { value = "second" };
        
        var node1 = MonadNode.FromPayload("TestType", content1);
        var node2 = MonadNode.FromPayload("TestType", content2);

        // Act & Assert
        node1.Hash.Should().NotBe(node2.Hash, "different content should produce different hash");
    }

    [Fact]
    public void NodeHash_IsStable_AcrossInstances()
    {
        // Arrange
        var content = new { value = "stable", array = new[] { 1, 2, 3 } };
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(2000000);
        var nodeId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        
        // Create two instances in different DAGs
        var dag1 = new MerkleDag();
        var dag2 = new MerkleDag();
        
        var node1 = new MonadNode(
            nodeId,
            "TestType",
            System.Text.Json.JsonSerializer.Serialize(content),
            timestamp,
            ImmutableArray<Guid>.Empty);
        
        var node2 = new MonadNode(
            nodeId,
            "TestType",
            System.Text.Json.JsonSerializer.Serialize(content),
            timestamp,
            ImmutableArray<Guid>.Empty);
        
        dag1.AddNode(node1);
        dag2.AddNode(node2);

        // Act & Assert
        node1.Hash.Should().Be(node2.Hash, "same data across different DAG instances should produce same hash");
        node1.VerifyHash().Should().BeTrue();
        node2.VerifyHash().Should().BeTrue();
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public void ConcurrentAddNodes_DoNotCorrupt()
    {
        // Arrange
        var dag = new MerkleDag();
        var nodeCount = 100;
        var threadCount = 10;

        // Act - Add 100 nodes from 10 threads concurrently
        var tasks = Enumerable.Range(0, nodeCount)
            .Select(i => Task.Run(() =>
            {
                var node = MonadNode.FromPayload($"Type{i % threadCount}", new { value = $"node{i}" });
                return dag.AddNode(node);
            }))
            .ToArray();

        Task.WaitAll(tasks);

        // Assert
        var results = tasks.Select(t => t.Result).ToList();
        var successfulAdds = results.Count(r => r.IsSuccess);
        
        successfulAdds.Should().BeGreaterThan(0, "at least some nodes should be added successfully");
        dag.NodeCount.Should().Be(successfulAdds, "node count should match successful additions");
        
        // Verify integrity after concurrent operations
        var integrityResult = dag.VerifyIntegrity();
        integrityResult.IsSuccess.Should().BeTrue("DAG should maintain integrity after concurrent additions");
    }

    [Fact]
    public void ConcurrentVerifyIntegrity_IsConsistent()
    {
        // Arrange
        var dag = new MerkleDag();
        
        // Add some nodes
        var nodes = Enumerable.Range(0, 50)
            .Select(i => MonadNode.FromPayload("TestType", new { value = $"node{i}" }))
            .ToList();
        
        foreach (var node in nodes)
        {
            dag.AddNode(node);
        }

        // Act - Verify integrity from multiple threads concurrently
        var verificationTasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => dag.VerifyIntegrity()))
            .ToArray();

        Task.WaitAll(verificationTasks);

        // Assert
        var results = verificationTasks.Select(t => t.Result).ToList();
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue("all verifications should succeed"));
        results.Should().AllSatisfy(r => r.Value.Should().BeTrue("all verifications should return true"));
        
        // All results should be consistent
        var firstResult = results[0].Value;
        results.Should().AllSatisfy(r => r.Value.Should().Be(firstResult, "verification results should be consistent"));
    }

    #endregion
}
