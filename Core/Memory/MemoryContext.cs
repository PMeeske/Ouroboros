using LangChainPipeline.Domain.Events;

namespace LangChainPipeline.Core.Memory;

/// <summary>
/// Memory context for conversational pipelines with property management and conversation history.
/// </summary>
/// <typeparam name="T">The type of the input context</typeparam>
public class MemoryContext<T>
{
    private readonly Queue<ConversationTurn> _turns = new();
    private readonly int _maxTurns;

    /// <summary>
    /// The underlying input context.
    /// </summary>
    public T Input { get; }

    /// <summary>
    /// Dictionary of properties stored in memory context.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new();

    /// <summary>
    /// Initializes a new instance of the MemoryContext class.
    /// </summary>
    /// <param name="input">The input context</param>
    /// <param name="maxTurns">Maximum number of conversation turns to keep in memory</param>
    public MemoryContext(T input, int maxTurns = 10)
    {
        Input = input;
        _maxTurns = maxTurns;
    }

    /// <summary>
    /// Sets a property in the memory context.
    /// </summary>
    /// <param name="key">The property key</param>
    /// <param name="value">The property value</param>
    /// <returns>The same memory context for method chaining</returns>
    public MemoryContext<T> SetProperty(string key, object value)
    {
        Properties[key] = value;
        return this;
    }

    /// <summary>
    /// Gets a property from the memory context.
    /// </summary>
    /// <typeparam name="TValue">The expected type of the property value</typeparam>
    /// <param name="key">The property key</param>
    /// <returns>The property value or default if not found</returns>
    public TValue? GetProperty<TValue>(string key)
    {
        if (Properties.TryGetValue(key, out var value) && value is TValue typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Adds a conversation turn to the memory.
    /// </summary>
    /// <param name="humanInput">The human input</param>
    /// <param name="aiResponse">The AI response</param>
    public void AddTurn(string humanInput, string aiResponse)
    {
        _turns.Enqueue(new ConversationTurn(humanInput, aiResponse, DateTime.UtcNow));

        // Maintain max turns limit
        while (_turns.Count > _maxTurns)
        {
            _turns.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Gets all conversation turns in chronological order.
    /// </summary>
    /// <returns>A read-only collection of conversation turns</returns>
    public IReadOnlyCollection<ConversationTurn> GetTurns()
    {
        return _turns.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the most recent conversation turn.
    /// </summary>
    /// <returns>The most recent turn or null if no turns exist</returns>
    public ConversationTurn? GetLastTurn()
    {
        return _turns.LastOrDefault();
    }

    /// <summary>
    /// Clears all conversation turns from memory.
    /// </summary>
    public void ClearTurns()
    {
        _turns.Clear();
    }
}