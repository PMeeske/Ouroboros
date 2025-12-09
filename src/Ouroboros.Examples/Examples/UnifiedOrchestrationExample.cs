// <copyright file="UnifiedOrchestrationExample.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Examples;

using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using LangChainPipeline.Agent.MetaAI;
using Ouroboros.Application;
using Ouroboros.Tools;

/// <summary>
/// Demonstrates the unified orchestration infrastructure.
/// Shows how different orchestrators can be composed using the harmonized interfaces.
/// </summary>
public static class UnifiedOrchestrationExample
{
    /// <summary>
    /// Demonstrates basic unified orchestrator usage with composition.
    /// </summary>
    public static async Task RunBasicUnifiedExample()
    {
        Console.WriteLine("=== Unified Orchestration - Basic Example ===\n");
        Console.WriteLine("This example shows the unified orchestrator interface and composition.\n");

        // Create a simple orchestrator using the base class
        var config = new OrchestratorConfig
        {
            EnableMetrics = true,
            EnableTracing = true,
            EnableSafetyChecks = true
        };

        var textProcessor = new TextProcessingOrchestrator(config);

        // Execute directly
        Console.WriteLine("--- Direct Execution ---");
        var result = await textProcessor.ExecuteAsync("Hello, unified orchestration!");
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Output: {result.Output}");
        Console.WriteLine($"Execution time: {result.ExecutionTime.TotalMilliseconds}ms");
        Console.WriteLine($"Metrics - Total: {result.Metrics.TotalExecutions}, Success rate: {result.Metrics.SuccessRate:P0}\n");

        // Execute with context
        Console.WriteLine("--- Execution with Context ---");
        var context = OrchestratorContext.Create()
            .WithMetadata("user_id", "user123")
            .WithMetadata("session_id", "session456");
        
        var contextResult = await textProcessor.ExecuteAsync("Contextual processing", context);
        Console.WriteLine($"Success: {contextResult.Success}");
        Console.WriteLine($"Output: {contextResult.Output}");
        Console.WriteLine($"Context metadata preserved: {contextResult.GetMetadata<string>("user_id")}\n");

        // Check health
        Console.WriteLine("--- Health Check ---");
        var health = await textProcessor.GetHealthAsync();
        Console.WriteLine($"Status: {health["status"]}");
        Console.WriteLine($"Total executions: {health["total_executions"]}");
        Console.WriteLine($"Average latency: {health["average_latency_ms"]}ms\n");

        Console.WriteLine("=== Example Complete ===\n");
    }

    /// <summary>
    /// Demonstrates orchestrator composition with the fluent API.
    /// </summary>
    public static async Task RunCompositionExample()
    {
        Console.WriteLine("=== Unified Orchestration - Composition Example ===\n");
        Console.WriteLine("This example shows how to compose orchestrators using the fluent API.\n");

        var config = OrchestratorConfig.Default();

        // Create individual orchestrators
        var textProcessor = new TextProcessingOrchestrator(config);
        var validator = new ValidationOrchestrator(config);
        var transformer = new TransformOrchestrator(config);

        // Compose them using the fluent API
        Console.WriteLine("--- Sequential Composition ---");
        var pipeline = textProcessor.AsComposable()
            .Then(validator.AsComposable())
            .Then(transformer.AsComposable());

        var pipelineResult = await pipeline.ExecuteAsync("Compose this");
        Console.WriteLine($"Pipeline result: {pipelineResult.Output}\n");

        // Map transformation
        Console.WriteLine("--- Map Transformation ---");
        var mapped = textProcessor.AsComposable()
            .Map(output => $"TRANSFORMED: {output.ToUpperInvariant()}");
        
        var mappedResult = await mapped.ExecuteAsync("map me");
        Console.WriteLine($"Mapped result: {mappedResult.Output}\n");

        // Tap for side effects
        Console.WriteLine("--- Tap for Logging ---");
        var logged = textProcessor.AsComposable()
            .Tap(output => Console.WriteLine($"[LOG] Intermediate result: {output}"));
        
        var loggedResult = await logged.ExecuteAsync("tap this");
        Console.WriteLine($"Final result: {loggedResult.Output}\n");

        Console.WriteLine("=== Example Complete ===\n");
    }

    /// <summary>
    /// Demonstrates parallel execution and fallback patterns.
    /// </summary>
    public static async Task RunAdvancedPatternsExample()
    {
        Console.WriteLine("=== Unified Orchestration - Advanced Patterns ===\n");

        var config = OrchestratorConfig.Default();

        // Parallel execution
        Console.WriteLine("--- Parallel Execution ---");
        var processor1 = new TextProcessingOrchestrator(config);
        var processor2 = new TextProcessingOrchestrator(config);
        var processor3 = new TextProcessingOrchestrator(config);

        var parallel = OrchestratorComposer.Parallel(processor1, processor2, processor3);
        var parallelResult = await parallel.ExecuteAsync("Process in parallel");
        
        Console.WriteLine($"Parallel results count: {parallelResult.Output?.Length}");
        if (parallelResult.Output != null)
        {
            for (int i = 0; i < parallelResult.Output.Length; i++)
            {
                Console.WriteLine($"  Result {i + 1}: {parallelResult.Output[i]}");
            }
        }
        Console.WriteLine();

        // Fallback pattern
        Console.WriteLine("--- Fallback Pattern ---");
        var primary = new ReliableOrchestrator(config, shouldFail: false);
        var fallback = new TextProcessingOrchestrator(config);

        var withFallback = OrchestratorComposer.WithFallback(primary, fallback);
        var fallbackResult = await withFallback.ExecuteAsync("Try with fallback");
        Console.WriteLine($"Fallback result: {fallbackResult.Output}\n");

        // Conditional routing
        Console.WriteLine("--- Conditional Routing ---");
        var shortProcessor = new TextProcessingOrchestrator(config);
        var longProcessor = new TransformOrchestrator(config);

        var conditional = OrchestratorComposer.Conditional(
            input => input.Length < 10,
            shortProcessor,
            longProcessor);

        var shortResult = await conditional.ExecuteAsync("short");
        var longResult = await conditional.ExecuteAsync("this is a longer input string");
        
        Console.WriteLine($"Short input result: {shortResult.Output}");
        Console.WriteLine($"Long input result: {longResult.Output}\n");

        // Retry pattern
        Console.WriteLine("--- Retry Pattern ---");
        var unreliable = new ReliableOrchestrator(config, shouldFail: true, failCount: 2);
        var withRetry = OrchestratorComposer.WithRetry(
            unreliable,
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(100));

        var retryResult = await withRetry.ExecuteAsync("Retry this");
        Console.WriteLine($"Retry result: {retryResult.Output}\n");

        Console.WriteLine("=== Example Complete ===\n");
    }

    /// <summary>
    /// Demonstrates integration with existing SmartModelOrchestrator.
    /// </summary>
    public static async Task RunSmartModelIntegrationExample()
    {
        Console.WriteLine("=== Unified Orchestration - Smart Model Integration ===\n");
        Console.WriteLine("This example shows existing orchestrators working with the unified interface.\n");

        try
        {
            var provider = new OllamaProvider();
            var model = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();

            // Create SmartModelOrchestrator (existing)
            var smartOrchestrator = new SmartModelOrchestrator(tools, "default");
            smartOrchestrator.RegisterModel(
                new ModelCapability(
                    "default",
                    new[] { "general", "reasoning" },
                    4096,
                    0.001,
                    1000,
                    ModelType.General),
                model);

            Console.WriteLine("SmartModelOrchestrator registered and ready.\n");
            Console.WriteLine("Note: The unified interface allows existing orchestrators to work");
            Console.WriteLine("alongside new harmonized orchestrators seamlessly.\n");

            // Show metrics compatibility
            var metrics = smartOrchestrator.GetMetrics();
            Console.WriteLine($"Metrics available: {metrics.Count} resources tracked");
            Console.WriteLine($"Unified metrics interface: ✓\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Ollama not available: {ex.Message}");
            Console.WriteLine("This is expected if Ollama is not running.\n");
        }

        Console.WriteLine("=== Example Complete ===\n");
    }

    // Example orchestrator implementations

    private sealed class TextProcessingOrchestrator : OrchestratorBase<string, string>
    {
        public TextProcessingOrchestrator(OrchestratorConfig config)
            : base("text_processor", config)
        {
        }

        protected override Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            return Task.FromResult($"Processed: {input}");
        }
    }

    private sealed class ValidationOrchestrator : OrchestratorBase<string, string>
    {
        public ValidationOrchestrator(OrchestratorConfig config)
            : base("validator", config)
        {
        }

        protected override Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            return Task.FromResult($"Validated: {input}");
        }

        protected override Result<bool, string> ValidateInput(string input, OrchestratorContext context)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Result<bool, string>.Failure("Input cannot be empty");
            }
            return Result<bool, string>.Success(true);
        }
    }

    private sealed class TransformOrchestrator : OrchestratorBase<string, string>
    {
        public TransformOrchestrator(OrchestratorConfig config)
            : base("transformer", config)
        {
        }

        protected override Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            return Task.FromResult($"Transformed: {input.ToUpperInvariant()}");
        }
    }

    private sealed class ReliableOrchestrator : OrchestratorBase<string, string>
    {
        private readonly bool _shouldFail;
        private readonly int _failCount;
        private int _attemptCount;

        public ReliableOrchestrator(OrchestratorConfig config, bool shouldFail = false, int failCount = 0)
            : base("reliable", config)
        {
            _shouldFail = shouldFail;
            _failCount = failCount;
            _attemptCount = 0;
        }

        protected override Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            _attemptCount++;
            if (_shouldFail && _attemptCount <= _failCount)
            {
                throw new InvalidOperationException($"Attempt {_attemptCount} failed");
            }
            return Task.FromResult($"Reliable: {input}");
        }
    }
}
