// <copyright file="ArrowTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Core;

using FluentAssertions;
using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the Arrow factory class covering identity, lift, and composition operations.
/// </summary>
[Trait("Category", "Unit")]
public class ArrowTests
{
    #region Identity Tests

    [Fact]
    public async Task Identity_ReturnsInputUnchanged()
    {
        // Arrange
        var identity = Arrow.Identity<int>();

        // Act
        var result = await identity(42);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task Identity_WorksWithReferenceTypes()
    {
        // Arrange
        var identity = Arrow.Identity<string>();

        // Act
        var result = await identity("hello");

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task Identity_PreservesNull()
    {
        // Arrange
        var identity = Arrow.Identity<string?>();

        // Act
        var result = await identity(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Identity_IsLeftIdentityForComposition()
    {
        // Left identity law: id >=> f ≡ f
        Step<int, int> f = x => Task.FromResult(x * 2);
        var identity = Arrow.Identity<int>();

        var composed = identity.Then(f);
        var composedResult = await composed(5);
        var directResult = await f(5);

        composedResult.Should().Be(directResult);
    }

    [Fact]
    public async Task Identity_IsRightIdentityForComposition()
    {
        // Right identity law: f >=> id ≡ f
        Step<int, int> f = x => Task.FromResult(x * 2);
        var identity = Arrow.Identity<int>();

        var composed = f.Then(identity);
        var composedResult = await composed(5);
        var directResult = await f(5);

        composedResult.Should().Be(directResult);
    }

    #endregion

    #region Lift Tests

    [Fact]
    public async Task Lift_WrapsFunction()
    {
        // Arrange
        var lifted = Arrow.Lift<int, int>(x => x * 2);

        // Act
        var result = await lifted(5);

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public async Task Lift_WorksWithTypeTransformation()
    {
        // Arrange
        var lifted = Arrow.Lift<int, string>(x => x.ToString());

        // Act
        var result = await lifted(42);

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public async Task Lift_PreservesReferenceEquality()
    {
        // Arrange
        var original = new List<int> { 1, 2, 3 };
        var lifted = Arrow.Lift<List<int>, List<int>>(x => x);

        // Act
        var result = await lifted(original);

        // Assert
        result.Should().BeSameAs(original);
    }

    #endregion

    #region LiftAsync Tests

    [Fact]
    public async Task LiftAsync_WrapsAsyncFunction()
    {
        // Arrange
        var lifted = Arrow.LiftAsync<int, int>(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Act
        var result = await lifted(5);

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public async Task LiftAsync_WorksWithTypeTransformation()
    {
        // Arrange
        var lifted = Arrow.LiftAsync<string, int>(async s =>
        {
            await Task.Delay(1);
            return s.Length;
        });

        // Act
        var result = await lifted("hello");

        // Assert
        result.Should().Be(5);
    }

    #endregion

    #region TryLift Tests

    [Fact]
    public async Task TryLift_ReturnsSuccessOnNoException()
    {
        // Arrange
        var triedArrow = Arrow.TryLift<int, int>(x => x * 2);

        // Act
        var result = await triedArrow(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public async Task TryLift_ReturnsFailureOnException()
    {
        // Arrange
        var triedArrow = Arrow.TryLift<string, int>(s => int.Parse(s));

        // Act
        var result = await triedArrow("not a number");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<FormatException>();
    }

    [Fact]
    public async Task TryLift_CapturesExceptionDetails()
    {
        // Arrange
        var triedArrow = Arrow.TryLift<int, int>(x =>
        {
            if (x < 0)
                throw new ArgumentException("Must be non-negative", nameof(x));
            return x;
        });

        // Act
        var result = await triedArrow(-5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("non-negative");
    }

    #endregion

    #region TryLiftAsync Tests

    [Fact]
    public async Task TryLiftAsync_ReturnsSuccessOnNoException()
    {
        // Arrange
        var triedArrow = Arrow.TryLiftAsync<int, int>(async x =>
        {
            await Task.Delay(1);
            return x * 2;
        });

        // Act
        var result = await triedArrow(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public async Task TryLiftAsync_ReturnsFailureOnException()
    {
        // Arrange
        var triedArrow = Arrow.TryLiftAsync<int, int>(async _ =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("Async failure");
        });

        // Act
        var result = await triedArrow(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidOperationException>();
        result.Error.Message.Should().Be("Async failure");
    }

    #endregion

    #region Success Arrow Tests

    [Fact]
    public async Task Success_AlwaysReturnsSuccessWithValue()
    {
        // Arrange
        var successArrow = Arrow.Success<string, int, string>(42);

        // Act
        var result = await successArrow("any input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task Success_IgnoresInput()
    {
        // Arrange
        var successArrow = Arrow.Success<int, string, Exception>("constant");

        // Act
        var result1 = await successArrow(1);
        var result2 = await successArrow(100);
        var result3 = await successArrow(-999);

        // Assert
        result1.Value.Should().Be("constant");
        result2.Value.Should().Be("constant");
        result3.Value.Should().Be("constant");
    }

    #endregion

    #region Failure Arrow Tests

    [Fact]
    public async Task Failure_AlwaysReturnsFailureWithError()
    {
        // Arrange
        var failureArrow = Arrow.Failure<string, int, string>("always fails");

        // Act
        var result = await failureArrow("any input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("always fails");
    }

    [Fact]
    public async Task Failure_IgnoresInput()
    {
        // Arrange
        var failureArrow = Arrow.Failure<int, string, string>("error");

        // Act
        var result1 = await failureArrow(1);
        var result2 = await failureArrow(100);

        // Assert
        result1.Error.Should().Be("error");
        result2.Error.Should().Be("error");
    }

    #endregion

    #region Some Arrow Tests

    [Fact]
    public async Task Some_AlwaysReturnsSomeWithValue()
    {
        // Arrange
        var someArrow = Arrow.Some<string, int>(42);

        // Act
        var result = await someArrow("any input");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task Some_IgnoresInput()
    {
        // Arrange
        var someArrow = Arrow.Some<int, string>("constant");

        // Act
        var result1 = await someArrow(1);
        var result2 = await someArrow(100);

        // Assert
        result1.Value.Should().Be("constant");
        result2.Value.Should().Be("constant");
    }

    #endregion

    #region None Arrow Tests

    [Fact]
    public async Task None_AlwaysReturnsNone()
    {
        // Arrange
        var noneArrow = Arrow.None<string, int>();

        // Act
        var result = await noneArrow("any input");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task None_IgnoresInput()
    {
        // Arrange
        var noneArrow = Arrow.None<int, string>();

        // Act
        var result1 = await noneArrow(1);
        var result2 = await noneArrow(100);

        // Assert
        result1.HasValue.Should().BeFalse();
        result2.HasValue.Should().BeFalse();
    }

    #endregion

    #region Compose Tests

    [Fact]
    public async Task Compose_CreatesComposerForArrows()
    {
        // Arrange
        var composer = Arrow.Compose<int, int, int>();
        Kleisli<int, int> f = x => Task.FromResult(x + 1);
        Kleisli<int, int> g = x => Task.FromResult(x * 2);

        // Act
        var composed = composer(f, g);
        var result = await composed(5);

        // Assert
        result.Should().Be(12); // (5 + 1) * 2
    }

    [Fact]
    public async Task Compose_SupportsTypeTransformation()
    {
        // Arrange
        var composer = Arrow.Compose<int, string, int>();
        Kleisli<int, string> f = x => Task.FromResult(x.ToString());
        Kleisli<string, int> g = s => Task.FromResult(s.Length);

        // Act
        var composed = composer(f, g);
        var result = await composed(12345);

        // Assert
        result.Should().Be(5); // "12345".Length
    }

    #endregion

    #region ComposeWith Curried Tests

    [Fact]
    public async Task ComposeWith_ReturnsPartiallyAppliedFunction()
    {
        // Arrange
        Kleisli<int, int> f = x => Task.FromResult(x + 1);
        Kleisli<int, int> g = x => Task.FromResult(x * 2);

        // Act
        var partiallyApplied = Arrow.ComposeWith<int, int, int>(f);
        var composed = partiallyApplied(g);
        var result = await composed(5);

        // Assert
        result.Should().Be(12); // (5 + 1) * 2
    }

    [Fact]
    public async Task ComposeWith_CanBeReusedForMultipleCompositions()
    {
        // Arrange
        Kleisli<int, int> f = x => Task.FromResult(x + 1);
        var partiallyApplied = Arrow.ComposeWith<int, int, int>(f);

        Kleisli<int, int> g1 = x => Task.FromResult(x * 2);
        Kleisli<int, int> g2 = x => Task.FromResult(x * 3);

        // Act
        var composed1 = partiallyApplied(g1);
        var composed2 = partiallyApplied(g2);

        var result1 = await composed1(5);
        var result2 = await composed2(5);

        // Assert
        result1.Should().Be(12); // (5 + 1) * 2
        result2.Should().Be(18); // (5 + 1) * 3
    }

    #endregion
}
