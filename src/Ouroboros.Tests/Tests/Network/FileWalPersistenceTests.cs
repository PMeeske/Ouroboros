// <copyright file="FileWalPersistenceTests.cs" company="PlaceholderCompany">
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
/// Tests for the FileWalPersistence implementation.
/// Validates WAL append, replay, flush, and error handling.
/// </summary>
[Trait("Category", "Unit")]
public class FileWalPersistenceTests : IDisposable
{
    private readonly string testWalPath;
    private readonly List<string> tempFiles;

    public FileWalPersistenceTests()
    {
        this.tempFiles = new List<string>();
        this.testWalPath = this.GetTempWalPath();
    }

    [Fact]
    public async Task AppendNode_AndReplay_RoundTrip_Succeeds()
    {
        // Arrange
        var node = MonadNode.FromReasoningState(new Draft("Test draft content"));
        await using var persistence = new FileWalPersistence(this.testWalPath);

        // Act - Append
        await persistence.AppendNodeAsync(node);
        await persistence.FlushAsync();

        // Act - Replay
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCount(1);
        entries[0].Type.Should().Be(WalEntryType.AddNode);
        entries[0].PayloadJson.Should().Contain("Test draft content");

        // Verify deserialization
        var deserializedNode = System.Text.Json.JsonSerializer.Deserialize<MonadNode>(entries[0].PayloadJson);
        deserializedNode.Should().NotBeNull();
        deserializedNode!.Id.Should().Be(node.Id);
        deserializedNode.TypeName.Should().Be(node.TypeName);
    }

    [Fact]
    public async Task AppendEdge_AndReplay_RoundTrip_Succeeds()
    {
        // Arrange
        var inputId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var edge = TransitionEdge.CreateSimple(
            inputId,
            outputId,
            "TestOperation",
            new { Prompt = "Test prompt" });

        await using var persistence = new FileWalPersistence(this.testWalPath);

        // Act - Append
        await persistence.AppendEdgeAsync(edge);
        await persistence.FlushAsync();

        // Act - Replay
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCount(1);
        entries[0].Type.Should().Be(WalEntryType.AddEdge);
        entries[0].PayloadJson.Should().Contain("TestOperation");

        // Verify deserialization
        var deserializedEdge = System.Text.Json.JsonSerializer.Deserialize<TransitionEdge>(entries[0].PayloadJson);
        deserializedEdge.Should().NotBeNull();
        deserializedEdge!.Id.Should().Be(edge.Id);
        deserializedEdge.OperationName.Should().Be(edge.OperationName);
    }

    [Fact]
    public async Task FlushAsync_EnsuresDurability()
    {
        // Arrange
        var node = MonadNode.FromReasoningState(new Draft("Test"));
        var persistence1 = new FileWalPersistence(this.testWalPath);

        // Act - Write and flush in first instance
        await persistence1.AppendNodeAsync(node);
        await persistence1.FlushAsync();
        await persistence1.DisposeAsync();

        // Act - Read from second instance (verifies durability)
        await using var persistence2 = new FileWalPersistence(this.testWalPath);
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence2.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCount(1);
        entries[0].Type.Should().Be(WalEntryType.AddNode);
    }

    [Fact]
    public async Task ReplayAsync_MissingFile_ReturnsEmpty()
    {
        // Arrange
        var nonExistentPath = this.GetTempWalPath();
        await using var persistence = new FileWalPersistence(nonExistentPath);

        // Act
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplayAsync_EmptyFile_ReturnsEmpty()
    {
        // Arrange
        var emptyFilePath = this.GetTempWalPath();
        await File.WriteAllTextAsync(emptyFilePath, string.Empty);
        await using var persistence = new FileWalPersistence(emptyFilePath);

        // Act
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplayAsync_CorruptedEntry_SkipsAndContinues()
    {
        // Arrange
        var node = MonadNode.FromReasoningState(new Draft("Valid entry"));
        await using var persistence = new FileWalPersistence(this.testWalPath);

        await persistence.AppendNodeAsync(node);
        await persistence.FlushAsync();

        // Manually append corrupted entry
        await File.AppendAllTextAsync(this.testWalPath, "{ corrupted json }\n");

        // Append another valid entry
        var node2 = MonadNode.FromReasoningState(new Draft("Second valid entry"));
        await persistence.AppendNodeAsync(node2);
        await persistence.FlushAsync();

        // Act
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert - Should have 2 valid entries, corrupted one skipped
        entries.Should().HaveCount(2);
        entries[0].PayloadJson.Should().Contain("Valid entry");
        entries[1].PayloadJson.Should().Contain("Second valid entry");
    }

    [Fact]
    public async Task MultipleAppends_PreservesOrder()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(this.testWalPath);
        var nodes = new[]
        {
            MonadNode.FromReasoningState(new Draft("First")),
            MonadNode.FromReasoningState(new Draft("Second")),
            MonadNode.FromReasoningState(new Draft("Third")),
        };

        // Act
        foreach (var node in nodes)
        {
            await persistence.AppendNodeAsync(node);
        }

        await persistence.FlushAsync();

        // Replay
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence.ReplayAsync())
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCount(3);
        entries[0].PayloadJson.Should().Contain("First");
        entries[1].PayloadJson.Should().Contain("Second");
        entries[2].PayloadJson.Should().Contain("Third");
    }

    [Fact]
    public async Task DisposeAsync_ClosesFileCorrectly()
    {
        // Arrange
        var node = MonadNode.FromReasoningState(new Draft("Test"));
        var persistence = new FileWalPersistence(this.testWalPath);

        // Act
        await persistence.AppendNodeAsync(node);
        await persistence.DisposeAsync();

        // Assert - Should be able to open file again
        await using var persistence2 = new FileWalPersistence(this.testWalPath);
        var entries = new List<WalEntry>();
        await foreach (var entry in persistence2.ReplayAsync())
        {
            entries.Add(entry);
        }

        entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task AppendNodeAsync_NullNode_ThrowsArgumentNullException()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(this.testWalPath);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => persistence.AppendNodeAsync(null!));
    }

    [Fact]
    public async Task AppendEdgeAsync_NullEdge_ThrowsArgumentNullException()
    {
        // Arrange
        await using var persistence = new FileWalPersistence(this.testWalPath);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => persistence.AppendEdgeAsync(null!));
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
        var path = Path.Combine(Path.GetTempPath(), $"wal-test-{Guid.NewGuid()}.wal");
        this.tempFiles.Add(path);
        return path;
    }
}
