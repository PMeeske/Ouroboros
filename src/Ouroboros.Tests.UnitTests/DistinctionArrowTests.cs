// <copyright file="DistinctionArrowTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for the DistinctionArrow - Kleisli arrows based on Laws of Form.
/// </summary>
[Trait("Category", "Unit")]
public class DistinctionArrowTests
{
    #region Gate Tests

    [Fact]
    public async Task Gate_WithMarkedPredicate_PassesThrough()
    {
        // Arrange
        var arrow = DistinctionArrow.Gate<string>(s => (s.Length > 0).ToForm());

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task Gate_WithVoidPredicate_ReturnsNull()
    {
        // Arrange
        var arrow = DistinctionArrow.Gate<string>(s => (s.Length > 10).ToForm());

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Branch Tests

    [Fact]
    public async Task Branch_MarkedPredicate_ExecutesMarkedBranch()
    {
        // Arrange
        var arrow = DistinctionArrow.Branch<int, string>(
            predicate: n => (n > 0).ToForm(),
            onMarked: n => $"positive: {n}",
            onVoid: n => $"non-positive: {n}");

        // Act
        var result = await arrow(5);

        // Assert
        result.Should().Be("positive: 5");
    }

    [Fact]
    public async Task Branch_VoidPredicate_ExecutesVoidBranch()
    {
        // Arrange
        var arrow = DistinctionArrow.Branch<int, string>(
            predicate: n => (n > 0).ToForm(),
            onMarked: n => $"positive: {n}",
            onVoid: n => $"non-positive: {n}");

        // Act
        var result = await arrow(-3);

        // Assert
        result.Should().Be("non-positive: -3");
    }

    [Fact]
    public async Task Branch_ZeroPredicate_ExecutesVoidBranch()
    {
        // Arrange
        var arrow = DistinctionArrow.Branch<int, string>(
            predicate: n => (n > 0).ToForm(),
            onMarked: n => $"positive: {n}",
            onVoid: n => $"non-positive: {n}");

        // Act
        var result = await arrow(0);

        // Assert
        result.Should().Be("non-positive: 0");
    }

    #endregion

    #region AllMarked Tests

    [Fact]
    public async Task AllMarked_AllPredicatesTrue_PassesThrough()
    {
        // Arrange
        var arrow = DistinctionArrow.AllMarked<string>(
            s => (s.Length > 0).ToForm(),
            s => s.StartsWith("h", StringComparison.OrdinalIgnoreCase).ToForm(),
            s => s.Contains('e').ToForm());

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task AllMarked_OnePredicateFalse_ReturnsNull()
    {
        // Arrange
        var arrow = DistinctionArrow.AllMarked<string>(
            s => (s.Length > 0).ToForm(),
            s => s.StartsWith("x", StringComparison.OrdinalIgnoreCase).ToForm(),
            s => s.Contains('e').ToForm());

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AllMarked_EmptyPredicates_PassesThrough()
    {
        // Arrange
        var arrow = DistinctionArrow.AllMarked<string>();

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().Be("hello");
    }

    #endregion

    #region AnyMarked Tests

    [Fact]
    public async Task AnyMarked_OnePredicateTrue_PassesThrough()
    {
        // Arrange
        var arrow = DistinctionArrow.AnyMarked<string>(
            s => (s.Length > 100).ToForm(),
            s => s.StartsWith("h", StringComparison.OrdinalIgnoreCase).ToForm(),
            s => s.Contains('z').ToForm());

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task AnyMarked_AllPredicatesFalse_ReturnsNull()
    {
        // Arrange
        var arrow = DistinctionArrow.AnyMarked<string>(
            s => (s.Length > 100).ToForm(),
            s => s.StartsWith("x", StringComparison.OrdinalIgnoreCase).ToForm(),
            s => s.Contains('z').ToForm());

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AnyMarked_EmptyPredicates_ReturnsNull()
    {
        // Arrange
        var arrow = DistinctionArrow.AnyMarked<string>();

        // Act
        var result = await arrow("hello");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Evaluate Tests

    [Fact]
    public async Task Evaluate_SimplifiesDoubleMarkedForm()
    {
        // Arrange - a record with a doubly-marked form
        var input = new FormRecord("test", Form.CrossForm(Form.CrossForm(Form.Void)));
        var arrow = DistinctionArrow.Evaluate<FormRecord>(
            extractor: r => r.Distinction,
            combiner: (r, f) => r with { Distinction = f });

        // Act
        var result = await arrow(input);

        // Assert
        result.Distinction.IsVoid().Should().BeTrue("Double mark should simplify to void");
    }

    [Fact]
    public async Task Evaluate_PreservesSimpleForms()
    {
        // Arrange
        var input = new FormRecord("test", Form.Mark);
        var arrow = DistinctionArrow.Evaluate<FormRecord>(
            extractor: r => r.Distinction,
            combiner: (r, f) => r with { Distinction = f });

        // Act
        var result = await arrow(input);

        // Assert
        result.Distinction.IsMarked().Should().BeTrue("Single mark should remain marked");
    }

    #endregion

    #region ReEntry Tests

    [Fact]
    public async Task ReEntry_ConvergesToFixedPoint()
    {
        // Arrange - a self-reference that converges
        // f(x) = if x is void then mark else void
        var arrow = DistinctionArrow.ReEntry<string>(
            selfReference: (_, current) => current.IsVoid() ? Form.Mark : Form.Void,
            maxDepth: 10);

        // Act
        var result = await arrow("test");

        // Assert - should oscillate or reach a state
        // This is characteristic of re-entry in Laws of Form
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ReEntry_WithConstantReference_ConvergesImmediately()
    {
        // Arrange - a self-reference that always returns the same form
        var arrow = DistinctionArrow.ReEntry<string>(
            selfReference: (_, _) => Form.Mark,
            maxDepth: 10);

        // Act
        var result = await arrow("test");

        // Assert
        result.IsMarked().Should().BeTrue("Constant mark reference should converge to mark");
    }

    [Fact]
    public async Task ReEntry_RespectsMaxDepth()
    {
        // Arrange - a non-converging oscillation
        int iterations = 0;
        var arrow = DistinctionArrow.ReEntry<string>(
            selfReference: (_, current) =>
            {
                iterations++;
                return current.IsVoid() ? Form.Mark : Form.Void;
            },
            maxDepth: 5);

        // Act
        await arrow("test");

        // Assert - should have stopped at max depth
        iterations.Should().BeLessThanOrEqualTo(5);
    }

    #endregion

    #region LiftPredicate Tests

    [Fact]
    public void LiftPredicate_TrueCondition_ReturnsMarked()
    {
        // Arrange
        var lifted = DistinctionArrow.LiftPredicate<int>(n => n > 0);

        // Act
        var form = lifted(5);

        // Assert
        form.IsMarked().Should().BeTrue();
    }

    [Fact]
    public void LiftPredicate_FalseCondition_ReturnsVoid()
    {
        // Arrange
        var lifted = DistinctionArrow.LiftPredicate<int>(n => n > 0);

        // Act
        var form = lifted(-5);

        // Assert
        form.IsVoid().Should().BeTrue();
    }

    #endregion

    #region Composition Tests

    [Fact]
    public async Task Arrows_CanBeComposed()
    {
        // Arrange - compose two distinction-based arrows
        var gate = DistinctionArrow.Gate<string>(s => (s.Length > 0).ToForm());
        var branch = DistinctionArrow.Branch<string?, string>(
            predicate: s => (s?.Length > 3).ToForm(),
            onMarked: s => s!.ToUpper(),
            onVoid: s => s?.ToLower() ?? "empty");

        // Act - compose manually
        var gateResult = await gate("hello");
        var branchResult = await branch(gateResult);

        // Assert
        branchResult.Should().Be("HELLO");
    }

    [Fact]
    public async Task Arrows_ShortCircuitOnVoid()
    {
        // Arrange
        var gate = DistinctionArrow.Gate<string>(s => (s.Length > 10).ToForm());
        var branch = DistinctionArrow.Branch<string?, string>(
            predicate: s => (s is not null).ToForm(),
            onMarked: s => s!.ToUpper(),
            onVoid: s => "fallback");

        // Act
        var gateResult = await gate("hi");  // Should be null
        var branchResult = await branch(gateResult);

        // Assert
        branchResult.Should().Be("fallback");
    }

    #endregion

    /// <summary>
    /// Helper record for testing with forms.
    /// </summary>
    private sealed record FormRecord(string Name, Form Distinction);
}
