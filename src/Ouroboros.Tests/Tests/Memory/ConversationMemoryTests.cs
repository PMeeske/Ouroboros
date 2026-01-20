// <copyright file="ConversationMemoryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Memory;

using FluentAssertions;
using Ouroboros.Core.Memory;
using Xunit;

/// <summary>
/// Unit tests for ConversationMemory and MemoryContext.
/// </summary>
[Trait("Category", "Unit")]
public class ConversationMemoryTests
{
    [Fact]
    public void Constructor_WithMaxTurns_InitializesCorrectly()
    {
        // Arrange & Act
        var memory = new ConversationMemory(maxTurns: 5);

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithDefaultMaxTurns_InitializesCorrectly()
    {
        // Arrange & Act
        var memory = new ConversationMemory();

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public void AddTurn_SingleTurn_AddsTurn()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        memory.AddTurn("Hello", "Hi there");

        // Assert
        var turns = memory.GetTurns();
        turns.Should().HaveCount(1);
        turns[0].HumanInput.Should().Be("Hello");
        turns[0].AiResponse.Should().Be("Hi there");
    }

    [Fact]
    public void AddTurn_MultipleTurns_AddsAllTurns()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        memory.AddTurn("First", "Response 1");
        memory.AddTurn("Second", "Response 2");
        memory.AddTurn("Third", "Response 3");

        // Assert
        memory.GetTurns().Should().HaveCount(3);
    }

    [Fact]
    public void AddTurn_ExceedsMaxTurns_RemovesOldestTurn()
    {
        // Arrange
        var memory = new ConversationMemory(maxTurns: 2);

        // Act
        memory.AddTurn("First", "Response 1");
        memory.AddTurn("Second", "Response 2");
        memory.AddTurn("Third", "Response 3");

        // Assert
        var turns = memory.GetTurns();
        turns.Should().HaveCount(2);
        turns[0].HumanInput.Should().Be("Second");
        turns[1].HumanInput.Should().Be("Third");
    }

    [Fact]
    public void GetFormattedHistory_EmptyMemory_ReturnsEmptyString()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        var history = memory.GetFormattedHistory();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetFormattedHistory_WithTurns_ReturnsFormattedString()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Hello", "Hi there");
        memory.AddTurn("How are you?", "I'm good");

        // Act
        var history = memory.GetFormattedHistory();

        // Assert
        history.Should().Contain("Human: Hello");
        history.Should().Contain("AI: Hi there");
        history.Should().Contain("Human: How are you?");
        history.Should().Contain("AI: I'm good");
    }

    [Fact]
    public void GetFormattedHistory_WithCustomPrefixes_UsesCustomPrefixes()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Hello", "Hi");

        // Act
        var history = memory.GetFormattedHistory("User", "Assistant");

        // Assert
        history.Should().Contain("User: Hello");
        history.Should().Contain("Assistant: Hi");
    }

    [Fact]
    public void Clear_WithTurns_RemovesAllTurns()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("First", "Response 1");
        memory.AddTurn("Second", "Response 2");

        // Act
        memory.Clear();

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public void ConversationTurn_RecordHasCorrectValues()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var turn = new ConversationTurn("Hello", "Hi", timestamp);

        // Assert
        turn.HumanInput.Should().Be("Hello");
        turn.AiResponse.Should().Be("Hi");
        turn.Timestamp.Should().Be(timestamp);
    }
}

/// <summary>
/// Unit tests for MemoryContext.
/// </summary>
[Trait("Category", "Unit")]
public class MemoryContextTests
{
    [Fact]
    public void Constructor_WithDataAndMemory_InitializesCorrectly()
    {
        // Arrange
        var memory = new ConversationMemory();
        var data = "test data";

        // Act
        var context = new MemoryContext<string>(data, memory);

        // Assert
        context.Data.Should().Be(data);
        context.Memory.Should().BeSameAs(memory);
        context.Properties.Should().NotBeNull();
    }

    [Fact]
    public void WithData_ChangesDataType_ReturnsNewContext()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("test", memory);

        // Act
        var newContext = context.WithData(42);

        // Assert
        newContext.Data.Should().Be(42);
        newContext.Memory.Should().BeSameAs(memory);
    }

    [Fact]
    public void SetProperty_AddsProperty_ReturnsNewContext()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("test", memory);

        // Act
        var newContext = context.SetProperty("key1", "value1");

        // Assert
        newContext.Properties.Should().ContainKey("key1");
        newContext.Properties["key1"].Should().Be("value1");
    }

    [Fact]
    public void SetProperty_MaintainsImmutability_OriginalUnchanged()
    {
        // Arrange
        var memory = new ConversationMemory();
        var originalContext = new MemoryContext<string>("test", memory);

        // Act
        var newContext = originalContext.SetProperty("key1", "value1");

        // Assert
        originalContext.Properties.Should().NotContainKey("key1");
        newContext.Properties.Should().ContainKey("key1");
    }

    [Fact]
    public void SetProperty_ChainedCalls_AccumulatesProperties()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("test", memory);

        // Act
        var newContext = context
            .SetProperty("key1", "value1")
            .SetProperty("key2", 42)
            .SetProperty("key3", true);

        // Assert
        newContext.Properties.Should().HaveCount(3);
        newContext.Properties["key1"].Should().Be("value1");
        newContext.Properties["key2"].Should().Be(42);
        newContext.Properties["key3"].Should().Be(true);
    }

    [Fact]
    public void GetProperty_ExistingKey_ReturnsValue()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("test", memory)
            .SetProperty("key1", "value1");

        // Act
        var value = context.GetProperty<string>("key1");

        // Assert
        value.Should().Be("value1");
    }

    [Fact]
    public void GetProperty_NonExistingKey_ReturnsDefault()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("test", memory);

        // Act
        var value = context.GetProperty<string>("nonexistent");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void SetProperty_OverwritesExistingKey_UpdatesValue()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("test", memory)
            .SetProperty("key1", "original");

        // Act
        var newContext = context.SetProperty("key1", "updated");

        // Assert
        newContext.Properties["key1"].Should().Be("updated");
    }
}
