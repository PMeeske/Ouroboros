using LangChainPipeline.Core.Memory;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for memory context functionality including conversation management.
/// </summary>
public class MemoryContextTests
{
    /// <summary>
    /// Tests basic memory context creation and property management.
    /// </summary>
    [Fact]
    public void TestMemoryContextBasics()
    {
        var input = "test input";
        var memory = new ConversationMemory(maxTurns: 3);
        var memoryContext = new MemoryContext<string>(input, memory);
        
        // Test input property
        Assert.Equal(input, memoryContext.Data);
            
        // Test property management
        memoryContext.SetProperty("key1", "value1");
        memoryContext.SetProperty("key2", 42);
        
        var value1 = memoryContext.GetProperty<string>("key1");
        var value2 = memoryContext.GetProperty<int>("key2");
        
        Assert.Equal("value1", value1);
        Assert.Equal(42, value2);
    }

    /// <summary>
    /// Tests conversation turn management with max turns limit.
    /// </summary>
    [Fact]
    public void TestConversationTurnManagement()
    {
        var memory = new ConversationMemory(maxTurns: 2);
        
        // Add turns beyond the limit
        memory.AddTurn("Hello", "Hi there");
        memory.AddTurn("How are you?", "I'm good");
        memory.AddTurn("What's your name?", "I'm an AI");
        
        var turns = memory.GetTurns();
        
        // Should only keep the last 2 turns
        Assert.Equal(2, turns.Count);
            
        var turnList = turns.ToList();
        Assert.Equal("How are you?", turnList[0].HumanInput);
        Assert.Equal("What's your name?", turnList[1].HumanInput);
    }

    /// <summary>
    /// Tests conversation history formatting.
    /// </summary>
    [Fact]
    public void TestConversationHistoryFormatting()
    {
        var memory = new ConversationMemory();
        memory.AddTurn("User question", "AI response");
        
        var history = memory.GetFormattedHistory();
        Assert.False(string.IsNullOrEmpty(history));
        Assert.Contains("User question", history);
        Assert.Contains("AI response", history);
    }
}