// <copyright file="NeuralSymbolicBridgeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.NeuralSymbolic;

using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Providers;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Tests for NeuralSymbolicBridge implementation.
/// </summary>
[Trait("Category", "Unit")]
public class NeuralSymbolicBridgeTests
{
    private readonly MockLLM _llm;
    private readonly MockKnowledgeBase _knowledgeBase;
    private readonly NeuralSymbolicBridge _bridge;

    public NeuralSymbolicBridgeTests()
    {
        _llm = new MockLLM();
        _knowledgeBase = new MockKnowledgeBase();
        _bridge = new NeuralSymbolicBridge(_llm, _knowledgeBase);
    }

    [Fact]
    public async Task NaturalLanguageToMeTTaAsync_WithValidInput_ReturnsExpression()
    {
        // Arrange
        var naturalLanguage = "All humans are mortal";

        // Act
        var result = await _bridge.NaturalLanguageToMeTTaAsync(naturalLanguage);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.RawExpression.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NaturalLanguageToMeTTaAsync_WithEmptyInput_ReturnsFailure()
    {
        // Act
        var result = await _bridge.NaturalLanguageToMeTTaAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task MeTTaToNaturalLanguageAsync_WithValidExpression_ReturnsExplanation()
    {
        // Arrange
        var expression = new MeTTaExpression(
            "(implies (human $x) (mortal $x))",
            ExpressionType.Rule,
            new List<string> { "implies", "human", "mortal" },
            new List<string> { "$x" },
            new Dictionary<string, object>());

        // Act
        var result = await _bridge.MeTTaToNaturalLanguageAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MeTTaToNaturalLanguageAsync_WithNullExpression_ReturnsFailure()
    {
        // Act
        var result = await _bridge.MeTTaToNaturalLanguageAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task HybridReasonAsync_SymbolicFirst_WithValidQuery_ReturnsResult()
    {
        // Arrange
        var query = "Is Socrates mortal?";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.SymbolicFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Query.Should().Be(query);
        result.Value.ModeUsed.Should().Be(ReasoningMode.SymbolicFirst);
    }

    [Fact]
    public async Task HybridReasonAsync_NeuralFirst_WithValidQuery_ReturnsResult()
    {
        // Arrange
        var query = "What is the capital of France?";

        // Act
        var result = await _bridge.HybridReasonAsync(query, ReasoningMode.NeuralFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ModeUsed.Should().Be(ReasoningMode.NeuralFirst);
    }

    [Fact]
    public async Task HybridReasonAsync_WithEmptyQuery_ReturnsFailure()
    {
        // Act
        var result = await _bridge.HybridReasonAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task CheckConsistencyAsync_WithConsistentHypothesis_ReturnsConsistentReport()
    {
        // Arrange
        var hypothesis = CreateTestHypothesis("All cats are animals");
        var rules = new List<SymbolicRule>
        {
            CreateTestRule("animal-rule", "(implies (cat $x) (animal $x))")
        };

        // Act
        var result = await _bridge.CheckConsistencyAsync(hypothesis, rules);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckConsistencyAsync_WithNullHypothesis_ReturnsFailure()
    {
        // Arrange
        var rules = new List<SymbolicRule>();

        // Act
        var result = await _bridge.CheckConsistencyAsync(null!, rules);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task GroundConceptAsync_WithValidInput_ReturnsGroundedConcept()
    {
        // Arrange
        var description = "A four-legged domestic animal";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var result = await _bridge.GroundConceptAsync(description, embedding);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(description);
        result.Value.Embedding.Should().BeEquivalentTo(embedding);
    }

    [Fact]
    public async Task GroundConceptAsync_WithEmptyDescription_ReturnsFailure()
    {
        // Arrange
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        // Act
        var result = await _bridge.GroundConceptAsync("", embedding);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task GroundConceptAsync_WithNullEmbedding_ReturnsFailure()
    {
        // Act
        var result = await _bridge.GroundConceptAsync("test", null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task ExtractRulesFromSkillAsync_WithValidSkill_ReturnsRules()
    {
        // Arrange
        var skill = CreateTestSkill();

        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractRulesFromSkillAsync_WithNullSkill_ReturnsFailure()
    {
        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    private static Ouroboros.Agent.MetaAI.Hypothesis CreateTestHypothesis(string statement)
    {
        return new Ouroboros.Agent.MetaAI.Hypothesis(
            Guid.NewGuid(),
            statement,
            "test-domain",
            0.8,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);
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

    private static Skill CreateTestSkill()
    {
        return new Skill(
            "test-skill",
            "A test skill for unit testing",
            new List<string>(),
            new List<PlanStep>
            {
                new PlanStep("Do something", new Dictionary<string, object>(), "Complete action", 1.0)
            },
            0.9,
            10,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Mock LLM for testing.
    /// </summary>
    private class MockLLM : IChatCompletionModel
    {
        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.CompletedTask;

            if (prompt.Contains("Convert the following natural language"))
            {
                return "(implies (human $x) (mortal $x))";
            }

            if (prompt.Contains("Explain the following MeTTa"))
            {
                return "If something is human, then it is mortal";
            }

            if (prompt.Contains("Check if this hypothesis"))
            {
                return "CONSISTENT: Yes\nCONFLICTS: None\nMISSING: None";
            }

            if (prompt.Contains("Given the concept"))
            {
                return "Type: Animal\nProperties: furry, four-legged, domestic\nRelations: is-a mammal, lives-with human";
            }

            if (prompt.Contains("Extract symbolic rules"))
            {
                return @"RULE: test-rule
METTA: (rule-pattern)
DESCRIPTION: A test rule
PRECONDITIONS: none
EFFECTS: test effect
---";
            }

            return "Mock LLM response";
        }
    }

    /// <summary>
    /// Mock knowledge base for testing.
    /// </summary>
    private class MockKnowledgeBase : ISymbolicKnowledgeBase
    {
        public int RuleCount => 0;

        public async Task<Result<Ouroboros.Tools.MeTTa.Unit, string>> AddRuleAsync(SymbolicRule rule, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return Result<Ouroboros.Tools.MeTTa.Unit, string>.Success(Ouroboros.Tools.MeTTa.Unit.Value);
        }

        public async Task<Result<List<SymbolicRule>, string>> QueryRulesAsync(string pattern, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return Result<List<SymbolicRule>, string>.Success(new List<SymbolicRule>());
        }

        public async Task<Result<string, string>> ExecuteMeTTaQueryAsync(string query, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return Result<string, string>.Success("Query result");
        }

        public async Task<Result<List<string>, string>> InferAsync(string fact, int maxDepth = 5, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            return Result<List<string>, string>.Success(new List<string>());
        }
    }
}
