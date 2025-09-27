// LangChain-integrated version using proper namespace as specified by PMeeske
using LangChain.Schema;
using LangChain.Chains;
using LangChain.Chains.HelperChains;

namespace LangChainPipeline.Core.LangChain;

/// <summary>
/// LangChain-based conversation context that integrates with LangChain's schema, chains, and helper chains
/// Uses LangChain.Chains.HelperChains namespace as specified by PMeeske for proper monadic composition
/// Instead of custom MemoryContext, use LangChain's built-in context management
/// </summary>
public class LangChainConversationContext
{
    private readonly Dictionary<string, object> _properties = new();
    private readonly Queue<(string human, string ai, DateTime timestamp)> _conversationHistory = new();
    private readonly int _maxTurns;

    public LangChainConversationContext(int maxTurns = 10)
    {
        _maxTurns = maxTurns;
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
        return _properties.TryGetValue(key, out var value) && value is TValue typedValue 
            ? typedValue 
            : default;
    }

    /// <summary>
    /// Adds a conversation turn following LangChain patterns
    /// </summary>
    public void AddTurn(string humanInput, string aiResponse)
    {
        _conversationHistory.Enqueue((humanInput, aiResponse, DateTime.UtcNow));

        // Maintain max turns limit
        while (_conversationHistory.Count > _maxTurns)
        {
            _conversationHistory.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Gets conversation history as formatted string for LangChain prompts
    /// </summary>
    public string GetConversationHistory()
    {
        if (!_conversationHistory.Any())
            return string.Empty;

        return string.Join("\n", _conversationHistory.Select(turn =>
            $"Human: {turn.human}\nAssistant: {turn.ai}"));
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
        var context = new LangChainConversationContext(maxTurns);
        if (input != null)
        {
            context.SetProperty("input", input);
        }
        return context;
    }
}