// <copyright file="SmartModelOrchestratorTests.cs" company="Adaptive Systems Inc.">
// Copyright (c) Adaptive Systems Inc. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using Ouroboros.Agent;
using Xunit;
using FluentAssertions;

/// <summary>
/// Comprehensive unit tests for SmartModelOrchestrator.
/// Tests model selection, use case classification, performance tracking, and error handling.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SmartModelOrchestratorTests
{
    /// <summary>
    /// Tests that orchestrator can be created successfully with valid parameters.
    /// </summary>
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateOrchestrator()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var fallbackModel = "default";

        // Act
        var orchestrator = new SmartModelOrchestrator(tools, fallbackModel);

        // Assert
        orchestrator.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException when tools is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullTools_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new SmartModelOrchestrator(null!, "default");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseTools");
    }

    /// <summary>
    /// Tests use case classification for code generation prompts.
    /// </summary>
    [Theory]
    [InlineData("Write a function to calculate fibonacci", UseCaseType.CodeGeneration)]
    [InlineData("Implement a binary search algorithm", UseCaseType.CodeGeneration)]
    [InlineData("Debug this Python code", UseCaseType.CodeGeneration)]
    [InlineData("Refactor this class for better readability", UseCaseType.CodeGeneration)]
    public void ClassifyUseCase_WithCodePrompts_ShouldReturnCodeGeneration(
        string prompt,
        UseCaseType expected)
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        UseCase result = orchestrator.ClassifyUseCase(prompt);

        // Assert
        result.Type.Should().Be(expected);
        result.RequiredCapabilities.Should().Contain("code");
    }

    /// <summary>
    /// Tests use case classification for reasoning prompts.
    /// </summary>
    [Theory]
    [InlineData("Analyze why functional programming uses immutability", UseCaseType.Reasoning)]
    [InlineData("Explain the logic behind this algorithm", UseCaseType.Reasoning)]
    [InlineData("Reason about the causes of this behavior", UseCaseType.Reasoning)]
    [InlineData("Deduce the pattern in this data", UseCaseType.Reasoning)]
    public void ClassifyUseCase_WithReasoningPrompts_ShouldReturnReasoning(
        string prompt,
        UseCaseType expected)
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        UseCase result = orchestrator.ClassifyUseCase(prompt);

        // Assert
        result.Type.Should().Be(expected);
        result.RequiredCapabilities.Should().Contain("reasoning");
    }

    /// <summary>
    /// Tests use case classification for creative prompts.
    /// </summary>
    [Theory]
    [InlineData("Create a short story about AI", UseCaseType.Creative)]
    [InlineData("Generate a poem about nature", UseCaseType.Creative)]
    [InlineData("Write an imaginative description", UseCaseType.Creative)]
    public void ClassifyUseCase_WithCreativePrompts_ShouldReturnCreative(
        string prompt,
        UseCaseType expected)
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        UseCase result = orchestrator.ClassifyUseCase(prompt);

        // Assert
        result.Type.Should().Be(expected);
        result.RequiredCapabilities.Should().Contain("creative");
    }

    /// <summary>
    /// Tests use case classification for summarization prompts.
    /// </summary>
    [Theory]
    [InlineData("Summarize this document", UseCaseType.Summarization)]
    [InlineData("Give me a brief overview", UseCaseType.Summarization)]
    [InlineData("Provide a TLDR of this article", UseCaseType.Summarization)]
    public void ClassifyUseCase_WithSummarizationPrompts_ShouldReturnSummarization(
        string prompt,
        UseCaseType expected)
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        UseCase result = orchestrator.ClassifyUseCase(prompt);

        // Assert
        result.Type.Should().Be(expected);
        result.RequiredCapabilities.Should().Contain("summarization");
    }

    /// <summary>
    /// Tests use case classification for tool use prompts.
    /// </summary>
    [Theory]
    [InlineData("Use the search tool to find information", UseCaseType.ToolUse)]
    [InlineData("Invoke the analysis tool on this data", UseCaseType.ToolUse)]
    [InlineData("Execute the data processing tool", UseCaseType.ToolUse)]
    public void ClassifyUseCase_WithToolUsePrompts_ShouldReturnToolUse(
        string prompt,
        UseCaseType expected)
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        UseCase result = orchestrator.ClassifyUseCase(prompt);

        // Assert
        result.Type.Should().Be(expected);
        result.RequiredCapabilities.Should().Contain("tool-use");
    }

    /// <summary>
    /// Tests use case classification defaults to conversation for ambiguous prompts.
    /// </summary>
    [Theory]
    [InlineData("Can you tell me more about that?")]
    [InlineData("What's the weather like?")]
    [InlineData("Tell me more")]
    public void ClassifyUseCase_WithAmbiguousPrompts_ShouldReturnConversation(string prompt)
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        UseCase result = orchestrator.ClassifyUseCase(prompt);

        // Assert
        result.Type.Should().Be(UseCaseType.Conversation);
        result.RequiredCapabilities.Should().Contain("general");
    }

    /// <summary>
    /// Tests that model registration adds capability and initializes metrics.
    /// </summary>
    [Fact]
    public void RegisterModel_WithValidCapability_ShouldAddToMetrics()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);
        var model = new MockChatModel("test-response");
        var capability = new ModelCapability(
            "test-model",
            new[] { "testing", "general" },
            MaxTokens: 2048,
            AverageCost: 1.0,
            AverageLatencyMs: 500,
            Type: ModelType.General);

        // Act
        orchestrator.RegisterModel(capability, model);
        var metrics = orchestrator.GetMetrics();

        // Assert
        metrics.Should().ContainKey("test-model");
        metrics["test-model"].ResourceName.Should().Be("test-model");
        metrics["test-model"].ExecutionCount.Should().Be(0);
        metrics["test-model"].SuccessRate.Should().Be(1.0);
    }

    /// <summary>
    /// Tests that RegisterModel throws ArgumentNullException for null capability.
    /// </summary>
    [Fact]
    public void RegisterModel_WithNullCapability_ShouldThrowArgumentNullException()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);
        var model = new MockChatModel("test");

        // Act
        Action act = () => orchestrator.RegisterModel(null!, model);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("capability");
    }

    /// <summary>
    /// Tests that RegisterModel throws ArgumentNullException for null model.
    /// </summary>
    [Fact]
    public void RegisterModel_WithNullModel_ShouldThrowArgumentNullException()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);
        var capability = new ModelCapability(
            "test-model",
            new[] { "test" },
            4096, 1.0, 500, ModelType.General);

        // Act
        Action act = () => orchestrator.RegisterModel(capability, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("model");
    }

    /// <summary>
    /// Tests that SelectModelAsync returns failure for empty prompt.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SelectModelAsync_WithEmptyPrompt_ShouldReturnFailure(string? prompt)
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        var result = await orchestrator.SelectModelAsync(prompt!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Prompt cannot be empty");
    }

    /// <summary>
    /// Tests that SelectModelAsync returns failure when no models registered.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithNoModelsRegistered_ShouldReturnFailure()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        var result = await orchestrator.SelectModelAsync("Test prompt");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No models registered");
    }

    /// <summary>
    /// Tests that SelectModelAsync selects code model for code prompts.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithCodePrompt_ShouldSelectCodeModel()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        var generalModel = new MockChatModel("general");
        var codeModel = new MockChatModel("code");

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

        // Act
        var result = await orchestrator.SelectModelAsync("Write a function to sort an array");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModelName.Should().Be("coder");
        result.Value.ConfidenceScore.Should().BeGreaterThan(0.5);
    }

    /// <summary>
    /// Tests that SelectModelAsync selects reasoning model for reasoning prompts.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithReasoningPrompt_ShouldSelectReasoningModel()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        var generalModel = new MockChatModel("general");
        var reasoningModel = new MockChatModel("reasoning");

        orchestrator.RegisterModel(
            new ModelCapability(
                "general",
                new[] { "general", "conversation" },
                4096, 1.0, 800, ModelType.General),
            generalModel);

        orchestrator.RegisterModel(
            new ModelCapability(
                "reasoner",
                new[] { "reasoning", "analysis", "logic" },
                4096, 1.2, 1000, ModelType.Reasoning),
            reasoningModel);

        // Act
        var result = await orchestrator.SelectModelAsync("Analyze why this algorithm is O(n log n)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModelName.Should().Be("reasoner");
        result.Value.ConfidenceScore.Should().BeGreaterThan(0.5);
    }

    /// <summary>
    /// Tests that RecordMetric updates performance metrics correctly.
    /// </summary>
    [Fact]
    public void RecordMetric_WithSuccessfulExecution_ShouldUpdateMetrics()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);
        var model = new MockChatModel("test");
        var capability = new ModelCapability(
            "test-model",
            new[] { "test" },
            4096, 1.0, 500, ModelType.General);

        orchestrator.RegisterModel(capability, model);

        // Act
        orchestrator.RecordMetric("test-model", latencyMs: 450, success: true);
        var metrics = orchestrator.GetMetrics();

        // Assert
        metrics["test-model"].ExecutionCount.Should().Be(1);
        metrics["test-model"].AverageLatencyMs.Should().Be(450);
        metrics["test-model"].SuccessRate.Should().Be(1.0);
    }

    /// <summary>
    /// Tests that RecordMetric updates success rate correctly on failure.
    /// </summary>
    [Fact]
    public void RecordMetric_WithFailedExecution_ShouldUpdateSuccessRate()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);
        var model = new MockChatModel("test");
        var capability = new ModelCapability(
            "test-model",
            new[] { "test" },
            4096, 1.0, 500, ModelType.General);

        orchestrator.RegisterModel(capability, model);

        // Act - Record one success and one failure
        orchestrator.RecordMetric("test-model", latencyMs: 450, success: true);
        orchestrator.RecordMetric("test-model", latencyMs: 550, success: false);
        var metrics = orchestrator.GetMetrics();

        // Assert
        metrics["test-model"].ExecutionCount.Should().Be(2);
        metrics["test-model"].SuccessRate.Should().Be(0.5); // 1 success out of 2
    }

    /// <summary>
    /// Tests that RecordMetric calculates average latency correctly.
    /// </summary>
    [Fact]
    public void RecordMetric_WithMultipleExecutions_ShouldCalculateAverageLatency()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);
        var model = new MockChatModel("test");
        var capability = new ModelCapability(
            "test-model",
            new[] { "test" },
            4096, 1.0, 500, ModelType.General);

        orchestrator.RegisterModel(capability, model);

        // Act - Record three executions: 400ms, 500ms, 600ms
        orchestrator.RecordMetric("test-model", latencyMs: 400, success: true);
        orchestrator.RecordMetric("test-model", latencyMs: 500, success: true);
        orchestrator.RecordMetric("test-model", latencyMs: 600, success: true);
        var metrics = orchestrator.GetMetrics();

        // Assert
        metrics["test-model"].ExecutionCount.Should().Be(3);
        metrics["test-model"].AverageLatencyMs.Should().BeApproximately(500, 0.1);
    }

    /// <summary>
    /// Tests that SelectModelAsync uses fallback model when primary not registered.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithMissingModel_ShouldUseFallback()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "fallback");

        var fallbackModel = new MockChatModel("fallback");

        // Register only fallback, but capability for "coder"
        orchestrator.RegisterModel(
            new ModelCapability(
                "coder",
                new[] { "code", "programming" },
                8192, 1.5, 1200, ModelType.Code));

        orchestrator.RegisterModel(
            new ModelCapability(
                "fallback",
                new[] { "general" },
                4096, 1.0, 800, ModelType.General),
            fallbackModel);

        // Act
        var result = await orchestrator.SelectModelAsync("Write a function");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SelectedModel.Should().Be(fallbackModel);
    }

    /// <summary>
    /// Tests that SelectModelAsync returns failure when no fallback available.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithNoFallback_ShouldReturnFailure()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools, "nonexistent");

        // Register capability without model instance
        orchestrator.RegisterModel(
            new ModelCapability(
                "coder",
                new[] { "code", "programming" },
                8192, 1.5, 1200, ModelType.Code));

        // Act
        var result = await orchestrator.SelectModelAsync("Write a function");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not registered and no fallback available");
    }

    /// <summary>
    /// Tests that GetMetrics returns read-only dictionary of current metrics.
    /// </summary>
    [Fact]
    public void GetMetrics_ShouldReturnCurrentMetrics()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);
        var model = new MockChatModel("test");
        var capability = new ModelCapability(
            "test-model",
            new[] { "test" },
            4096, 1.0, 500, ModelType.General);

        orchestrator.RegisterModel(capability, model);

        // Act
        var metrics = orchestrator.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.Should().BeAssignableTo<IReadOnlyDictionary<string, PerformanceMetrics>>();
        metrics.Should().ContainKey("test-model");
    }

    /// <summary>
    /// Tests that cost-aware scoring favors lower cost models for cost-sensitive use cases.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithCostSensitiveUseCase_ShouldPreferLowerCostModel()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        var cheapModel = new MockChatModel("cheap");
        var expensiveModel = new MockChatModel("expensive");

        // Register a cheap general model and an expensive general model
        orchestrator.RegisterModel(
            new ModelCapability(
                "cheap-general",
                new[] { "general", "conversation" },
                4096, AverageCost: 0.5, AverageLatencyMs: 800, Type: ModelType.General),
            cheapModel);

        orchestrator.RegisterModel(
            new ModelCapability(
                "expensive-general",
                new[] { "general", "conversation" },
                8192, AverageCost: 5.0, AverageLatencyMs: 600, Type: ModelType.General),
            expensiveModel);

        // Act - general conversation has higher CostWeight
        var result = await orchestrator.SelectModelAsync("Hello, how are you?");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Both models are general type, but cheap model should be preferred due to cost scoring
        result.Value.ModelName.Should().Be("cheap-general");
    }

    /// <summary>
    /// Tests that cost-aware scoring normalizes costs across models correctly.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithVariedCosts_ShouldNormalizeCostScoring()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        var lowCost = new MockChatModel("low");
        var midCost = new MockChatModel("mid");
        var highCost = new MockChatModel("high");

        orchestrator.RegisterModel(
            new ModelCapability("low-cost", new[] { "general" }, 4096, AverageCost: 1.0, AverageLatencyMs: 1000, Type: ModelType.General),
            lowCost);

        orchestrator.RegisterModel(
            new ModelCapability("mid-cost", new[] { "general" }, 4096, AverageCost: 3.0, AverageLatencyMs: 800, Type: ModelType.General),
            midCost);

        orchestrator.RegisterModel(
            new ModelCapability("high-cost", new[] { "general" }, 4096, AverageCost: 5.0, AverageLatencyMs: 600, Type: ModelType.General),
            highCost);

        // Act
        var result = await orchestrator.SelectModelAsync("General question");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Low cost model should score highest due to cost normalization
        result.Value.ModelName.Should().Be("low-cost");
    }

    /// <summary>
    /// Tests that type match still dominates over cost for specialized use cases.
    /// </summary>
    [Fact]
    public async Task SelectModelAsync_WithSpecializedUseCase_TypeMatchDominatesCost()
    {
        // Arrange
        var tools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(tools);

        var cheapGeneral = new MockChatModel("cheap-general");
        var expensiveCode = new MockChatModel("expensive-code");

        orchestrator.RegisterModel(
            new ModelCapability("cheap-general", new[] { "general" }, 4096, AverageCost: 0.5, AverageLatencyMs: 800, Type: ModelType.General),
            cheapGeneral);

        orchestrator.RegisterModel(
            new ModelCapability("expensive-code", new[] { "code", "programming" }, 8192, AverageCost: 3.0, AverageLatencyMs: 1000, Type: ModelType.Code),
            expensiveCode);

        // Act - code generation use case should prefer code model despite higher cost
        var result = await orchestrator.SelectModelAsync("Implement a binary search function");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModelName.Should().Be("expensive-code");
    }
}
