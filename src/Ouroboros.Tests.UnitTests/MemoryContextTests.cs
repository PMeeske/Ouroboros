// <copyright file="MemoryContextTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

/// <summary>
/// Tests for memory context functionality including conversation management.
/// </summary>
[Trait("Category", "Unit")]
public static class MemoryContextTests
{
    /// <summary>
    /// Tests basic memory context creation and property management.
    /// </summary>
    public static void TestMemoryContextBasics()
    {
        Console.WriteLine("Testing MemoryContext basics...");

        var input = "test input";
        var memory = new ConversationMemory(maxTurns: 3);
        var memoryContext = new MemoryContext<string>(input, memory);

        // Test input property
        if (memoryContext.Data != input)
        {
            throw new Exception("Input property not set correctly");
        }

        // Test property management
        var contextWithKey1 = memoryContext.SetProperty("key1", "value1");
        var contextWithKey2 = contextWithKey1.SetProperty("key2", 42);

        var value1 = contextWithKey2.GetProperty<string>("key1");
        var value2 = contextWithKey2.GetProperty<int>("key2");

        if (value1 != "value1")
        {
            throw new Exception("String property not retrieved correctly");
        }

        if (value2 != 42)
        {
            throw new Exception("Integer property not retrieved correctly");
        }

        Console.WriteLine("✓ MemoryContext basics test passed");
    }

    /// <summary>
    /// Tests that MemoryContext SetProperty maintains immutability.
    /// </summary>
    public static void TestMemoryContextImmutability()
    {
        Console.WriteLine("Testing MemoryContext immutability...");

        var input = "test input";
        var memory = new ConversationMemory(maxTurns: 3);
        var originalContext = new MemoryContext<string>(input, memory);

        // Add a property to the original context
        var newContext = originalContext.SetProperty("key1", "value1");

        // Original context should not have the property
        var originalValue = originalContext.GetProperty<string>("key1");
        if (originalValue != null)
        {
            throw new Exception("Original context was mutated - immutability broken!");
        }

        // New context should have the property
        var newValue = newContext.GetProperty<string>("key1");
        if (newValue != "value1")
        {
            throw new Exception("New context does not have the expected property");
        }

        // Contexts should be different instances
        if (ReferenceEquals(originalContext, newContext))
        {
            throw new Exception("SetProperty returned the same instance - immutability broken!");
        }

        Console.WriteLine("✓ MemoryContext immutability test passed");
    }

    /// <summary>
    /// Tests conversation turn management with max turns limit.
    /// </summary>
    public static void TestConversationTurnManagement()
    {
        Console.WriteLine("Testing conversation turn management...");

        var memory = new ConversationMemory(maxTurns: 2);

        // Add turns beyond the limit
        memory.AddTurn("Hello", "Hi there");
        memory.AddTurn("How are you?", "I'm good");
        memory.AddTurn("What's your name?", "I'm an AI");

        var turns = memory.GetTurns();

        // Should only keep the last 2 turns
        if (turns.Count != 2)
        {
            throw new Exception($"Expected 2 turns, got {turns.Count}");
        }

        var turnList = turns.ToList();
        if (turnList[0].HumanInput != "How are you?")
        {
            throw new Exception("First turn not correct after limit reached");
        }

        if (turnList[1].HumanInput != "What's your name?")
        {
            throw new Exception("Second turn not correct after limit reached");
        }

        Console.WriteLine("✓ Conversation turn management test passed");
    }

    /// <summary>
    /// Tests conversation history formatting.
    /// </summary>
    public static void TestConversationHistoryFormatting()
    {
        Console.WriteLine("Testing conversation history formatting...");

        var memory = new ConversationMemory();
        memory.AddTurn("User question", "AI response");

        var history = memory.GetFormattedHistory();
        if (string.IsNullOrEmpty(history))
        {
            throw new Exception("Conversation history not generated");
        }

        if (!history.Contains("User question") || !history.Contains("AI response"))
        {
            throw new Exception("Conversation history missing content");
        }

        Console.WriteLine("✓ Conversation history formatting test passed");
    }

    /// <summary>
    /// Runs all memory context tests.
    /// </summary>
    public static void RunAllTests()
    {
        Console.WriteLine("=== Running MemoryContext Tests ===");

        TestMemoryContextBasics();
        TestMemoryContextImmutability();
        TestConversationTurnManagement();
        TestConversationHistoryFormatting();

        Console.WriteLine("✓ All MemoryContext tests passed!\n");
    }
}
