// <copyright file="ConversationMemoryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Core.Memory;

using FluentAssertions;
using Ouroboros.Core.Memory;
using Xunit;

/// <summary>
/// Tests for ConversationMemory, ConversationTurn, MemoryContext, and MemoryArrows.
/// </summary>
[Trait("Category", "Unit")]
public class ConversationMemoryTests
{
    #region ConversationMemory Tests

    [Fact]
    public void Constructor_WithDefaultMaxTurns_Creates()
    {
        // Act
        var memory = new ConversationMemory();

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithCustomMaxTurns_Creates()
    {
        // Act
        var memory = new ConversationMemory(maxTurns: 5);

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public void AddTurn_AddsSingleTurn()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        memory.AddTurn("Hello", "Hi there!");

        // Assert
        memory.GetTurns().Should().HaveCount(1);
        memory.GetTurns()[0].HumanInput.Should().Be("Hello");
        memory.GetTurns()[0].AiResponse.Should().Be("Hi there!");
    }

    [Fact]
    public void AddTurn_AddsMultipleTurns()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        memory.AddTurn("Question 1", "Answer 1");
        memory.AddTurn("Question 2", "Answer 2");
        memory.AddTurn("Question 3", "Answer 3");

        // Assert
        memory.GetTurns().Should().HaveCount(3);
    }

    [Fact]
    public void AddTurn_RespectsMaxTurnsLimit()
    {
        // Arrange
        var memory = new ConversationMemory(maxTurns: 2);

        // Act
        memory.AddTurn("Q1", "A1");
        memory.AddTurn("Q2", "A2");
        memory.AddTurn("Q3", "A3"); // Should push out Q1

        // Assert
        memory.GetTurns().Should().HaveCount(2);
        memory.GetTurns()[0].HumanInput.Should().Be("Q2"); // Q1 removed
        memory.GetTurns()[1].HumanInput.Should().Be("Q3");
    }

    [Fact]
    public void AddTurn_SetsTimestamp()
    {
        // Arrange
        var memory = new ConversationMemory();
        var beforeAdd = DateTime.UtcNow;

        // Act
        memory.AddTurn("Hello", "Hi");

        // Assert
        var turn = memory.GetTurns()[0];
        turn.Timestamp.Should().BeOnOrAfter(beforeAdd);
        turn.Timestamp.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void GetFormattedHistory_EmptyMemory_ReturnsEmpty()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        var history = memory.GetFormattedHistory();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void GetFormattedHistory_WithTurns_FormatsCorrectly()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Hello", "Hi there!");

        // Act
        var history = memory.GetFormattedHistory();

        // Assert
        history.Should().Contain("Human: Hello");
        history.Should().Contain("AI: Hi there!");
    }

    [Fact]
    public void GetFormattedHistory_WithCustomPrefixes()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Hello", "Hi there!");

        // Act
        var history = memory.GetFormattedHistory("User", "Assistant");

        // Assert
        history.Should().Contain("User: Hello");
        history.Should().Contain("Assistant: Hi there!");
    }

    [Fact]
    public void GetFormattedHistory_MultipleTurns_FormatsAllTurns()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Q1", "A1");
        memory.AddTurn("Q2", "A2");

        // Act
        var history = memory.GetFormattedHistory();

        // Assert
        history.Should().Contain("Human: Q1");
        history.Should().Contain("AI: A1");
        history.Should().Contain("Human: Q2");
        history.Should().Contain("AI: A2");
    }

    [Fact]
    public void Clear_RemovesAllTurns()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Q1", "A1");
        memory.AddTurn("Q2", "A2");

        // Act
        memory.Clear();

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    #endregion

    #region ConversationTurn Tests

    [Fact]
    public void ConversationTurn_Constructor_SetsAllProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var turn = new ConversationTurn("human input", "ai response", timestamp);

        // Assert
        turn.HumanInput.Should().Be("human input");
        turn.AiResponse.Should().Be("ai response");
        turn.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ConversationTurn_Equality_SameValues_AreEqual()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var turn1 = new ConversationTurn("hello", "hi", timestamp);
        var turn2 = new ConversationTurn("hello", "hi", timestamp);

        // Assert
        turn1.Should().Be(turn2);
    }

    #endregion

    #region MemoryContext Tests

    [Fact]
    public void MemoryContext_Constructor_SetsProperties()
    {
        // Arrange
        var memory = new ConversationMemory();
        var properties = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var context = new MemoryContext<string>("data", memory, properties);

        // Assert
        context.Data.Should().Be("data");
        context.Memory.Should().Be(memory);
        context.Properties.Should().ContainKey("key");
    }

    [Fact]
    public void MemoryContext_NullProperties_CreatesEmptyDictionary()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        var context = new MemoryContext<string>("data", memory, null);

        // Assert
        context.Properties.Should().NotBeNull();
        context.Properties.Should().BeEmpty();
    }

    [Fact]
    public void MemoryContext_WithData_ChangesDataType()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("hello", memory);

        // Act
        var intContext = context.WithData(42);

        // Assert
        intContext.Data.Should().Be(42);
        intContext.Memory.Should().Be(memory);
    }

    [Fact]
    public void MemoryContext_SetProperty_AddsNewProperty()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var updated = context.SetProperty("newKey", "newValue");

        // Assert
        updated.Properties.Should().ContainKey("newKey");
        updated.Properties["newKey"].Should().Be("newValue");
        context.Properties.Should().NotContainKey("newKey"); // Original unchanged
    }

    [Fact]
    public void MemoryContext_SetProperty_UpdatesExistingProperty()
    {
        // Arrange
        var memory = new ConversationMemory();
        var props = new Dictionary<string, object> { ["key"] = "old" };
        var context = new MemoryContext<string>("data", memory, props);

        // Act
        var updated = context.SetProperty("key", "new");

        // Assert
        updated.Properties["key"].Should().Be("new");
    }

    [Fact]
    public void MemoryContext_GetProperty_ReturnsValue()
    {
        // Arrange
        var memory = new ConversationMemory();
        var props = new Dictionary<string, object> { ["count"] = 42 };
        var context = new MemoryContext<string>("data", memory, props);

        // Act
        var value = context.GetProperty<int>("count");

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void MemoryContext_GetProperty_MissingKey_ReturnsDefault()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var value = context.GetProperty<string>("missing");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void MemoryContext_GetProperty_WrongType_ReturnsDefault()
    {
        // Arrange
        var memory = new ConversationMemory();
        var props = new Dictionary<string, object> { ["value"] = "string" };
        var context = new MemoryContext<string>("data", memory, props);

        // Act
        var value = context.GetProperty<int>("value");

        // Assert
        value.Should().Be(default);
    }

    #endregion

    #region MemoryArrows Tests

    [Fact]
    public async Task LoadMemory_LoadsHistoryIntoContext()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Question", "Answer");
        var context = new MemoryContext<string>("data", memory);

        // Act
        var arrow = MemoryArrows.LoadMemory<string>();
        var result = await arrow(context);

        // Assert
        result.GetProperty<string>("history").Should().Contain("Question");
        result.GetProperty<string>("history").Should().Contain("Answer");
    }

    [Fact]
    public async Task LoadMemory_WithCustomKey_UsesKey()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Q", "A");
        var context = new MemoryContext<string>("data", memory);

        // Act
        var arrow = MemoryArrows.LoadMemory<string>("customHistory");
        var result = await arrow(context);

        // Assert
        result.Properties.Should().ContainKey("customHistory");
        result.GetProperty<string>("customHistory").Should().Contain("Q");
    }

    [Fact]
    public async Task UpdateMemory_AddsNewTurn()
    {
        // Arrange
        var memory = new ConversationMemory();
        var props = new Dictionary<string, object>
        {
            ["input"] = "User question",
            ["text"] = "AI answer"
        };
        var context = new MemoryContext<string>("data", memory, props);

        // Act
        var arrow = MemoryArrows.UpdateMemory<string>();
        await arrow(context);

        // Assert
        memory.GetTurns().Should().HaveCount(1);
        memory.GetTurns()[0].HumanInput.Should().Be("User question");
        memory.GetTurns()[0].AiResponse.Should().Be("AI answer");
    }

    [Fact]
    public async Task UpdateMemory_WithEmptyValues_DoesNotAddTurn()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var arrow = MemoryArrows.UpdateMemory<string>();
        await arrow(context);

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public async Task Template_ReplacesPlaceholders()
    {
        // Arrange
        var memory = new ConversationMemory();
        var props = new Dictionary<string, object>
        {
            ["name"] = "John",
            ["topic"] = "AI"
        };
        var context = new MemoryContext<string>("data", memory, props);

        // Act
        var arrow = MemoryArrows.Template("Hello {name}, let's discuss {topic}!");
        var result = await arrow(context);

        // Assert
        result.Data.Should().Be("Hello John, let's discuss AI!");
    }

    [Fact]
    public async Task Template_MissingPlaceholder_LeavesAsIs()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var arrow = MemoryArrows.Template("Hello {name}!");
        var result = await arrow(context);

        // Assert
        result.Data.Should().Be("Hello {name}!");
    }

    [Fact]
    public async Task Set_AddsPropertyToContext()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var arrow = MemoryArrows.Set<string>("myValue", "myKey");
        var result = await arrow(context);

        // Assert
        result.GetProperty<string>("myKey").Should().Be("myValue");
    }

    [Fact]
    public async Task MockLlm_ProcessesInput()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("Test prompt", memory);

        // Act
        var arrow = MemoryArrows.MockLlm();
        var result = await arrow(context);

        // Assert
        result.Data.Should().Contain("AI Response:");
        result.Data.Should().Contain("11"); // "Test prompt".Length
    }

    [Fact]
    public async Task MockLlm_SetsTextProperty()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("prompt", memory);

        // Act
        var arrow = MemoryArrows.MockLlm();
        var result = await arrow(context);

        // Assert
        result.GetProperty<string>("text").Should().Be(result.Data);
    }

    [Fact]
    public async Task MockLlm_WithCustomPrefix()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("prompt", memory);

        // Act
        var arrow = MemoryArrows.MockLlm("Custom:");
        var result = await arrow(context);

        // Assert
        result.Data.Should().StartWith("Custom:");
    }

    [Fact]
    public async Task ExtractProperty_ExtractsAsData()
    {
        // Arrange
        var memory = new ConversationMemory();
        var props = new Dictionary<string, object> { ["extractMe"] = 42 };
        var context = new MemoryContext<string>("original", memory, props);

        // Act
        var arrow = MemoryArrows.ExtractProperty<string, int>("extractMe");
        var result = await arrow(context);

        // Assert
        result.Data.Should().Be(42);
    }

    #endregion
}
