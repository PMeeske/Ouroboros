// <copyright file="EasyApiExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Easy;
using LangChain.Providers.Ollama;
using LangChain.DocumentLoaders;

/// <summary>
/// Demonstrates the simplified Easy API for creating Ouroboros pipelines.
/// Shows the progression from Easy API to DSL to Core monadic architecture.
/// </summary>
public static class EasyApiExample
{
    /// <summary>
    /// Demonstrates basic Easy API usage with simple pipeline creation.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunBasicExample()
    {
        Console.WriteLine("=== Ouroboros.Easy - Basic Example ===\n");
        Console.WriteLine("This example shows the simplified API for creating pipelines.");
        Console.WriteLine("Perfect for beginners and rapid prototyping.\n");

        // Setup (you would typically inject these)
        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        var toolAwareChatModel = new ToolAwareChatModel(chatModel, new ToolRegistry());

        // Create a simple pipeline using the Easy API
        Console.WriteLine("Creating a basic reasoning pipeline...");
        var result = await Pipeline.Create("quantum computing")
            .Draft()
            .Critique()
            .Improve()
            .WithChatModel(toolAwareChatModel)
            .WithEmbeddingModel(embedModel)
            .RunAsync();

        if (result.Success)
        {
            Console.WriteLine($"\n✓ Pipeline executed successfully!");
            Console.WriteLine($"\nFinal Output:\n{result.Output}\n");
            Console.WriteLine($"Total steps executed: {result.Branch.Events.Count}");
        }
        else
        {
            Console.WriteLine($"\n✗ Pipeline failed: {result.Error}");
        }
    }

    /// <summary>
    /// Demonstrates using pre-configured pipeline patterns.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunPreConfiguredPatterns()
    {
        Console.WriteLine("\n=== Ouroboros.Easy - Pre-configured Patterns ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        var toolAwareChatModel = new ToolAwareChatModel(chatModel, new ToolRegistry());

        // 1. Basic Reasoning (Draft -> Critique -> Improve)
        Console.WriteLine("1. Basic Reasoning Pattern:");
        var basicPipeline = Pipeline.BasicReasoning("AI safety")
            .WithChatModel(toolAwareChatModel)
            .WithEmbeddingModel(embedModel);
        
        Console.WriteLine($"   Pipeline: {basicPipeline.ToDSL().Split('\n')[0]}");

        // 2. Full Reasoning (Think -> Draft -> Critique -> Improve)
        Console.WriteLine("\n2. Full Reasoning Pattern:");
        var fullPipeline = Pipeline.FullReasoning("neural networks")
            .WithChatModel(toolAwareChatModel)
            .WithEmbeddingModel(embedModel);
        
        Console.WriteLine($"   Pipeline: {fullPipeline.ToDSL().Split('\n')[0]}");

        // 3. Iterative Reasoning (multiple refinement cycles)
        Console.WriteLine("\n3. Iterative Reasoning Pattern (3 iterations):");
        var iterativePipeline = Pipeline.IterativeReasoning("transformer architecture", iterations: 3)
            .WithChatModel(toolAwareChatModel)
            .WithEmbeddingModel(embedModel);
        
        Console.WriteLine($"   Pipeline: {iterativePipeline.ToDSL().Split('\n')[0]}");

        // 4. Summarization
        Console.WriteLine("\n4. Summarization Pattern:");
        var summarizePipeline = Pipeline.Summarize("complex research paper")
            .WithChatModel(toolAwareChatModel)
            .WithEmbeddingModel(embedModel);
        
        Console.WriteLine($"   Pipeline: {summarizePipeline.ToDSL().Split('\n')[0]}");

        Console.WriteLine("\nAll patterns configured successfully!");
    }

    /// <summary>
    /// Demonstrates the learning progression: Easy → DSL → Core.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunLearningProgression()
    {
        Console.WriteLine("\n=== Ouroboros.Easy - Learning Progression ===\n");
        Console.WriteLine("Demonstrating the progression from Easy API to Core architecture.\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        var toolAwareChatModel = new ToolAwareChatModel(chatModel, new ToolRegistry());

        // Level 1: Easy API (Beginners)
        Console.WriteLine("📘 Level 1: Easy API (Beginner-Friendly)");
        Console.WriteLine("────────────────────────────────────────");
        var easyPipeline = Pipeline.Create("quantum computing")
            .Draft()
            .Critique()
            .Improve()
            .WithChatModel(toolAwareChatModel)
            .WithEmbeddingModel(embedModel);
        
        Console.WriteLine("Code:");
        Console.WriteLine("  Pipeline.Create(\"quantum computing\")");
        Console.WriteLine("      .Draft()");
        Console.WriteLine("      .Critique()");
        Console.WriteLine("      .Improve()");
        Console.WriteLine("      .RunAsync();");
        Console.WriteLine("\n✓ Simple, fluent, intuitive\n");

        // Level 2: DSL Representation (Intermediate)
        Console.WriteLine("📗 Level 2: DSL Representation (Intermediate)");
        Console.WriteLine("────────────────────────────────────────────");
        var dsl = easyPipeline.ToDSL();
        Console.WriteLine("DSL Output:");
        Console.WriteLine(dsl);
        Console.WriteLine("✓ Understand the pipeline structure\n");

        // Level 3: Core Monadic Architecture (Advanced)
        Console.WriteLine("📕 Level 3: Core Monadic Architecture (Advanced)");
        Console.WriteLine("──────────────────────────────────────────────");
        var coreArrow = easyPipeline.ToCore();
        Console.WriteLine("Extracted Kleisli Arrow:");
        Console.WriteLine($"  Type: {coreArrow.GetType().Name}");
        Console.WriteLine($"  Signature: Step<PipelineBranch, PipelineBranch>");
        Console.WriteLine("\nThis arrow can now be composed with other arrows:");
        Console.WriteLine("  var composedArrow = arrow.Then(customArrow).Map(transform);");
        Console.WriteLine("\n✓ Full control over monadic composition\n");

        Console.WriteLine("🎓 Learning Path Complete!");
        Console.WriteLine("   Easy → DSL → Core: Each level builds on the previous");
    }

    /// <summary>
    /// Demonstrates configuration options.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunConfigurationExample()
    {
        Console.WriteLine("\n=== Ouroboros.Easy - Configuration Options ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        var toolAwareChatModel = new ToolAwareChatModel(chatModel, new ToolRegistry());

        // Highly configured pipeline
        var pipeline = Pipeline.Create("advanced topic")
            .Think()                        // Add thinking step
            .Draft()                        // Generate draft
            .Critique()                     // Critique draft
            .Improve()                      // Improve based on critique
            .Summarize()                    // Summarize result
            .WithModel("llama3")            // Model selection
            .WithTemperature(0.8)           // Generation temperature
            .WithContextDocuments(15)       // RAG document count
            .WithChatModel(toolAwareChatModel)      // Chat model
            .WithEmbeddingModel(embedModel)         // Embedding model
            .WithVectorStore(new TrackedVectorStore())  // Custom vector store
            .WithDataSource(DataSource.FromPath(Environment.CurrentDirectory)); // Data source

        Console.WriteLine("Configured pipeline with all options:");
        Console.WriteLine(pipeline.ToDSL());
    }

    /// <summary>
    /// Runs all Easy API examples.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunAllExamples()
    {
        try
        {
            await RunBasicExample();
            await RunPreConfiguredPatterns();
            await RunLearningProgression();
            await RunConfigurationExample();
            
            Console.WriteLine("\n✓ All Easy API examples completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error running examples: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }
}
