using LangChainPipeline.Core.Memory;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for memory context functionality including conversation management.
/// </summary>
public class MemoryContextTests
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
            throw new Exception("Input property not set correctly");
            
        // Test property management
        memoryContext.SetProperty("key1", "value1");
        memoryContext.SetProperty("key2", 42);
        
        var value1 = memoryContext.GetProperty<string>("key1");
        var value2 = memoryContext.GetProperty<int>("key2");
        
        if (value1 != "value1")
            throw new Exception("String property not retrieved correctly");
            
        if (value2 != 42)
            throw new Exception("Integer property not retrieved correctly");
            
        Console.WriteLine("✓ MemoryContext basics test passed");
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
            throw new Exception($"Expected 2 turns, got {turns.Count}");
            
        var turnList = turns.ToList();
        if (turnList[0].HumanInput != "How are you?")
            throw new Exception("First turn not correct after limit reached");
            
        if (turnList[1].HumanInput != "What's your name?")
            throw new Exception("Second turn not correct after limit reached");
            
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
            throw new Exception("Conversation history not generated");
            
        if (!history.Contains("User question") || !history.Contains("AI response"))
            throw new Exception("Conversation history missing content");
            
        Console.WriteLine("✓ Conversation history formatting test passed");
    }

    /// <summary>
    /// Runs all memory context tests.
    /// </summary>
    public static void RunAllTests()
    {
        Console.WriteLine("=== Running MemoryContext Tests ===");
        
        TestMemoryContextBasics();
        TestConversationTurnManagement();
        TestConversationHistoryFormatting();
        
        Console.WriteLine("✓ All MemoryContext tests passed!\n");
    }
}