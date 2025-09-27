// LangChain-based conversation pipeline to replace custom ConversationBuilder
using LangChain.Schema;
using LangChain.Chains;

namespace LangChainPipeline.Core.LangChain;

/// <summary>
/// LangChain-based conversation pipeline that uses LangChain's chain and schema concepts
/// instead of custom ConversationBuilder
/// </summary>
public class LangChainConversationPipeline
{
    private readonly List<Func<LangChainConversationContext, Task<LangChainConversationContext>>> _steps = new();

    /// <summary>
    /// Adds a processing step to the pipeline using LangChain patterns
    /// </summary>
    public LangChainConversationPipeline AddStep(
        Func<LangChainConversationContext, Task<LangChainConversationContext>> step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a simple transformation step
    /// </summary>
    public LangChainConversationPipeline AddTransformation(
        Func<LangChainConversationContext, LangChainConversationContext> transformation)
    {
        _steps.Add(context => Task.FromResult(transformation(context)));
        return this;
    }

    /// <summary>
    /// Adds a property setter step
    /// </summary>
    public LangChainConversationPipeline SetProperty(string key, object value)
    {
        return AddTransformation(context => context.SetProperty(key, value));
    }

    /// <summary>
    /// Adds conversation history to the context
    /// </summary>
    public LangChainConversationPipeline WithConversationHistory()
    {
        return AddTransformation(context =>
        {
            var history = context.GetConversationHistory();
            if (!string.IsNullOrEmpty(history))
            {
                context.SetProperty("conversation_history", history);
            }
            return context;
        });
    }

    /// <summary>
    /// Runs the pipeline and returns the processed context
    /// </summary>
    public async Task<LangChainConversationContext> RunAsync(LangChainConversationContext initialContext)
    {
        var currentContext = initialContext;

        foreach (var step in _steps)
        {
            currentContext = await step(currentContext);
        }

        return currentContext;
    }

    /// <summary>
    /// Factory method to create a new pipeline
    /// </summary>
    public static LangChainConversationPipeline Create() => new();
}

/// <summary>
/// Builder extensions to create LangChain-based conversational flows
/// </summary>
public static class LangChainConversationBuilder
{
    /// <summary>
    /// Creates a conversational pipeline builder using LangChain patterns
    /// </summary>
    public static LangChainConversationPipeline CreateConversationPipeline()
    {
        return new LangChainConversationPipeline();
    }

    /// <summary>
    /// Extension to add AI response generation step (placeholder for LLM integration)
    /// </summary>
    public static LangChainConversationPipeline AddAIResponseGeneration(
        this LangChainConversationPipeline pipeline,
        Func<string, Task<string>> responseGenerator)
    {
        return pipeline.AddStep(async context =>
        {
            var input = context.GetProperty<string>("input") ?? "";
            var aiResponse = await responseGenerator(input);
            context.SetProperty("text", aiResponse);
            return context;
        });
    }
}