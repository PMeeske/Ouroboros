// <copyright file="AtomTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Hyperon;

using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Xunit;

/// <summary>
/// Tests for the Atom, Symbol, Variable, and Expression types.
/// </summary>
[Trait("Category", "Unit")]
public class AtomTests
{
    #region Symbol Tests

    [Fact]
    public void Symbol_Creation_SetsNameCorrectly()
    {
        // Arrange & Act
        var symbol = new Symbol("Human");

        // Assert
        symbol.Name.Should().Be("Human");
    }

    [Fact]
    public void Symbol_ToSExpr_ReturnsName()
    {
        // Arrange
        var symbol = new Symbol("Socrates");

        // Act
        var sexpr = symbol.ToSExpr();

        // Assert
        sexpr.Should().Be("Socrates");
    }

    [Fact]
    public void Symbol_ToString_ReturnsSExpr()
    {
        // Arrange
        var symbol = new Symbol("Test");

        // Act
        var str = symbol.ToString();

        // Assert
        str.Should().Be("Test");
    }

    [Fact]
    public void Symbol_Equality_SameNameAreEqual()
    {
        // Arrange
        var symbol1 = new Symbol("Human");
        var symbol2 = new Symbol("Human");

        // Assert
        symbol1.Should().Be(symbol2);
        (symbol1 == symbol2).Should().BeTrue();
        symbol1.GetHashCode().Should().Be(symbol2.GetHashCode());
    }

    [Fact]
    public void Symbol_Equality_DifferentNamesAreNotEqual()
    {
        // Arrange
        var symbol1 = new Symbol("Human");
        var symbol2 = new Symbol("Mortal");

        // Assert
        symbol1.Should().NotBe(symbol2);
        (symbol1 != symbol2).Should().BeTrue();
    }

    [Fact]
    public void Symbol_ContainsVariables_ReturnsFalse()
    {
        // Arrange
        var symbol = new Symbol("Human");

        // Act & Assert
        symbol.ContainsVariables().Should().BeFalse();
    }

    #endregion

    #region Variable Tests

    [Fact]
    public void Variable_Creation_SetsNameCorrectly()
    {
        // Arrange & Act
        var variable = new Variable("x");

        // Assert
        variable.Name.Should().Be("x");
    }

    [Fact]
    public void Variable_ToSExpr_ReturnsDollarPrefixedName()
    {
        // Arrange
        var variable = new Variable("person");

        // Act
        var sexpr = variable.ToSExpr();

        // Assert
        sexpr.Should().Be("$person");
    }

    [Fact]
    public void Variable_Equality_SameNameAreEqual()
    {
        // Arrange
        var var1 = new Variable("x");
        var var2 = new Variable("x");

        // Assert
        var1.Should().Be(var2);
        var1.GetHashCode().Should().Be(var2.GetHashCode());
    }

    [Fact]
    public void Variable_Equality_DifferentNamesAreNotEqual()
    {
        // Arrange
        var var1 = new Variable("x");
        var var2 = new Variable("y");

        // Assert
        var1.Should().NotBe(var2);
    }

    [Fact]
    public void Variable_ContainsVariables_ReturnsTrue()
    {
        // Arrange
        var variable = new Variable("x");

        // Act & Assert
        variable.ContainsVariables().Should().BeTrue();
    }

    #endregion

    #region Expression Tests

    [Fact]
    public void Expression_EmptyExpression_ToSExprReturnsEmptyParens()
    {
        // Arrange
        var expr = new Expression(ImmutableList<Atom>.Empty);

        // Act
        var sexpr = expr.ToSExpr();

        // Assert
        sexpr.Should().Be("()");
    }

    [Fact]
    public void Expression_SingleChild_ToSExprCorrect()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("Human"));

        // Act
        var sexpr = expr.ToSExpr();

        // Assert
        sexpr.Should().Be("(Human)");
    }

    [Fact]
    public void Expression_MultipleChildren_ToSExprCorrect()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Act
        var sexpr = expr.ToSExpr();

        // Assert
        sexpr.Should().Be("(Human Socrates)");
    }

    [Fact]
    public void Expression_NestedExpression_ToSExprCorrect()
    {
        // Arrange
        var innerExpr = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));
        var outerExpr = Atom.Expr(Atom.Sym("implies"), innerExpr, Atom.Expr(Atom.Sym("Mortal"), Atom.Var("x")));

        // Act
        var sexpr = outerExpr.ToSExpr();

        // Assert
        sexpr.Should().Be("(implies (Human $x) (Mortal $x))");
    }

    [Fact]
    public void Expression_Equality_SameStructureAreEqual()
    {
        // Arrange
        var expr1 = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));
        var expr2 = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Assert
        expr1.Should().Be(expr2);
        expr1.GetHashCode().Should().Be(expr2.GetHashCode());
    }

    [Fact]
    public void Expression_Equality_DifferentStructureAreNotEqual()
    {
        // Arrange
        var expr1 = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));
        var expr2 = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Plato"));

        // Assert
        expr1.Should().NotBe(expr2);
    }

    [Fact]
    public void Expression_Equality_DifferentLengthAreNotEqual()
    {
        // Arrange
        var expr1 = Atom.Expr(Atom.Sym("Human"));
        var expr2 = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Assert
        expr1.Should().NotBe(expr2);
    }

    [Fact]
    public void Expression_ContainsVariables_WithVariable_ReturnsTrue()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));

        // Act & Assert
        expr.ContainsVariables().Should().BeTrue();
    }

    [Fact]
    public void Expression_ContainsVariables_WithoutVariable_ReturnsFalse()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Act & Assert
        expr.ContainsVariables().Should().BeFalse();
    }

    [Fact]
    public void Expression_ContainsVariables_NestedVariable_ReturnsTrue()
    {
        // Arrange
        var expr = Atom.Expr(
            Atom.Sym("implies"),
            Atom.Expr(Atom.Sym("Human"), Atom.Var("x")),
            Atom.Expr(Atom.Sym("Mortal"), Atom.Var("x")));

        // Act & Assert
        expr.ContainsVariables().Should().BeTrue();
    }

    [Fact]
    public void Expression_Head_ReturnsFirstChild()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Act
        var head = expr.Head();

        // Assert
        head.HasValue.Should().BeTrue();
        head.Value.Should().Be(Atom.Sym("Human"));
    }

    [Fact]
    public void Expression_Head_EmptyExpression_ReturnsNone()
    {
        // Arrange
        var expr = new Expression(ImmutableList<Atom>.Empty);

        // Act
        var head = expr.Head();

        // Assert
        head.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Expression_Tail_ReturnsAllButFirst()
    {
        // Arrange
        var expr = Atom.Expr(Atom.Sym("add"), Atom.Sym("1"), Atom.Sym("2"), Atom.Sym("3"));

        // Act
        var tail = expr.Tail();

        // Assert
        tail.Should().HaveCount(3);
        tail[0].Should().Be(Atom.Sym("1"));
        tail[1].Should().Be(Atom.Sym("2"));
        tail[2].Should().Be(Atom.Sym("3"));
    }

    #endregion

    #region Factory Methods Tests

    [Fact]
    public void Atom_Sym_CreatesSymbol()
    {
        // Act
        var atom = Atom.Sym("Test");

        // Assert
        atom.Should().BeOfType<Symbol>();
        atom.Name.Should().Be("Test");
    }

    [Fact]
    public void Atom_Var_CreatesVariable()
    {
        // Act
        var atom = Atom.Var("x");

        // Assert
        atom.Should().BeOfType<Variable>();
        atom.Name.Should().Be("x");
    }

    [Fact]
    public void Atom_Expr_CreatesExpression()
    {
        // Act
        var atom = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"));

        // Assert
        atom.Should().BeOfType<Expression>();
        atom.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Atom_Expr_FromEnumerable_CreatesExpression()
    {
        // Arrange
        var children = new List<Atom> { Atom.Sym("a"), Atom.Sym("b") };

        // Act
        var atom = Atom.Expr(children);

        // Assert
        atom.Children.Should().HaveCount(2);
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void Expression_IsImmutable_OriginalUnchanged()
    {
        // Arrange
        var original = Atom.Expr(Atom.Sym("a"), Atom.Sym("b"));
        var originalSexpr = original.ToSExpr();

        // Act - Try to get children and modify (this should not affect original)
        var children = original.Children;

        // Assert
        original.ToSExpr().Should().Be(originalSexpr);
        original.Children.Should().HaveCount(2);
    }

    #endregion
}
