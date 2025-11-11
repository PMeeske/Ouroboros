// <copyright file="MemoryPipeExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Memory;

/// <summary>
/// Extension methods to integrate memory-aware operations with the existing Kleisli pipe system.
/// </summary>
public static class MemoryPipeExtensions
{
    /// <summary>
    /// Create a memory context from a plain value.
    /// </summary>
    /// <returns></returns>
    public static MemoryContext<T> WithMemory<T>(this T value, ConversationMemory? memory = null)
        => new(value, memory ?? new ConversationMemory());

    /// <summary>
    /// Lift a regular Step into a memory-aware Step.
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<TIn>, MemoryContext<TOut>> LiftToMemory<TIn, TOut>(
        this Step<TIn, TOut> step)
    {
        return async context =>
        {
            var result = await step(context.Data);
            return context.WithData(result);
        };
    }

    /// <summary>
    /// Convert a memory-aware step to a compatible node for interop.
    /// </summary>
    /// <returns></returns>
    public static PipeNode<MemoryContext<TIn>, MemoryContext<TOut>> ToMemoryNode<TIn, TOut>(
        this Step<MemoryContext<TIn>, MemoryContext<TOut>> step,
        string? name = null)
    {
        return step.ToCompatNode(name ?? $"Memory[{typeof(TIn).Name}->{typeof(TOut).Name}]");
    }

    /// <summary>
    /// Create a conversational chain builder similar to LangChain's approach.
    /// </summary>
    /// <returns></returns>
    public static ConversationChainBuilder<T> StartConversation<T>(
        this T initialData,
        ConversationMemory? memory = null)
    {
        var context = initialData.WithMemory(memory);
        return new ConversationChainBuilder<T>(context);
    }

    /// <summary>
    /// Extract the final result from a memory context.
    /// </summary>
    /// <returns></returns>
    public static T ExtractData<T>(this MemoryContext<T> context) => context.Data;

    /// <summary>
    /// Extract a specific property from a memory context.
    /// </summary>
    /// <returns></returns>
    public static TValue? ExtractProperty<TValue>(this MemoryContext<object> context, string key)
        => context.GetProperty<TValue>(key);
}

/// <summary>
/// Fluent builder for conversational chains that mirrors LangChain's approach
/// but uses our Kleisli pipe system.
/// </summary>
public class ConversationChainBuilder<T>
{
    private readonly MemoryContext<T> initialContext;
    private readonly List<Step<MemoryContext<object>, MemoryContext<object>>> steps = [];

    public ConversationChainBuilder(MemoryContext<T> initialContext)
    {
        this.initialContext = initialContext;
    }

    /// <summary>
    /// Add a step to load memory (similar to LangChain's LoadMemory).
    /// </summary>
    /// <returns></returns>
    public ConversationChainBuilder<T> LoadMemory(
        string outputKey = "history",
        string humanPrefix = "Human",
        string aiPrefix = "AI")
    {
        this.steps.Add(MemoryArrows.LoadMemory<object>(outputKey, humanPrefix, aiPrefix));
        return this;
    }

    /// <summary>
    /// Add a template processing step (similar to LangChain's Template).
    /// </summary>
    /// <returns></returns>
    public ConversationChainBuilder<T> Template(string template)
    {
        this.steps.Add(MemoryArrows.Template<object>(template));
        return this;
    }

    /// <summary>
    /// Add a mock LLM step (similar to LangChain's LLM).
    /// </summary>
    /// <returns></returns>
    public ConversationChainBuilder<T> Llm(string mockPrefix = "AI Response:")
    {
        this.steps.Add(MemoryArrows.MockLlm<object>(mockPrefix));
        return this;
    }

    /// <summary>
    /// Add a step to update memory (similar to LangChain's UpdateMemory).
    /// </summary>
    /// <returns></returns>
    public ConversationChainBuilder<T> UpdateMemory(
        string inputKey = "input",
        string responseKey = "text")
    {
        this.steps.Add(MemoryArrows.UpdateMemory<object>(inputKey, responseKey));
        return this;
    }

    /// <summary>
    /// Add a step to set a value (similar to LangChain's Set).
    /// </summary>
    /// <returns></returns>
    public ConversationChainBuilder<T> Set(object value, string key)
    {
        this.steps.Add(MemoryArrows.Set<object>(value, key));
        return this;
    }

    /// <summary>
    /// Build and execute the conversational chain.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<MemoryContext<object>> RunAsync()
    {
        var initialData = this.initialContext.Data ?? (object)string.Empty;
        var context = this.initialContext.WithData<object>(initialData);

        foreach (var step in this.steps)
        {
            context = await step(context);
        }

        return context;
    }

    /// <summary>
    /// Build and extract a specific property value.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<TResult?> RunAsync<TResult>(string propertyKey)
    {
        var result = await this.RunAsync();
        return result.GetProperty<TResult>(propertyKey);
    }
}
