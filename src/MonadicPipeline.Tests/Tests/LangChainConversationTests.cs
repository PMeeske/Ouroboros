// <copyright file="LangChainConversationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for LangChain-based conversation functionality.
/// </summary>
public static class LangChainConversationTests
{
    /// <summary>
    /// Tests basic LangChain conversation context creation and property management.
    /// </summary>
    public static void TestLangChainConversationContextBasics()
    {
        Console.WriteLine("Testing LangChain ConversationContext basics...");

        var context = new LangChainConversationContext(maxTurns: 3);

        // Test property management
        context.SetProperty("key1", "value1");
        context.SetProperty("key2", 42);

        var value1 = context.GetProperty<string>("key1");
        var value2 = context.GetProperty<int>("key2");

        if (value1 != "value1")
        {
            throw new Exception("String property not retrieved correctly");
        }

        if (value2 != 42)
        {
            throw new Exception("Integer property not retrieved correctly");
        }

        Console.WriteLine("✓ LangChain ConversationContext basics test passed");
    }

    /// <summary>
    /// Tests conversation turn management with max turns limit using LangChain approach.
    /// </summary>
    public static void TestLangChainConversationTurnManagement()
    {
        Console.WriteLine("Testing LangChain conversation turn management...");

        var context = new LangChainConversationContext(maxTurns: 2);

        // Add turns beyond the limit
        context.AddTurn("Hello", "Hi there");
        context.AddTurn("How are you?", "I'm good");
        context.AddTurn("What's your name?", "I'm an AI");

        var history = context.GetConversationHistory();

        // Should only keep the last 2 turns
        var lines = history.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var turnCount = lines.Count(line => line.StartsWith("Human:"));

        if (turnCount != 2)
        {
            throw new Exception($"Expected 2 turns, got {turnCount}");
        }

        if (!history.Contains("How are you?"))
        {
            throw new Exception("First turn not correct after limit reached");
        }

        if (!history.Contains("What's your name?"))
        {
            throw new Exception("Second turn not correct after limit reached");
        }

        Console.WriteLine("✓ LangChain conversation turn management test passed");
    }

    /// <summary>
    /// Tests the WithLangChainMemory extension method.
    /// </summary>
    public static void TestWithLangChainMemoryExtension()
    {
        Console.WriteLine("Testing WithLangChainMemory extension...");

        var input = "test string";
        var context = input.WithLangChainMemory(5);

        var retrievedInput = context.GetProperty<string>("input");
        if (retrievedInput != input)
        {
            throw new Exception("WithLangChainMemory extension did not wrap input correctly");
        }

        var history = context.GetConversationHistory();
        if (!string.IsNullOrEmpty(history))
        {
            throw new Exception("New conversation context should have no history");
        }

        Console.WriteLine("✓ WithLangChainMemory extension test passed");
    }

    /// <summary>
    /// Tests LangChain conversation pipeline functionality.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestLangChainConversationPipeline()
    {
        Console.WriteLine("Testing LangChain conversation pipeline...");

        var context = "test input".WithLangChainMemory();
        context.AddTurn("Previous question", "Previous answer");

        var pipeline = LangChainConversationBuilder.CreateConversationPipeline()
            .WithConversationHistory()
            .SetProperty("test_prop", "test_value")
            .AddAiResponseGeneration(async input => await Task.FromResult($"Response to: {input}"));

        var result = await pipeline.RunAsync(context);

        var historyProp = result.GetProperty<string>("conversation_history");
        if (string.IsNullOrEmpty(historyProp))
        {
            throw new Exception("Conversation history not added to properties");
        }

        var testProp = result.GetProperty<string>("test_prop");
        if (testProp != "test_value")
        {
            throw new Exception("Test property not set correctly");
        }

        var aiResponse = result.GetProperty<string>("text");
        if (string.IsNullOrEmpty(aiResponse))
        {
            throw new Exception("AI response not generated");
        }

        Console.WriteLine("✓ LangChain conversation pipeline test passed");
    }

    /// <summary>
    /// Runs all LangChain conversation tests.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running LangChain Conversation Tests ===");

        TestLangChainConversationContextBasics();
        TestLangChainConversationTurnManagement();
        TestWithLangChainMemoryExtension();
        await TestLangChainConversationPipeline();

        Console.WriteLine("✓ All LangChain Conversation tests passed!\n");
    }
}
