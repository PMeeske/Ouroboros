// ============================================================================
// Memory-Aware Kleisli Pipeline Integration
// Extension methods to integrate memory functionality with existing pipes
// ============================================================================

using LangChainPipeline.Core.Steps;
using LangChainPipeline.Core.Memory;
using LangChainPipeline.Core.Interop;

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
            var result = await step(context.Data);
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
        var context = initialData.WithMemory(memory);
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
    private readonly List<Step<MemoryContext<object>, MemoryContext<object>>> _steps = new();
    
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
        var loadStep = new Step<MemoryContext<object>, MemoryContext<object>>(context =>
        {
            var history = context.Memory.GetFormattedHistory(humanPrefix, aiPrefix);
            return Task.FromResult(context.SetProperty(outputKey, history));
        });
        
        _steps.Add(loadStep);
        return this;
    }
    
    /// <summary>
    /// Add a template processing step (similar to LangChain's Template)
    /// </summary>
    public ConversationChainBuilder<T> Template(string template)
    {
        var templateStep = new Step<MemoryContext<object>, MemoryContext<object>>(context =>
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
        });
        
        _steps.Add(templateStep);
        return this;
    }
    
    /// <summary>
    /// Add a mock LLM step (similar to LangChain's LLM)
    /// </summary>
    public ConversationChainBuilder<T> LLM(string mockPrefix = "AI Response:")
    {
        var llmStep = new Step<MemoryContext<object>, MemoryContext<object>>(context =>
        {
            var prompt = context.Data?.ToString() ?? string.Empty;
            var response = $"{mockPrefix} Processing prompt with {prompt.Length} characters - {DateTime.Now:HH:mm:ss}";
            
            var result = context
                .WithData<object>(response)
                .SetProperty("text", response);
                
            return Task.FromResult(result);
        });
        
        _steps.Add(llmStep);
        return this;
    }
    
    /// <summary>
    /// Add a step to update memory (similar to LangChain's UpdateMemory)
    /// </summary>
    public ConversationChainBuilder<T> UpdateMemory(
        string inputKey = "input", 
        string responseKey = "text")
    {
        var updateStep = new Step<MemoryContext<object>, MemoryContext<object>>(context =>
        {
            var input = context.GetProperty<string>(inputKey) ?? string.Empty;
            var response = context.GetProperty<string>(responseKey) ?? string.Empty;
            
            if (!string.IsNullOrWhiteSpace(input) && !string.IsNullOrWhiteSpace(response))
            {
                context.Memory.AddTurn(input, response);
            }
            
            return Task.FromResult(context);
        });
        
        _steps.Add(updateStep);
        return this;
    }
    
    /// <summary>
    /// Add a step to set a value (similar to LangChain's Set)
    /// </summary>
    public ConversationChainBuilder<T> Set(object value, string key)
    {
        var setStep = new Step<MemoryContext<object>, MemoryContext<object>>(context =>
            Task.FromResult(context.SetProperty(key, value)));
        
        _steps.Add(setStep);
        return this;
    }
    
    /// <summary>
    /// Build and execute the conversational chain
    /// </summary>
    public async Task<MemoryContext<object>> RunAsync()
    {
        var context = _initialContext.WithData<object>(_initialContext.Data!);
        
        foreach (var step in _steps)
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
        var result = await RunAsync();
        return result.GetProperty<TResult>(propertyKey);
    }
}