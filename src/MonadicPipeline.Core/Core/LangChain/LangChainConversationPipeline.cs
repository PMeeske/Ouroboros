// <copyright file="LangChainConversationPipeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LangChain;
/// <summary>
/// LangChain-integrated conversation pipeline that properly uses official LangChain chains
/// integrated with the existing monadic pipeline patterns.
/// </summary>
public class LangChainConversationPipeline
{
    private readonly List<Func<LangChainConversationContext, Task<LangChainConversationContext>>> steps = [];

    /// <summary>
    /// Adds a processing step to the pipeline using LangChain patterns.
    /// </summary>
    /// <returns></returns>
    public LangChainConversationPipeline AddStep(
        Func<LangChainConversationContext, Task<LangChainConversationContext>> step)
    {
        this.steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a simple transformation step.
    /// </summary>
    /// <returns></returns>
    public LangChainConversationPipeline AddTransformation(
        Func<LangChainConversationContext, LangChainConversationContext> transformation)
    {
        this.steps.Add(context => Task.FromResult(transformation(context)));
        return this;
    }

    /// <summary>
    /// Adds a property setter step.
    /// </summary>
    /// <returns></returns>
    public LangChainConversationPipeline SetProperty(string key, object value)
    {
        return this.AddTransformation(context => context.SetProperty(key, value));
    }

    /// <summary>
    /// Adds conversation history to the context.
    /// </summary>
    /// <returns></returns>
    public LangChainConversationPipeline WithConversationHistory()
    {
        return this.AddTransformation(context =>
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
    /// Runs the pipeline and returns the processed context.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<LangChainConversationContext> RunAsync(LangChainConversationContext initialContext)
    {
        var currentContext = initialContext;

        foreach (Func<LangChainConversationContext, Task<LangChainConversationContext>> step in this.steps)
        {
            currentContext = await step(currentContext);
        }

        return currentContext;
    }

    /// <summary>
    /// Factory method to create a new pipeline.
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationPipeline Create() => new();
}

/// <summary>
/// Builder extensions to create LangChain-integrated conversational flows using official LangChain chains.
/// </summary>
public static class LangChainConversationBuilder
{
    /// <summary>
    /// Creates a conversational pipeline builder using proper LangChain integration.
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationPipeline CreateConversationPipeline()
    {
        return new LangChainConversationPipeline();
    }

    /// <summary>
    /// Extension to add AI response generation step using proper LangChain LLMChain.
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationPipeline AddAiResponseGeneration(
        this LangChainConversationPipeline pipeline,
        IChatModel llm,
        PromptTemplate prompt,
        string outputKey = "text")
    {
        return pipeline.AddLangChainLlm(llm, prompt, outputKey);
    }

    /// <summary>
    /// Extension to add AI response generation step (backward compatibility with function generator).
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationPipeline AddAiResponseGeneration(
        this LangChainConversationPipeline pipeline,
        Func<string, Task<string>> responseGenerator)
    {
        return pipeline.AddStep(async context =>
        {
            var input = context.GetProperty<string>("input") ?? string.Empty;
            var aiResponse = await responseGenerator(input);
            context.SetProperty("text", aiResponse);
            return context;
        });
    }
}
