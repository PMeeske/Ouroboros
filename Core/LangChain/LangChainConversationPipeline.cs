// LangChain-integrated conversation pipeline builder

using LangChain.Providers;
using LangChain.Prompts.Base;

namespace LangChainPipeline.Core.LangChain;

/// <summary>
/// LangChain-integrated conversation pipeline that properly uses official LangChain chains
/// integrated with the existing monadic pipeline patterns
/// </summary>
public class LangChainConversationPipeline
{
    private readonly List<Func<LangChainConversationContext, Task<LangChainConversationContext>>> _steps = [];

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
/// Builder extensions to create LangChain-integrated conversational flows using official LangChain chains
/// </summary>
public static class LangChainConversationBuilder
{
    /// <summary>
    /// Creates a conversational pipeline builder using proper LangChain integration
    /// </summary>
    public static LangChainConversationPipeline CreateConversationPipeline()
    {
        return new LangChainConversationPipeline();
    }

    /// <summary>
    /// Extension to add AI response generation step using proper LangChain LLMChain
    /// </summary>
    public static LangChainConversationPipeline AddAiResponseGeneration(
        this LangChainConversationPipeline pipeline,
        IChatModel llm,
        BasePromptTemplate prompt,
        string outputKey = "text")
    {
        return pipeline.AddLangChainLlm(llm, prompt, outputKey);
    }

    /// <summary>
    /// Extension to add AI response generation step (backward compatibility with function generator)
    /// </summary>
    public static LangChainConversationPipeline AddAiResponseGeneration(
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