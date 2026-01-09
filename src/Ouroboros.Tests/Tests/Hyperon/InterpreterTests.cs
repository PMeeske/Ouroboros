// <copyright file="InterpreterTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Hyperon;

using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Hyperon.Parsing;
using Xunit;

/// <summary>
/// Tests for the Interpreter class.
/// </summary>
[Trait("Category", "Unit")]
public class InterpreterTests
{
    private readonly AtomSpace space;
    private readonly Interpreter interpreter;
    private readonly SExpressionParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpreterTests"/> class.
    /// </summary>
    public InterpreterTests()
    {
        space = new AtomSpace();
        interpreter = new Interpreter(space);
        parser = new SExpressionParser();
    }

    private Atom Parse(string input)
    {
        var result = parser.Parse(input);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Parse failed: {result.Error}");
        }

        return result.Value;
    }

    #region Basic Query Tests

    [Fact]
    public void Evaluate_DirectFactMatch_Succeeds()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act
        var results = interpreter.Evaluate(Parse("(Human Socrates)")).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void Evaluate_NoMatch_ReturnsEmpty()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act
        var results = interpreter.Evaluate(Parse("(Animal Dog)")).ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_PatternWithVariable_ReturnsMatches()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(Human Plato)"));
        space.Add(Parse("(Human Aristotle)"));

        // Act
        var results = interpreter.Evaluate(Parse("(Human $x)")).ToList();

        // Assert
        results.Should().HaveCount(3);
        results.Select(r => r.ToSExpr()).Should().Contain("(Human Socrates)");
        results.Select(r => r.ToSExpr()).Should().Contain("(Human Plato)");
        results.Select(r => r.ToSExpr()).Should().Contain("(Human Aristotle)");
    }

    #endregion

    #region Implies Rule Tests

    [Fact]
    public void Evaluate_ImpliesRule_DerivesConclusion()
    {
        // Arrange - Classic Socrates syllogism
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(implies (Human $x) (Mortal $x))"));

        // Act
        var results = interpreter.Evaluate(Parse("(Mortal Socrates)")).ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.ToSExpr() == "(Mortal Socrates)");
    }

    [Fact]
    public void Evaluate_ImpliesRule_MultipleMatches()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(Human Plato)"));
        space.Add(Parse("(implies (Human $x) (Mortal $x))"));

        // Act - Query for all mortals
        var query = Parse("(Mortal $y)");
        var results = interpreter.EvaluateWithBindings(query)
            .Select(r => r.Result.ToSExpr())
            .Distinct()
            .ToList();

        // Assert
        results.Should().Contain("(Mortal Socrates)");
        results.Should().Contain("(Mortal Plato)");
    }

    [Fact]
    public void Evaluate_ImpliesRule_NoMatchingCondition()
    {
        // Arrange
        space.Add(Parse("(implies (Human $x) (Mortal $x))"));

        // Act - Query for Mortal when no Humans exist
        var results = interpreter.Evaluate(Parse("(Mortal Socrates)")).ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_ChainedImpliesRules_NotDirectlySupported()
    {
        // Arrange - Chained rules (Human -> Mortal -> WillDie)
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(implies (Human $x) (Mortal $x))"));
        space.Add(Parse("(implies (Mortal $x) (WillDie $x))"));

        // Note: This implementation doesn't fully support chained inference.
        // (WillDie Socrates) would require (Mortal Socrates) to be in the space first.
        // This is a known limitation - for full forward chaining, derived facts
        // would need to be materialized or a more sophisticated inference engine used.
        var results = interpreter.Evaluate(Parse("(Mortal Socrates)")).ToList();

        // Assert - Direct derivation works
        results.Should().NotBeEmpty();
    }

    #endregion

    #region Grounded Operations Tests

    [Fact]
    public void Evaluate_EqualOperation_SameValues()
    {
        // Act
        var results = interpreter.Evaluate(
            Atom.Expr(Atom.Sym("equal"), Atom.Sym("a"), Atom.Sym("a")))
            .ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(Atom.Sym("True"));
    }

    [Fact]
    public void Evaluate_EqualOperation_DifferentValues()
    {
        // Act
        var results = interpreter.Evaluate(
            Atom.Expr(Atom.Sym("equal"), Atom.Sym("a"), Atom.Sym("b")))
            .ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_NotOperation_NoMatches()
    {
        // Arrange - Empty space
        // Act
        var results = interpreter.Evaluate(
            Atom.Expr(Atom.Sym("not"), Parse("(Human Socrates)")))
            .ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(Atom.Sym("True"));
    }

    [Fact]
    public void Evaluate_NotOperation_HasMatches()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act
        var results = interpreter.Evaluate(
            Atom.Expr(Atom.Sym("not"), Parse("(Human Socrates)")))
            .ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_MatchOperation_FindsMatches()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(Human Plato)"));

        // Act
        var results = interpreter.Evaluate(
            Atom.Expr(Atom.Sym("match"), Parse("(Human $x)")))
            .ToList();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_QuoteOperation_ReturnsUnmodified()
    {
        // Act
        var results = interpreter.Evaluate(
            Atom.Expr(Atom.Sym("quote"), Parse("($x $y)")))
            .ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].ToSExpr().Should().Be("($x $y)");
    }

    #endregion

    #region Succeeds Tests

    [Fact]
    public void Succeeds_MatchingFact_ReturnsTrue()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act & Assert
        interpreter.Succeeds(Parse("(Human Socrates)")).Should().BeTrue();
    }

    [Fact]
    public void Succeeds_NoMatch_ReturnsFalse()
    {
        // Act & Assert
        interpreter.Succeeds(Parse("(Human Socrates)")).Should().BeFalse();
    }

    [Fact]
    public void Succeeds_DerivedFromRule_ReturnsTrue()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(implies (Human $x) (Mortal $x))"));

        // Act & Assert
        interpreter.Succeeds(Parse("(Mortal Socrates)")).Should().BeTrue();
    }

    #endregion

    #region EvaluateFirst Tests

    [Fact]
    public void EvaluateFirst_HasResults_ReturnsSome()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act
        var result = interpreter.EvaluateFirst(Parse("(Human $x)"));

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void EvaluateFirst_NoResults_ReturnsNone()
    {
        // Act
        var result = interpreter.EvaluateFirst(Parse("(Human $x)"));

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region EvaluateWithBindings Tests

    [Fact]
    public void EvaluateWithBindings_ReturnsBindings()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act
        var results = interpreter.EvaluateWithBindings(Parse("(Human $x)")).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Bindings.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
    }

    #endregion

    #region AtomSpace Integration Tests

    [Fact]
    public void AtomSpace_Query_FindsPatternMatches()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(Human Plato)"));
        space.Add(Parse("(Animal Dog)"));

        // Act
        var results = space.Query(Parse("(Human $x)")).ToList();

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public void AtomSpace_Contains_ExactMatch()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act & Assert
        space.Contains(Parse("(Human Socrates)")).Should().BeTrue();
        space.Contains(Parse("(Human Plato)")).Should().BeFalse();
    }

    [Fact]
    public void AtomSpace_AddRange_AddsMultiple()
    {
        // Arrange
        var atoms = new[]
        {
            Parse("(Human Socrates)"),
            Parse("(Human Plato)"),
            Parse("(Human Aristotle)"),
        };

        // Act
        var count = space.AddRange(atoms);

        // Assert
        count.Should().Be(3);
        space.Count.Should().Be(3);
    }

    [Fact]
    public void AtomSpace_Remove_RemovesAtom()
    {
        // Arrange
        space.Add(Parse("(Human Socrates)"));

        // Act
        var removed = space.Remove(Parse("(Human Socrates)"));

        // Assert
        removed.Should().BeTrue();
        space.Contains(Parse("(Human Socrates)")).Should().BeFalse();
    }

    #endregion

    #region End-to-End Syllogism Tests

    [Fact]
    public void EndToEnd_SocratesSyllogism_Complete()
    {
        // Arrange - The classic syllogism
        // Premise 1: All humans are mortal
        // Premise 2: Socrates is human
        // Conclusion: Socrates is mortal

        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(implies (Human $x) (Mortal $x))"));

        // Act
        var isMortal = interpreter.Succeeds(Parse("(Mortal Socrates)"));

        // Assert
        isMortal.Should().BeTrue();
    }

    [Fact]
    public void EndToEnd_SocratesSyllogism_NegativeCase()
    {
        // Arrange - Socrates is human, but is Zeus mortal?
        space.Add(Parse("(Human Socrates)"));
        space.Add(Parse("(implies (Human $x) (Mortal $x))"));

        // Act
        var zeusMortal = interpreter.Succeeds(Parse("(Mortal Zeus)"));

        // Assert
        zeusMortal.Should().BeFalse();
    }

    #endregion
}
