// <copyright file="HybridReasoningTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.NeuralSymbolic;

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Tests for hybrid reasoning modes.
/// </summary>
[Trait("Category", "Unit")]
public class HybridReasoningTests
{
    private readonly MockLLM _llm;
    private readonly MockKnowledgeBase _knowledgeBase;
    private readonly NeuralSymbolicBridge _bridge;

    public HybridReasoningTests()
    {
        _llm = new MockLLM();
        _knowledgeBase = new MockKnowledgeBase();
        _bridge = new NeuralSymbolicBridge(_llm, _knowledgeBase);
    }

    [Fact]
    public async Task HybridReason_SymbolicFirst_AttemptsSymbolicThenNeural()
    {
        // Arrange
        var query = "What is the answer?";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.SymbolicFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModeUsed.Should().Be(ReasoningMode.SymbolicFirst);
        result.Value.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HybridReason_NeuralFirst_AttemptsNeuralThenVerifies()
    {
        // Arrange
        var query = "Explain quantum computing";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.NeuralFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModeUsed.Should().Be(ReasoningMode.NeuralFirst);
        result.Value.NeuralSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HybridReason_Parallel_RunsBothModes()
    {
        // Arrange
        var query = "Test parallel reasoning";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.Parallel);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModeUsed.Should().Be(ReasoningMode.Parallel);
        result.Value.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task HybridReason_SymbolicOnly_UsesOnlySymbolic()
    {
        // Arrange
        var query = "Symbolic query";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.SymbolicOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModeUsed.Should().Be(ReasoningMode.SymbolicOnly);
    }

    [Fact]
    public async Task HybridReason_NeuralOnly_UsesOnlyNeural()
    {
        // Arrange
        var query = "Neural query";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.NeuralOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModeUsed.Should().Be(ReasoningMode.NeuralOnly);
        result.Value.NeuralSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HybridReason_TracksReasoningSteps()
    {
        // Arrange
        var query = "Complex query requiring steps";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.SymbolicFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().NotBeEmpty();
        result.Value.Steps.Should().Contain(s => s.StepNumber > 0);
    }

    [Fact]
    public async Task HybridReason_MeasuresDuration()
    {
        // Arrange
        var query = "Timed query";

        // Act
        var result = await _bridge.HybridReasonAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task HybridReason_ReturnsConfidenceScore()
    {
        // Arrange
        var query = "Confidence test";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.SymbolicFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Theory]
    [InlineData(ReasoningMode.SymbolicFirst)]
    [InlineData(ReasoningMode.NeuralFirst)]
    [InlineData(ReasoningMode.Parallel)]
    [InlineData(ReasoningMode.SymbolicOnly)]
    [InlineData(ReasoningMode.NeuralOnly)]
    public async Task HybridReason_AllModes_ProduceValidResults(ReasoningMode mode)
    {
        // Arrange
        var query = $"Test query for {mode}";

        // Act
        var result = await _bridge.HybridReasonAsync(query, mode);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Query.Should().Be(query);
        result.Value.ModeUsed.Should().Be(mode);
    }

    [Fact]
    public async Task HybridReason_CapturesAnswer()
    {
        // Arrange
        var query = "What is 2+2?";

        // Act
        var result = await _bridge.HybridReasonAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Answer.Should().NotBeEmpty();
    }

    /// <summary>
    /// Mock LLM for testing.
    /// </summary>
    private class MockLLM : IChatCompletionModel
    {
        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return "Mock reasoning response with detailed explanation";
        }
    }

    /// <summary>
    /// Mock knowledge base for testing.
    /// </summary>
    private class MockKnowledgeBase : ISymbolicKnowledgeBase
    {
        public int RuleCount => 0;

        public Task<Result<Ouroboros.Tools.MeTTa.Unit, string>> AddRuleAsync(SymbolicRule rule, CancellationToken ct = default)
        {
            return Task.FromResult(Result<Ouroboros.Tools.MeTTa.Unit, string>.Success(Ouroboros.Tools.MeTTa.Unit.Value));
        }

        public Task<Result<List<SymbolicRule>, string>> QueryRulesAsync(string pattern, CancellationToken ct = default)
        {
            return Task.FromResult(Result<List<SymbolicRule>, string>.Success(new List<SymbolicRule>()));
        }

        public Task<Result<string, string>> ExecuteMeTTaQueryAsync(string query, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success("Symbolic reasoning result"));
        }

        public Task<Result<List<string>, string>> InferAsync(string fact, int maxDepth = 5, CancellationToken ct = default)
        {
            return Task.FromResult(Result<List<string>, string>.Success(new List<string> { "inferred-fact-1" }));
        }
    }
}
