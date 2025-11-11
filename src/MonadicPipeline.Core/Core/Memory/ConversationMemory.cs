// <copyright file="ConversationMemory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Memory;

using System.Collections.Concurrent;

/// <summary>
/// Represents a memory context that maintains conversation history
/// and can be used with Kleisli arrows for memory-aware processing.
/// </summary>
public class ConversationMemory
{
    private readonly ConcurrentQueue<ConversationTurn> turns = new();
    private readonly int maxTurns;

    public ConversationMemory(int maxTurns = 10)
    {
        this.maxTurns = maxTurns;
    }

    /// <summary>
    /// Add a turn to the conversation memory.
    /// </summary>
    public void AddTurn(string humanInput, string aiResponse)
    {
        this.turns.Enqueue(new ConversationTurn(humanInput, aiResponse, DateTime.UtcNow));

        // Maintain max turns limit
        while (this.turns.Count > this.maxTurns)
        {
            this.turns.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Get the conversation history formatted for prompts.
    /// </summary>
    /// <returns></returns>
    public string GetFormattedHistory(string humanPrefix = "Human", string aiPrefix = "AI")
    {
        var turns = this.turns.ToArray();
        if (turns.Length == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", turns.Select(turn =>
            $"{humanPrefix}: {turn.HumanInput}\n{aiPrefix}: {turn.AiResponse}"));
    }

    /// <summary>
    /// Get all conversation turns.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<ConversationTurn> GetTurns() => this.turns.ToArray();

    /// <summary>
    /// Clear all memory.
    /// </summary>
    public void Clear() => this.turns.Clear();
}

/// <summary>
/// Represents a single turn in a conversation.
/// </summary>
public record ConversationTurn(
    string HumanInput,
    string AiResponse,
    DateTime Timestamp);

/// <summary>
/// Memory-aware pipeline context that carries both data and memory state.
/// </summary>
/// <typeparam name="T">The data type being processed.</typeparam>
public record MemoryContext<T>(
    T Data,
    ConversationMemory Memory,
    Dictionary<string, object>? Properties = null)
{
    public Dictionary<string, object> Properties { get; } = Properties ?? new();

    /// <summary>
    /// Create a new context with updated data.
    /// </summary>
    /// <returns></returns>
    public MemoryContext<TNew> WithData<TNew>(TNew newData)
        => new(newData, this.Memory, this.Properties);

    /// <summary>
    /// Set a property value.
    /// </summary>
    /// <returns></returns>
    public MemoryContext<T> SetProperty(string key, object value)
    {
        var newProperties = new Dictionary<string, object>(this.Properties)
        {
            [key] = value,
        };
        return new MemoryContext<T>(this.Data, this.Memory, newProperties);
    }

    /// <summary>
    /// Get a property value.
    /// </summary>
    /// <returns></returns>
    public TValue? GetProperty<TValue>(string key)
        => this.Properties.TryGetValue(key, out var value) && value is TValue typed
            ? typed
            : default;
}

/// <summary>
/// Kleisli arrows for memory-aware operations.
/// </summary>
public static class MemoryArrows
{
    /// <summary>
    /// Create a memory-aware arrow that loads conversation history into the context.
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<T>, MemoryContext<T>> LoadMemory<T>(
        string outputKey = "history",
        string humanPrefix = "Human",
        string aiPrefix = "AI")
    {
        return context =>
        {
            var history = context.Memory.GetFormattedHistory(humanPrefix, aiPrefix);
            return Task.FromResult(context.SetProperty(outputKey, history));
        };
    }

    /// <summary>
    /// Create a memory-aware arrow that updates memory with new conversation turn.
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<T>, MemoryContext<T>> UpdateMemory<T>(
        string inputKey = "input",
        string responseKey = "text")
    {
        return context =>
        {
            var input = context.GetProperty<string>(inputKey) ?? string.Empty;
            var response = context.GetProperty<string>(responseKey) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(input) && !string.IsNullOrWhiteSpace(response))
            {
                context.Memory.AddTurn(input, response);
            }

            return Task.FromResult(context);
        };
    }

    /// <summary>
    /// Create a template processing arrow for conversation prompts.
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<string>, MemoryContext<string>> Template(string template)
    {
        return context =>
        {
            var processedTemplate = template;

            // Replace template variables with values from properties
            foreach (var prop in context.Properties)
            {
                var placeholder = $"{{{prop.Key}}}";
                if (processedTemplate.Contains(placeholder))
                {
                    processedTemplate = processedTemplate.Replace(placeholder, prop.Value?.ToString() ?? string.Empty);
                }
            }

            return Task.FromResult(context.WithData(processedTemplate));
        };
    }

    /// <summary>
    /// Create a template processing arrow for conversation prompts (generic version).
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<object>, MemoryContext<object>> Template<T>(string template)
    {
        return context =>
        {
            var processedTemplate = template;

            // Replace template variables with values from properties
            foreach (var prop in context.Properties)
            {
                var placeholder = $"{{{prop.Key}}}";
                if (processedTemplate.Contains(placeholder))
                {
                    processedTemplate = processedTemplate.Replace(placeholder, prop.Value?.ToString() ?? string.Empty);
                }
            }

            return Task.FromResult(context.WithData<object>(processedTemplate));
        };
    }

    /// <summary>
    /// Set a value in the memory context.
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<T>, MemoryContext<T>> Set<T>(object value, string key)
    {
        return context => Task.FromResult(context.SetProperty(key, value));
    }

    /// <summary>
    /// Create a mock LLM step for demonstration purposes.
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<string>, MemoryContext<string>> MockLlm(string mockPrefix = "AI Response:")
    {
        return context =>
        {
            var prompt = context.Data;
            var response = $"{mockPrefix} Processing prompt with {prompt.Length} characters - {DateTime.Now:HH:mm:ss}";

            var result = context
                .WithData(response)
                .SetProperty("text", response);

            return Task.FromResult(result);
        };
    }

    /// <summary>
    /// Create a mock LLM step for demonstration purposes (generic version).
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<object>, MemoryContext<object>> MockLlm<T>(string mockPrefix = "AI Response:")
    {
        return context =>
        {
            var prompt = context.Data?.ToString() ?? string.Empty;
            var response = $"{mockPrefix} Processing prompt with {prompt.Length} characters - {DateTime.Now:HH:mm:ss}";

            var result = context
                .WithData<object>(response)
                .SetProperty("text", response);

            return Task.FromResult(result);
        };
    }

    /// <summary>
    /// Extract a property value as the main data.
    /// </summary>
    /// <returns></returns>
    public static Step<MemoryContext<T>, MemoryContext<TOut>> ExtractProperty<T, TOut>(string key)
    {
        return context =>
        {
            var value = context.GetProperty<TOut>(key) ?? default(TOut)!;
            return Task.FromResult(context.WithData(value));
        };
    }
}
