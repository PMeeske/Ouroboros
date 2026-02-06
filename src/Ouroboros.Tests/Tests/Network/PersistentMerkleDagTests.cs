// <copyright file="PersistentMerkleDagTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Network;

using System.Collections.Immutable;
using System.IO;
using FluentAssertions;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Ouroboros.Network.Persistence;
using Xunit;

/// <summary>
/// Tests for the PersistentMerkleDag implementation.
/// Validates WAL-based persistence, recovery, and integrity verification.
/// </summary>
[Trait("Category", "Unit")]
public class PersistentMerkleDagTests : IDisposable
{
    private readonly List<string> tempFiles;

    public PersistentMerkleDagTests()
    {
        this.tempFiles = new List<string>();
    }

    [Fact]
    public async Task AddNode_And_Restore_RoundTrip_Succeeds()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var node1 = MonadNode.FromReasoningState(new Draft("First draft"));
        var node2 = MonadNode.FromReasoningState(new Draft("Second draft"));

        // Act - Add nodes and dispose
        var persistence1 = new FileWalPersistence(walPath);
        var dag1 = PersistentMerkleDag.Create(persistence1);

        var result1 = await dag1.AddNodeAsync(node1);
        var result2 = await dag1.AddNodeAsync(node2);
        await dag1.FlushAsync();
        await dag1.DisposeAsync();

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        // Act - Restore from WAL
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);

        // Assert
        restoreResult.IsSuccess.Should().BeTrue();
        var dag2 = restoreResult.Value;

        dag2.NodeCount.Should().Be(2);
        dag2.GetNode(node1.Id).HasValue.Should().BeTrue();
        dag2.GetNode(node2.Id).HasValue.Should().BeTrue();

        await dag2.DisposeAsync();
    }

    [Fact]
    public async Task AddEdge_And_Restore_RoundTrip_Succeeds()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var inputNode = MonadNode.FromReasoningState(new Draft("Input"));
        var outputNode = MonadNode.FromReasoningState(new Critique("Output"));
        var edge = TransitionEdge.CreateSimple(
            inputNode.Id,
            outputNode.Id,
            "UseCritique",
            new { Prompt = "Critique this" });

        // Act - Build DAG and dispose
        var persistence1 = new FileWalPersistence(walPath);
        var dag1 = PersistentMerkleDag.Create(persistence1);

        await dag1.AddNodeAsync(inputNode);
        await dag1.AddNodeAsync(outputNode);
        await dag1.AddEdgeAsync(edge);
        await dag1.FlushAsync();
        await dag1.DisposeAsync();

        // Act - Restore from WAL
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);

        // Assert
        restoreResult.IsSuccess.Should().BeTrue();
        var dag2 = restoreResult.Value;

        dag2.NodeCount.Should().Be(2);
        dag2.EdgeCount.Should().Be(1);
        dag2.GetEdge(edge.Id).HasValue.Should().BeTrue();

        var restoredEdge = dag2.GetEdge(edge.Id).Value;
        restoredEdge.OperationName.Should().Be("UseCritique");
        restoredEdge.InputIds.Should().Contain(inputNode.Id);
        restoredEdge.OutputId.Should().Be(outputNode.Id);

        await dag2.DisposeAsync();
    }

    [Fact]
    public async Task VerifyIntegrity_AfterRestore_Succeeds()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var node1 = MonadNode.FromReasoningState(new Draft("First"));
        var node2 = MonadNode.FromReasoningState(
            new Critique("Second"),
            ImmutableArray.Create(node1.Id));

        // Build DAG with parent-child relationship
        var persistence1 = new FileWalPersistence(walPath);
        var dag1 = PersistentMerkleDag.Create(persistence1);
        await dag1.AddNodeAsync(node1);
        await dag1.AddNodeAsync(node2);
        await dag1.FlushAsync();
        await dag1.DisposeAsync();

        // Act - Restore and verify
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);

        // Assert
        restoreResult.IsSuccess.Should().BeTrue();
        var dag2 = restoreResult.Value;

        var integrityResult = dag2.VerifyIntegrity();
        integrityResult.IsSuccess.Should().BeTrue();
        integrityResult.Value.Should().BeTrue();

        await dag2.DisposeAsync();
    }

    [Fact]
    public async Task RestoreAsync_EmptyWal_ReturnsEmptyDag()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var persistence = new FileWalPersistence(walPath);

        // Act
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence);

        // Assert
        restoreResult.IsSuccess.Should().BeTrue();
        var dag = restoreResult.Value;

        dag.NodeCount.Should().Be(0);
        dag.EdgeCount.Should().Be(0);

        await dag.DisposeAsync();
    }

    [Fact]
    public async Task RestoreAsync_CorruptedWalEntry_ReturnsError()
    {
        // Arrange
        var walPath = this.GetTempWalPath();

        // Create a valid entry first
        var node = MonadNode.FromReasoningState(new Draft("Valid"));
        var persistence1 = new FileWalPersistence(walPath);
        await persistence1.AppendNodeAsync(node);
        await persistence1.FlushAsync();
        await persistence1.DisposeAsync();

        // Manually corrupt the WAL with an entry that references non-existent parent
        var invalidNode = MonadNode.FromReasoningState(
            new Draft("Invalid"),
            ImmutableArray.Create(Guid.NewGuid())); // Non-existent parent
        var entry = new WalEntry(
            WalEntryType.AddNode,
            DateTimeOffset.UtcNow,
            System.Text.Json.JsonSerializer.Serialize(invalidNode));
        await File.AppendAllTextAsync(
            walPath,
            System.Text.Json.JsonSerializer.Serialize(entry) + "\n");

        // Act
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);

        // Assert - Should fail due to corrupted entry
        restoreResult.IsFailure.Should().BeTrue();
        restoreResult.Error.Should().Contain("error");

        await persistence2.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentWrites_MaintainConsistency()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var persistence = new FileWalPersistence(walPath);
        var dag = PersistentMerkleDag.Create(persistence);

        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var node = MonadNode.FromReasoningState(new Draft($"Draft {i}"));
            await dag.AddNodeAsync(node);
        });

        // Act
        await Task.WhenAll(tasks);
        await dag.FlushAsync();
        await dag.DisposeAsync();

        // Act - Restore and verify
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);

        // Assert
        restoreResult.IsSuccess.Should().BeTrue();
        var restoredDag = restoreResult.Value;
        restoredDag.NodeCount.Should().Be(10);

        await restoredDag.DisposeAsync();
    }

    [Fact]
    public async Task TopologicalSort_AfterRestore_PreservesOrdering()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var root = MonadNode.FromReasoningState(new Draft("Root"));
        var child1 = MonadNode.FromReasoningState(
            new Critique("Child1"),
            ImmutableArray.Create(root.Id));
        var child2 = MonadNode.FromReasoningState(
            new FinalSpec("Child2"),
            ImmutableArray.Create(root.Id));

        // Build DAG
        var persistence1 = new FileWalPersistence(walPath);
        var dag1 = PersistentMerkleDag.Create(persistence1);
        await dag1.AddNodeAsync(root);
        await dag1.AddNodeAsync(child1);
        await dag1.AddNodeAsync(child2);
        await dag1.FlushAsync();
        await dag1.DisposeAsync();

        // Act - Restore and sort
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);
        var dag2 = restoreResult.Value;

        var sortResult = dag2.TopologicalSort();

        // Assert
        sortResult.IsSuccess.Should().BeTrue();
        var sorted = sortResult.Value;

        sorted.Should().HaveCount(3);
        sorted[0].Id.Should().Be(root.Id); // Root should be first
        sorted.Skip(1).Select(n => n.Id).Should().Contain(new[] { child1.Id, child2.Id });

        await dag2.DisposeAsync();
    }

    [Fact]
    public async Task GetRootNodes_AfterRestore_ReturnsCorrectNodes()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var root1 = MonadNode.FromReasoningState(new Draft("Root1"));
        var root2 = MonadNode.FromReasoningState(new Draft("Root2"));
        var child = MonadNode.FromReasoningState(
            new Critique("Child"),
            ImmutableArray.Create(root1.Id));

        var persistence1 = new FileWalPersistence(walPath);
        var dag1 = PersistentMerkleDag.Create(persistence1);
        await dag1.AddNodeAsync(root1);
        await dag1.AddNodeAsync(root2);
        await dag1.AddNodeAsync(child);
        await dag1.FlushAsync();
        await dag1.DisposeAsync();

        // Act
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);
        var dag2 = restoreResult.Value;

        var rootNodes = dag2.GetRootNodes().ToList();

        // Assert
        rootNodes.Should().HaveCount(2);
        rootNodes.Select(n => n.Id).Should().Contain(new[] { root1.Id, root2.Id });

        await dag2.DisposeAsync();
    }

    [Fact]
    public async Task Compaction_ProducesEquivalentDag()
    {
        // Arrange
        var walPath = this.GetTempWalPath();
        var node1 = MonadNode.FromReasoningState(new Draft("First"));
        var node2 = MonadNode.FromReasoningState(new Draft("Second"));
        var edge = TransitionEdge.CreateSimple(
            node1.Id,
            node2.Id,
            "Transform",
            new { Param = "value" });

        // Build original DAG
        var persistence1 = new FileWalPersistence(walPath);
        var dag1 = PersistentMerkleDag.Create(persistence1);
        await dag1.AddNodeAsync(node1);
        await dag1.AddNodeAsync(node2);
        await dag1.AddEdgeAsync(edge);
        await dag1.FlushAsync();
        await dag1.DisposeAsync();

        var originalSize = new FileInfo(walPath).Length;

        // Act - Compact
        var compactResult = await WalCompactor.CompactAsync(walPath);

        // Assert
        compactResult.IsSuccess.Should().BeTrue();

        // Restore and verify equivalence
        var persistence2 = new FileWalPersistence(walPath);
        var restoreResult = await PersistentMerkleDag.RestoreAsync(persistence2);
        var dag2 = restoreResult.Value;

        dag2.NodeCount.Should().Be(2);
        dag2.EdgeCount.Should().Be(1);
        dag2.GetNode(node1.Id).HasValue.Should().BeTrue();
        dag2.GetNode(node2.Id).HasValue.Should().BeTrue();
        dag2.GetEdge(edge.Id).HasValue.Should().BeTrue();

        await dag2.DisposeAsync();
    }

    public void Dispose()
    {
        // Clean up all temp files
        foreach (var file in this.tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }

                // Also clean up backup files
                var backupFile = file + ".backup";
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        this.tempFiles.Clear();
    }

    private string GetTempWalPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"persistent-dag-test-{Guid.NewGuid()}.wal");
        this.tempFiles.Add(path);
        return path;
    }
}
