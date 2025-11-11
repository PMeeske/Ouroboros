// <copyright file="LangChainConversationContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LangChain;

/// <summary>
/// LangChain-integrated conversation context that properly bridges with official LangChain chains
/// while maintaining the existing monadic pipeline patterns.
/// </summary>
public class LangChainConversationContext
{
    private readonly Dictionary<string, object> properties = new();
    private readonly ConversationMemory memory;

    public LangChainConversationContext(int maxTurns = 10)
    {
        this.memory = new ConversationMemory(maxTurns);
    }

    /// <summary>
    /// Sets a property in the context using LangChain patterns.
    /// </summary>
    /// <returns></returns>
    public LangChainConversationContext SetProperty(string key, object value)
    {
        this.properties[key] = value;
        return this;
    }

    /// <summary>
    /// Gets a property from the context.
    /// </summary>
    /// <returns></returns>
    public TValue? GetProperty<TValue>(string key)
    {
        return this.properties.TryGetValue(key, out var value) && value is TValue typedValue
            ? typedValue
            : default;
    }

    /// <summary>
    /// Adds a conversation turn following LangChain patterns.
    /// </summary>
    public void AddTurn(string humanInput, string aiResponse)
    {
        this.memory.AddTurn(humanInput, aiResponse);
    }

    /// <summary>
    /// Gets conversation history as formatted string for LangChain prompts.
    /// </summary>
    /// <returns></returns>
    public string GetConversationHistory()
    {
        return this.memory.GetFormattedHistory();
    }

    /// <summary>
    /// Get all properties as dictionary for LangChain context.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, object> GetProperties() => new(this.properties);
}

/// <summary>
/// Extension methods to integrate with existing WithMemory pattern using LangChain.
/// </summary>
public static class LangChainMemoryExtensions
{
    /// <summary>
    /// Wraps input with LangChain-based conversation context.
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationContext WithLangChainMemory<T>(this T input, int maxTurns = 10)
    {
        var context = new LangChainConversationContext(maxTurns);
        if (input != null)
        {
            context.SetProperty("input", input);
        }

        return context;
    }
}
