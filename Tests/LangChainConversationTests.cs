using LangChainPipeline.Core.LangChain;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for LangChain-based conversation functionality.
/// </summary>
public class LangChainConversationTests
{
    /// <summary>
    /// Tests basic LangChain conversation context creation and property management.
    /// </summary>
    [Fact]
    public void TestLangChainConversationContextBasics()
    {
        var context = new LangChainConversationContext(maxTurns: 3);
        
        // Test property management
        context.SetProperty("key1", "value1");
        context.SetProperty("key2", 42);
        
        var value1 = context.GetProperty<string>("key1");
        var value2 = context.GetProperty<int>("key2");
        
        Assert.Equal("value1", value1);
        Assert.Equal(42, value2);
    }

    /// <summary>
    /// Tests conversation turn management with max turns limit using LangChain approach.
    /// </summary>
    [Fact]
    public void TestLangChainConversationTurnManagement()
    {
        var context = new LangChainConversationContext(maxTurns: 2);
        
        // Add turns beyond the limit
        context.AddTurn("Hello", "Hi there");
        context.AddTurn("How are you?", "I'm good");
        context.AddTurn("What's your name?", "I'm an AI");
        
        var history = context.GetConversationHistory();
        
        // Should only keep the last 2 turns
        var lines = history.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var turnCount = lines.Count(line => line.StartsWith("Human:"));
        
        Assert.Equal(2, turnCount);
        Assert.Contains("How are you?", history);
        Assert.Contains("What's your name?", history);
    }

    /// <summary>
    /// Tests the WithLangChainMemory extension method.
    /// </summary>
    [Fact]
    public void TestWithLangChainMemoryExtension()
    {
        var input = "test string";
        var context = input.WithLangChainMemory(5);
        
        var retrievedInput = context.GetProperty<string>("input");
        Assert.Equal(input, retrievedInput);
            
        var history = context.GetConversationHistory();
        Assert.True(string.IsNullOrEmpty(history));
    }

    /// <summary>
    /// Tests LangChain conversation pipeline functionality.
    /// </summary>
    [Fact]
    public async Task TestLangChainConversationPipeline()
    {
        var context = "test input".WithLangChainMemory();
        context.AddTurn("Previous question", "Previous answer");
        
        var pipeline = LangChainConversationBuilder.CreateConversationPipeline()
            .WithConversationHistory()
            .SetProperty("test_prop", "test_value")
            .AddAIResponseGeneration(async input => await Task.FromResult($"Response to: {input}"));
        
        var result = await pipeline.RunAsync(context);
        
        var historyProp = result.GetProperty<string>("conversation_history");
        Assert.False(string.IsNullOrEmpty(historyProp));
            
        var testProp = result.GetProperty<string>("test_prop");
        Assert.Equal("test_value", testProp);
            
        var aiResponse = result.GetProperty<string>("text");
        Assert.False(string.IsNullOrEmpty(aiResponse));
    }
}