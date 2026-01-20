// <copyright file="UnificationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Hyperon;

using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Xunit;

/// <summary>
/// Tests for the Unifier and Substitution classes.
/// </summary>
[Trait("Category", "Unit")]
public class UnificationTests
{
    #region Substitution Tests

    [Fact]
    public void Substitution_Empty_HasNoBindings()
    {
        // Arrange & Act
        var subst = Substitution.Empty;

        // Assert
        subst.IsEmpty.Should().BeTrue();
        subst.Count.Should().Be(0);
    }

    [Fact]
    public void Substitution_Bind_CreatesBinding()
    {
        // Arrange
        var subst = Substitution.Empty;

        // Act
        var bound = subst.Bind("x", Atom.Sym("Socrates"));

        // Assert
        bound.Count.Should().Be(1);
        bound.Lookup("x").HasValue.Should().BeTrue();
        bound.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
    }

    [Fact]
    public void Substitution_Of_CreatesSingleBinding()
    {
        // Act
        var subst = Substitution.Of("x", Atom.Sym("Socrates"));

        // Assert
        subst.Count.Should().Be(1);
        subst.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
    }

    [Fact]
    public void Substitution_Lookup_UnboundVariable_ReturnsNone()
    {
        // Arrange
        var subst = Substitution.Of("x", Atom.Sym("Socrates"));

        // Act
        var result = subst.Lookup("y");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Substitution_Apply_ReplacesVariable()
    {
        // Arrange
        var subst = Substitution.Of("x", Atom.Sym("Socrates"));
        var variable = Atom.Var("x");

        // Act
        var result = subst.Apply(variable);

        // Assert
        result.Should().Be(Atom.Sym("Socrates"));
    }

    [Fact]
    public void Substitution_Apply_LeavesUnboundVariable()
    {
        // Arrange
        var subst = Substitution.Of("x", Atom.Sym("Socrates"));
        var variable = Atom.Var("y");

        // Act
        var result = subst.Apply(variable);

        // Assert
        result.Should().Be(Atom.Var("y"));
    }

    [Fact]
    public void Substitution_Apply_ReplacesVariablesInExpression()
    {
        // Arrange
        var subst = Substitution.Of("x", Atom.Sym("Socrates"));
        var expr = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));

        // Act
        var result = subst.Apply(expr);

        // Assert
        result.Should().Be(Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates")));
    }

    [Fact]
    public void Substitution_Compose_CombinesBindings()
    {
        // Arrange
        var subst1 = Substitution.Of("x", Atom.Sym("Socrates"));
        var subst2 = Substitution.Of("y", Atom.Sym("Plato"));

        // Act
        var composed = subst1.Compose(subst2);

        // Assert
        composed.Should().NotBeNull();
        composed!.Count.Should().Be(2);
        composed.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
        composed.Lookup("y").Value.Should().Be(Atom.Sym("Plato"));
    }

    [Fact]
    public void Substitution_Compose_ConflictingBindings_ReturnsNull()
    {
        // Arrange
        var subst1 = Substitution.Of("x", Atom.Sym("Socrates"));
        var subst2 = Substitution.Of("x", Atom.Sym("Plato"));

        // Act
        var composed = subst1.Compose(subst2);

        // Assert
        composed.Should().BeNull();
    }

    [Fact]
    public void Substitution_Compose_SameBindings_Succeeds()
    {
        // Arrange
        var subst1 = Substitution.Of("x", Atom.Sym("Socrates"));
        var subst2 = Substitution.Of("x", Atom.Sym("Socrates"));

        // Act
        var composed = subst1.Compose(subst2);

        // Assert
        composed.Should().NotBeNull();
        composed!.Count.Should().Be(1);
    }

    [Fact]
    public void Substitution_ToString_ShowsBindings()
    {
        // Arrange
        var subst = Substitution.Of("x", Atom.Sym("Socrates"));

        // Act
        var str = subst.ToString();

        // Assert
        str.Should().Contain("$x");
        str.Should().Contain("Socrates");
    }

    #endregion

    #region Basic Unification Tests

    [Fact]
    public void Unify_IdenticalSymbols_Succeeds()
    {
        // Arrange
        var a = Atom.Sym("Human");
        var b = Atom.Sym("Human");

        // Act
        var result = Unifier.Unify(a, b);

        // Assert
        result.Should().NotBeNull();
        result!.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Unify_DifferentSymbols_Fails()
    {
        // Arrange
        var a = Atom.Sym("Human");
        var b = Atom.Sym("Mortal");

        // Act
        var result = Unifier.Unify(a, b);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Unify_VariableWithSymbol_Binds()
    {
        // Arrange
        var pattern = Atom.Var("x");
        var target = Atom.Sym("Socrates");

        // Act
        var result = Unifier.Unify(pattern, target);

        // Assert
        result.Should().NotBeNull();
        result!.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
    }

    [Fact]
    public void Unify_SymbolWithVariable_Binds()
    {
        // Arrange
        var pattern = Atom.Sym("Socrates");
        var target = Atom.Var("x");

        // Act
        var result = Unifier.Unify(pattern, target);

        // Assert
        result.Should().NotBeNull();
        result!.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
    }

    [Fact]
    public void Unify_VariableWithVariable_Binds()
    {
        // Arrange
        var a = Atom.Var("x");
        var b = Atom.Var("y");

        // Act
        var result = Unifier.Unify(a, b);

        // Assert
        result.Should().NotBeNull();
        // Either x binds to y or y binds to x
        var hasXorY = result!.Lookup("x").HasValue || result.Lookup("y").HasValue;
        hasXorY.Should().BeTrue();
    }

    #endregion

    #region Expression Unification Tests

    [Fact]
    public void Unify_IdenticalExpressions_Succeeds()
    {
        // Arrange
        var a = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));
        var b = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Act
        var result = Unifier.Unify(a, b);

        // Assert
        result.Should().NotBeNull();
        result!.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Unify_DifferentExpressions_Fails()
    {
        // Arrange
        var a = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));
        var b = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Plato"));

        // Act
        var result = Unifier.Unify(a, b);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Unify_ExpressionWithVariable_BindsVariable()
    {
        // Arrange
        var pattern = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));
        var target = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Act
        var result = Unifier.Unify(pattern, target);

        // Assert
        result.Should().NotBeNull();
        result!.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
    }

    [Fact]
    public void Unify_ExpressionDifferentLengths_Fails()
    {
        // Arrange
        var a = Atom.Expr(Atom.Sym("Human"));
        var b = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Act
        var result = Unifier.Unify(a, b);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Unify_NestedExpressions_BindsVariables()
    {
        // Arrange
        var pattern = Atom.Expr(
            Atom.Sym("implies"),
            Atom.Expr(Atom.Sym("Human"), Atom.Var("x")),
            Atom.Var("conclusion"));

        var target = Atom.Expr(
            Atom.Sym("implies"),
            Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates")),
            Atom.Expr(Atom.Sym("Mortal"), Atom.Sym("Socrates")));

        // Act
        var result = Unifier.Unify(pattern, target);

        // Assert
        result.Should().NotBeNull();
        result!.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
        result.Lookup("conclusion").Value.Should().Be(
            Atom.Expr(Atom.Sym("Mortal"), Atom.Sym("Socrates")));
    }

    [Fact]
    public void Unify_MultipleOccurrences_SameVariable_Consistent()
    {
        // Arrange
        var pattern = Atom.Expr(Atom.Sym("equal"), Atom.Var("x"), Atom.Var("x"));
        var target = Atom.Expr(Atom.Sym("equal"), Atom.Sym("5"), Atom.Sym("5"));

        // Act
        var result = Unifier.Unify(pattern, target);

        // Assert
        result.Should().NotBeNull();
        result!.Lookup("x").Value.Should().Be(Atom.Sym("5"));
    }

    [Fact]
    public void Unify_MultipleOccurrences_SameVariable_Inconsistent_Fails()
    {
        // Arrange
        var pattern = Atom.Expr(Atom.Sym("equal"), Atom.Var("x"), Atom.Var("x"));
        var target = Atom.Expr(Atom.Sym("equal"), Atom.Sym("5"), Atom.Sym("6"));

        // Act
        var result = Unifier.Unify(pattern, target);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Occurs Check Tests

    [Fact]
    public void Unify_OccursCheck_PreventsCycle()
    {
        // Arrange - Try to unify $x with an expression containing $x
        var pattern = Atom.Var("x");
        var target = Atom.Expr(Atom.Sym("f"), Atom.Var("x"));

        // Act
        var result = Unifier.Unify(pattern, target);

        // Assert - Should fail due to occurs check
        result.Should().BeNull();
    }

    #endregion

    #region UnifyAll Tests

    [Fact]
    public void UnifyAll_FindsAllMatches()
    {
        // Arrange
        var pattern = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));
        var atoms = new List<Atom>
        {
            Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates")),
            Atom.Expr(Atom.Sym("Human"), Atom.Sym("Plato")),
            Atom.Expr(Atom.Sym("Animal"), Atom.Sym("Dog")),
        };

        // Act
        var results = Unifier.UnifyAll(pattern, atoms).ToList();

        // Assert
        results.Should().HaveCount(2);
        results[0].Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
        results[1].Lookup("x").Value.Should().Be(Atom.Sym("Plato"));
    }

    #endregion

    #region CanUnify Tests

    [Fact]
    public void CanUnify_CompatibleAtoms_ReturnsTrue()
    {
        // Arrange
        var a = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));
        var b = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

        // Act & Assert
        Unifier.CanUnify(a, b).Should().BeTrue();
    }

    [Fact]
    public void CanUnify_IncompatibleAtoms_ReturnsFalse()
    {
        // Arrange
        var a = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));
        var b = Atom.Expr(Atom.Sym("Animal"), Atom.Sym("Dog"));

        // Act & Assert
        Unifier.CanUnify(a, b).Should().BeFalse();
    }

    #endregion

    #region Initial Substitution Tests

    [Fact]
    public void Unify_WithInitialSubstitution_RespectsExistingBindings()
    {
        // Arrange
        var initial = Substitution.Of("x", Atom.Sym("Socrates"));
        var pattern = Atom.Var("y");
        var target = Atom.Sym("Plato");

        // Act
        var result = Unifier.Unify(pattern, target, initial);

        // Assert
        result.Should().NotBeNull();
        result!.Lookup("x").Value.Should().Be(Atom.Sym("Socrates"));
        result.Lookup("y").Value.Should().Be(Atom.Sym("Plato"));
    }

    #endregion
}
