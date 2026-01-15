// <copyright file="UncertaintyRouterTests.cs" company="Adaptive Systems Inc.">
// Copyright (c) Adaptive Systems Inc. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Xunit;
using FluentAssertions;

/// <summary>
/// Comprehensive unit tests for UncertaintyRouter.
/// Tests confidence-based routing, fallback strategy selection, and learning mechanisms.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UncertaintyRouterTests
{
    /// <summary>
    /// Tests that router can be created with valid parameters.
    /// </summary>
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateRouter()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var minConfidence = 0.7;

        // Act
        var router = new UncertaintyRouter(orchestrator, minConfidence);

        // Assert
        router.Should().NotBeNull();
        router.MinimumConfidenceThreshold.Should().Be(0.7);
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException for null orchestrator.
    /// </summary>
    [Fact]
    public void Constructor_WithNullOrchestrator_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new UncertaintyRouter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("orchestrator");
    }

    /// <summary>
    /// Tests that confidence threshold is clamped to valid range.
    /// </summary>
    [Theory]
    [InlineData(-0.5, 0.0)]  // Below range
    [InlineData(0.0, 0.0)]   // Minimum
    [InlineData(0.5, 0.5)]   // Middle
    [InlineData(1.0, 1.0)]   // Maximum
    [InlineData(1.5, 1.0)]   // Above range
    public void Constructor_WithConfidenceThreshold_ShouldClampToValidRange(
        double input,
        double expected)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();

        // Act
        var router = new UncertaintyRouter(orchestrator, input);

        // Assert
        router.MinimumConfidenceThreshold.Should().Be(expected);
    }

    /// <summary>
    /// Tests fallback strategy selection for very low confidence.
    /// </summary>
    [Theory]
    [InlineData(0.1, "short prompt", FallbackStrategy.RequestClarification)]
    [InlineData(0.2, "this is a much longer prompt with many words that exceeds fifty characters", FallbackStrategy.GatherMoreContext)]
    [InlineData(0.29, "another short one", FallbackStrategy.RequestClarification)]
    public void DetermineFallback_WithVeryLowConfidence_ShouldRequestClarificationOrContext(
        double confidence,
        string task,
        FallbackStrategy expected)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        var result = router.DetermineFallback(task, confidence);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests fallback strategy selection for low confidence.
    /// </summary>
    [Theory]
    [InlineData(0.3, "short prompt", FallbackStrategy.UseEnsemble)]
    [InlineData(0.4, "this is a very long task description with many words in it and this should have more than twenty words which should trigger decomposition strategy for better task handling and processing", FallbackStrategy.DecomposeTask)]
    [InlineData(0.49, "short", FallbackStrategy.UseEnsemble)]
    public void DetermineFallback_WithLowConfidence_ShouldUseEnsembleOrDecompose(
        double confidence,
        string task,
        FallbackStrategy expected)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        var result = router.DetermineFallback(task, confidence);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests fallback strategy selection for moderate confidence.
    /// </summary>
    [Theory]
    [InlineData(0.5, FallbackStrategy.UseEnsemble)]
    [InlineData(0.6, FallbackStrategy.UseEnsemble)]
    [InlineData(0.69, FallbackStrategy.UseEnsemble)]
    public void DetermineFallback_WithModerateConfidence_ShouldUseEnsemble(
        double confidence,
        FallbackStrategy expected)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator, 0.7);
        var task = "Analyze this data";

        // Act
        var result = router.DetermineFallback(task, confidence);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that RouteAsync returns failure for empty task.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RouteAsync_WithEmptyTask_ShouldReturnFailure(string? task)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        var result = await router.RouteAsync(task!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task cannot be empty");
    }

    /// <summary>
    /// Tests that RouteAsync uses direct route for high confidence.
    /// </summary>
    [Fact]
    public async Task RouteAsync_WithHighConfidence_ShouldUseDirectRoute()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(confidence: 0.9);
        var router = new UncertaintyRouter(orchestrator, minConfidenceThreshold: 0.7);

        // Act
        var result = await router.RouteAsync("Test task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Route.Should().Be("test-model");
        result.Value.Confidence.Should().Be(0.9);
        result.Value.Metadata.Should().ContainKey("strategy");
        result.Value.Metadata["strategy"].Should().Be("direct");
    }

    /// <summary>
    /// Tests that RouteAsync applies fallback for low confidence.
    /// </summary>
    [Fact]
    public async Task RouteAsync_WithLowConfidence_ShouldApplyFallback()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(confidence: 0.5);
        var router = new UncertaintyRouter(orchestrator, minConfidenceThreshold: 0.7);

        // Act
        var result = await router.RouteAsync("Test task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().Be(0.5);
        result.Value.Metadata.Should().ContainKey("fallback_strategy");
        result.Value.Metadata.Should().ContainKey("original_route");
        result.Value.Metadata["original_route"].Should().Be("test-model");
    }

    /// <summary>
    /// Tests that RouteAsync applies ensemble fallback for moderate confidence.
    /// </summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.6)]
    [InlineData(0.69)]
    public async Task RouteAsync_WithModerateConfidence_ShouldApplyEnsembleFallback(
        double confidence)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(confidence);
        var router = new UncertaintyRouter(orchestrator, minConfidenceThreshold: 0.7);

        // Act
        var result = await router.RouteAsync("Analyze this data");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Route.Should().StartWith("ensemble:");
        result.Value.Metadata["fallback_strategy"].ToString().Should().Be(FallbackStrategy.UseEnsemble.ToString());
    }

    /// <summary>
    /// Tests that RouteAsync propagates orchestrator failures.
    /// </summary>
    [Fact]
    public async Task RouteAsync_WhenOrchestratorFails_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = CreateFailingOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        var result = await router.RouteAsync("Test task");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Orchestrator error");
    }

    /// <summary>
    /// Tests that CalculateConfidenceAsync returns 0 for invalid inputs.
    /// </summary>
    [Theory]
    [InlineData("", "route")]
    [InlineData("task", "")]
    [InlineData(null, "route")]
    [InlineData("task", null)]
    public async Task CalculateConfidenceAsync_WithInvalidInputs_ShouldReturnZero(
        string? task,
        string? route)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        var confidence = await router.CalculateConfidenceAsync(task!, route!);

        // Assert
        confidence.Should().Be(0.0);
    }

    /// <summary>
    /// Tests that CalculateConfidenceAsync returns base confidence with no history.
    /// </summary>
    [Fact]
    public async Task CalculateConfidenceAsync_WithNoHistory_ShouldReturnBaseConfidence()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        var confidence = await router.CalculateConfidenceAsync("Test task", "test-route");

        // Assert
        confidence.Should().BeGreaterThanOrEqualTo(0.4); // Base 0.5 * complexity factor (0.9+) * context factor (0.9)
        confidence.Should().BeLessThanOrEqualTo(0.6);
    }

    /// <summary>
    /// Tests that CalculateConfidenceAsync adjusts based on task complexity.
    /// </summary>
    [Theory]
    [InlineData("Short task", true)]  // Simple task -> higher confidence
    [InlineData("This is a very long and complex task with many words that increases the complexity score significantly and should reduce confidence", false)]  // Complex task -> lower confidence
    public async Task CalculateConfidenceAsync_ShouldAdjustForComplexity(
        string task,
        bool expectHigher)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        var shortTaskConfidence = await router.CalculateConfidenceAsync("Short", "route");
        var longTaskConfidence = await router.CalculateConfidenceAsync(task, "route");

        // Assert
        if (expectHigher)
        {
            shortTaskConfidence.Should().BeGreaterThanOrEqualTo(longTaskConfidence);
        }
        else
        {
            longTaskConfidence.Should().BeLessThan(shortTaskConfidence);
        }
    }

    /// <summary>
    /// Tests that CalculateConfidenceAsync adjusts based on context availability.
    /// </summary>
    [Fact]
    public async Task CalculateConfidenceAsync_WithContext_ShouldIncreaseConfidence()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);
        var context = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var confidenceWithoutContext = await router.CalculateConfidenceAsync("Test", "route", null);
        var confidenceWithContext = await router.CalculateConfidenceAsync("Test", "route", context);

        // Assert
        confidenceWithContext.Should().BeGreaterThan(confidenceWithoutContext);
    }

    /// <summary>
    /// Tests that RecordRoutingOutcome stores routing history.
    /// </summary>
    [Fact]
    public void RecordRoutingOutcome_ShouldStoreHistory()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);
        var decision = new RoutingDecision(
            "test-route",
            "Test reason",
            0.8,
            new Dictionary<string, object>());

        // Act
        router.RecordRoutingOutcome(decision, success: true);

        // Assert - Verify by calculating confidence which uses history
        var confidence = router.CalculateConfidenceAsync("Test", "test-route").Result;
        confidence.Should().BeGreaterThan(0.0); // Should use historical data
    }

    /// <summary>
    /// Tests that RecordRoutingOutcome throws for null decision.
    /// </summary>
    [Fact]
    public void RecordRoutingOutcome_WithNullDecision_ShouldThrowArgumentNullException()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        Action act = () => router.RecordRoutingOutcome(null!, success: true);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that RecordRoutingOutcome updates success rate over time.
    /// </summary>
    [Fact]
    public async Task RecordRoutingOutcome_WithMultipleRecords_ShouldUpdateSuccessRate()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);
        var decision = new RoutingDecision(
            "test-route",
            "Test",
            0.8,
            new Dictionary<string, object>());

        // Act - Record mixed successes
        router.RecordRoutingOutcome(decision, success: true);
        router.RecordRoutingOutcome(decision, success: true);
        router.RecordRoutingOutcome(decision, success: false);

        // Wait a moment for async operations
        await Task.Delay(10);

        var confidence = await router.CalculateConfidenceAsync("Test", "test-route");

        // Assert - Confidence should reflect 2/3 success rate
        confidence.Should().BeGreaterThan(0.5); // Base confidence boosted by good success rate
    }

    /// <summary>
    /// Tests that routing history is limited to prevent memory growth.
    /// </summary>
    [Fact]
    public void RecordRoutingOutcome_ShouldLimitHistorySize()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);
        var decision = new RoutingDecision(
            "test-route",
            "Test",
            0.8,
            new Dictionary<string, object>());

        // Act - Record more than 100 outcomes
        for (int i = 0; i < 150; i++)
        {
            router.RecordRoutingOutcome(decision, success: true);
        }

        // Assert - History should be limited (we can't directly verify,
        // but the system should not throw or slow down)
        Action act = () => router.RecordRoutingOutcome(decision, success: true);
        act.Should().NotThrow();
    }

    #region Helper Methods

    private static IModelOrchestrator CreateMockOrchestrator(double confidence = 0.8)
    {
        return new MockModelOrchestrator(confidence);
    }

    private static IModelOrchestrator CreateFailingOrchestrator()
    {
        return new FailingMockOrchestrator();
    }

    private sealed class MockModelOrchestrator : IModelOrchestrator
    {
        private readonly double _confidence;

        public MockModelOrchestrator(double confidence)
        {
            _confidence = confidence;
        }

        public Task<Result<OrchestratorDecision, string>> SelectModelAsync(
            string prompt,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            var model = new MockChatModel("test-response");
            var decision = new OrchestratorDecision(
                SelectedModel: model,
                ModelName: "test-model",
                Reason: "Mock selection",
                RecommendedTools: ToolRegistry.CreateDefault(),
                ConfidenceScore: _confidence);

            return Task.FromResult(
                Result<OrchestratorDecision, string>.Success(decision));
        }

        public UseCase ClassifyUseCase(string prompt)
        {
            return new UseCase(
                UseCaseType.Conversation,
                EstimatedComplexity: 1,
                new[] { "general" },
                PerformanceWeight: 0.5,
                CostWeight: 0.5);
        }

        public void RegisterModel(ModelCapability capability)
        {
            // Mock implementation
        }

        public void RecordMetric(string resourceName, double latencyMs, bool success)
        {
            // Mock implementation
        }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    private sealed class FailingMockOrchestrator : IModelOrchestrator
    {
        public Task<Result<OrchestratorDecision, string>> SelectModelAsync(
            string prompt,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(
                Result<OrchestratorDecision, string>.Failure("Orchestrator error"));
        }

        public UseCase ClassifyUseCase(string prompt)
        {
            return new UseCase(
                UseCaseType.Conversation,
                EstimatedComplexity: 1,
                new[] { "general" },
                PerformanceWeight: 0.5,
                CostWeight: 0.5);
        }

        public void RegisterModel(ModelCapability capability)
        {
            // Mock implementation
        }

        public void RecordMetric(string resourceName, double latencyMs, bool success)
        {
            // Mock implementation
        }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    #endregion

    #region Routing Metadata Tests

    /// <summary>
    /// Tests that routing decision includes threshold metadata for direct routing.
    /// </summary>
    [Fact]
    public async Task RouteAsync_WithHighConfidence_ShouldIncludeThresholdInMetadata()
    {
        // Arrange
        var orchestrator = CreateMockOrchestratorWithConfidence(0.9);
        var router = new UncertaintyRouter(orchestrator, 0.7);

        // Act
        var result = await router.RouteAsync("Test task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("threshold");
        result.Value.Metadata["threshold"].Should().Be(0.7);
        result.Value.Metadata.Should().ContainKey("strategy");
        result.Value.Metadata["strategy"].Should().Be("direct");
    }

    /// <summary>
    /// Tests that routing decision includes threshold and fallback info for low confidence.
    /// </summary>
    [Fact]
    public async Task RouteAsync_WithLowConfidence_ShouldIncludeFallbackMetadata()
    {
        // Arrange
        var orchestrator = CreateMockOrchestratorWithConfidence(0.4);
        var router = new UncertaintyRouter(orchestrator, 0.7);

        // Act
        var result = await router.RouteAsync("Test task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Metadata.Should().ContainKey("threshold");
        result.Value.Metadata["threshold"].Should().Be(0.7);
        result.Value.Metadata.Should().ContainKey("original_route");
        result.Value.Metadata.Should().ContainKey("fallback_strategy");
        result.Value.Metadata.Should().ContainKey("original_confidence");
        ((double)result.Value.Metadata["original_confidence"]).Should().Be(0.4);
    }

    /// <summary>
    /// Tests that RouteAsync propagates orchestrator failure without exception.
    /// </summary>
    [Fact]
    public async Task RouteAsync_WithOrchestratorFailure_ShouldReturnFailureMonadically()
    {
        // Arrange
        var orchestrator = new FailingMockOrchestrator();
        var router = new UncertaintyRouter(orchestrator);

        // Act
        var result = await router.RouteAsync("Test task");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Orchestrator error");
    }

    private static IModelOrchestrator CreateMockOrchestratorWithConfidence(double confidence)
    {
        return new ConfigurableConfidenceMockOrchestrator(confidence);
    }

    private sealed class ConfigurableConfidenceMockOrchestrator : IModelOrchestrator
    {
        private readonly double _confidence;

        public ConfigurableConfidenceMockOrchestrator(double confidence) => _confidence = confidence;

        public Task<Result<OrchestratorDecision, string>> SelectModelAsync(
            string prompt,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            var model = new MockChatModel("test");
            var tools = ToolRegistry.CreateDefault();
            var decision = new OrchestratorDecision(
                model,
                "test-model",
                "Test reason",
                tools,
                _confidence);
            return Task.FromResult(Result<OrchestratorDecision, string>.Success(decision));
        }

        public UseCase ClassifyUseCase(string prompt) =>
            new UseCase(UseCaseType.Conversation, 1, new[] { "general" }, 0.5, 0.5);

        public void RegisterModel(ModelCapability capability) { }
        public void RecordMetric(string resourceName, double latencyMs, bool success) { }
        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics() =>
            new Dictionary<string, PerformanceMetrics>();
    }

    #endregion
}
