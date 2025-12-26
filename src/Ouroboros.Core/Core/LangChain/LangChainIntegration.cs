// <copyright file="LangChainIntegration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LangChain;
/// <summary>
/// Integration layer that bridges LangChain chains with the monadic pipeline system.
/// </summary>
public static class LangChainIntegration
{
    /// <summary>
    /// Converts a LangChain BaseStackableChain into a monadic KleisliResult.
    /// </summary>
    /// <returns></returns>
    public static KleisliResult<Dictionary<string, object>, Dictionary<string, object>, string> ToMonadicKleisli(
        this BaseStackableChain chain)
    {
        return async input =>
        {
            try
            {
                var chainValues = new ChainValues(input);
                var result = await chain.CallAsync(chainValues);
                return Result<Dictionary<string, object>, string>.Success(result.Value);
            }
            catch (Exception ex)
            {
                return Result<Dictionary<string, object>, string>.Failure($"LangChain execution failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Converts a LangChain LlmChain into a monadic KleisliResult.
    /// </summary>
    /// <returns></returns>
    public static KleisliResult<Dictionary<string, object>, Dictionary<string, object>, string> ToMonadicKleisli(
        this LlmChain chain)
    {
        return async input =>
        {
            try
            {
                var chainValues = new ChainValues(input);
                var result = await chain.CallAsync(chainValues);
                return Result<Dictionary<string, object>, string>.Success(result.Value);
            }
            catch (Exception ex)
            {
                return Result<Dictionary<string, object>, string>.Failure($"LangChain LLM execution failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Converts a LangChain BaseStackableChain into a plain Step (throws on error).
    /// </summary>
    /// <returns></returns>
    public static Step<Dictionary<string, object>, Dictionary<string, object>> ToStep(
        this BaseStackableChain chain)
    {
        return async input =>
        {
            var chainValues = new ChainValues(input);
            var result = await chain.CallAsync(chainValues);
            return result.Value;
        };
    }

    /// <summary>
    /// Converts a LangChain LlmChain into a plain Step (throws on error).
    /// </summary>
    /// <returns></returns>
    public static Step<Dictionary<string, object>, Dictionary<string, object>> ToStep(
        this LlmChain chain)
    {
        return async input =>
        {
            var chainValues = new ChainValues(input);
            var result = await chain.CallAsync(chainValues);
            return result.Value;
        };
    }

    /// <summary>
    /// Creates a LangChain SetChain wrapped as a monadic KleisliResult.
    /// </summary>
    /// <returns></returns>
    public static KleisliResult<Dictionary<string, object>, Dictionary<string, object>, string> CreateSetKleisli(
        object value, string outputKey = "query")
    {
        var setChain = new SetChain(value, outputKey);
        return setChain.ToMonadicKleisli();
    }

    /// <summary>
    /// Creates a LangChain LLMChain wrapped as a monadic KleisliResult.
    /// </summary>
    /// <returns></returns>
    public static KleisliResult<Dictionary<string, object>, Dictionary<string, object>, string> CreateLlmKleisli(
        IChatModel llm,
        PromptTemplate prompt,
        string outputKey = "text")
    {
        var llmChain = new LlmChain(new LlmChainInput(llm, prompt)
        {
            OutputKey = outputKey,
        });

        return llmChain.ToMonadicKleisli();
    }

    /// <summary>
    /// Creates a LangChain SetChain wrapped as a Step.
    /// </summary>
    /// <returns></returns>
    public static Step<Dictionary<string, object>, Dictionary<string, object>> CreateSetStep(
        object value, string outputKey = "query")
    {
        var setChain = new SetChain(value, outputKey);
        return setChain.ToStep();
    }

    /// <summary>
    /// Creates a LangChain LLMChain wrapped as a Step.
    /// </summary>
    /// <returns></returns>
    public static Step<Dictionary<string, object>, Dictionary<string, object>> CreateLlmStep(
        IChatModel llm,
        PromptTemplate prompt,
        string outputKey = "text")
    {
        var llmChain = new LlmChain(new LlmChainInput(llm, prompt)
        {
            OutputKey = outputKey,
        });

        return llmChain.ToStep();
    }
}

/// <summary>
/// Extensions for integrating LangChain chains with the conversation system.
/// </summary>
public static class LangChainConversationIntegration
{
    /// <summary>
    /// Updates the LangChain conversation pipeline to use proper LangChain chains.
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationPipeline AddLangChainLlm(
        this LangChainConversationPipeline pipeline,
        IChatModel llm,
        BasePromptTemplate prompt,
        string outputKey = "text")
    {
        return pipeline.AddStep(async context =>
        {
            try
            {
                // Create LLM chain
                var llmChain = new LlmChain(new LlmChainInput(llm, prompt)
                {
                    OutputKey = outputKey,
                });

                // Convert context properties to chain input
                var chainInput = new ChainValues(context.GetProperties());

                // Execute LangChain
                var result = await llmChain.CallAsync(chainInput);

                // Update context with results
                foreach (var kvp in result.Value)
                {
                    context.SetProperty(kvp.Key, kvp.Value);
                }

                return context;
            }
            catch (Exception ex)
            {
                // Handle errors gracefully in the monadic pipeline
                context.SetProperty("error", $"LangChain LLM execution failed: {ex.Message}");
                return context;
            }
        });
    }

    /// <summary>
    /// Adds a LangChain set operation to the pipeline.
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationPipeline AddLangChainSet(
        this LangChainConversationPipeline pipeline,
        object value,
        string outputKey = "query")
    {
        return pipeline.AddStep(async context =>
        {
            try
            {
                // Create SetChain
                var setChain = new SetChain(value, outputKey);

                // Convert context to chain input
                var chainInput = new ChainValues(context.GetProperties());

                // Execute chain
                var result = await setChain.CallAsync(chainInput);

                // Update context
                foreach (var kvp in result.Value)
                {
                    context.SetProperty(kvp.Key, kvp.Value);
                }

                return context;
            }
            catch (Exception ex)
            {
                context.SetProperty("error", $"LangChain Set operation failed: {ex.Message}");
                return context;
            }
        });
    }

    /// <summary>
    /// Adds a generic LangChain stackable chain to the pipeline.
    /// </summary>
    /// <returns></returns>
    public static LangChainConversationPipeline AddLangChainStep(
        this LangChainConversationPipeline pipeline,
        BaseStackableChain chain)
    {
        return pipeline.AddStep(async context =>
        {
            try
            {
                // Convert context to chain input
                var chainInput = new ChainValues(context.GetProperties());

                // Execute chain
                var result = await chain.CallAsync(chainInput);

                // Update context
                foreach (var kvp in result.Value)
                {
                    context.SetProperty(kvp.Key, kvp.Value);
                }

                return context;
            }
            catch (Exception ex)
            {
                context.SetProperty("error", $"LangChain chain execution failed: {ex.Message}");
                return context;
            }
        });
    }
}
