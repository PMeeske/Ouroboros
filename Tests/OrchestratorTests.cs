// ==========================================================
// AI Orchestrator Tests
// Comprehensive tests for model orchestration, use case
// classification, and performance tracking
// ==========================================================

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for AI orchestrator capabilities including model selection,
/// use case classification, and performance tracking.
/// </summary>
public static class OrchestratorTests
{
    /// <summary>
    /// Tests basic orchestrator creation and configuration.
    /// </summary>
    public static void TestOrchestratorCreation()
    {
        Console.WriteLine("=== Test: Orchestrator Creation ===");

        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "default");

        // Register a test model capability
        var capability = new ModelCapability(
            "test-model",
            new[] { "testing", "general" },
            MaxTokens: 2048,
            AverageCost: 1.0,
            AverageLatencyMs: 500,
            Type: ModelType.General);

        orchestrator.RegisterModel(capability);

        Console.WriteLine("✓ Orchestrator created successfully");
        Console.WriteLine("✓ Model capability registered");

        var metrics = orchestrator.GetMetrics();
        if (!metrics.ContainsKey("test-model"))
        {
            throw new Exception("Model should be in metrics after registration!");
        }

        Console.WriteLine($"✓ Metrics initialized for model: {metrics["test-model"].ResourceName}");
        Console.WriteLine("✓ Test passed!\n");
    }

    /// <summary>
    /// Tests use case classification for different prompt types.
    /// </summary>
    public static void TestUseCaseClassification()
    {
        Console.WriteLine("=== Test: Use Case Classification ===");

        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "default");

        // Test code generation classification
        var codePrompt = "Write a function to calculate fibonacci numbers";
        var codeCase = orchestrator.ClassifyUseCase(codePrompt);
        if (codeCase.Type != UseCaseType.CodeGeneration)
        {
            throw new Exception($"Expected CodeGeneration, got {codeCase.Type}");
        }
        Console.WriteLine("✓ Code generation prompt classified correctly");

        // Test reasoning classification
        var reasoningPrompt = "Analyze why functional programming uses immutability";
        var reasoningCase = orchestrator.ClassifyUseCase(reasoningPrompt);
        if (reasoningCase.Type != UseCaseType.Reasoning)
        {
            throw new Exception($"Expected Reasoning, got {reasoningCase.Type}");
        }
        Console.WriteLine("✓ Reasoning prompt classified correctly");

        // Test creative classification
        var creativePrompt = "Create a short story about AI";
        var creativeCase = orchestrator.ClassifyUseCase(creativePrompt);
        if (creativeCase.Type != UseCaseType.Creative)
        {
            throw new Exception($"Expected Creative, got {creativeCase.Type}");
        }
        Console.WriteLine("✓ Creative prompt classified correctly");

        // Test summarization classification
        var summaryPrompt = "Summarize this long document about machine learning";
        var summaryCase = orchestrator.ClassifyUseCase(summaryPrompt);
        if (summaryCase.Type != UseCaseType.Summarization)
        {
            throw new Exception($"Expected Summarization, got {summaryCase.Type}");
        }
        Console.WriteLine("✓ Summarization prompt classified correctly");

        // Test tool use classification
        var toolPrompt = "Use the search tool to find information";
        var toolCase = orchestrator.ClassifyUseCase(toolPrompt);
        if (toolCase.Type != UseCaseType.ToolUse)
        {
            throw new Exception($"Expected ToolUse, got {toolCase.Type}");
        }
        Console.WriteLine("✓ Tool use prompt classified correctly");

        Console.WriteLine("✓ All use cases classified correctly!\n");
    }

    /// <summary>
    /// Tests model selection based on use case.
    /// </summary>
    public static async Task TestModelSelection()
    {
        Console.WriteLine("=== Test: Model Selection ===");

        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "general");

        // Create mock models
        var generalModel = new MockChatModel("general-response");
        var codeModel = new MockChatModel("code-response");
        var reasoningModel = new MockChatModel("reasoning-response");

        // Register capabilities
        orchestrator.RegisterModel(
            new ModelCapability(
                "general",
                new[] { "general", "conversation" },
                4096, 1.0, 800, ModelType.General),
            generalModel);

        orchestrator.RegisterModel(
            new ModelCapability(
                "coder",
                new[] { "code", "programming", "debugging" },
                8192, 1.5, 1200, ModelType.Code),
            codeModel);

        orchestrator.RegisterModel(
            new ModelCapability(
                "reasoner",
                new[] { "reasoning", "analysis", "logic" },
                4096, 1.2, 1000, ModelType.Reasoning),
            reasoningModel);

        // Test code prompt selects code model
        var codePrompt = "Write a function to reverse a string";
        var codeDecision = await orchestrator.SelectModelAsync(codePrompt);

        codeDecision.Match(
            decision =>
            {
                if (decision.ModelName != "coder")
                {
                    throw new Exception($"Expected 'coder' model, got '{decision.ModelName}'");
                }
                Console.WriteLine($"✓ Code prompt selected '{decision.ModelName}' model");
                Console.WriteLine($"  Reason: {decision.Reason}");
            },
            error => throw new Exception($"Selection failed: {error}"));

        // Test reasoning prompt selects reasoning model
        var reasoningPrompt = "Explain the principle of monadic composition";
        var reasoningDecision = await orchestrator.SelectModelAsync(reasoningPrompt);

        reasoningDecision.Match(
            decision =>
            {
                if (decision.ModelName != "reasoner")
                {
                    throw new Exception($"Expected 'reasoner' model, got '{decision.ModelName}'");
                }
                Console.WriteLine($"✓ Reasoning prompt selected '{decision.ModelName}' model");
                Console.WriteLine($"  Reason: {decision.Reason}");
            },
            error => throw new Exception($"Selection failed: {error}"));

        // Test general prompt selects general model
        var generalPrompt = "Hello, how are you?";
        var generalDecision = await orchestrator.SelectModelAsync(generalPrompt);

        generalDecision.Match(
            decision =>
            {
                if (decision.ModelName != "general")
                {
                    throw new Exception($"Expected 'general' model, got '{decision.ModelName}'");
                }
                Console.WriteLine($"✓ General prompt selected '{decision.ModelName}' model");
                Console.WriteLine($"  Reason: {decision.Reason}");
            },
            error => throw new Exception($"Selection failed: {error}"));

        Console.WriteLine("✓ All model selections correct!\n");
    }

    /// <summary>
    /// Tests performance metric tracking.
    /// </summary>
    public static void TestPerformanceTracking()
    {
        Console.WriteLine("=== Test: Performance Tracking ===");

        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "default");

        // Register a model
        orchestrator.RegisterModel(new ModelCapability(
            "test-model",
            new[] { "test" },
            2048, 1.0, 500, ModelType.General));

        // Record some metrics
        orchestrator.RecordMetric("test-model", 450, true);
        orchestrator.RecordMetric("test-model", 550, true);
        orchestrator.RecordMetric("test-model", 500, false);

        var metrics = orchestrator.GetMetrics();
        if (!metrics.TryGetValue("test-model", out var modelMetrics))
        {
            throw new Exception("Metrics not found for test-model!");
        }

        if (modelMetrics.ExecutionCount != 3)
        {
            throw new Exception($"Expected 3 executions, got {modelMetrics.ExecutionCount}");
        }
        Console.WriteLine($"✓ Execution count tracked correctly: {modelMetrics.ExecutionCount}");

        var expectedAvgLatency = (450 + 550 + 500) / 3.0;
        if (Math.Abs(modelMetrics.AverageLatencyMs - expectedAvgLatency) > 0.01)
        {
            throw new Exception($"Expected avg latency {expectedAvgLatency}, got {modelMetrics.AverageLatencyMs}");
        }
        Console.WriteLine($"✓ Average latency calculated correctly: {modelMetrics.AverageLatencyMs:F1}ms");

        var expectedSuccessRate = 2.0 / 3.0; // 2 successes out of 3
        if (Math.Abs(modelMetrics.SuccessRate - expectedSuccessRate) > 0.01)
        {
            throw new Exception($"Expected success rate {expectedSuccessRate}, got {modelMetrics.SuccessRate}");
        }
        Console.WriteLine($"✓ Success rate calculated correctly: {modelMetrics.SuccessRate:P0}");

        Console.WriteLine("✓ All metrics tracked correctly!\n");
    }

    /// <summary>
    /// Tests orchestrator builder pattern.
    /// </summary>
    public static void TestOrchestratorBuilder()
    {
        Console.WriteLine("=== Test: Orchestrator Builder ===");

        var tools = ToolRegistry.CreateDefault();
        var mockModel = new MockChatModel("test-response");

        var builder = new OrchestratorBuilder(tools, "default")
            .WithModel(
                "test",
                mockModel,
                ModelType.General,
                new[] { "testing" },
                maxTokens: 2048,
                avgLatencyMs: 500)
            .WithMetricTracking(true);

        var orchestratedModel = builder.Build();

        if (orchestratedModel == null)
        {
            throw new Exception("Failed to build orchestrated model!");
        }
        Console.WriteLine("✓ Orchestrator built successfully");

        var underlyingOrchestrator = builder.GetOrchestrator();
        if (underlyingOrchestrator == null)
        {
            throw new Exception("Failed to get underlying orchestrator!");
        }
        Console.WriteLine("✓ Can access underlying orchestrator");

        var metrics = underlyingOrchestrator.GetMetrics();
        if (!metrics.ContainsKey("test"))
        {
            throw new Exception("Test model not found in metrics!");
        }
        Console.WriteLine("✓ Model registered in orchestrator");

        Console.WriteLine("✓ Builder pattern works correctly!\n");
    }

    /// <summary>
    /// Tests composable tool extensions.
    /// </summary>
    public static async Task TestComposableTools()
    {
        Console.WriteLine("=== Test: Composable Tools ===");

        var tools = ToolRegistry.CreateDefault();
        var mathTool = tools.Get("math");

        if (mathTool == null)
        {
            Console.WriteLine("⚠ Math tool not available, skipping composable tools test");
            return;
        }

        // Test retry wrapper
        var reliableMath = mathTool.WithRetry(maxRetries: 2);
        var result1 = await reliableMath.InvokeAsync("2+2");
        if (!result1.IsSuccess)
        {
            throw new Exception("Retry tool should succeed!");
        }
        Console.WriteLine("✓ Retry wrapper works");

        // Test caching wrapper
        var cachedMath = mathTool.WithCaching(TimeSpan.FromMinutes(1));
        var result2 = await cachedMath.InvokeAsync("3+3");
        var result3 = await cachedMath.InvokeAsync("3+3"); // Should be cached
        if (!result2.IsSuccess || !result3.IsSuccess)
        {
            throw new Exception("Cached tool should succeed!");
        }
        Console.WriteLine("✓ Caching wrapper works");

        // Test timeout wrapper
        var timedMath = mathTool.WithTimeout(TimeSpan.FromSeconds(5));
        var result4 = await timedMath.InvokeAsync("5*5");
        if (!result4.IsSuccess)
        {
            throw new Exception("Timeout tool should succeed for fast operations!");
        }
        Console.WriteLine("✓ Timeout wrapper works");

        // Test tool chaining
        var chainedTool = ToolBuilder.Chain(
            "chained",
            "Chains multiple operations",
            mathTool);
        var result5 = await chainedTool.InvokeAsync("10/2");
        if (!result5.IsSuccess)
        {
            throw new Exception("Chained tool should succeed!");
        }
        Console.WriteLine("✓ Tool chaining works");

        Console.WriteLine("✓ All composable tool features work!\n");
    }

    /// <summary>
    /// Runs all orchestrator tests.
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("AI ORCHESTRATOR TESTS");
        Console.WriteLine(new string('=', 60) + "\n");

        TestOrchestratorCreation();
        TestUseCaseClassification();
        await TestModelSelection();
        TestPerformanceTracking();
        TestOrchestratorBuilder();
        await TestComposableTools();

        Console.WriteLine(new string('=', 60));
        Console.WriteLine("✓ ALL ORCHESTRATOR TESTS PASSED!");
        Console.WriteLine(new string('=', 60) + "\n");
    }
}

/// <summary>
/// Mock chat model for testing.
/// </summary>
internal sealed class MockChatModel : IChatCompletionModel
{
    private readonly string _response;

    public MockChatModel(string response)
    {
        _response = response;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        return Task.FromResult(_response);
    }
}
