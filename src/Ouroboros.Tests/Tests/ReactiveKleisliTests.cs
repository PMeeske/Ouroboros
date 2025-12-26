// <copyright file="ReactiveKleisliTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using System.Reactive.Linq;
using FluentAssertions;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the ReactiveKleisli implementation.
/// Validates monadic composition and reactive stream operations.
/// </summary>
public class ReactiveKleisliTests
{
    [Fact]
    public async Task ReactiveKleisli_Identity_ReturnsInput()
    {
        // Arrange
        var identity = ReactiveKleisliExtensions.Identity<int>();

        // Act
        var result = await identity(42).ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public async Task ReactiveKleisli_Lift_WrapsFunction()
    {
        // Arrange
        var arrow = ReactiveKleisliExtensions.Lift<int, string>(x => x.ToString());

        // Act
        var result = await arrow(42).ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public async Task ReactiveKleisli_LiftAsync_WrapsAsyncFunction()
    {
        // Arrange
        var arrow = ReactiveKleisliExtensions.LiftAsync<int, string>(
            x => Task.FromResult(x.ToString()));

        // Act
        var result = await arrow(42).ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public async Task ReactiveKleisli_LiftObservable_WrapsObservable()
    {
        // Arrange
        var arrow = ReactiveKleisliExtensions.LiftObservable<int, int>(
            x => Observable.Range(x, 3));

        // Act
        var result = await arrow(5).ToList();

        // Assert
        result.Should().Equal(5, 6, 7);
    }

    [Fact]
    public async Task ReactiveKleisli_FromEnumerable_ConvertsEnumerable()
    {
        // Arrange
        var arrow = ReactiveKleisliExtensions.FromEnumerable<int, int>(
            x => new[] { x, x * 2, x * 3 });

        // Act
        var result = await arrow(5).ToList();

        // Assert
        result.Should().Equal(5, 10, 15);
    }

    [Fact]
    public async Task ReactiveKleisli_Compose_ComposesArrows()
    {
        // Arrange
        ReactiveKleisli<int, int> f = x => Observable.Range(x, 2);
        ReactiveKleisli<int, string> g = x => Observable.Return($"value{x}");

        // Act
        var composed = f.Compose(g);
        var result = await composed(1).ToList();

        // Assert
        result.Should().Equal("value1", "value2");
    }

    [Fact]
    public async Task ReactiveKleisli_Then_ComposesArrows()
    {
        // Arrange
        ReactiveKleisli<int, int> f = x => Observable.Range(x, 2);
        ReactiveKleisli<int, string> g = x => Observable.Return($"value{x}");

        // Act
        var composed = f.Then(g);
        var result = await composed(1).ToList();

        // Assert
        result.Should().Equal("value1", "value2");
    }

    [Fact]
    public async Task ReactiveKleisli_Map_TransformsResults()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => Observable.Range(x, 3);

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = await mapped(5).ToList();

        // Assert
        result.Should().Equal("5", "6", "7");
    }

    [Fact]
    public async Task ReactiveKleisli_Union_MergesStreams()
    {
        // Arrange
        ReactiveKleisli<int, int> f = x => Observable.Return(x);
        ReactiveKleisli<int, int> g = x => Observable.Return(x + 10);

        // Act
        var union = f.Union(g);
        var result = await union(1).ToList();

        // Assert
        result.Should().Contain(new[] { 1, 11 });
    }

    [Fact]
    public async Task ReactiveKleisli_Merge_MergesStreams()
    {
        // Arrange
        ReactiveKleisli<int, int> f = x => Observable.Return(x);
        ReactiveKleisli<int, int> g = x => Observable.Return(x + 10);

        // Act
        var merged = f.Merge(g);
        var result = await merged(1).ToList();

        // Assert
        result.Should().Contain(new[] { 1, 11 });
    }

    [Fact]
    public async Task ReactiveKleisli_Distinct_RemovesDuplicates()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => new[] { x, x, x + 1, x + 1, x + 2 }.ToObservable();

        // Act
        var distinct = arrow.Distinct();
        var result = await distinct(1).ToList();

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReactiveKleisli_DistinctUntilChanged_RemovesConsecutiveDuplicates()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => new[] { x, x, x + 1, x + 1, x, x + 2 }.ToObservable();

        // Act
        var distinct = arrow.DistinctUntilChanged();
        var result = await distinct(1).ToList();

        // Assert
        result.Should().Equal(1, 2, 1, 3);
    }

    [Fact]
    public async Task ReactiveKleisli_Where_FiltersResults()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => Observable.Range(x, 4);

        // Act
        var filtered = arrow.Where(x => x % 2 == 0);
        var result = await filtered(1).ToList();

        // Assert
        result.Should().Equal(2, 4);
    }

    [Fact]
    public async Task ReactiveKleisli_Take_LimitsResults()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => Observable.Range(x, 10);

        // Act
        var limited = arrow.Take(3);
        var result = await limited(1).ToList();

        // Assert
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReactiveKleisli_Skip_SkipsResults()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => Observable.Range(x, 5);

        // Act
        var skipped = arrow.Skip(2);
        var result = await skipped(1).ToList();

        // Assert
        result.Should().Equal(3, 4, 5);
    }

    [Fact]
    public async Task ReactiveKleisli_Buffer_GroupsResults()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => Observable.Range(x, 5);

        // Act
        var buffered = arrow.Buffer(2);
        var result = await buffered(1).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Equal(1, 2);
        result[1].Should().Equal(3, 4);
        result[2].Should().Equal(5);
    }

    [Fact]
    public async Task ReactiveKleisli_Scan_AccumulatesResults()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x => Observable.Range(x, 3);

        // Act
        var scanned = arrow.Scan(0, (acc, val) => acc + val);
        var result = await scanned(1).ToList();

        // Assert
        result.Should().Equal(1, 3, 6);
    }

    [Fact]
    public async Task ReactiveKleisli_Do_ExecutesSideEffect()
    {
        // Arrange
        var sideEffects = new List<int>();
        ReactiveKleisli<int, int> arrow = x => Observable.Range(x, 3);

        // Act
        var withSideEffect = arrow.Do(x => sideEffects.Add(x));
        var result = await withSideEffect(1).ToList();

        // Assert
        result.Should().Equal(1, 2, 3);
        sideEffects.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReactiveKleisli_Catch_HandlesErrors()
    {
        // Arrange
        ReactiveKleisli<int, int> arrow = x =>
            Observable.Throw<int>(new InvalidOperationException("Test error"));

        // Act
        var withErrorHandling = arrow.Catch<int, int>(ex => Observable.Return(-1));
        var result = await withErrorHandling(1).ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().Be(-1);
    }

    [Fact]
    public async Task ReactiveKleisli_MonadicLaw_LeftIdentity()
    {
        // Arrange - return >=> f = f
        var f = ReactiveKleisliExtensions.FromEnumerable<int, int>(
            x => new[] { x, x * 2 });
        var identity = ReactiveKleisliExtensions.Identity<int>();

        // Act
        var composed = identity.Compose(f);
        var direct = f;

        // Assert
        var composedResult = await composed(5).ToList();
        var directResult = await direct(5).ToList();
        composedResult.Should().Equal(directResult);
    }

    [Fact]
    public async Task ReactiveKleisli_MonadicLaw_RightIdentity()
    {
        // Arrange - f >=> return = f
        var f = ReactiveKleisliExtensions.FromEnumerable<int, int>(
            x => new[] { x, x * 2 });
        var identity = ReactiveKleisliExtensions.Identity<int>();

        // Act
        var composed = f.Compose(identity);
        var direct = f;

        // Assert
        var composedResult = await composed(5).ToList();
        var directResult = await direct(5).ToList();
        composedResult.Should().Equal(directResult);
    }

    [Fact]
    public async Task ReactiveKleisli_MonadicLaw_Associativity()
    {
        // Arrange - (f >=> g) >=> h = f >=> (g >=> h)
        ReactiveKleisli<int, int> f = x => Observable.Range(x, 2);
        ReactiveKleisli<int, int> g = x => Observable.Return(x * 2);
        ReactiveKleisli<int, string> h = x => Observable.Return(x.ToString());

        // Act
        var left = f.Compose(g).Compose(h);
        var right = f.Compose(g.Compose(h));

        // Assert
        var leftResult = await left(1).ToList();
        var rightResult = await right(1).ToList();
        leftResult.Should().Equal(rightResult);
    }

    [Fact]
    public async Task ReactiveKleisli_SupportsCovariance()
    {
        // Arrange - covariance allows derived type assignment
        ReactiveKleisli<int, string> specificArrow = x => Observable.Return(x.ToString());
        ReactiveKleisli<int, object> generalArrow = specificArrow;

        // Act
        var result = await generalArrow(42).ToList();

        // Assert
        result.Should().ContainSingle().Which.Should().Be("42");
    }

    [Fact]
    public async Task ReactiveKleisli_SupportsContravariance()
    {
        // Arrange - contravariance allows base type assignment for input
        ReactiveKleisli<object, int> generalArrow = obj => Observable.Return(obj.GetHashCode());
        ReactiveKleisli<string, int> specificArrow = generalArrow;

        // Act
        var result = await specificArrow("test").ToList();

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReactiveKleisli_SelectMany_WithResultSelector()
    {
        // Arrange
        ReactiveKleisli<int, int> source = x => Observable.Range(x, 2);

        // Act
        var result = await source.SelectMany(
            mid => Observable.Range(mid * 2, 2),
            (mid, final) => final).Invoke(1).ToList();

        // Assert
        result.Should().Equal(2, 3, 4, 5);
    }
}
