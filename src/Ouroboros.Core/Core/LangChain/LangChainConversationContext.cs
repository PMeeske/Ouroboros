#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// LangChain-integrated conversation context for monadic composition

namespace LangChainPipeline.Core.LangChain;

/// <summary>
/// LangChain-integrated conversation context that properly bridges with official LangChain chains
/// while maintaining the existing monadic pipeline patterns
/// </summary>
public class LangChainConversationContext
{
    private readonly Dictionary<string, object> _properties = new();
    private readonly ConversationMemory _memory;

    public LangChainConversationContext(int maxTurns = 10)
    {
        _memory = new ConversationMemory(maxTurns);
    }

    /// <summary>
    /// Sets a property in the context using LangChain patterns
    /// </summary>
    public LangChainConversationContext SetProperty(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Gets a property from the context
    /// </summary>
    public TValue? GetProperty<TValue>(string key)
    {
        return _properties.TryGetValue(key, out object? value) && value is TValue typedValue
            ? typedValue
            : default;
    }

    /// <summary>
    /// Adds a conversation turn following LangChain patterns
    /// </summary>
    public void AddTurn(string humanInput, string aiResponse)
    {
        _memory.AddTurn(humanInput, aiResponse);
    }

    /// <summary>
    /// Gets conversation history as formatted string for LangChain prompts
    /// </summary>
    public string GetConversationHistory()
    {
        return _memory.GetFormattedHistory();
    }

    /// <summary>
    /// Get all properties as dictionary for LangChain context
    /// </summary>
    public Dictionary<string, object> GetProperties() => new(_properties);
}

/// <summary>
/// Extension methods to integrate with existing WithMemory pattern using LangChain
/// </summary>
public static class LangChainMemoryExtensions
{
    /// <summary>
    /// Wraps input with LangChain-based conversation context
    /// </summary>
    public static LangChainConversationContext WithLangChainMemory<T>(this T input, int maxTurns = 10)
    {
        LangChainConversationContext context = new LangChainConversationContext(maxTurns);
        if (input != null)
        {
            context.SetProperty("input", input);
        }
        return context;
    }
}
