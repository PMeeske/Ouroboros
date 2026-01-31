// <copyright file="SymbolicKnowledgeBaseTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.NeuralSymbolic;

using FluentAssertions;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Tests for SymbolicKnowledgeBase implementation.
/// </summary>
[Trait("Category", "Unit")]
public class SymbolicKnowledgeBaseTests
{
    private readonly MockMeTTaEngine _mettaEngine;
    private readonly SymbolicKnowledgeBase _knowledgeBase;

    public SymbolicKnowledgeBaseTests()
    {
        _mettaEngine = new MockMeTTaEngine();
        _knowledgeBase = new SymbolicKnowledgeBase(_mettaEngine);
    }

    [Fact]
    public async Task AddRuleAsync_WithValidRule_ReturnsSuccess()
    {
        // Arrange
        var rule = CreateTestRule("test-rule", "(implies (human $x) (mortal $x))");

        // Act
        var result = await _knowledgeBase.AddRuleAsync(rule);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _knowledgeBase.RuleCount.Should().Be(1);
    }

    [Fact]
    public async Task AddRuleAsync_WithNullRule_ReturnsFailure()
    {
        // Act
        var result = await _knowledgeBase.AddRuleAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task QueryRulesAsync_WithMatchingPattern_ReturnsMatchingRules()
    {
        // Arrange
        var rule1 = CreateTestRule("mortal-rule", "(implies (human $x) (mortal $x))");
        var rule2 = CreateTestRule("animal-rule", "(implies (cat $x) (animal $x))");
        await _knowledgeBase.AddRuleAsync(rule1);
        await _knowledgeBase.AddRuleAsync(rule2);

        // Act
        var result = await _knowledgeBase.QueryRulesAsync("mortal");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("mortal-rule");
    }

    [Fact]
    public async Task QueryRulesAsync_WithEmptyPattern_ReturnsFailure()
    {
        // Act
        var result = await _knowledgeBase.QueryRulesAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task ExecuteMeTTaQueryAsync_WithValidQuery_ReturnsResult()
    {
        // Arrange
        var query = "(match &self (human $x) $x)";

        // Act
        var result = await _knowledgeBase.ExecuteMeTTaQueryAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InferAsync_WithValidFact_ReturnsInferences()
    {
        // Arrange
        var rule = CreateTestRule("test-rule", "(implies (human $x) (mortal $x))");
        await _knowledgeBase.AddRuleAsync(rule);
        var fact = "(human socrates)";

        // Act
        var result = await _knowledgeBase.InferAsync(fact);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task InferAsync_WithInvalidMaxDepth_ReturnsFailure()
    {
        // Arrange
        var fact = "(human socrates)";

        // Act
        var result = await _knowledgeBase.InferAsync(fact, maxDepth: 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be positive");
    }

    [Fact]
    public void RuleCount_InitiallyZero()
    {
        // Assert
        _knowledgeBase.RuleCount.Should().Be(0);
    }

    [Fact]
    public async Task RuleCount_IncreasesWithAddedRules()
    {
        // Arrange
        var rule1 = CreateTestRule("rule1", "(test1)");
        var rule2 = CreateTestRule("rule2", "(test2)");

        // Act
        await _knowledgeBase.AddRuleAsync(rule1);
        await _knowledgeBase.AddRuleAsync(rule2);

        // Assert
        _knowledgeBase.RuleCount.Should().Be(2);
    }

    private static SymbolicRule CreateTestRule(string name, string metta)
    {
        return new SymbolicRule(
            name,
            metta,
            $"Test rule: {name}",
            new List<string>(),
            new List<string>(),
            1.0,
            RuleSource.UserProvided);
    }

    /// <summary>
    /// Mock MeTTa engine for testing.
    /// </summary>
    private class MockMeTTaEngine : IMeTTaEngine
    {
        private readonly List<string> _facts = new();

        public async Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return Result<string, string>.Success($"Result for: {query}");
        }

        public async Task<Result<Ouroboros.Tools.MeTTa.Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            _facts.Add(fact);
            return Result<Ouroboros.Tools.MeTTa.Unit, string>.Success(Ouroboros.Tools.MeTTa.Unit.Value);
        }

        public async Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return Result<string, string>.Success("Rule applied");
        }

        public async Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return Result<bool, string>.Success(true);
        }

        public async Task<Result<Ouroboros.Tools.MeTTa.Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            await Task.CompletedTask;
            _facts.Clear();
            return Result<Ouroboros.Tools.MeTTa.Unit, string>.Success(Ouroboros.Tools.MeTTa.Unit.Value);
        }

        public void Dispose()
        {
        }
    }
}
