// <copyright file="OrchestratorExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using Ouroboros.Application;
using Ouroboros.Application.Tools;
using Ouroboros.Tools;

/// <summary>
/// Demonstrates the AI orchestrator capabilities for intelligent model and tool selection.
/// Shows how the orchestrator analyzes prompts, selects optimal models, and tracks performance.
/// </summary>
public static class OrchestratorExample
{
    /// <summary>
    /// Demonstrates basic orchestrator setup and model selection.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunBasicOrchestratorExample()
    {
        Console.WriteLine("=== AI Orchestrator - Basic Example ===\n");
        Console.WriteLine("This example shows how the orchestrator intelligently selects models based on use case.\n");

        // Setup models
        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter generalModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaChatAdapter codeModel = new OllamaChatAdapter(new OllamaChatModel(provider, "codellama"));
        OllamaChatAdapter reasoningModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

        // Setup tools
        ToolRegistry tools = ToolRegistry.CreateDefault();

        // Build orchestrator with multiple models
        OrchestratedChatModel orchestrator = new OrchestratorBuilder(tools, "general")
            .WithModel(
                "general",
                generalModel,
                ModelType.General,
                new[] { "conversation", "general-purpose", "versatile" },
                maxTokens: 4096,
                avgLatencyMs: 1000)
            .WithModel(
                "coder",
                codeModel,
                ModelType.Code,
                new[] { "code", "programming", "debugging", "syntax" },
                maxTokens: 8192,
                avgLatencyMs: 1500)
            .WithModel(
                "reasoner",
                reasoningModel,
                ModelType.Reasoning,
                new[] { "reasoning", "analysis", "logic", "explanation" },
                maxTokens: 4096,
                avgLatencyMs: 1200)
            .WithMetricTracking(true)
            .Build();

        Console.WriteLine("✓ Orchestrator configured with 3 models\n");

        // Test different prompt types
        (string, string)[] testPrompts = new[]
        {
            ("Code generation", "Write a function to calculate fibonacci numbers"),
            ("Reasoning", "Explain why the sky is blue using physics principles"),
            ("General chat", "What's your favorite color and why?"),
            ("Code debugging", "Debug this code: def fib(n) return fib(n-1) + fib(n-2)"),
        };

        foreach ((string category, string prompt) in testPrompts)
        {
            Console.WriteLine($"--- {category} ---");
            Console.WriteLine($"Prompt: {prompt}");

            try
            {
                string response = await orchestrator.GenerateTextAsync(prompt);
                Console.WriteLine($"Response: {response.Substring(0, Math.Min(150, response.Length))}...\n");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Connection refused"))
                {
                    Console.WriteLine("⚠ Ollama not running - using simulated response\n");
                }
                else
                {
                    throw;
                }
            }
        }

        Console.WriteLine("=== Example Complete ===\n");
    }

    /// <summary>
    /// Demonstrates orchestrator with tool selection and performance tracking.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunOrchestratorWithToolsExample()
    {
        Console.WriteLine("\n=== AI Orchestrator - Tools & Performance ===\n");
        Console.WriteLine("This example shows orchestrator selecting tools and tracking performance.\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        ToolRegistry tools = new ToolRegistry();
        TrackedVectorStore store = new TrackedVectorStore();
        PipelineBranch branch = new PipelineBranch("orchestrator-test", store, DataSource.FromPath(Environment.CurrentDirectory));

        CliPipelineState state = new CliPipelineState
        {
            Branch = branch,
            Llm = null!,
            Tools = tools,
            Embed = embedModel,
            RetrievalK = 8,
            Trace = false,
        };

        // Add pipeline steps as tools
        tools = tools.WithPipelineSteps(state);
        Console.WriteLine($"✓ Registered {tools.Count} tools\n");

        // Build orchestrator
        OrchestratorBuilder orchestratorBuilder = new OrchestratorBuilder(tools, "default")
            .WithModel(
                "default",
                chatModel,
                ModelType.General,
                new[] { "general", "tools", "function-calling" },
                avgLatencyMs: 1000)
            .WithMetricTracking(true);

        OrchestratedChatModel orchestratedModel = orchestratorBuilder.Build();
        IModelOrchestrator underlyingOrchestrator = orchestratorBuilder.GetOrchestrator();

        Console.WriteLine("Testing orchestrator with tool-aware prompts:\n");

        string toolPrompt = @"Analyze the concept of 'monadic composition' in functional programming.
Consider using available pipeline tools to enhance your analysis.
Available tools include: run_usedraft, run_usecritique, run_useimprove.";

        try
        {
            (string response, List<ToolExecution> toolCalls, OrchestratorDecision? decision) = await orchestratedModel
                .GenerateWithOrchestratedToolsAsync(toolPrompt);

            Console.WriteLine("=== Orchestrator Decision ===");
            if (decision != null)
            {
                Console.WriteLine($"Selected Model: {decision.ModelName}");
                Console.WriteLine($"Reason: {decision.Reason}");
                Console.WriteLine($"Confidence: {decision.ConfidenceScore:P0}");
                Console.WriteLine($"Tools Available: {decision.RecommendedTools.Count}");
            }

            Console.WriteLine("\n=== Response ===");
            Console.WriteLine(response.Substring(0, Math.Min(300, response.Length)) + "...");

            if (toolCalls.Any())
            {
                Console.WriteLine($"\n✓ Invoked {toolCalls.Count} tools:");
                foreach (ToolExecution call in toolCalls)
                {
                    Console.WriteLine($"  - {call.ToolName}");
                }
            }

            // Show performance metrics
            Console.WriteLine("\n=== Performance Metrics ===");
            IReadOnlyDictionary<string, PerformanceMetrics> metrics = underlyingOrchestrator.GetMetrics();
            foreach ((string name, PerformanceMetrics metric) in metrics.Take(5))
            {
                Console.WriteLine($"{name}:");
                Console.WriteLine($"  Executions: {metric.ExecutionCount}");
                Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs:F0}ms");
                Console.WriteLine($"  Success Rate: {metric.SuccessRate:P0}");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused"))
            {
                Console.WriteLine("⚠ Ollama not running - skipping execution");
            }
            else
            {
                throw;
            }
        }

        Console.WriteLine("\n=== Example Complete ===\n");
    }

    /// <summary>
    /// Demonstrates use case classification.
    /// </summary>
    public static void RunUseCaseClassificationExample()
    {
        Console.WriteLine("\n=== Use Case Classification ===\n");
        Console.WriteLine("This example shows how the orchestrator classifies different prompt types.\n");

        ToolRegistry tools = ToolRegistry.CreateDefault();
        SmartModelOrchestrator orchestrator = new SmartModelOrchestrator(tools, "default");

        string[] prompts = new[]
        {
            "Write a Python function to sort a list",
            "Explain quantum entanglement in simple terms",
            "Create a short story about a robot",
            "Summarize this long document...",
            "Use the search tool to find information about AI",
            "Hello, how are you today?",
        };

        foreach (string? prompt in prompts)
        {
            UseCase useCase = orchestrator.ClassifyUseCase(prompt);
            Console.WriteLine($"Prompt: \"{prompt.Substring(0, Math.Min(50, prompt.Length))}...\"");
            Console.WriteLine($"  → Type: {useCase.Type}");
            Console.WriteLine($"  → Complexity: {useCase.EstimatedComplexity}");
            Console.WriteLine($"  → Capabilities: {string.Join(", ", useCase.RequiredCapabilities)}");
            Console.WriteLine($"  → Performance Weight: {useCase.PerformanceWeight:P0}");
            Console.WriteLine();
        }

        Console.WriteLine("=== Example Complete ===\n");
    }

    /// <summary>
    /// Demonstrates composable tools with orchestrator.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunComposableToolsExample()
    {
        Console.WriteLine("\n=== Composable Tools with Orchestrator ===\n");
        Console.WriteLine("This example shows advanced tool composition with performance tracking.\n");

        ToolRegistry tools = ToolRegistry.CreateDefault();
        SmartModelOrchestrator orchestrator = new SmartModelOrchestrator(tools, "default");

        // Create composable tools with performance tracking
        ITool? mathTool = tools.Get("math");
        if (mathTool != null)
        {
            // Add retry logic
            ITool reliableMath = mathTool.WithRetry(maxRetries: 3, delayMs: 100);

            // Add performance tracking
            ITool trackedMath = reliableMath.WithPerformanceTracking(
                (name, latency, success) =>
                {
                    orchestrator.RecordMetric(name, latency, success);
                    Console.WriteLine($"[METRIC] {name}: {latency:F0}ms, Success: {success}");
                });

            // Add caching
            ITool cachedMath = trackedMath.WithCaching(TimeSpan.FromMinutes(5));

            // Add timeout protection
            ITool safeMath = cachedMath.WithTimeout(TimeSpan.FromSeconds(10));

            Console.WriteLine("✓ Created composable math tool with:");
            Console.WriteLine("  - Retry logic (3 attempts)");
            Console.WriteLine("  - Performance tracking");
            Console.WriteLine("  - Result caching (5 min)");
            Console.WriteLine("  - Timeout protection (10s)\n");

            // Test the tool
            string[] testInputs = new[] { "2+2", "10*5", "100/4", "2+2" }; // Last is cached

            foreach (string? input in testInputs)
            {
                Result<string, string> result = await safeMath.InvokeAsync(input);
                result.Match(
                    success => Console.WriteLine($"Input: {input} → Result: {success}"),
                    failure => Console.WriteLine($"Input: {input} → Error: {failure}"));
            }

            Console.WriteLine("\n✓ Notice the cached result for '2+2' has minimal latency!");
        }

        // Create parallel tool execution
        ITool parallelTool = OrchestratorToolExtensions.Parallel(
            "parallel_math",
            "Executes multiple calculations in parallel",
            results => string.Join(", ", results),
            tools.Get("math") ?? new MathTool(),
            tools.Get("math") ?? new MathTool());

        Console.WriteLine("\n=== Parallel Tool Execution ===");
        Result<string, string> parallelResult = await parallelTool.InvokeAsync("5*5");
        parallelResult.Match(
            success => Console.WriteLine($"Parallel result: {success}"),
            failure => Console.WriteLine($"Error: {failure}"));

        Console.WriteLine("\n=== Performance Metrics ===");
        IReadOnlyDictionary<string, PerformanceMetrics> metrics = orchestrator.GetMetrics();
        foreach ((string name, PerformanceMetrics metric) in metrics)
        {
            Console.WriteLine($"{name}:");
            Console.WriteLine($"  Executions: {metric.ExecutionCount}");
            Console.WriteLine($"  Success Rate: {metric.SuccessRate:P0}");
            if (metric.AverageLatencyMs > 0)
            {
                Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs:F1}ms");
            }
        }

        Console.WriteLine("\n=== Example Complete ===\n");
    }

    /// <summary>
    /// Runs all orchestrator examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("AI ORCHESTRATOR - COMPREHENSIVE EXAMPLES");
        Console.WriteLine(new string('=', 70) + "\n");

        await RunBasicOrchestratorExample();
        RunUseCaseClassificationExample();
        await RunComposableToolsExample();
        await RunOrchestratorWithToolsExample();

        Console.WriteLine(new string('=', 70));
        Console.WriteLine("✓ ALL ORCHESTRATOR EXAMPLES COMPLETED!");
        Console.WriteLine(new string('=', 70) + "\n");
    }
}
