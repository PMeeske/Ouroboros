// <copyright file="KleisliExtensionsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Core;

using FluentAssertions;
using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for KleisliExtensions covering composition, mapping, and monadic operations.
/// </summary>
[Trait("Category", "Unit")]
public class KleisliExtensionsTests
{
    #region Step Then Composition Tests

    [Fact]
    public async Task Step_Then_ComposesArrows()
    {
        // Arrange
        Step<int, int> double_ = x => Task.FromResult(x * 2);
        Step<int, int> addTen = x => Task.FromResult(x + 10);

        // Act
        var composed = double_.Then(addTen);
        var result = await composed(5);

        // Assert
        result.Should().Be(20); // (5 * 2) + 10
    }

    [Fact]
    public async Task Step_Then_IsAssociative()
    {
        // Arrange
        Step<int, int> f = x => Task.FromResult(x + 1);
        Step<int, int> g = x => Task.FromResult(x * 2);
        Step<int, int> h = x => Task.FromResult(x - 3);

        // Act
        var leftAssoc = f.Then(g).Then(h);
        var rightAssoc = f.Then(g.Then(h));

        var leftResult = await leftAssoc(5);
        var rightResult = await rightAssoc(5);

        // Assert - both should produce same result
        leftResult.Should().Be(rightResult);
        leftResult.Should().Be(9); // ((5+1)*2)-3 = 9
    }

    [Fact]
    public async Task Step_Then_WithTypeTransformation()
    {
        // Arrange
        Step<int, string> intToString = x => Task.FromResult(x.ToString());
        Step<string, int> stringLength = s => Task.FromResult(s.Length);

        // Act
        var composed = intToString.Then(stringLength);
        var result = await composed(12345);

        // Assert
        result.Should().Be(5); // "12345".Length
    }

    #endregion

    #region Kleisli Then Composition Tests

    [Fact]
    public async Task Kleisli_Then_ComposesArrows()
    {
        // Arrange
        Kleisli<string, int> parseString = s => Task.FromResult(int.Parse(s));
        Kleisli<int, string> intToHex = i => Task.FromResult($"0x{i:X}");

        // Act
        var composed = parseString.Then(intToHex);
        var result = await composed("255");

        // Assert
        result.Should().Be("0xFF");
    }

    [Fact]
    public async Task MixedComposition_StepToKleisli()
    {
        // Arrange
        Step<int, double> stepArrow = x => Task.FromResult(x / 2.0);
        Kleisli<double, string> kleisliArrow = d => Task.FromResult($"Result: {d.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");

        // Act
        var composed = stepArrow.Then(kleisliArrow);
        var result = await composed(10);

        // Assert
        result.Should().Be("Result: 5.00");
    }

    [Fact]
    public async Task MixedComposition_KleisliToStep()
    {
        // Arrange
        Kleisli<string, int> kleisliArrow = s => Task.FromResult(s.Length);
        Step<int, bool> stepArrow = i => Task.FromResult(i > 5);

        // Act
        var composed = kleisliArrow.Then(stepArrow);
        var result = await composed("Hello World");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Map Tests

    [Fact]
    public async Task Step_Map_TransformsResult()
    {
        // Arrange
        Step<int, int> arrow = x => Task.FromResult(x * 2);

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = await mapped(5);

        // Assert
        result.Should().Be("10");
    }

    [Fact]
    public async Task Kleisli_Map_TransformsResult()
    {
        // Arrange
        Kleisli<string, int> arrow = s => Task.FromResult(s.Length);

        // Act
        var mapped = arrow.Map(i => i > 3);
        var result = await mapped("Hello");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Step_MapAsync_TransformsWithAsyncFunction()
    {
        // Arrange
        Step<int, int> arrow = x => Task.FromResult(x * 2);

        // Act
        var mapped = arrow.MapAsync(async x =>
        {
            await Task.Delay(1);
            return x.ToString();
        });
        var result = await mapped(5);

        // Assert
        result.Should().Be("10");
    }

    [Fact]
    public async Task Kleisli_MapAsync_TransformsWithAsyncFunction()
    {
        // Arrange
        Kleisli<string, int> arrow = s => Task.FromResult(s.Length);

        // Act
        var mapped = arrow.MapAsync(async i =>
        {
            await Task.Delay(1);
            return i * 2;
        });
        var result = await mapped("Hello");

        // Assert
        result.Should().Be(10);
    }

    #endregion

    #region Tap Tests

    [Fact]
    public async Task Step_Tap_ExecutesSideEffect()
    {
        // Arrange
        Step<int, int> arrow = x => Task.FromResult(x * 2);
        var sideEffectValue = 0;

        // Act
        var tapped = arrow.Tap(x => sideEffectValue = x);
        var result = await tapped(5);

        // Assert
        result.Should().Be(10);
        sideEffectValue.Should().Be(10);
    }

    [Fact]
    public async Task Step_Tap_DoesNotModifyResult()
    {
        // Arrange
        Step<string, string> arrow = s => Task.FromResult(s.ToUpper());
        var callCount = 0;

        // Act
        var tapped = arrow.Tap(_ => callCount++);
        var result = await tapped("hello");

        // Assert
        result.Should().Be("HELLO");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task Kleisli_Tap_ExecutesSideEffect()
    {
        // Arrange
        Kleisli<int, string> arrow = x => Task.FromResult($"Number: {x}");
        var capturedValue = "";

        // Act
        var tapped = arrow.Tap(s => capturedValue = s);
        var result = await tapped(42);

        // Assert
        result.Should().Be("Number: 42");
        capturedValue.Should().Be("Number: 42");
    }

    #endregion

    #region Catch Tests

    [Fact]
    public async Task Step_Catch_ReturnsSuccessOnNoException()
    {
        // Arrange
        Step<int, int> arrow = x => Task.FromResult(x * 2);

        // Act
        var caught = arrow.Catch();
        var result = await caught(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public async Task Step_Catch_ReturnsFailureOnException()
    {
        // Arrange
        Step<int, int> arrow = x => throw new InvalidOperationException("Test error");

        // Act
        var caught = arrow.Catch();
        var result = await caught(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidOperationException>();
        result.Error.Message.Should().Be("Test error");
    }

    [Fact]
    public async Task Kleisli_Catch_ReturnsSuccessOnNoException()
    {
        // Arrange
        Kleisli<string, int> arrow = s => Task.FromResult(s.Length);

        // Act
        var caught = arrow.Catch();
        var result = await caught("Hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public async Task Kleisli_Catch_ReturnsFailureOnException()
    {
        // Arrange
        Kleisli<string, int> arrow = s => throw new ArgumentException("Invalid string");

        // Act
        var caught = arrow.Catch();
        var result = await caught("test");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ArgumentException>();
    }

    #endregion

    #region KleisliResult Then Tests

    [Fact]
    public async Task KleisliResult_Then_PropagatesSuccess()
    {
        // Arrange
        KleisliResult<int, int, string> first = x => Task.FromResult(Result<int, string>.Success(x * 2));
        KleisliResult<int, int, string> second = x => Task.FromResult(Result<int, string>.Success(x + 10));

        // Act
        var composed = first.Then(second);
        var result = await composed(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(20); // (5 * 2) + 10
    }

    [Fact]
    public async Task KleisliResult_Then_PropagatesFirstError()
    {
        // Arrange
        KleisliResult<int, int, string> first = _ => Task.FromResult(Result<int, string>.Failure("First error"));
        KleisliResult<int, int, string> second = x => Task.FromResult(Result<int, string>.Success(x + 10));

        // Act
        var composed = first.Then(second);
        var result = await composed(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("First error");
    }

    [Fact]
    public async Task KleisliResult_Then_PropagatesSecondError()
    {
        // Arrange
        KleisliResult<int, int, string> first = x => Task.FromResult(Result<int, string>.Success(x * 2));
        KleisliResult<int, int, string> second = _ => Task.FromResult(Result<int, string>.Failure("Second error"));

        // Act
        var composed = first.Then(second);
        var result = await composed(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Second error");
    }

    #endregion

    #region KleisliResult Map Tests

    [Fact]
    public async Task KleisliResult_Map_TransformsSuccess()
    {
        // Arrange
        KleisliResult<int, int, string> arrow = x => Task.FromResult(Result<int, string>.Success(x * 2));

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = await mapped(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("10");
    }

    [Fact]
    public async Task KleisliResult_Map_PreservesError()
    {
        // Arrange
        KleisliResult<int, int, string> arrow = _ => Task.FromResult(Result<int, string>.Failure("Error"));

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = await mapped(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Error");
    }

    #endregion

    #region KleisliResult Tap Tests

    [Fact]
    public async Task KleisliResult_Tap_ExecutesSideEffectOnSuccess()
    {
        // Arrange
        KleisliResult<int, int, string> arrow = x => Task.FromResult(Result<int, string>.Success(x * 2));
        var capturedValue = 0;

        // Act
        var tapped = arrow.Tap(x => capturedValue = x);
        var result = await tapped(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
        capturedValue.Should().Be(10);
    }

    [Fact]
    public async Task KleisliResult_Tap_DoesNotExecuteOnError()
    {
        // Arrange
        KleisliResult<int, int, string> arrow = _ => Task.FromResult(Result<int, string>.Failure("Error"));
        var wasCalled = false;

        // Act
        var tapped = arrow.Tap(_ => wasCalled = true);
        var result = await tapped(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        wasCalled.Should().BeFalse();
    }

    #endregion

    #region KleisliOption Then Tests

    [Fact]
    public async Task KleisliOption_Then_PropagatesSome()
    {
        // Arrange
        KleisliOption<int, int> first = x => Task.FromResult(Option<int>.Some(x * 2));
        KleisliOption<int, int> second = x => Task.FromResult(Option<int>.Some(x + 10));

        // Act
        var composed = first.Then(second);
        var result = await composed(5);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(20);
    }

    [Fact]
    public async Task KleisliOption_Then_PropagatesFirstNone()
    {
        // Arrange
        KleisliOption<int, int> first = _ => Task.FromResult(Option<int>.None());
        KleisliOption<int, int> second = x => Task.FromResult(Option<int>.Some(x + 10));

        // Act
        var composed = first.Then(second);
        var result = await composed(5);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task KleisliOption_Then_PropagatesSecondNone()
    {
        // Arrange
        KleisliOption<int, int> first = x => Task.FromResult(Option<int>.Some(x * 2));
        KleisliOption<int, int> second = _ => Task.FromResult(Option<int>.None());

        // Act
        var composed = first.Then(second);
        var result = await composed(5);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region KleisliOption Map Tests

    [Fact]
    public async Task KleisliOption_Map_TransformsSome()
    {
        // Arrange
        KleisliOption<int, int> arrow = x => Task.FromResult(Option<int>.Some(x * 2));

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = await mapped(5);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("10");
    }

    [Fact]
    public async Task KleisliOption_Map_PreservesNone()
    {
        // Arrange
        KleisliOption<int, int> arrow = _ => Task.FromResult(Option<int>.None());

        // Act
        var mapped = arrow.Map(x => x.ToString());
        var result = await mapped(5);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region KleisliOption ToResult Tests

    [Fact]
    public async Task KleisliOption_ToResult_ConvertsSomeToSuccess()
    {
        // Arrange
        KleisliOption<int, int> arrow = x => Task.FromResult(Option<int>.Some(x * 2));

        // Act
        var asResult = arrow.ToResult("Value was None");
        var result = await asResult(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public async Task KleisliOption_ToResult_ConvertsNoneToFailure()
    {
        // Arrange
        KleisliOption<int, int> arrow = _ => Task.FromResult(Option<int>.None());

        // Act
        var asResult = arrow.ToResult("Value was None");
        var result = await asResult(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Value was None");
    }

    #endregion

    #region ComposeWith Tests

    [Fact]
    public async Task ComposeWith_ComposesUsingProvidedComposer()
    {
        // Arrange
        Kleisli<int, int> f = x => Task.FromResult(x + 1);
        Kleisli<int, int> g = x => Task.FromResult(x * 2);
        var composer = Arrow.Compose<int, int, int>();

        // Act
        var composed = f.ComposeWith(composer, g);
        var result = await composed(5);

        // Assert
        result.Should().Be(12); // (5 + 1) * 2
    }

    #endregion

    #region PartialCompose Tests

    [Fact]
    public async Task PartialCompose_CreatesPartiallyAppliedComposition()
    {
        // Arrange
        Kleisli<int, int> f = x => Task.FromResult(x + 1);
        Kleisli<int, int> g = x => Task.FromResult(x * 2);

        // Act
        var partiallyApplied = f.PartialCompose<int, int, int>();
        var composed = partiallyApplied(g);
        var result = await composed(5);

        // Assert
        result.Should().Be(12); // (5 + 1) * 2
    }

    #endregion

    #region Compose with Function Tests

    [Fact]
    public async Task Compose_WithFunction_AppliesComposition()
    {
        // Arrange
        Kleisli<int, int> f = x => Task.FromResult(x + 1);

        // Act
        var composed = f.Compose<int, int, int>(first =>
            async input => (await first(input)) * 3);
        var result = await composed(5);

        // Assert
        result.Should().Be(18); // (5 + 1) * 3
    }

    #endregion
}
