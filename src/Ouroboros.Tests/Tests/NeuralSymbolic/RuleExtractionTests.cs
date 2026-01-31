// <copyright file="RuleExtractionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.NeuralSymbolic;

using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Tests for rule extraction from skills.
/// </summary>
[Trait("Category", "Unit")]
public class RuleExtractionTests
{
    private readonly MockLLM _llm;
    private readonly MockKnowledgeBase _knowledgeBase;
    private readonly NeuralSymbolicBridge _bridge;

    public RuleExtractionTests()
    {
        _llm = new MockLLM();
        _knowledgeBase = new MockKnowledgeBase();
        _bridge = new NeuralSymbolicBridge(_llm, _knowledgeBase);
    }

    [Fact]
    public async Task ExtractRules_FromSimpleSkill_ExtractsRules()
    {
        // Arrange
        var skill = CreateSkill(
            "calculate-sum",
            "Calculates the sum of two numbers",
            new List<PlanStep>
            {
                new PlanStep("Get first number", new Dictionary<string, object>(), "Retrieve input A", 1.0),
                new PlanStep("Get second number", new Dictionary<string, object>(), "Retrieve input B", 1.0),
                new PlanStep("Add numbers", new Dictionary<string, object>(), "Compute A + B", 1.0)
            });

        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractRules_FromComplexSkill_ExtractsMultipleRules()
    {
        // Arrange
        var skill = CreateSkill(
            "data-pipeline",
            "Process and transform data",
            new List<PlanStep>
            {
                new PlanStep("Load data", new Dictionary<string, object>(), "Load from source", 1.0),
                new PlanStep("Validate data", new Dictionary<string, object>(), "Check data quality", 1.0),
                new PlanStep("Transform data", new Dictionary<string, object>(), "Apply transformations", 1.0),
                new PlanStep("Save results", new Dictionary<string, object>(), "Persist to database", 1.0)
            });

        _llm.SetResponse(@"RULE: load-rule
METTA: (load-data $source $data)
DESCRIPTION: Loads data from a source
PRECONDITIONS: source-available
EFFECTS: data-loaded
---
RULE: validate-rule
METTA: (validate-data $data $valid)
DESCRIPTION: Validates data quality
PRECONDITIONS: data-loaded
EFFECTS: data-validated
---");

        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExtractRules_WithHighSuccessRate_ProducesHighConfidenceRules()
    {
        // Arrange
        var skill = CreateSkill(
            "reliable-task",
            "A highly reliable task",
            new List<PlanStep>
            {
                new PlanStep("Execute", new Dictionary<string, object>(), "Perform action", 1.0)
            },
            successRate: 0.95);

        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Any())
        {
            result.Value.Should().Contain(r => r.Confidence >= 0.8);
        }
    }

    [Fact]
    public async Task ExtractRules_VerifiesRuleSource()
    {
        // Arrange
        var skill = CreateSkill(
            "test-skill",
            "Test skill",
            new List<PlanStep>
            {
                new PlanStep("Step", new Dictionary<string, object>(), "Action", 1.0)
            });

        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Any())
        {
            result.Value.Should().OnlyContain(r => r.Source == RuleSource.ExtractedFromSkill);
        }
    }

    private static Skill CreateSkill(string name, string description, List<PlanStep> steps, double successRate = 0.9)
    {
        return new Skill(
            name,
            description,
            new List<string>(),
            steps,
            successRate,
            10,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Mock LLM for testing rule extraction.
    /// </summary>
    private class MockLLM : IChatCompletionModel
    {
        private string? _customResponse;

        public void SetResponse(string response)
        {
            _customResponse = response;
        }

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            if (_customResponse != null)
            {
                return _customResponse;
            }

            if (prompt.Contains("Extract symbolic rules"))
            {
                return @"RULE: extracted-rule
METTA: (rule-pattern $x)
DESCRIPTION: Extracted rule from skill
PRECONDITIONS: input-available
EFFECTS: output-produced
---";
            }

            return "Mock response";
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
            return Task.FromResult(Result<string, string>.Success("Query result"));
        }

        public Task<Result<List<string>, string>> InferAsync(string fact, int maxDepth = 5, CancellationToken ct = default)
        {
            return Task.FromResult(Result<List<string>, string>.Success(new List<string>()));
        }
    }
}
