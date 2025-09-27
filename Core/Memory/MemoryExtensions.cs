using LangChainPipeline.Core.Steps;

namespace LangChainPipeline.Core.Memory;

/// <summary>
/// Extension methods for integrating memory context with the pipeline system.
/// </summary>
public static class MemoryExtensions
{
    /// <summary>
    /// Wraps input in a memory context with default settings.
    /// </summary>
    /// <typeparam name="T">The type of input</typeparam>
    /// <param name="input">The input to wrap</param>
    /// <param name="maxTurns">Maximum number of conversation turns to keep</param>
    /// <returns>A new memory context containing the input</returns>
    public static MemoryContext<T> WithMemory<T>(this T input, int maxTurns = 10)
    {
        return new MemoryContext<T>(input, maxTurns);
    }

    /// <summary>
    /// Creates a contextual step that operates on memory context.
    /// </summary>
    /// <typeparam name="TInput">The input type wrapped in memory context</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <typeparam name="TContext">The context type</typeparam>
    /// <param name="step">The step that operates on memory context</param>
    /// <returns>A contextual step that works with memory context</returns>
    public static ContextualStep<MemoryContext<TInput>, TOutput, TContext> WithMemoryContext<TInput, TOutput, TContext>(
        this Step<MemoryContext<TInput>, TOutput> step)
    {
        return ContextualStep.FromPure<MemoryContext<TInput>, TOutput, TContext>(
            step, 
            "Processing with memory context");
    }

    /// <summary>
    /// Maps over the input within a memory context.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <param name="memoryContext">The memory context</param>
    /// <param name="mapper">The function to map the input</param>
    /// <returns>A new memory context with the mapped input</returns>
    public static MemoryContext<TOutput> MapInput<TInput, TOutput>(
        this MemoryContext<TInput> memoryContext,
        Func<TInput, TOutput> mapper)
    {
        var newContext = new MemoryContext<TOutput>(mapper(memoryContext.Input), memoryContext.GetTurns().Count);
        
        // Copy properties
        foreach (var kvp in memoryContext.Properties)
        {
            newContext.Properties[kvp.Key] = kvp.Value;
        }
        
        // Copy conversation turns
        foreach (var turn in memoryContext.GetTurns())
        {
            newContext.AddTurn(turn.HumanInput, turn.AiResponse);
        }
        
        return newContext;
    }

    /// <summary>
    /// Adds conversation history to the memory context as a formatted string property.
    /// </summary>
    /// <typeparam name="T">The input type</typeparam>
    /// <param name="memoryContext">The memory context</param>
    /// <param name="propertyName">The property name to store the history (default: "conversation_history")</param>
    /// <returns>The memory context with conversation history added as a property</returns>
    public static MemoryContext<T> WithConversationHistory<T>(
        this MemoryContext<T> memoryContext,
        string propertyName = "conversation_history")
    {
        var turns = memoryContext.GetTurns();
        if (turns.Any())
        {
            var history = string.Join("\n---\n", turns.Select(t => 
                $"Human: {t.HumanInput}\nAI: {t.AiResponse}\nTime: {t.Timestamp:yyyy-MM-dd HH:mm:ss}"));
            memoryContext.SetProperty(propertyName, history);
        }
        
        return memoryContext;
    }
}