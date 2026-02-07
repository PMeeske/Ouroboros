// <copyright file="SymbolicFallbackTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Tests.ConsolidatedMind;

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Providers;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Tests for symbolic fallback functionality in ConsolidatedMind.
/// </summary>
[Trait("Category", "Unit")]
public class SymbolicFallbackTests
{
    /// <summary>
    /// Verifies that SymbolicReasoner is registered correctly and available for fallback.
    /// </summary>
    [Fact]
    public void SymbolicReasoner_Registration_IsSuccessful()
    {
        // Arrange
        var mind = new ConsolidatedMind(new MindConfig(FallbackOnError: true));
        var mockEngine = new MockMeTTaEngine();
        
        // Act
        mind.WithSymbolicFallback(mockEngine);

        // Assert
        mind.Specialists.Should().ContainKey(SpecializedRole.SymbolicReasoner);
        var symbolicSpecialist = mind.Specialists[SpecializedRole.SymbolicReasoner];
        symbolicSpecialist.ModelName.Should().Contain("Symbolic");
    }

    /// <summary>
    /// Verifies that ProcessAsync falls back to SymbolicReasoner when all LLM models fail.
    /// Uses a deterministic setup by registering a failing Mathematical specialist that matches the likely route.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenPrimaryFails_FallsBackToSymbolic()
    {
        // Arrange
        var mind = new ConsolidatedMind(new MindConfig(FallbackOnError: true));
        
        // Register failing specialists for deterministic routing
        // TaskAnalyzer will likely route "What is 2+2?" to Mathematical role
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Mathematical,
            new FailingChatModel(),
            "failing-mathematical",
            new[] { "math" }));

        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.DeepReasoning,
            new FailingChatModel(),
            "failing-deep",
            new[] { "reasoning" }));

        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.CodeExpert,
            new FailingChatModel(),
            "failing-code",
            new[] { "code" }));

        // Register symbolic fallback
        var mockEngine = new MockMeTTaEngine();
        mind.WithSymbolicFallback(mockEngine);

        // Act
        var response = await mind.ProcessAsync("What is 2+2?");

        // Assert
        response.Should().NotBeNull();
        response.Response.Should().Contain("[Symbolic Reasoning");
        response.UsedRoles.Should().Contain(SpecializedRole.SymbolicReasoner);
    }

    /// <summary>
    /// Verifies that SymbolicReasonerAdapter produces valid output.
    /// </summary>
    [Fact]
    public async Task SymbolicReasonerAdapter_GenerateText_ReturnsStructuredResponse()
    {
        // Arrange
        var mockEngine = new MockMeTTaEngine();
        var adapter = new SymbolicReasonerAdapter(mockEngine);

        // Act
        var response = await adapter.GenerateTextAsync("Test query");

        // Assert
        response.Should().NotBeNullOrEmpty();
        response.Should().Contain("[Symbolic Reasoning");
    }

    /// <summary>
    /// Verifies that SymbolicReasonerAdapter never throws exceptions.
    /// </summary>
    [Fact]
    public async Task SymbolicReasonerAdapter_NeverThrows()
    {
        // Arrange
        var failingEngine = new FailingMeTTaEngine();
        var adapter = new SymbolicReasonerAdapter(failingEngine);

        // Act
        var response = await adapter.GenerateTextAsync("This will cause an error");

        // Assert
        response.Should().NotBeNullOrEmpty();
        response.Should().Contain("Limited Mode");
    }

    /// <summary>
    /// Verifies that WithSymbolicFallback registers the specialist correctly.
    /// </summary>
    [Fact]
    public void WithSymbolicFallback_RegistersSpecialist()
    {
        // Arrange
        var mind = new ConsolidatedMind();
        var mockEngine = new MockMeTTaEngine();

        // Act
        mind.WithSymbolicFallback(mockEngine);

        // Assert
        mind.Specialists.Should().ContainKey(SpecializedRole.SymbolicReasoner);
        var specialist = mind.Specialists[SpecializedRole.SymbolicReasoner];
        specialist.Role.Should().Be(SpecializedRole.SymbolicReasoner);
        specialist.ModelName.Should().Contain("Symbolic");
    }

    /// <summary>
    /// Verifies that response includes SymbolicReasoner in UsedRoles when it's used.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WithSymbolicFallback_IncludesRoleInResponse()
    {
        // Arrange
        var mind = new ConsolidatedMind(new MindConfig(FallbackOnError: true));
        
        // Register a failing primary specialist
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse,
            new FailingChatModel(),
            "failing-model",
            new[] { "general" }));

        var mockEngine = new MockMeTTaEngine();
        mind.WithSymbolicFallback(mockEngine);

        // Act
        var response = await mind.ProcessAsync("Test query");

        // Assert
        response.UsedRoles.Should().Contain(SpecializedRole.SymbolicReasoner);
        response.UsedRoles.Length.Should().BeGreaterThan(1); // Should include failed role + symbolic
    }

    /// <summary>
    /// Verifies that without symbolic fallback, errors propagate when all specialists fail.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WithNoFallback_ThrowsWhenAllFail()
    {
        // Arrange
        var mind = new ConsolidatedMind(new MindConfig(FallbackOnError: true));
        
        // Register only a failing specialist (no symbolic fallback)
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.QuickResponse,
            new FailingChatModel(),
            "failing-model",
            new[] { "general" }));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mind.ProcessAsync("Test query"));
    }

    /// <summary>
    /// Verifies that SymbolicReasonerAdapter with bridge uses SymbolicOnly mode.
    /// </summary>
    [Fact]
    public async Task SymbolicReasonerAdapter_WithBridge_UsesSymbolicOnlyMode()
    {
        // Arrange
        var mockBridge = new MockNeuralSymbolicBridge();
        var adapter = new SymbolicReasonerAdapter(mockBridge);

        // Act
        var response = await adapter.GenerateTextAsync("Test query");

        // Assert
        mockBridge.LastReasoningMode.Should().Be(ReasoningMode.SymbolicOnly);
        response.Should().Contain("[Symbolic Reasoning");
    }

    /// <summary>
    /// Verifies that confidence is lower when using symbolic fallback.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_LowConfidence_WhenUsingSymbolicFallback()
    {
        // Arrange
        var mind = new ConsolidatedMind(new MindConfig(FallbackOnError: true));
        
        // Register a failing primary - use Analyst to avoid circular fallback
        mind.RegisterSpecialist(new SpecializedModel(
            SpecializedRole.Analyst,
            new FailingChatModel(),
            "failing-analyst",
            new[] { "analysis" }));

        var mockEngine = new MockMeTTaEngine();
        mind.WithSymbolicFallback(mockEngine);

        // Act
        var response = await mind.ProcessAsync("Test query");

        // Assert
        response.Confidence.Should().BeLessThan(1.0); // Fallback has reduced confidence
        response.UsedRoles.Should().Contain(SpecializedRole.SymbolicReasoner);
    }

    // ============================================================================
    // Mock implementations for testing
    // ============================================================================

    private class MockMeTTaEngine : IMeTTaEngine
    {
        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success("Mock symbolic reasoning result"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success("Rule applied"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            return Task.FromResult(Result<bool, string>.Success(true));
        }

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void Dispose() { }
    }

    private class FailingMeTTaEngine : IMeTTaEngine
    {
        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Failure("Simulated engine failure"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit, string>.Failure("Simulated failure"));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Failure("Simulated failure"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            return Task.FromResult(Result<bool, string>.Failure("Simulated failure"));
        }

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit, string>.Failure("Simulated failure"));
        }

        public void Dispose() { }
    }

    private class FailingChatModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Simulated LLM failure");
        }
    }

    private class MockNeuralSymbolicBridge : INeuralSymbolicBridge
    {
        public ReasoningMode? LastReasoningMode { get; private set; }

        public Task<Result<List<SymbolicRule>, string>> ExtractRulesFromSkillAsync(
            Skill skill,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<List<SymbolicRule>, string>.Success(new List<SymbolicRule>()));
        }

        public Task<Result<MeTTaExpression, string>> NaturalLanguageToMeTTaAsync(
            string naturalLanguage,
            CancellationToken ct = default)
        {
            var expr = new MeTTaExpression("(mock)", ExpressionType.Atom, new List<string>(), new List<string>(), new Dictionary<string, object>());
            return Task.FromResult(Result<MeTTaExpression, string>.Success(expr));
        }

        public Task<Result<string, string>> MeTTaToNaturalLanguageAsync(
            MeTTaExpression expression,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success("Mock explanation"));
        }

        public Task<Result<ReasoningResult, string>> HybridReasonAsync(
            string query,
            ReasoningMode mode = ReasoningMode.SymbolicFirst,
            CancellationToken ct = default)
        {
            LastReasoningMode = mode;
            var result = new ReasoningResult(
                query,
                "Mock symbolic reasoning result",
                mode,
                new List<ReasoningStep>(),
                0.8,
                true,
                false,
                TimeSpan.FromMilliseconds(100));
            return Task.FromResult(Result<ReasoningResult, string>.Success(result));
        }

        public Task<Result<ConsistencyReport, string>> CheckConsistencyAsync(
            Ouroboros.Agent.MetaAI.Hypothesis hypothesis,
            IReadOnlyList<SymbolicRule> knowledgeBase,
            CancellationToken ct = default)
        {
            var report = new ConsistencyReport(true, new List<LogicalConflict>(), new List<string>(), new List<string>(), 1.0);
            return Task.FromResult(Result<ConsistencyReport, string>.Success(report));
        }

        public Task<Result<GroundedConcept, string>> GroundConceptAsync(
            string conceptDescription,
            float[] embedding,
            CancellationToken ct = default)
        {
            var concept = new GroundedConcept(conceptDescription, "mock-type", new List<string>(), new List<string>(), embedding, 1.0);
            return Task.FromResult(Result<GroundedConcept, string>.Success(concept));
        }
    }
}
