// <copyright file="MemoryContextXUnitTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Memory;

using FluentAssertions;
using Ouroboros.Core.Memory;
using Xunit;

/// <summary>
/// xUnit tests for MemoryContext and ConversationMemory functionality.
/// </summary>
[Trait("Category", "Unit")]
public class MemoryContextXUnitTests
{
    #region ConversationMemory Tests

    [Fact]
    public void ConversationMemory_Constructor_CreatesEmptyMemory()
    {
        // Arrange & Act
        var memory = new ConversationMemory();

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public void ConversationMemory_Constructor_WithMaxTurns_SetsLimit()
    {
        // Arrange & Act
        var memory = new ConversationMemory(maxTurns: 5);

        // Assert - Verify by adding turns
        for (int i = 0; i < 10; i++)
        {
            memory.AddTurn($"Input {i}", $"Response {i}");
        }

        memory.GetTurns().Should().HaveCount(5);
    }

    [Fact]
    public void AddTurn_SingleTurn_StoredCorrectly()
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
    public void AddTurn_MultipleTurns_StoredInOrder()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        memory.AddTurn("First", "Response 1");
        memory.AddTurn("Second", "Response 2");
        memory.AddTurn("Third", "Response 3");

        // Assert
        var turns = memory.GetTurns();
        turns.Should().HaveCount(3);
        turns[0].HumanInput.Should().Be("First");
        turns[1].HumanInput.Should().Be("Second");
        turns[2].HumanInput.Should().Be("Third");
    }

    [Fact]
    public void AddTurn_ExceedsMaxTurns_OldestRemoved()
    {
        // Arrange
        var memory = new ConversationMemory(maxTurns: 2);

        // Act
        memory.AddTurn("First", "R1");
        memory.AddTurn("Second", "R2");
        memory.AddTurn("Third", "R3");

        // Assert
        var turns = memory.GetTurns();
        turns.Should().HaveCount(2);
        turns[0].HumanInput.Should().Be("Second");
        turns[1].HumanInput.Should().Be("Third");
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
        memory.AddTurn("Hello", "Hi");

        // Act
        var history = memory.GetFormattedHistory();

        // Assert
        history.Should().Contain("Human: Hello");
        history.Should().Contain("AI: Hi");
    }

    [Fact]
    public void GetFormattedHistory_CustomPrefixes_UsesCustomPrefixes()
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
    public void GetFormattedHistory_MultipleTurns_FormatsAll()
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
    public void Clear_WithTurns_RemovesAll()
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

    [Fact]
    public void GetTurns_ReturnsReadOnlyList()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Hello", "Hi");

        // Act
        var turns = memory.GetTurns();

        // Assert
        turns.Should().BeAssignableTo<IReadOnlyList<ConversationTurn>>();
    }

    #endregion

    #region ConversationTurn Tests

    [Fact]
    public void ConversationTurn_RecordCreation_StoresValues()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var turn = new ConversationTurn("Input", "Response", timestamp);

        // Assert
        turn.HumanInput.Should().Be("Input");
        turn.AiResponse.Should().Be("Response");
        turn.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ConversationTurn_Equality_WorksCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var turn1 = new ConversationTurn("Input", "Response", timestamp);
        var turn2 = new ConversationTurn("Input", "Response", timestamp);

        // Assert
        turn1.Should().Be(turn2);
        (turn1 == turn2).Should().BeTrue();
    }

    #endregion

    #region MemoryContext Tests

    [Fact]
    public void MemoryContext_Constructor_CreatesWithData()
    {
        // Arrange
        var memory = new ConversationMemory();

        // Act
        var context = new MemoryContext<string>("test data", memory);

        // Assert
        context.Data.Should().Be("test data");
        context.Memory.Should().BeSameAs(memory);
    }

    [Fact]
    public void MemoryContext_SetProperty_CreatesNewContextWithProperty()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var newContext = context.SetProperty("key1", "value1");

        // Assert
        newContext.GetProperty<string>("key1").Should().Be("value1");
    }

    [Fact]
    public void MemoryContext_SetProperty_MaintainsImmutability()
    {
        // Arrange
        var memory = new ConversationMemory();
        var originalContext = new MemoryContext<string>("data", memory);

        // Act
        var newContext = originalContext.SetProperty("key1", "value1");

        // Assert - Original unchanged
        originalContext.GetProperty<string>("key1").Should().BeNull();
        // New has property
        newContext.GetProperty<string>("key1").Should().Be("value1");
    }

    [Fact]
    public void MemoryContext_SetProperty_MultipleTimes_AccumulatesProperties()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var newContext = context
            .SetProperty("key1", "value1")
            .SetProperty("key2", 42)
            .SetProperty("key3", true);

        // Assert
        newContext.GetProperty<string>("key1").Should().Be("value1");
        newContext.GetProperty<int>("key2").Should().Be(42);
        newContext.GetProperty<bool>("key3").Should().BeTrue();
    }

    [Fact]
    public void MemoryContext_GetProperty_WithMissingKey_ReturnsDefault()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);

        // Act
        var value = context.GetProperty<string>("nonexistent");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void MemoryContext_GetProperty_WithWrongType_ReturnsDefault()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory)
            .SetProperty("key", "string value");

        // Act
        var value = context.GetProperty<int>("key");

        // Assert
        value.Should().Be(default(int));
    }

    [Fact]
    public void MemoryContext_WithData_CreatesNewContextWithNewData()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("original", memory)
            .SetProperty("key", "value");

        // Act
        var newContext = context.WithData(42);

        // Assert
        newContext.Data.Should().Be(42);
        newContext.Memory.Should().BeSameAs(memory);
        newContext.GetProperty<string>("key").Should().Be("value");
    }

    [Fact]
    public void MemoryContext_WithData_TypeConversion_Works()
    {
        // Arrange
        var memory = new ConversationMemory();
        var stringContext = new MemoryContext<string>("text", memory);

        // Act
        var intContext = stringContext.WithData(123);

        // Assert
        intContext.Should().BeOfType<MemoryContext<int>>();
        intContext.Data.Should().Be(123);
    }

    #endregion

    #region MemoryArrows Tests

    [Fact]
    public async Task LoadMemory_WithEmptyMemory_SetsEmptyHistory()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("input", memory);
        var arrow = MemoryArrows.LoadMemory<string>();

        // Act
        var result = await arrow(context);

        // Assert
        result.GetProperty<string>("history").Should().BeEmpty();
    }

    [Fact]
    public async Task LoadMemory_WithHistory_SetsFormattedHistory()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Hello", "Hi");
        var context = new MemoryContext<string>("input", memory);
        var arrow = MemoryArrows.LoadMemory<string>();

        // Act
        var result = await arrow(context);

        // Assert
        var history = result.GetProperty<string>("history");
        history.Should().Contain("Human: Hello");
        history.Should().Contain("AI: Hi");
    }

    [Fact]
    public async Task LoadMemory_CustomOutputKey_UsesCustomKey()
    {
        // Arrange
        var memory = new ConversationMemory();
        memory.AddTurn("Q", "A");
        var context = new MemoryContext<string>("input", memory);
        var arrow = MemoryArrows.LoadMemory<string>(outputKey: "conv_history");

        // Act
        var result = await arrow(context);

        // Assert
        result.GetProperty<string>("conv_history").Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateMemory_WithValidProperties_AddsToMemory()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("input", memory)
            .SetProperty("input", "User question")
            .SetProperty("text", "AI answer");
        var arrow = MemoryArrows.UpdateMemory<string>();

        // Act
        await arrow(context);

        // Assert
        var turns = memory.GetTurns();
        turns.Should().HaveCount(1);
        turns[0].HumanInput.Should().Be("User question");
        turns[0].AiResponse.Should().Be("AI answer");
    }

    [Fact]
    public async Task UpdateMemory_WithEmptyInput_DoesNotAddTurn()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory)
            .SetProperty("input", "")
            .SetProperty("text", "response");
        var arrow = MemoryArrows.UpdateMemory<string>();

        // Act
        await arrow(context);

        // Assert
        memory.GetTurns().Should().BeEmpty();
    }

    [Fact]
    public async Task Template_WithVariables_ReplacesPlaceholders()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("ignored", memory)
            .SetProperty("name", "Alice")
            .SetProperty("age", 30);
        var arrow = MemoryArrows.Template("Hello {name}, you are {age} years old.");

        // Act
        var result = await arrow(context);

        // Assert
        result.Data.Should().Be("Hello Alice, you are 30 years old.");
    }

    [Fact]
    public async Task Template_WithNoVariables_ReturnsOriginal()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("ignored", memory);
        var arrow = MemoryArrows.Template("No variables here");

        // Act
        var result = await arrow(context);

        // Assert
        result.Data.Should().Be("No variables here");
    }

    [Fact]
    public async Task Set_SetsPropertyValue()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("data", memory);
        var arrow = MemoryArrows.Set<string>("test value", "custom_key");

        // Act
        var result = await arrow(context);

        // Assert
        result.GetProperty<string>("custom_key").Should().Be("test value");
    }

    [Fact]
    public async Task MockLlm_GeneratesResponse()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("Test prompt", memory);
        var arrow = MemoryArrows.MockLlm();

        // Act
        var result = await arrow(context);

        // Assert
        result.Data.Should().Contain("AI Response:");
        result.Data.Should().Contain("11"); // "Test prompt" has 11 characters
        result.GetProperty<string>("text").Should().NotBeEmpty();
    }

    [Fact]
    public async Task MockLlm_CustomPrefix_UsesPrefix()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("prompt", memory);
        var arrow = MemoryArrows.MockLlm("CustomBot:");

        // Act
        var result = await arrow(context);

        // Assert
        result.Data.Should().Contain("CustomBot:");
    }

    [Fact]
    public async Task ExtractProperty_ExtractsValueAsData()
    {
        // Arrange
        var memory = new ConversationMemory();
        var context = new MemoryContext<string>("original", memory)
            .SetProperty("extracted", "new value");
        var arrow = MemoryArrows.ExtractProperty<string, string>("extracted");

        // Act
        var result = await arrow(context);

        // Assert
        result.Data.Should().Be("new value");
    }

    #endregion
}
