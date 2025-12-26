// <copyright file="EventStoreTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.Persistence;
using Ouroboros.Domain.States;
using Xunit;

/// <summary>
/// Tests for event store functionality.
/// </summary>
public class EventStoreTests
{
    [Fact]
    public async Task AppendEventsAsync_NewBranch_StoresEventsAndReturnsVersion()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        var events = new List<PipelineEvent>
        {
            CreateTestEvent(),
            CreateTestEvent(),
        };

        // Act
        var version = await store.AppendEventsAsync(branchId, events);

        // Assert
        version.Should().Be(1); // 2 events: version 0, 1
    }

    [Fact]
    public async Task GetEventsAsync_ExistingBranch_ReturnsAllEvents()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        var events = new List<PipelineEvent>
        {
            CreateTestEvent(),
            CreateTestEvent(),
            CreateTestEvent(),
        };
        await store.AppendEventsAsync(branchId, events);

        // Act
        var retrievedEvents = await store.GetEventsAsync(branchId);

        // Assert
        retrievedEvents.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetEventsAsync_NonExistentBranch_ReturnsEmptyList()
    {
        // Arrange
        var store = new InMemoryEventStore();

        // Act
        var events = await store.GetEventsAsync("non-existent");

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_WithFromVersion_ReturnsEventsFromVersion()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        var events = new List<PipelineEvent>
        {
            CreateTestEvent(),
            CreateTestEvent(),
            CreateTestEvent(),
            CreateTestEvent(),
        };
        await store.AppendEventsAsync(branchId, events);

        // Act
        var retrievedEvents = await store.GetEventsAsync(branchId, fromVersion: 2);

        // Assert
        retrievedEvents.Should().HaveCount(2); // Events at version 2 and 3
    }

    [Fact]
    public async Task GetVersionAsync_ExistingBranch_ReturnsCurrentVersion()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        var events = new List<PipelineEvent>
        {
            CreateTestEvent(),
            CreateTestEvent(),
        };
        await store.AppendEventsAsync(branchId, events);

        // Act
        var version = await store.GetVersionAsync(branchId);

        // Assert
        version.Should().Be(1);
    }

    [Fact]
    public async Task GetVersionAsync_NonExistentBranch_ReturnsMinusOne()
    {
        // Arrange
        var store = new InMemoryEventStore();

        // Act
        var version = await store.GetVersionAsync("non-existent");

        // Assert
        version.Should().Be(-1);
    }

    [Fact]
    public async Task BranchExistsAsync_ExistingBranch_ReturnsTrue()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        await store.AppendEventsAsync(branchId, new[] { CreateTestEvent() });

        // Act
        var exists = await store.BranchExistsAsync(branchId);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task BranchExistsAsync_NonExistentBranch_ReturnsFalse()
    {
        // Arrange
        var store = new InMemoryEventStore();

        // Act
        var exists = await store.BranchExistsAsync("non-existent");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AppendEventsAsync_WithCorrectExpectedVersion_Succeeds()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        await store.AppendEventsAsync(branchId, new[] { CreateTestEvent() });

        // Act
        var newVersion = await store.AppendEventsAsync(
            branchId,
            new[] { CreateTestEvent() },
            expectedVersion: 0);

        // Assert
        newVersion.Should().Be(1);
    }

    [Fact]
    public async Task AppendEventsAsync_WithWrongExpectedVersion_ThrowsConcurrencyException()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        await store.AppendEventsAsync(branchId, new[] { CreateTestEvent() });

        // Act
        Func<Task> act = async () => await store.AppendEventsAsync(
            branchId,
            new[] { CreateTestEvent() },
            expectedVersion: 5); // Wrong version

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>()
            .Where(e => e.BranchId == branchId && e.ExpectedVersion == 5 && e.ActualVersion == 0);
    }

    [Fact]
    public async Task DeleteBranchAsync_ExistingBranch_RemovesBranch()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        await store.AppendEventsAsync(branchId, new[] { CreateTestEvent() });

        // Act
        await store.DeleteBranchAsync(branchId);

        // Assert
        var exists = await store.BranchExistsAsync(branchId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AppendEventsAsync_EmptyList_ReturnsCurrentVersion()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";
        await store.AppendEventsAsync(branchId, new[] { CreateTestEvent() });

        // Act
        var version = await store.AppendEventsAsync(branchId, Array.Empty<PipelineEvent>());

        // Assert
        version.Should().Be(0); // Still version 0 since no events were added
    }

    [Fact]
    public async Task AppendEventsAsync_MultipleConcurrentWrites_MaintainsConsistency()
    {
        // Arrange
        var store = new InMemoryEventStore();
        var branchId = "test-branch";

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                await store.AppendEventsAsync(branchId, new[] { CreateTestEvent() });
            }));

        await Task.WhenAll(tasks);

        // Assert
        var events = await store.GetEventsAsync(branchId);
        events.Should().HaveCount(10);

        var version = await store.GetVersionAsync(branchId);
        version.Should().Be(9); // 10 events: versions 0-9
    }

    private static PipelineEvent CreateTestEvent()
    {
        return new ReasoningStep(
            Guid.NewGuid(),
            "Draft",
            new Draft("Test content"),
            DateTime.UtcNow,
            "Test prompt",
            null);
    }
}
