// <copyright file="HybridModelRouterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using FluentAssertions;
using Ouroboros.Providers.Routing;
using Xunit;

namespace Ouroboros.Tests.Providers.Routing;

/// <summary>
/// Unit tests for HybridModelRouter class.
/// Validates routing logic and fallback behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HybridModelRouterTests
{
    [Fact]
    public void Constructor_WithValidConfig_CreatesRouter()
    {
        // Arrange
        var defaultModel = new MockChatModel("default");
        var config = new HybridRoutingConfig(defaultModel);

        // Act
        var router = new HybridModelRouter(config);

        // Assert
        router.Should().NotBeNull();
        router.DetectionStrategy.Should().Be(TaskDetectionStrategy.Heuristic);
    }

    [Fact]
    public async Task GenerateTextAsync_WithSimplePrompt_UsesDefaultModel()
    {
        // Arrange
        var defaultModel = new MockChatModel("default-response");
        var reasoningModel = new MockChatModel("reasoning-response");
        var config = new HybridRoutingConfig(defaultModel, ReasoningModel: reasoningModel);
        var router = new HybridModelRouter(config);

        // Act
        string result = await router.GenerateTextAsync("Hello!");

        // Assert
        result.Should().Contain("default-response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithReasoningPrompt_UsesReasoningModel()
    {
        // Arrange
        var defaultModel = new MockChatModel("default-response");
        var reasoningModel = new MockChatModel("reasoning-response");
        var config = new HybridRoutingConfig(defaultModel, ReasoningModel: reasoningModel);
        var router = new HybridModelRouter(config);

        // Act
        string result = await router.GenerateTextAsync("Explain why the sky is blue.");

        // Assert
        result.Should().Contain("reasoning-response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithCodingPrompt_UsesCodingModel()
    {
        // Arrange
        var defaultModel = new MockChatModel("default-response");
        var codingModel = new MockChatModel("coding-response");
        var config = new HybridRoutingConfig(defaultModel, CodingModel: codingModel);
        var router = new HybridModelRouter(config);

        // Act
        string result = await router.GenerateTextAsync("Write a function to sort an array.");

        // Assert
        result.Should().Contain("coding-response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithPlanningPrompt_UsesPlanningModel()
    {
        // Arrange
        var defaultModel = new MockChatModel("default-response");
        var planningModel = new MockChatModel("planning-response");
        var config = new HybridRoutingConfig(defaultModel, PlanningModel: planningModel);
        var router = new HybridModelRouter(config);

        // Act
        string result = await router.GenerateTextAsync("Create a step-by-step plan for deployment.");

        // Assert
        result.Should().Contain("planning-response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithFailedModel_UsesFallback()
    {
        // Arrange
        var defaultModel = new FailingMockChatModel("failing");
        var fallbackModel = new MockChatModel("fallback-success");
        var config = new HybridRoutingConfig(defaultModel, FallbackModel: fallbackModel);
        var router = new HybridModelRouter(config);

        // Act
        string result = await router.GenerateTextAsync("Test prompt");

        // Assert
        result.Should().Contain("fallback-success");
    }

    [Fact]
    public async Task GenerateTextAsync_WithNoSpecializedModel_UsesDefault()
    {
        // Arrange
        var defaultModel = new MockChatModel("default-response");
        var config = new HybridRoutingConfig(defaultModel);
        var router = new HybridModelRouter(config);

        // Act - Reasoning prompt but no specialized reasoning model
        string result = await router.GenerateTextAsync("Explain quantum mechanics.");

        // Assert
        result.Should().Contain("default-response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithAllModelsFailing_ReturnsError()
    {
        // Arrange
        var defaultModel = new FailingMockChatModel("failing-default");
        var fallbackModel = new FailingMockChatModel("failing-fallback");
        var config = new HybridRoutingConfig(defaultModel, FallbackModel: fallbackModel);
        var router = new HybridModelRouter(config);

        // Act
        string result = await router.GenerateTextAsync("Test");

        // Assert
        result.Should().Contain("[hybrid-router-error]");
    }

    [Fact]
    public void DetectTaskTypeForPrompt_ReturnsCorrectType()
    {
        // Arrange
        var defaultModel = new MockChatModel("default");
        var config = new HybridRoutingConfig(defaultModel);
        var router = new HybridModelRouter(config);

        // Act
        TaskType reasoningTask = router.DetectTaskTypeForPrompt("Why does this happen?");
        TaskType codingTask = router.DetectTaskTypeForPrompt("Implement a function.");
        TaskType simpleTask = router.DetectTaskTypeForPrompt("Hello");

        // Assert
        reasoningTask.Should().Be(TaskType.Reasoning);
        codingTask.Should().Be(TaskType.Coding);
        simpleTask.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void GetModelForTaskType_ReturnsCorrectModel()
    {
        // Arrange
        var defaultModel = new MockChatModel("default");
        var reasoningModel = new MockChatModel("reasoning");
        var config = new HybridRoutingConfig(defaultModel, ReasoningModel: reasoningModel);
        var router = new HybridModelRouter(config);

        // Act
        IChatCompletionModel simpleModel = router.GetModelForTaskType(TaskType.Simple);
        IChatCompletionModel reasoningModelResult = router.GetModelForTaskType(TaskType.Reasoning);

        // Assert
        simpleModel.Should().Be(defaultModel);
        reasoningModelResult.Should().Be(reasoningModel);
    }

    [Fact]
    public async Task GenerateTextAsync_WithDifferentDetectionStrategies_WorksCorrectly()
    {
        // Arrange
        var defaultModel = new MockChatModel("default");
        var reasoningModel = new MockChatModel("reasoning");
        var config = new HybridRoutingConfig(
            defaultModel,
            ReasoningModel: reasoningModel,
            DetectionStrategy: TaskDetectionStrategy.Hybrid);
        var router = new HybridModelRouter(config);

        // Act
        string result = await router.GenerateTextAsync("Analyze this problem carefully.");

        // Assert
        result.Should().Contain("reasoning");
    }
}

/// <summary>
/// Mock chat model that always fails.
/// </summary>
internal sealed class FailingMockChatModel : IChatCompletionModel
{
    private readonly string _name;

    public FailingMockChatModel(string name)
    {
        _name = name;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        throw new InvalidOperationException($"Mock model {_name} failed");
    }
}
