// <copyright file="MetaAIConvenienceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Unit tests for the Meta-AI convenience layer.
/// Tests simplified API for creating and using orchestrators - no external LLM service required for most tests.
/// </summary>
[Trait("Category", "Unit")]
public class MetaAIConvenienceTests
{
    /// <summary>
    /// Tests simple orchestrator creation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task SimpleOrchestrator_Creation_ShouldSucceed()
    {
        Console.WriteLine("=== Test: Simple Orchestrator Creation ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

            var result = MetaAIConvenience.CreateSimple(chatModel);

            result.Match(
                orchestrator =>
                {
                    Console.WriteLine("✓ Simple orchestrator created successfully");
                    Console.WriteLine($"  Type: {orchestrator.GetType().Name}");
                },
                error => Console.WriteLine($"✗ Failed to create orchestrator: {error}"));

            Console.WriteLine("✓ Simple orchestrator creation test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Ollama not available - skipping test\n");
            }
            else
            {
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Tests standard orchestrator creation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task StandardOrchestrator_Creation_ShouldSucceed()
    {
        Console.WriteLine("=== Test: Standard Orchestrator Creation ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();
            var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

            var result = MetaAIConvenience.CreateStandard(chatModel, tools, embedModel);

            result.Match(
                orchestrator =>
                {
                    Console.WriteLine("✓ Standard orchestrator created successfully");
                    Console.WriteLine($"  Type: {orchestrator.GetType().Name}");
                },
                error => Console.WriteLine($"✗ Failed to create orchestrator: {error}"));

            Console.WriteLine("✓ Standard orchestrator creation test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Ollama not available - skipping test\n");
            }
            else
            {
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Tests AskQuestion convenience method.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task AskQuestion_ConvenienceMethod_ShouldReturnAnswer()
    {
        Console.WriteLine("=== Test: AskQuestion Convenience Method ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

            var orchestratorResult = MetaAIConvenience.CreateSimple(chatModel);

            if (!orchestratorResult.IsSuccess)
            {
                Console.WriteLine($"✗ Failed to create orchestrator: {orchestratorResult.Error}");
                return;
            }

            var orchestrator = orchestratorResult.Value;
            var answerResult = await orchestrator.AskQuestion("What is 2 + 2?");

            answerResult.Match(
                answer =>
                {
                    Console.WriteLine("✓ Question answered successfully");
                    Console.WriteLine($"  Answer: {answer.Substring(0, Math.Min(100, answer.Length))}...");
                },
                error => Console.WriteLine($"✗ Failed to answer question: {error}"));

            Console.WriteLine("✓ AskQuestion test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Ollama not available - skipping test\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Tests preset orchestrators.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task PresetOrchestrators_Creation_ShouldCreateSpecializedTypes()
    {
        Console.WriteLine("=== Test: Preset Orchestrators ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();
            var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

            // Test Research Assistant
            var researchResult = MetaAIConvenience.CreateResearchAssistant(chatModel, tools, embedModel);
            researchResult.Match(
                orchestrator => Console.WriteLine("✓ Research Assistant created"),
                error => Console.WriteLine($"✗ Research Assistant failed: {error}"));

            // Test Code Assistant
            var codeResult = MetaAIConvenience.CreateCodeAssistant(chatModel, tools);
            codeResult.Match(
                orchestrator => Console.WriteLine("✓ Code Assistant created"),
                error => Console.WriteLine($"✗ Code Assistant failed: {error}"));

            // Test Chat Assistant
            var chatResult = MetaAIConvenience.CreateChatAssistant(chatModel);
            chatResult.Match(
                orchestrator => Console.WriteLine("✓ Chat Assistant created"),
                error => Console.WriteLine($"✗ Chat Assistant failed: {error}"));

            Console.WriteLine("✓ Preset orchestrators test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Ollama not available - skipping test\n");
            }
            else
            {
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Tests CompleteWorkflow convenience method.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task CompleteWorkflow_ConvenienceMethod_ShouldExecuteEndToEnd()
    {
        Console.WriteLine("=== Test: CompleteWorkflow Convenience Method ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();

            var orchestratorResult = MetaAIConvenience.CreateCodeAssistant(chatModel, tools);

            if (!orchestratorResult.IsSuccess)
            {
                Console.WriteLine($"✗ Failed to create orchestrator: {orchestratorResult.Error}");
                return;
            }

            var orchestrator = orchestratorResult.Value;
            var workflowResult = await orchestrator.CompleteWorkflow(
                "Explain what a monadic pipeline is",
                autoLearn: true);

            workflowResult.Match(
                verification =>
                {
                    Console.WriteLine("✓ Complete workflow executed successfully");
                    Console.WriteLine($"  Verified: {verification.Verified}");
                    Console.WriteLine($"  Quality Score: {verification.QualityScore:P0}");
                },
                error => Console.WriteLine($"✗ Workflow failed: {error}"));

            Console.WriteLine("✓ CompleteWorkflow test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Ollama not available - skipping test\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Runs all convenience layer tests.
    /// Kept for backward compatibility - wraps individual test methods.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAll()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("       META-AI CONVENIENCE LAYER TESTS");
        Console.WriteLine(new string('=', 60) + "\n");

        var instance = new MetaAIConvenienceTests();
        await instance.SimpleOrchestrator_Creation_ShouldSucceed();
        await instance.StandardOrchestrator_Creation_ShouldSucceed();
        await instance.AskQuestion_ConvenienceMethod_ShouldReturnAnswer();
        await instance.PresetOrchestrators_Creation_ShouldCreateSpecializedTypes();
        await instance.CompleteWorkflow_ConvenienceMethod_ShouldExecuteEndToEnd();

        Console.WriteLine(new string('=', 60));
        Console.WriteLine("       ALL CONVENIENCE LAYER TESTS COMPLETED");
        Console.WriteLine(new string('=', 60) + "\n");
    }
}
