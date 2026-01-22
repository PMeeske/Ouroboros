// <copyright file="HybridRoutingExample.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using LangChain.Providers.Ollama;
using Ouroboros.Providers;
using Ouroboros.Providers.DeepSeek;
using Ouroboros.Providers.Routing;

namespace Ouroboros.Examples;

/// <summary>
/// Example demonstrating hybrid model routing with DeepSeek.
/// Shows how to route different task types to appropriate models for optimal cost/quality.
/// </summary>
public static class HybridRoutingExample
{
    /// <summary>
    /// Runs the hybrid routing example with local Ollama models.
    /// </summary>
    public static async Task RunLocalAsync()
    {
        Console.WriteLine("=== Hybrid Model Routing Example (Local) ===\n");

        // Initialize Ollama provider
        var provider = new OllamaProvider();

        // Create different models for different tasks
        var defaultModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3.1:8b"));
        var reasoningModel = DeepSeekChatModel.CreateLocal(provider, DeepSeekChatModel.ModelDeepSeekR1_8B);
        var codingModel = new OllamaChatAdapter(new OllamaChatModel(provider, "codellama:13b"));

        // Configure hybrid routing
        var config = new HybridRoutingConfig(
            DefaultModel: defaultModel,
            ReasoningModel: reasoningModel,
            CodingModel: codingModel,
            DetectionStrategy: TaskDetectionStrategy.Hybrid);

        var router = new HybridModelRouter(config);

        // Test different task types
        await TestRouting(router, "Hello, how are you?", "Simple greeting");
        await TestRouting(router, "Explain why neural networks can approximate any function.", "Reasoning task");
        await TestRouting(router, "Write a Python function to implement binary search.", "Coding task");
        await TestRouting(router, "Create a step-by-step plan for migrating to microservices.", "Planning task");

        Console.WriteLine("\n=== Example Complete ===");
    }

    /// <summary>
    /// Runs the hybrid routing example with Ollama Cloud (requires API key).
    /// </summary>
    public static async Task RunCloudAsync()
    {
        Console.WriteLine("=== Hybrid Model Routing Example (Ollama Cloud) ===\n");

        try
        {
            // Create models using Ollama Cloud
            var settings = new ChatRuntimeSettings(Temperature: 0.7, MaxTokens: 1024);
            
            var defaultModel = new OllamaCloudChatModel(
                "https://api.ollama.ai",
                Environment.GetEnvironmentVariable("OLLAMA_CLOUD_API_KEY") ?? "",
                "llama3.1:8b",
                settings);

            var reasoningModel = DeepSeekChatModel.FromEnvironment(
                DeepSeekChatModel.ModelDeepSeekR1_32B,
                settings);

            var codingModel = new OllamaCloudChatModel(
                "https://api.ollama.ai",
                Environment.GetEnvironmentVariable("OLLAMA_CLOUD_API_KEY") ?? "",
                "codellama:34b",
                settings);

            // Configure hybrid routing with cloud models
            var config = new HybridRoutingConfig(
                DefaultModel: defaultModel,
                ReasoningModel: reasoningModel,
                CodingModel: codingModel,
                FallbackModel: defaultModel,
                DetectionStrategy: TaskDetectionStrategy.Hybrid);

            var router = new HybridModelRouter(config);

            // Test different task types
            await TestRouting(router, "What is 2+2?", "Simple question");
            await TestRouting(router, "Analyze the philosophical implications of artificial consciousness.", "Deep reasoning");
            await TestRouting(router, "Implement a REST API with authentication in C#.", "Complex coding");

            Console.WriteLine("\n=== Example Complete ===");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine("Set OLLAMA_CLOUD_ENDPOINT and OLLAMA_CLOUD_API_KEY (or DEEPSEEK_API_KEY) environment variables.");
        }
    }

    /// <summary>
    /// Runs the hybrid routing example with mixed local/cloud setup.
    /// Uses local models for simple tasks and cloud for complex reasoning.
    /// </summary>
    public static async Task RunHybridAsync()
    {
        Console.WriteLine("=== Hybrid Model Routing Example (Mixed Local/Cloud) ===\n");

        try
        {
            var provider = new OllamaProvider();
            var settings = new ChatRuntimeSettings(Temperature: 0.7, MaxTokens: 1024);

            // Local models for simple/coding tasks
            var defaultModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3.1:8b"));
            var codingModel = new OllamaChatAdapter(new OllamaChatModel(provider, "codellama:13b"));

            // Cloud DeepSeek for reasoning and planning
            var reasoningModel = DeepSeekChatModel.FromEnvironment(
                DeepSeekChatModel.ModelDeepSeekR1_32B,
                settings);

            // Configure hybrid routing
            var config = new HybridRoutingConfig(
                DefaultModel: defaultModel,
                ReasoningModel: reasoningModel,
                PlanningModel: reasoningModel, // Use DeepSeek for planning too
                CodingModel: codingModel,
                FallbackModel: defaultModel, // Fallback to local on cloud failure
                DetectionStrategy: TaskDetectionStrategy.Hybrid);

            var router = new HybridModelRouter(config);

            Console.WriteLine("Strategy: Local models for simple/coding, DeepSeek Cloud for reasoning/planning\n");

            // Test routing with cost-conscious approach
            await TestRouting(router, "Hi there!", "Simple (local)");
            await TestRouting(router, "Why does consciousness emerge from neural activity?", "Reasoning (cloud)");
            await TestRouting(router, "def hello(): print('hi')", "Coding (local)");
            await TestRouting(router, "Design a multi-phase rollout strategy for a new feature.", "Planning (cloud)");

            Console.WriteLine("\n=== Example Complete ===");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine("For cloud reasoning, set OLLAMA_CLOUD_ENDPOINT and DEEPSEEK_API_KEY.");
            Console.WriteLine("Falling back to local-only example...\n");
            await RunLocalAsync();
        }
    }

    /// <summary>
    /// Tests routing for a specific prompt and displays the result.
    /// </summary>
    private static async Task TestRouting(HybridModelRouter router, string prompt, string description)
    {
        Console.WriteLine($"[{description}]");
        Console.WriteLine($"Prompt: \"{prompt}\"");
        
        // Detect task type
        TaskType taskType = router.DetectTaskTypeForPrompt(prompt);
        Console.WriteLine($"Detected: {taskType}");

        try
        {
            // Generate response
            string response = await router.GenerateTextAsync(prompt);
            Console.WriteLine($"Response: {response.Substring(0, Math.Min(150, response.Length))}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Entry point for running the example.
    /// </summary>
    public static async Task RunAsync(string[] args)
    {
        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "local";

        switch (mode)
        {
            case "local":
                await RunLocalAsync();
                break;
            case "cloud":
                await RunCloudAsync();
                break;
            case "hybrid":
            case "mixed":
                await RunHybridAsync();
                break;
            default:
                Console.WriteLine("Usage: HybridRoutingExample <local|cloud|hybrid>");
                Console.WriteLine("  local  - Use local Ollama models only");
                Console.WriteLine("  cloud  - Use Ollama Cloud models only");
                Console.WriteLine("  hybrid - Mix local and cloud models for cost optimization");
                break;
        }
    }
}
