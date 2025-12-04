#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ============================================================================
// Memory-Aware Kleisli Pipeline Integration
// Extension methods to integrate memory functionality with existing pipes
// ============================================================================

namespace LangChainPipeline.Core.Memory;

/// <summary>
/// Extension methods to integrate memory-aware operations with the existing Kleisli pipe system
/// </summary>
public static class MemoryPipeExtensions
{
    /// <summary>
    /// Create a memory context from a plain value
    /// </summary>
    public static MemoryContext<T> WithMemory<T>(this T value, ConversationMemory? memory = null)
        => new(value, memory ?? new ConversationMemory());

    /// <summary>
    /// Lift a regular Step into a memory-aware Step
    /// </summary>
    public static Step<MemoryContext<TIn>, MemoryContext<TOut>> LiftToMemory<TIn, TOut>(
        this Step<TIn, TOut> step)
    {
        return async context =>
        {
            TOut? result = await step(context.Data);
            return context.WithData(result);
        };
    }

    /// <summary>
    /// Convert a memory-aware step to a compatible node for interop
    /// </summary>
    public static PipeNode<MemoryContext<TIn>, MemoryContext<TOut>> ToMemoryNode<TIn, TOut>(
        this Step<MemoryContext<TIn>, MemoryContext<TOut>> step,
        string? name = null)
    {
        return step.ToCompatNode(name ?? $"Memory[{typeof(TIn).Name}->{typeof(TOut).Name}]");
    }

    /// <summary>
    /// Create a conversational chain builder similar to LangChain's approach
    /// </summary>
    public static ConversationChainBuilder<T> StartConversation<T>(
        this T initialData,
        ConversationMemory? memory = null)
    {
        MemoryContext<T> context = initialData.WithMemory(memory);
        return new ConversationChainBuilder<T>(context);
    }

    /// <summary>
    /// Extract the final result from a memory context
    /// </summary>
    public static T ExtractData<T>(this MemoryContext<T> context) => context.Data;

    /// <summary>
    /// Extract a specific property from a memory context
    /// </summary>
    public static TValue? ExtractProperty<TValue>(this MemoryContext<object> context, string key)
        => context.GetProperty<TValue>(key);
}

/// <summary>
/// Fluent builder for conversational chains that mirrors LangChain's approach
/// but uses our Kleisli pipe system
/// </summary>
public class ConversationChainBuilder<T>
{
    private readonly MemoryContext<T> _initialContext;
    private readonly List<Step<MemoryContext<object>, MemoryContext<object>>> _steps = [];

    public ConversationChainBuilder(MemoryContext<T> initialContext)
    {
        _initialContext = initialContext;
    }

    /// <summary>
    /// Add a step to load memory (similar to LangChain's LoadMemory)
    /// </summary>
    public ConversationChainBuilder<T> LoadMemory(
        string outputKey = "history",
        string humanPrefix = "Human",
        string aiPrefix = "AI")
    {
        _steps.Add(MemoryArrows.LoadMemory<object>(outputKey, humanPrefix, aiPrefix));
        return this;
    }

    /// <summary>
    /// Add a template processing step (similar to LangChain's Template)
    /// </summary>
    public ConversationChainBuilder<T> Template(string template)
    {
        _steps.Add(MemoryArrows.Template<object>(template));
        return this;
    }

    /// <summary>
    /// Add a mock LLM step (similar to LangChain's LLM)
    /// </summary>
    public ConversationChainBuilder<T> Llm(string mockPrefix = "AI Response:")
    {
        _steps.Add(MemoryArrows.MockLlm<object>(mockPrefix));
        return this;
    }

    /// <summary>
    /// Add a step to update memory (similar to LangChain's UpdateMemory)
    /// </summary>
    public ConversationChainBuilder<T> UpdateMemory(
        string inputKey = "input",
        string responseKey = "text")
    {
        _steps.Add(MemoryArrows.UpdateMemory<object>(inputKey, responseKey));
        return this;
    }

    /// <summary>
    /// Add a step to set a value (similar to LangChain's Set)
    /// </summary>
    public ConversationChainBuilder<T> Set(object value, string key)
    {
        _steps.Add(MemoryArrows.Set<object>(value, key));
        return this;
    }

    /// <summary>
    /// Build and execute the conversational chain
    /// </summary>
    public async Task<MemoryContext<object>> RunAsync()
    {
        object initialData = _initialContext.Data ?? (object)string.Empty;
        MemoryContext<object> context = _initialContext.WithData<object>(initialData);

        foreach (Step<MemoryContext<object>, MemoryContext<object>> step in _steps)
        {
            context = await step(context);
        }

        return context;
    }

    /// <summary>
    /// Build and extract a specific property value
    /// </summary>
    public async Task<TResult?> RunAsync<TResult>(string propertyKey)
    {
        MemoryContext<object> result = await RunAsync();
        return result.GetProperty<TResult>(propertyKey);
    }
}
