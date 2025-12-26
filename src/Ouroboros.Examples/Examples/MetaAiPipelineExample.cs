// <copyright file="MetaAiPipelineExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Application;
using Ouroboros.Application.Tools;
using Ouroboros.Tools;

/// <summary>
/// Demonstrates meta-AI capabilities where the pipeline can think about its own thinking.
/// The LLM can invoke pipeline steps as tools to enhance and improve its own reasoning process.
/// </summary>
public static class MetaAiPipelineExample
{
    /// <summary>
    /// Demonstrates basic meta-AI capability where the LLM uses pipeline tools.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunBasicMetaAiExample()
    {
        Console.WriteLine("=== Meta-AI Pipeline Example ===\n");
        Console.WriteLine("This example shows the pipeline using its own steps as tools.");
        Console.WriteLine("The LLM can invoke pipeline operations to enhance its reasoning.\n");

        // Setup
        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        ToolRegistry tools = new ToolRegistry();
        TrackedVectorStore store = new TrackedVectorStore();
        PipelineBranch branch = new PipelineBranch("meta-ai", store, DataSource.FromPath(Environment.CurrentDirectory));

        CliPipelineState state = new CliPipelineState
        {
            Branch = branch,
            Llm = null!, // Will be set after tools
            Tools = tools,
            Embed = embedModel,
            RetrievalK = 8,
            Trace = true,
        };

        // Register pipeline steps as tools - this enables meta-AI capabilities
        Console.WriteLine("Registering pipeline steps as tools...");
        tools = tools.WithPipelineSteps(state);
        Console.WriteLine($"✓ Registered {tools.Count} tools\n");

        // Create LLM with tool awareness
        ToolAwareChatModel llm = new ToolAwareChatModel(chatModel, tools);
        state.Llm = llm;
        state.Tools = tools;

        // List some available pipeline tools
        Console.WriteLine("Available pipeline tools:");
        IEnumerable<ITool> pipelineTools = tools.All.Where(t => t.Name.StartsWith("run_")).Take(10);
        foreach (ITool? tool in pipelineTools)
        {
            Console.WriteLine($"  - {tool.Name}: {tool.Description}");
        }

        Console.WriteLine();

        // Create a meta-AI prompt
        string prompt = @"You are a self-aware AI pipeline with access to your own execution tools.

Available pipeline operations you can invoke:
- run_usedraft: Generate an initial draft response
- run_usecritique: Critically analyze a draft
- run_useimprove: Enhance draft based on critique
- run_setprompt: Set a new prompt for processing
- run_llm: Execute LLM generation

To invoke a tool, use: [TOOL:toolname {""args"": ""arguments""}]

Task: Explain the concept of 'meta-AI' - an AI system that can reason about and improve its own thinking process.
Then demonstrate this by using your pipeline tools to enhance your answer through iteration.

Think step-by-step:
1. What is meta-AI?
2. Which pipeline tools could help improve your explanation?
3. How would you use them?";

        Console.WriteLine("Executing meta-AI pipeline...\n");

        try
        {
            (string response, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);

            Console.WriteLine("=== LLM Response ===");
            Console.WriteLine(response);
            Console.WriteLine();

            if (toolCalls.Any())
            {
                Console.WriteLine($"✓ The LLM invoked {toolCalls.Count} pipeline tools:");
                foreach (ToolExecution call in toolCalls)
                {
                    Console.WriteLine($"\n  Tool: {call.ToolName}");
                    Console.WriteLine($"  Args: {call.Arguments}");
                    Console.WriteLine($"  Result: {call.Output}");
                }

                Console.WriteLine("\n✓ Meta-AI demonstration successful!");
            }
            else
            {
                Console.WriteLine("Note: The LLM explained the concept but did not invoke tools.");
                Console.WriteLine("This may require a more capable model or different prompt structure.");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused"))
            {
                Console.WriteLine("⚠ Ollama is not running. Please start Ollama to run this example.");
            }
            else
            {
                throw;
            }
        }

        Console.WriteLine("\n=== Example Complete ===");
    }

    /// <summary>
    /// Demonstrates selective pipeline tool registration for controlled meta-AI behavior.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunSelectiveMetaAiExample()
    {
        Console.WriteLine("\n=== Selective Meta-AI Example ===\n");
        Console.WriteLine("This example shows how to give the LLM access to only specific pipeline tools.");
        Console.WriteLine("This provides controlled meta-AI capabilities.\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        ToolRegistry tools = new ToolRegistry();
        TrackedVectorStore store = new TrackedVectorStore();
        PipelineBranch branch = new PipelineBranch("selective-meta-ai", store, DataSource.FromPath(Environment.CurrentDirectory));

        CliPipelineState state = new CliPipelineState
        {
            Branch = branch,
            Llm = null!,
            Tools = tools,
            Embed = embedModel,
            RetrievalK = 8,
            Trace = false,
        };

        // Register only specific pipeline steps as tools
        string[] allowedSteps = new[] { "UseDraft", "UseCritique", "UseImprove" };
        Console.WriteLine($"Registering only specific tools: {string.Join(", ", allowedSteps)}");
        tools = tools.WithPipelineSteps(state, allowedSteps);
        Console.WriteLine($"✓ Registered {tools.Count} tools (limited set)\n");

        ToolAwareChatModel llm = new ToolAwareChatModel(chatModel, tools);
        state.Llm = llm;
        state.Tools = tools;

        string prompt = @"You have access to a limited set of pipeline refinement tools:
- run_usedraft: Create initial draft
- run_usecritique: Analyze and critique
- run_useimprove: Enhance based on critique

Explain how you would use these tools to create a high-quality response to: 'What is functional programming?'";

        Console.WriteLine("Executing selective meta-AI pipeline...\n");

        try
        {
            (string response, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);

            Console.WriteLine("=== Response ===");
            Console.WriteLine(response);

            if (toolCalls.Any())
            {
                Console.WriteLine($"\n✓ Invoked {toolCalls.Count} tools from the allowed set");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused"))
            {
                Console.WriteLine("⚠ Ollama is not running.");
            }
            else
            {
                throw;
            }
        }

        Console.WriteLine("\n=== Example Complete ===");
    }

    /// <summary>
    /// Demonstrates using the DSL with meta-AI capabilities.
    /// </summary>
    public static void ShowDslMetaAiUsage()
    {
        Console.WriteLine("\n=== DSL with Meta-AI Example ===\n");
        Console.WriteLine("The pipeline DSL can be used with meta-AI capabilities enabled.");
        Console.WriteLine("Pipeline steps are automatically registered as tools.\n");

        Console.WriteLine("Example DSL commands that work with meta-AI:");
        Console.WriteLine();
        Console.WriteLine("  # Simple pipeline with tool awareness");
        Console.WriteLine("  dotnet run -- pipeline --dsl \"SetPrompt('Explain meta-AI') | LLM\"");
        Console.WriteLine();
        Console.WriteLine("  # Complex refinement pipeline");
        Console.WriteLine("  dotnet run -- pipeline --dsl \"SetTopic('functional programming') | UseDraft | UseCritique | UseImprove\"");
        Console.WriteLine();
        Console.WriteLine("  # Pipeline that can self-improve");
        Console.WriteLine("  dotnet run -- pipeline --dsl \"SetPrompt('What are monads?') | UseRefinementLoop('2')\"");
        Console.WriteLine();
        Console.WriteLine("The LLM running in these pipelines can invoke other pipeline steps as tools!");
        Console.WriteLine("This creates a meta-AI layer where the pipeline thinks about its own thinking.");
        Console.WriteLine();
        Console.WriteLine("=== Example Complete ===");
    }
}
