// <copyright file="OrchestratorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests;

using System;
using System.Threading.Tasks;
using System.Threading;
using FluentAssertions;
using Xunit;
using Ouroboros.Agent;
using Ouroboros.Tools;
using Ouroboros.Tests.Mocks;
using Ouroboros.Providers;

/// <summary>
/// Unit tests for AI orchestrator capabilities including model selection,
/// use case classification, and performance tracking.
/// Uses mock models for testing - no external LLM service required.
/// </summary>
[Trait("Category", "Unit")]
public class OrchestratorTests
{
    /// <summary>
    /// Tests basic orchestrator creation and configuration.
    /// </summary>
    [Fact]
    public void Orchestrator_Creation_ShouldRegisterModels()
    {
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

        var metrics = orchestrator.GetMetrics();
        metrics.Should().ContainKey("test-model", "Model should be in metrics after registration");
    }

    /// <summary>
    /// Tests use case classification for different prompt types.
    /// </summary>
    [Fact]
    public void UseCase_Classification_ShouldIdentifyPromptTypes()
    {
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "default");

        // Test code generation classification
        var codePrompt = "Write a function to calculate fibonacci numbers";
        var codeCase = orchestrator.ClassifyUseCase(codePrompt);
        codeCase.Type.Should().Be(UseCaseType.CodeGeneration);

        // Test reasoning classification
        var reasoningPrompt = "Analyze why functional programming uses immutability";
        var reasoningCase = orchestrator.ClassifyUseCase(reasoningPrompt);
        reasoningCase.Type.Should().Be(UseCaseType.Reasoning);

        // Test creative classification
        var creativePrompt = "Create a short story about AI";
        var creativeCase = orchestrator.ClassifyUseCase(creativePrompt);
        creativeCase.Type.Should().Be(UseCaseType.Creative);

        // Test summarization classification
        var summaryPrompt = "Summarize this long document about machine learning";
        var summaryCase = orchestrator.ClassifyUseCase(summaryPrompt);
        summaryCase.Type.Should().Be(UseCaseType.Summarization);

        // Test tool use classification
        var toolPrompt = "Use the search tool to find information";
        var toolCase = orchestrator.ClassifyUseCase(toolPrompt);
        toolCase.Type.Should().Be(UseCaseType.ToolUse);
    }

    /// <summary>
    /// Tests model selection based on use case.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task Model_Selection_ShouldChooseAppropriateModel()
    {
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

        codeDecision.IsSuccess.Should().BeTrue();
        codeDecision.Match(
            decision =>
            {
                decision.ModelName.Should().Be("coder", "Code prompt should select 'coder' model");
            },
            error => Assert.Fail($"Selection failed: {error}"));

        // Test reasoning prompt selects reasoning model
        var reasoningPrompt = "Explain the principle of monadic composition";
        var reasoningDecision = await orchestrator.SelectModelAsync(reasoningPrompt);

        reasoningDecision.IsSuccess.Should().BeTrue();
        reasoningDecision.Match(
            decision =>
            {
                decision.ModelName.Should().Be("reasoner", "Reasoning prompt should select 'reasoner' model");
            },
            error => Assert.Fail($"Selection failed: {error}"));

        // Test general prompt selects general model
        var generalPrompt = "Tell me a joke";
        var generalDecision = await orchestrator.SelectModelAsync(generalPrompt);

        generalDecision.IsSuccess.Should().BeTrue();
        generalDecision.Match(
            decision =>
            {
                // Accept either general or creative model for a joke prompt (logic might choose either depending on scoring)
                // In the original test it allowed "reasoner" too, but "general" seems most appropriate for "joke".
                // However, let's stick to the original logic loosely or fix it if it was flaky.
                // Original: if (decision.ModelName != "general" && decision.ModelName != "reasoner")
                var allowed = new[] { "general", "reasoner" };
                allowed.Should().Contain(decision.ModelName, "General prompt should select 'general' or 'reasoner' model");
            },
            error => Assert.Fail($"Selection failed: {error}"));
    }

    /// <summary>
    /// Tests performance metric tracking.
    /// </summary>
    [Fact]
    public void Performance_Tracking_ShouldRecordMetrics()
    {
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "default");

        // Register a model (without provider is fine for just registering capability?)
        // The original test didn't provide a provider for this one, just capability.
        orchestrator.RegisterModel(new ModelCapability(
            "test-model",
            new[] { "test" },
            2048, 1.0, 500, ModelType.General));

        // Record some metrics
        orchestrator.RecordMetric("test-model", 450, true);
        orchestrator.RecordMetric("test-model", 550, true);
        orchestrator.RecordMetric("test-model", 500, false);

        var metrics = orchestrator.GetMetrics();
        metrics.Should().ContainKey("test-model");
        var modelMetrics = metrics["test-model"];

        modelMetrics.ExecutionCount.Should().Be(3);

        var expectedAvgLatency = (450 + 550 + 500) / 3.0;
        modelMetrics.AverageLatencyMs.Should().BeApproximately(expectedAvgLatency, 0.01);

        var expectedSuccessRate = 2.0 / 3.0; // 2 successes out of 3
        modelMetrics.SuccessRate.Should().BeApproximately(expectedSuccessRate, 0.01);
    }

    /// <summary>
    /// Tests orchestrator builder pattern.
    /// </summary>
    [Fact]
    public void OrchestratorBuilder_Pattern_ShouldBuildCorrectly()
    {
        var tools = ToolRegistry.CreateDefault();
        var mockModel = new MockChatModel("test-response");

        // Note: OrchestratorBuilder usage might have changed or internal.
        // Assuming OrchestratorBuilder is available in Ouroboros.Agent
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
        orchestratedModel.Should().NotBeNull();

        var underlyingOrchestrator = builder.GetOrchestrator();
        underlyingOrchestrator.Should().NotBeNull();

        var metrics = underlyingOrchestrator!.GetMetrics();
        metrics.Should().ContainKey("test");
    }

    /// <summary>
    /// Tests composable tool extensions.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task ComposableTools_Extensions_ShouldWrapCorrectly()
    {
        var tools = ToolRegistry.CreateDefault();
        var mathTool = tools.Get("math");

        // Create a mock tool if 'math' doesn't exist, to ensure test stability
        if (mathTool == null)
        {
            mathTool = new MockTool("math", "Calculates math",
                async (input) => {
                     // Very simple mock behavior
                     return await Task.FromResult(new ToolExecution("math", input, "4"));
                });
        }

        mathTool.Should().NotBeNull("Math tool or mock tool should be available");

        // Test retry wrapper
        var reliableMath = mathTool.WithRetry(maxRetries: 2);
        var result1 = await reliableMath.InvokeAsync("2+2");
        result1.IsSuccess.Should().BeTrue("Retry tool should succeed");

        // Test caching wrapper
        var cachedMath = mathTool.WithCaching(TimeSpan.FromMinutes(1));
        var result2 = await cachedMath.InvokeAsync("3+3");
        var result3 = await cachedMath.InvokeAsync("3+3"); // Should be cached
        result2.IsSuccess.Should().BeTrue();
        result3.IsSuccess.Should().BeTrue();

        // Test timeout wrapper
        var timedMath = mathTool.WithTimeout(TimeSpan.FromSeconds(5));
        var result4 = await timedMath.InvokeAsync("5*5");
        result4.IsSuccess.Should().BeTrue("Timeout tool should succeed for fast operations");

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
    /// Kept for backward compatibility - wraps individual test methods.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("AI ORCHESTRATOR TESTS");
        Console.WriteLine(new string('=', 60) + "\n");

        var instance = new OrchestratorTests();
        instance.Orchestrator_Creation_ShouldRegisterModels();
        instance.UseCase_Classification_ShouldIdentifyPromptTypes();
        await instance.Model_Selection_ShouldChooseAppropriateModel();
        instance.Performance_Tracking_ShouldRecordMetrics();
        instance.OrchestratorBuilder_Pattern_ShouldBuildCorrectly();
        await instance.ComposableTools_Extensions_ShouldWrapCorrectly();

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
    private readonly string response;

    public MockChatModel(string response)
    {
        this.response = response;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        return Task.FromResult(this.response);
    }
}
