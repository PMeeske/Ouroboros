// <copyright file="KleisliSetTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using FluentAssertions;
using LangChainPipeline.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the KleisliSet implementation.
/// Validates monadic composition and set-theoretic operations.
/// </summary>
public class KleisliSetTests
{
    [Fact]
    public void KleisliSet_Identity_ReturnsInput()
    {
        // Arrange
        var identity = KleisliSetExtensions.Identity<int>();

        // Act
        var result = identity(42);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public void KleisliSet_Lift_WrapsFunction()
    {
        // Arrange
        var arrow = KleisliSetExtensions.Lift<int, string>(x => x.ToString());

        // Act
        var result = arrow(42);

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public void KleisliSet_LiftMany_WrapsEnumerable()
    {
        // Arrange
        var arrow = KleisliSetExtensions.LiftMany<int, int>(x => new[] { x, x * 2, x * 3 });

        // Act
        var result = arrow(5).ToList();

        // Assert
        result.Should().Equal(5, 10, 15);
    }

    [Fact]
    public void KleisliSet_Then_ComposesArrows()
    {
        // Arrange
        KleisliSet<int, int> f = x => new[] { x, x + 1 };
        KleisliSet<int, string> g = x => new[] { $"a{x}", $"b{x}" };

        // Act
        var composed = f.Then(g);
        var result = composed(1).ToList();

        // Assert
        result.Should().Equal("a1", "b1", "a2", "b2");
    }

    [Fact]
    public void KleisliSet_Map_TransformsResults()
    {
        // Arrange
        KleisliSet<int, int> arrow = x => new[] { x, x * 2, x * 3 };

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = mapped(5).ToList();

        // Assert
        result.Should().Equal("5", "10", "15");
    }

    [Fact]
    public void KleisliSet_Union_CombinesResults()
    {
        // Arrange
        KleisliSet<int, int> f = x => new[] { x, x + 1 };
        KleisliSet<int, int> g = x => new[] { x + 2, x + 3 };

        // Act
        var union = f.Union(g);
        var result = union(1).ToList();

        // Assert
        result.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void KleisliSet_Intersect_FindsCommonResults()
    {
        // Arrange
        KleisliSet<int, int> f = x => new[] { x, x + 1, x + 2 };
        KleisliSet<int, int> g = x => new[] { x + 1, x + 2, x + 3 };

        // Act
        var intersect = f.Intersect(g);
        var result = intersect(1).ToList();

        // Assert
        result.Should().Equal(2, 3);
    }

    [Fact]
    public void KleisliSet_Except_RemovesResults()
    {
        // Arrange
        KleisliSet<int, int> f = x => new[] { x, x + 1, x + 2 };
        KleisliSet<int, int> g = x => new[] { x + 1, x + 2 };

        // Act
        var except = f.Except(g);
        var result = except(1).ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public void KleisliSet_Where_FiltersResults()
    {
        // Arrange
        KleisliSet<int, int> arrow = x => new[] { x, x + 1, x + 2, x + 3 };

        // Act
        var filtered = arrow.Where(x => x % 2 == 0);
        var result = filtered(1).ToList();

        // Assert
        result.Should().Equal(2, 4);
    }

    [Fact]
    public void KleisliSet_Distinct_RemovesDuplicates()
    {
        // Arrange
        KleisliSet<int, int> arrow = x => new[] { x, x, x + 1, x + 1, x + 2 };

        // Act
        var distinct = arrow.Distinct();
        var result = distinct(1).ToList();

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void KleisliSet_MonadicLaw_LeftIdentity()
    {
        // Arrange - return >=> f = f
        var f = KleisliSetExtensions.LiftMany<int, int>(x => new[] { x, x * 2 });
        var identity = KleisliSetExtensions.Identity<int>();

        // Act
        var composed = identity.Then(f);
        var direct = f;

        // Assert
        composed(5).Should().Equal(direct(5));
    }

    [Fact]
    public void KleisliSet_MonadicLaw_RightIdentity()
    {
        // Arrange - f >=> return = f
        var f = KleisliSetExtensions.LiftMany<int, int>(x => new[] { x, x * 2 });
        var identity = KleisliSetExtensions.Identity<int>();

        // Act
        var composed = f.Then(identity);
        var direct = f;

        // Assert
        composed(5).Should().Equal(direct(5));
    }

    [Fact]
    public void KleisliSet_MonadicLaw_Associativity()
    {
        // Arrange - (f >=> g) >=> h = f >=> (g >=> h)
        KleisliSet<int, int> f = x => new[] { x, x + 1 };
        KleisliSet<int, int> g = x => new[] { x * 2 };
        KleisliSet<int, string> h = x => new[] { x.ToString() };

        // Act
        var left = f.Then(g).Then(h);
        var right = f.Then(g.Then(h));

        // Assert
        left(1).Should().Equal(right(1));
    }

    [Fact]
    public void KleisliSet_SupportsCovariance()
    {
        // Arrange - covariance allows derived type assignment
        KleisliSet<int, string> specificArrow = x => new[] { x.ToString() };
        KleisliSet<int, object> generalArrow = specificArrow;

        // Act
        var result = generalArrow(42).ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public void KleisliSet_SupportsContravariance()
    {
        // Arrange - contravariance allows base type assignment for input
        KleisliSet<object, int> generalArrow = obj => new[] { obj.GetHashCode() };
        KleisliSet<string, int> specificArrow = generalArrow;

        // Act
        var result = specificArrow("test").ToList();

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void KleisliSet_SelectMany_WithResultSelector()
    {
        // Arrange
        KleisliSet<int, int> source = x => new[] { x, x + 1 };

        // Act
        var result = source.SelectMany(
            mid => new[] { mid * 2, mid * 3 },
            (mid, final) => final).Invoke(1).ToList();

        // Assert
        result.Should().Equal(2, 3, 4, 6);
    }
}
