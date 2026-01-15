// <copyright file="AsyncKleisliTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the AsyncKleisli implementation.
/// Validates monadic composition and async stream operations.
/// </summary>
[Trait("Category", "Unit")]
public class AsyncKleisliTests
{
    [Fact]
    public async Task AsyncKleisli_Identity_ReturnsInput()
    {
        // Arrange
        var identity = AsyncKleisliExtensions.Identity<int>();

        // Act
        var result = await ToListAsync(identity(42));

        // Assert
        result.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public async Task AsyncKleisli_Lift_WrapsFunction()
    {
        // Arrange
        var arrow = AsyncKleisliExtensions.Lift<int, string>(x => x.ToString());

        // Act
        var result = await ToListAsync(arrow(42));

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public async Task AsyncKleisli_LiftAsync_WrapsAsyncFunction()
    {
        // Arrange
        var arrow = AsyncKleisliExtensions.LiftAsync<int, string>(
            x => Task.FromResult(x.ToString()));

        // Act
        var result = await ToListAsync(arrow(42));

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public async Task AsyncKleisli_LiftMany_WrapsAsyncEnumerable()
    {
        // Arrange
        var arrow = AsyncKleisliExtensions.LiftMany<int, int>(
            x => ToAsyncEnumerable(new[] { x, x * 2, x * 3 }));

        // Act
        var result = await ToListAsync(arrow(5));

        // Assert
        result.Should().Equal(5, 10, 15);
    }

    [Fact]
    public async Task AsyncKleisli_Then_ComposesArrows()
    {
        // Arrange
        AsyncKleisli<int, int> f = x => ToAsyncEnumerable(new[] { x, x + 1 });
        AsyncKleisli<int, string> g = x => ToAsyncEnumerable(new[] { $"a{x}", $"b{x}" });

        // Act
        var composed = f.Then(g);
        var result = await ToListAsync(composed(1));

        // Assert
        result.Should().Equal("a1", "b1", "a2", "b2");
    }

    [Fact]
    public async Task AsyncKleisli_Map_TransformsResults()
    {
        // Arrange
        AsyncKleisli<int, int> arrow = x => ToAsyncEnumerable(new[] { x, x * 2, x * 3 });

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = await ToListAsync(mapped(5));

        // Assert
        result.Should().Equal("5", "10", "15");
    }

    [Fact]
    public async Task AsyncKleisli_MapAsync_TransformsWithAsyncFunction()
    {
        // Arrange
        AsyncKleisli<int, int> arrow = x => ToAsyncEnumerable(new[] { x, x * 2 });

        // Act
        var mapped = arrow.MapAsync(x => Task.FromResult(x.ToString()));
        var result = await ToListAsync(mapped(5));

        // Assert
        result.Should().Equal("5", "10");
    }

    [Fact]
    public async Task AsyncKleisli_Union_MergesStreams()
    {
        // Arrange
        AsyncKleisli<int, int> f = x => ToAsyncEnumerable(new[] { x, x + 1 });
        AsyncKleisli<int, int> g = x => ToAsyncEnumerable(new[] { x + 10, x + 11 });

        // Act
        var union = f.Union(g);
        var result = await ToListAsync(union(1));

        // Assert
        result.Should().Contain(new[] { 1, 2, 11, 12 });
    }

    [Fact]
    public async Task AsyncKleisli_Where_FiltersResults()
    {
        // Arrange
        AsyncKleisli<int, int> arrow = x => ToAsyncEnumerable(new[] { x, x + 1, x + 2, x + 3 });

        // Act
        var filtered = arrow.Where(x => x % 2 == 0);
        var result = await ToListAsync(filtered(1));

        // Assert
        result.Should().Equal(2, 4);
    }

    [Fact]
    public async Task AsyncKleisli_WhereAsync_FiltersWithAsyncPredicate()
    {
        // Arrange
        AsyncKleisli<int, int> arrow = x => ToAsyncEnumerable(new[] { x, x + 1, x + 2 });

        // Act
        var filtered = arrow.WhereAsync(x => Task.FromResult(x > 1));
        var result = await ToListAsync(filtered(1));

        // Assert
        result.Should().Equal(2, 3);
    }

    [Fact]
    public async Task AsyncKleisli_Take_LimitsResults()
    {
        // Arrange
        AsyncKleisli<int, int> arrow = x => ToAsyncEnumerable(new[] { x, x + 1, x + 2, x + 3 });

        // Act
        var limited = arrow.Take(2);
        var result = await ToListAsync(limited(1));

        // Assert
        result.Should().Equal(1, 2);
    }

    [Fact]
    public async Task AsyncKleisli_Distinct_RemovesDuplicates()
    {
        // Arrange
        AsyncKleisli<int, int> arrow = x => ToAsyncEnumerable(new[] { x, x, x + 1, x + 1, x + 2 });

        // Act
        var distinct = arrow.Distinct();
        var result = await ToListAsync(distinct(1));

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task AsyncKleisli_MonadicLaw_LeftIdentity()
    {
        // Arrange - return >=> f = f
        var f = AsyncKleisliExtensions.LiftMany<int, int>(
            x => ToAsyncEnumerable(new[] { x, x * 2 }));
        var identity = AsyncKleisliExtensions.Identity<int>();

        // Act
        var composed = identity.Then(f);
        var direct = f;

        // Assert
        var composedResult = await ToListAsync(composed(5));
        var directResult = await ToListAsync(direct(5));
        composedResult.Should().Equal(directResult);
    }

    [Fact]
    public async Task AsyncKleisli_MonadicLaw_RightIdentity()
    {
        // Arrange - f >=> return = f
        var f = AsyncKleisliExtensions.LiftMany<int, int>(
            x => ToAsyncEnumerable(new[] { x, x * 2 }));
        var identity = AsyncKleisliExtensions.Identity<int>();

        // Act
        var composed = f.Then(identity);
        var direct = f;

        // Assert
        var composedResult = await ToListAsync(composed(5));
        var directResult = await ToListAsync(direct(5));
        composedResult.Should().Equal(directResult);
    }

    [Fact]
    public async Task AsyncKleisli_MonadicLaw_Associativity()
    {
        // Arrange - (f >=> g) >=> h = f >=> (g >=> h)
        AsyncKleisli<int, int> f = x => ToAsyncEnumerable(new[] { x, x + 1 });
        AsyncKleisli<int, int> g = x => ToAsyncEnumerable(new[] { x * 2 });
        AsyncKleisli<int, string> h = x => ToAsyncEnumerable(new[] { x.ToString() });

        // Act
        var left = f.Then(g).Then(h);
        var right = f.Then(g.Then(h));

        // Assert
        var leftResult = await ToListAsync(left(1));
        var rightResult = await ToListAsync(right(1));
        leftResult.Should().Equal(rightResult);
    }

    [Fact]
    public async Task AsyncKleisli_SupportsCovariance()
    {
        // Arrange - covariance allows derived type assignment
        AsyncKleisli<int, string> specificArrow = x => ToAsyncEnumerable(new[] { x.ToString() });
        AsyncKleisli<int, object> generalArrow = specificArrow;

        // Act
        var result = await ToListAsync(generalArrow(42));

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public async Task AsyncKleisli_SupportsContravariance()
    {
        // Arrange - contravariance allows base type assignment for input
        AsyncKleisli<object, int> generalArrow = obj => ToAsyncEnumerable(new[] { obj.GetHashCode() });
        AsyncKleisli<string, int> specificArrow = generalArrow;

        // Act
        var result = await ToListAsync(specificArrow("test"));

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AsyncKleisli_SelectMany_WithResultSelector()
    {
        // Arrange
        AsyncKleisli<int, int> source = x => ToAsyncEnumerable(new[] { x, x + 1 });

        // Act
        var result = await ToListAsync(
            source.SelectMany(
                mid => ToAsyncEnumerable(new[] { mid * 2, mid * 3 }),
                (mid, final) => final).Invoke(1));

        // Assert
        result.Should().Equal(2, 3, 4, 6);
    }

    // Helper methods
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
