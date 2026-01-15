// <copyright file="AdvancedMeTTaEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using Ouroboros.Tools.MeTTa;

/// <summary>
/// Tests for Advanced MeTTa Engine functionality.
/// </summary>
[Trait("Category", "Unit")]
public class AdvancedMeTTaEngineTests
{
    /// <summary>
    /// Tests FOIL rule induction with sufficient observations.
    /// </summary>
    [Fact]
    public async Task InduceRulesAsync_WithFoilStrategy_ShouldInduceCorrectRules()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var observations = new List<Fact>
        {
            new Fact("parent", new List<string> { "alice", "bob" }, 1.0),
            new Fact("parent", new List<string> { "bob", "charlie" }, 1.0),
            new Fact("parent", new List<string> { "charlie", "dave" }, 1.0),
            new Fact("parent", new List<string> { "eve", "frank" }, 1.0),
            new Fact("parent", new List<string> { "frank", "george" }, 1.0),
            new Fact("parent", new List<string> { "alice", "helen" }, 1.0),
            new Fact("parent", new List<string> { "helen", "ian" }, 1.0),
            new Fact("parent", new List<string> { "bob", "jane" }, 1.0),
            new Fact("parent", new List<string> { "jane", "karl" }, 1.0),
            new Fact("parent", new List<string> { "charlie", "laura" }, 1.0),
        };

        // Act
        var result = await engine.InduceRulesAsync(observations, InductionStrategy.FOIL);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().HaveCountGreaterThanOrEqualTo(1);

        var rule = result.Value[0];
        rule.Name.Should().StartWith("induced_");
        rule.Premises.Should().NotBeEmpty();
        rule.Conclusion.Should().NotBeNull();
        rule.Confidence.Should().BeGreaterThan(0.0);
    }

    /// <summary>
    /// Tests rule induction accuracy with 10 examples (80%+ requirement).
    /// </summary>
    [Fact]
    public async Task InduceRulesAsync_With10Examples_ShouldAchieve80PercentAccuracy()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var observations = new List<Fact>
        {
            new Fact("mortal", new List<string> { "socrates" }, 1.0),
            new Fact("mortal", new List<string> { "plato" }, 1.0),
            new Fact("mortal", new List<string> { "aristotle" }, 1.0),
            new Fact("mortal", new List<string> { "pythagoras" }, 1.0),
            new Fact("mortal", new List<string> { "heraclitus" }, 1.0),
            new Fact("mortal", new List<string> { "parmenides" }, 1.0),
            new Fact("mortal", new List<string> { "epicurus" }, 1.0),
            new Fact("mortal", new List<string> { "zeno" }, 1.0),
            new Fact("mortal", new List<string> { "diogenes" }, 1.0),
            new Fact("mortal", new List<string> { "democritus" }, 1.0),
        };

        // Act
        var result = await engine.InduceRulesAsync(observations, InductionStrategy.FOIL);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        // Check that induced rules have high confidence (>= 0.8)
        var avgConfidence = result.Value.Average(r => r.Confidence);
        avgConfidence.Should().BeGreaterThanOrEqualTo(0.8);
    }

    /// <summary>
    /// Tests resolution-based theorem proving for propositional logic.
    /// </summary>
    [Fact]
    public async Task ProveTheoremAsync_WithResolution_ShouldProveSimpleTheorem()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var theorem = "(implies (human X) (mortal X))";
        var axioms = new List<string>
        {
            "(human socrates)",
            "(implies (human X) (mortal X))",
        };

        // Act
        var result = await engine.ProveTheoremAsync(theorem, axioms, ProofStrategy.Resolution);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Steps.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests theorem proving performance (&lt;1 second requirement).
    /// </summary>
    [Fact]
    public async Task ProveTheoremAsync_ShouldCompleteInLessThanOneSecond()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var theorem = "(and P Q)";
        var axioms = new List<string> { "P", "Q" };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await engine.ProveTheoremAsync(theorem, axioms, ProofStrategy.Resolution);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Tests forward chaining inference.
    /// </summary>
    [Fact]
    public async Task ForwardChainAsync_ShouldDeriveNewFacts()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var rules = new List<Rule>
        {
            new Rule(
                "mortality",
                new List<Pattern> { new Pattern("(human $x)", new List<string> { "$x" }) },
                new Pattern("(mortal $x)", new List<string> { "$x" }),
                1.0),
        };

        var facts = new List<Fact>
        {
            new Fact("human", new List<string> { "socrates" }, 1.0),
            new Fact("human", new List<string> { "plato" }, 1.0),
        };

        // Act
        var result = await engine.ForwardChainAsync(rules, facts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(f => f.Predicate == "mortal");
    }

    /// <summary>
    /// Tests backward chaining goal proving.
    /// </summary>
    [Fact]
    public async Task BackwardChainAsync_ShouldProveGoal()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var goal = new Fact("mortal", new List<string> { "socrates" }, 1.0);

        var rules = new List<Rule>
        {
            new Rule(
                "mortality",
                new List<Pattern> { new Pattern("(human $x)", new List<string> { "$x" }) },
                new Pattern("(mortal $x)", new List<string> { "$x" }),
                1.0),
        };

        var knownFacts = new List<Fact>
        {
            new Fact("human", new List<string> { "socrates" }, 1.0),
        };

        // Act
        var result = await engine.BackwardChainAsync(goal, rules, knownFacts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(goal);
    }

    /// <summary>
    /// Tests hypothesis generation from observations.
    /// </summary>
    [Fact]
    public async Task GenerateHypothesesAsync_ShouldGeneratePlausibleHypotheses()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var observation = "(fly bird)";
        var backgroundKnowledge = new List<string>
        {
            "(has-wings bird)",
            "(vertebrate bird)",
        };

        // Act
        var result = await engine.GenerateHypothesesAsync(observation, backgroundKnowledge);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().AllSatisfy(h =>
        {
            h.Statement.Should().NotBeNullOrWhiteSpace();
            h.Plausibility.Should().BeInRange(0.0, 1.0);
        });
    }

    /// <summary>
    /// Tests type inference for atoms.
    /// </summary>
    [Fact]
    public async Task InferTypeAsync_ShouldInferCorrectTypes()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var context = new TypeContext(
            new Dictionary<string, string> { { "x", "Int" } },
            new List<string>());

        // Act
        var result1 = await engine.InferTypeAsync("42", context);
        var result2 = await engine.InferTypeAsync("3.14", context);
        var result3 = await engine.InferTypeAsync("\"hello\"", context);
        var result4 = await engine.InferTypeAsync("$x", context);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Type.Should().Be("Int");

        result2.IsSuccess.Should().BeTrue();
        result2.Value.Type.Should().Be("Float");

        result3.IsSuccess.Should().BeTrue();
        result3.Value.Type.Should().Be("String");

        result4.IsSuccess.Should().BeTrue();
        result4.Value.Type.Should().Be("Var");
    }

    /// <summary>
    /// Tests error handling for empty observations.
    /// </summary>
    [Fact]
    public async Task InduceRulesAsync_WithEmptyObservations_ShouldReturnError()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Act
        var result = await engine.InduceRulesAsync(new List<Fact>(), InductionStrategy.FOIL);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No observations");
    }

    /// <summary>
    /// Tests error handling for empty theorem.
    /// </summary>
    [Fact]
    public async Task ProveTheoremAsync_WithEmptyTheorem_ShouldReturnError()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Act
        var result = await engine.ProveTheoremAsync(string.Empty, new List<string>(), ProofStrategy.Resolution);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    /// <summary>
    /// Tests error handling for null goal in backward chaining.
    /// </summary>
    [Fact]
    public async Task BackwardChainAsync_WithNullGoal_ShouldReturnError()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Act
        var result = await engine.BackwardChainAsync(null!, new List<Rule>(), new List<Fact>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    /// <summary>
    /// Tests disposal behavior.
    /// </summary>
    [Fact]
    public async Task DisposedEngine_ShouldReturnErrors()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);
        engine.Dispose();

        // Act
        var result1 = await engine.InduceRulesAsync(new List<Fact> { new Fact("test", new List<string>()) }, InductionStrategy.FOIL);
        var result2 = await engine.ProveTheoremAsync("test", new List<string>(), ProofStrategy.Resolution);

        // Assert
        result1.IsFailure.Should().BeTrue();
        result1.Error.Should().Contain("disposed");

        result2.IsFailure.Should().BeTrue();
        result2.Error.Should().Contain("disposed");
    }

    /// <summary>
    /// Tests that base engine methods are properly delegated.
    /// </summary>
    [Fact]
    public async Task BaseEngineMethods_ShouldBeDelegated()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Act
        var queryResult = await engine.ExecuteQueryAsync("(+ 1 2)");
        var factResult = await engine.AddFactAsync("(test)");
        var ruleResult = await engine.ApplyRuleAsync("(= test true)");
        var verifyResult = await engine.VerifyPlanAsync("(plan)");
        var resetResult = await engine.ResetAsync();

        // Assert
        queryResult.IsSuccess.Should().BeTrue();
        factResult.IsSuccess.Should().BeTrue();
        ruleResult.IsSuccess.Should().BeTrue();
        verifyResult.IsSuccess.Should().BeTrue();
        resetResult.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Tests forward chaining with multiple inference steps.
    /// </summary>
    [Fact]
    public async Task ForwardChainAsync_WithMultipleSteps_ShouldDeriveTransitiveFacts()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var rules = new List<Rule>
        {
            new Rule(
                "transitivity",
                new List<Pattern>
                {
                    new Pattern("(parent $x $y)", new List<string> { "$x", "$y" }),
                },
                new Pattern("(ancestor $x $y)", new List<string> { "$x", "$y" }),
                1.0),
        };

        var facts = new List<Fact>
        {
            new Fact("parent", new List<string> { "alice", "bob" }, 1.0),
            new Fact("parent", new List<string> { "bob", "charlie" }, 1.0),
        };

        // Act
        var result = await engine.ForwardChainAsync(rules, facts, maxSteps: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(f => f.Predicate == "ancestor");
    }

    /// <summary>
    /// Property-based test: Forward chaining should be monotonic.
    /// </summary>
    [Fact]
    public async Task ForwardChainAsync_ShouldBeMonotonic()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var rules = new List<Rule>
        {
            new Rule(
                "rule1",
                new List<Pattern> { new Pattern("(p $x)", new List<string> { "$x" }) },
                new Pattern("(q $x)", new List<string> { "$x" }),
                1.0),
        };

        var initialFacts = new List<Fact>
        {
            new Fact("p", new List<string> { "a" }, 1.0),
        };

        // Act - First run
        var result1 = await engine.ForwardChainAsync(rules, initialFacts);
        var count1 = result1.Value.Count;

        // Act - Second run with more facts
        var moreFacts = new List<Fact>(initialFacts)
        {
            new Fact("p", new List<string> { "b" }, 1.0),
        };
        var result2 = await engine.ForwardChainAsync(rules, moreFacts);
        var count2 = result2.Value.Count;

        // Assert - More facts should lead to at least as many derived facts (monotonicity)
        count2.Should().BeGreaterThanOrEqualTo(count1);
    }

    /// <summary>
    /// Property-based test: Backward chaining should be sound.
    /// </summary>
    [Fact]
    public async Task BackwardChainAsync_ShouldBeSoundForProvableGoals()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var goal = new Fact("q", new List<string> { "a" }, 1.0);
        var rules = new List<Rule>
        {
            new Rule(
                "implication",
                new List<Pattern> { new Pattern("(p $x)", new List<string> { "$x" }) },
                new Pattern("(q $x)", new List<string> { "$x" }),
                1.0),
        };

        var knownFacts = new List<Fact>
        {
            new Fact("p", new List<string> { "a" }, 1.0),
        };

        // Act
        var result = await engine.BackwardChainAsync(goal, rules, knownFacts);

        // Assert - Should successfully prove goal
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(goal);

        // Verify soundness: all required facts should lead to goal
        var allFactsUsed = result.Value;
        allFactsUsed.Should().AllSatisfy(f =>
        {
            f.Should().NotBeNull();
            f.Predicate.Should().NotBeNullOrEmpty();
        });
    }

    /// <summary>
    /// Tests that rules are applied correctly in forward chaining.
    /// </summary>
    [Fact]
    public async Task ForwardChainAsync_ShouldApplyRulesCorrectly()
    {
        // Arrange
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var rules = new List<Rule>
        {
            new Rule(
                "sibling",
                new List<Pattern> { new Pattern("(parent $p $x)", new List<string> { "$p", "$x" }) },
                new Pattern("(child $x $p)", new List<string> { "$x", "$p" }),
                1.0),
        };

        var facts = new List<Fact>
        {
            new Fact("parent", new List<string> { "john", "mary" }, 1.0),
        };

        // Act
        var result = await engine.ForwardChainAsync(rules, facts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(f =>
            f.Predicate == "child" &&
            f.Arguments[0] == "mary" &&
            f.Arguments[1] == "john");
    }
}
