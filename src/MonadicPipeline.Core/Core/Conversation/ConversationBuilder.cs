// <copyright file="ConversationBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Conversation;

/// <summary>
/// Builder for creating conversational pipelines with memory management.
/// </summary>
/// <typeparam name="TInput">The input type.</typeparam>
/// <typeparam name="TContext">The context type.</typeparam>
public class ConversationBuilder<TInput, TContext>
{
    private readonly List<ContextualStep<MemoryContext<TInput>, MemoryContext<TInput>, TContext>> steps = [];
    private readonly TContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationBuilder{TInput, TContext}"/> class.
    /// Initializes a new conversation builder with the specified context.
    /// </summary>
    /// <param name="context">The context for the conversation.</param>
    public ConversationBuilder(TContext context)
    {
        this.context = context;
    }

    /// <summary>
    /// Adds a step to the conversation pipeline.
    /// </summary>
    /// <param name="step">The step to add.</param>
    /// <returns>The conversation builder for method chaining.</returns>
    public ConversationBuilder<TInput, TContext> AddStep(
        ContextualStep<MemoryContext<TInput>, MemoryContext<TInput>, TContext> step)
    {
        this.steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a processing step that modifies the memory context.
    /// </summary>
    /// <param name="processor">The processing function.</param>
    /// <param name="logMessage">Optional log message.</param>
    /// <returns>The conversation builder for method chaining.</returns>
    public ConversationBuilder<TInput, TContext> AddProcessor(
        Func<MemoryContext<TInput>, TContext, Task<MemoryContext<TInput>>> processor,
        string? logMessage = null)
    {
        ContextualStep<MemoryContext<TInput>, MemoryContext<TInput>, TContext> step = new ContextualStep<MemoryContext<TInput>, MemoryContext<TInput>, TContext>(
            async (input, context) =>
            {
                MemoryContext<TInput> result = await processor(input, context);
                List<string> logs = logMessage != null ? [logMessage] : new List<string>();
                return (result, logs);
            });

        this.steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a simple transformation step.
    /// </summary>
    /// <param name="transformer">The transformation function.</param>
    /// <param name="logMessage">Optional log message.</param>
    /// <returns>The conversation builder for method chaining.</returns>
    public ConversationBuilder<TInput, TContext> AddTransformation(
        Func<MemoryContext<TInput>, MemoryContext<TInput>> transformer,
        string? logMessage = null)
    {
        return this.AddProcessor(
            (input, context) => Task.FromResult(transformer(input)),
            logMessage);
    }

    /// <summary>
    /// Builds and returns the complete conversational pipeline.
    /// </summary>
    /// <returns>A step that processes the entire conversation pipeline.</returns>
    public Step<MemoryContext<TInput>, (MemoryContext<TInput> result, List<string> logs)> Build()
    {
        return async input =>
        {
            MemoryContext<TInput> currentInput = input;
            List<string> allLogs = new List<string>();

            foreach (ContextualStep<MemoryContext<TInput>, MemoryContext<TInput>, TContext> step in this.steps)
            {
                (MemoryContext<TInput> result, List<string> logs) = await step(currentInput, this.context);
                currentInput = result;
                allLogs.AddRange(logs);
            }

            return (currentInput, allLogs);
        };
    }

    /// <summary>
    /// Builds and runs the conversational pipeline, returning only the result.
    /// </summary>
    /// <returns>A step that processes the conversation and returns the result.</returns>
    public Step<MemoryContext<TInput>, MemoryContext<TInput>> BuildAndRun()
    {
        Step<MemoryContext<TInput>, (MemoryContext<TInput> result, List<string> logs)> pipeline = this.Build();
        return async input =>
        {
            (MemoryContext<TInput> result, List<string> _) = await pipeline(input);
            return result;
        };
    }

    /// <summary>
    /// Runs the conversational pipeline with the provided input.
    /// </summary>
    /// <param name="input">The input memory context.</param>
    /// <returns>The processed memory context.</returns>
    public async Task<MemoryContext<TInput>> RunAsync(MemoryContext<TInput> input)
    {
        Step<MemoryContext<TInput>, MemoryContext<TInput>> pipeline = this.BuildAndRun();
        return await pipeline(input);
    }
}
