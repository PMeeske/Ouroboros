// <copyright file="ResultExtensionsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Core;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for ResultExtensions covering LINQ operations, combination, and utility methods.
/// </summary>
[Trait("Category", "Unit")]
public class ResultExtensionsTests
{
    #region Select Tests (Map alias)

    [Fact]
    public void Select_OnSuccess_TransformsValue()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var selected = result.Select(x => x * 2);

        // Assert
        selected.IsSuccess.Should().BeTrue();
        selected.Value.Should().Be(10);
    }

    [Fact]
    public void Select_OnFailure_PreservesError()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var selected = result.Select(x => x * 2);

        // Assert
        selected.IsFailure.Should().BeTrue();
        selected.Error.Should().Be("error");
    }

    #endregion

    #region SelectMany Tests (Bind alias)

    [Fact]
    public void SelectMany_OnSuccess_AppliesFunction()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var selected = result.SelectMany(x => Result<int, string>.Success(x * 2));

        // Assert
        selected.IsSuccess.Should().BeTrue();
        selected.Value.Should().Be(10);
    }

    [Fact]
    public void SelectMany_OnFailure_PreservesError()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var selected = result.SelectMany(x => Result<int, string>.Success(x * 2));

        // Assert
        selected.IsFailure.Should().BeTrue();
        selected.Error.Should().Be("error");
    }

    [Fact]
    public void SelectMany_WithResultSelector_CombinesValues()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var selected = result.SelectMany(
            x => Result<int, string>.Success(x + 3),
            (original, intermediate) => original * intermediate);

        // Assert
        selected.IsSuccess.Should().BeTrue();
        selected.Value.Should().Be(40); // 5 * (5 + 3) = 5 * 8 = 40
    }

    [Fact]
    public void SelectMany_WithResultSelector_PropagatesFirstError()
    {
        // Arrange
        var result = Result<int, string>.Failure("first error");

        // Act
        var selected = result.SelectMany(
            x => Result<int, string>.Success(x + 3),
            (original, intermediate) => original * intermediate);

        // Assert
        selected.IsFailure.Should().BeTrue();
        selected.Error.Should().Be("first error");
    }

    [Fact]
    public void SelectMany_WithResultSelector_PropagatesSecondError()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var selected = result.SelectMany(
            _ => Result<int, string>.Failure("second error"),
            (original, intermediate) => original * intermediate);

        // Assert
        selected.IsFailure.Should().BeTrue();
        selected.Error.Should().Be("second error");
    }

    #endregion

    #region Combine Tests

    [Fact]
    public void Combine_TwoSuccesses_ReturnsTuple()
    {
        // Arrange
        var first = Result<int, string>.Success(5);
        var second = Result<string, string>.Success("hello");

        // Act
        var combined = first.Combine(second);

        // Assert
        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().Be((5, "hello"));
    }

    [Fact]
    public void Combine_FirstFailure_PropagatesError()
    {
        // Arrange
        var first = Result<int, string>.Failure("first error");
        var second = Result<string, string>.Success("hello");

        // Act
        var combined = first.Combine(second);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().Be("first error");
    }

    [Fact]
    public void Combine_SecondFailure_PropagatesError()
    {
        // Arrange
        var first = Result<int, string>.Success(5);
        var second = Result<string, string>.Failure("second error");

        // Act
        var combined = first.Combine(second);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().Be("second error");
    }

    [Fact]
    public void Combine_ThreeSuccesses_ReturnsTuple()
    {
        // Arrange
        var first = Result<int, string>.Success(1);
        var second = Result<string, string>.Success("two");
        var third = Result<double, string>.Success(3.0);

        // Act
        var combined = first.Combine(second, third);

        // Assert
        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().Be((1, "two", 3.0));
    }

    [Fact]
    public void Combine_ThreeWithMiddleFailure_PropagatesError()
    {
        // Arrange
        var first = Result<int, string>.Success(1);
        var second = Result<string, string>.Failure("middle error");
        var third = Result<double, string>.Success(3.0);

        // Act
        var combined = first.Combine(second, third);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().Be("middle error");
    }

    [Fact]
    public void Combine_ThreeWithLastFailure_PropagatesError()
    {
        // Arrange
        var first = Result<int, string>.Success(1);
        var second = Result<string, string>.Success("two");
        var third = Result<double, string>.Failure("last error");

        // Act
        var combined = first.Combine(second, third);

        // Assert
        combined.IsFailure.Should().BeTrue();
        combined.Error.Should().Be("last error");
    }

    #endregion

    #region Where Tests

    [Fact]
    public void Where_PredicatePasses_ReturnsOriginal()
    {
        // Arrange
        var result = Result<int, string>.Success(10);

        // Act
        var filtered = result.Where(x => x > 5, "Value must be greater than 5");

        // Assert
        filtered.IsSuccess.Should().BeTrue();
        filtered.Value.Should().Be(10);
    }

    [Fact]
    public void Where_PredicateFails_ReturnsError()
    {
        // Arrange
        var result = Result<int, string>.Success(3);

        // Act
        var filtered = result.Where(x => x > 5, "Value must be greater than 5");

        // Assert
        filtered.IsFailure.Should().BeTrue();
        filtered.Error.Should().Be("Value must be greater than 5");
    }

    [Fact]
    public void Where_OnFailure_PreservesOriginalError()
    {
        // Arrange
        var result = Result<int, string>.Failure("original error");

        // Act
        var filtered = result.Where(x => x > 5, "predicate error");

        // Assert
        filtered.IsFailure.Should().BeTrue();
        filtered.Error.Should().Be("original error");
    }

    #endregion

    #region Tap Tests

    [Fact]
    public void Tap_OnSuccess_ExecutesAction()
    {
        // Arrange
        var result = Result<int, string>.Success(42);
        var capturedValue = 0;

        // Act
        var tapped = result.Tap(x => capturedValue = x);

        // Assert
        tapped.IsSuccess.Should().BeTrue();
        tapped.Value.Should().Be(42);
        capturedValue.Should().Be(42);
    }

    [Fact]
    public void Tap_OnFailure_DoesNotExecuteAction()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");
        var wasCalled = false;

        // Act
        var tapped = result.Tap(_ => wasCalled = true);

        // Assert
        tapped.IsFailure.Should().BeTrue();
        wasCalled.Should().BeFalse();
    }

    [Fact]
    public void Tap_ReturnsOriginalResult()
    {
        // Arrange
        var original = Result<int, string>.Success(42);

        // Act
        var tapped = original.Tap(_ => { });

        // Assert
        tapped.Should().Be(original);
    }

    #endregion

    #region TapError Tests

    [Fact]
    public void TapError_OnFailure_ExecutesAction()
    {
        // Arrange
        var result = Result<int, string>.Failure("error message");
        var capturedError = "";

        // Act
        var tapped = result.TapError(e => capturedError = e);

        // Assert
        tapped.IsFailure.Should().BeTrue();
        capturedError.Should().Be("error message");
    }

    [Fact]
    public void TapError_OnSuccess_DoesNotExecuteAction()
    {
        // Arrange
        var result = Result<int, string>.Success(42);
        var wasCalled = false;

        // Act
        var tapped = result.TapError(_ => wasCalled = true);

        // Assert
        tapped.IsSuccess.Should().BeTrue();
        wasCalled.Should().BeFalse();
    }

    [Fact]
    public void TapError_ReturnsOriginalResult()
    {
        // Arrange
        var original = Result<int, string>.Failure("error");

        // Act
        var tapped = original.TapError(_ => { });

        // Assert
        tapped.Should().Be(original);
    }

    #endregion

    #region OrElse Tests

    [Fact]
    public void OrElse_OnSuccess_ReturnsValue()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var value = result.OrElse(0);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void OrElse_OnFailure_ReturnsFallback()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var value = result.OrElse(99);

        // Assert
        value.Should().Be(99);
    }

    [Fact]
    public void OrElse_WithFunc_OnSuccess_ReturnsValue()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var value = result.OrElse(error => error.Length);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void OrElse_WithFunc_OnFailure_ComputesFallback()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var value = result.OrElse(error => error.Length * 10);

        // Assert
        value.Should().Be(50); // "error".Length * 10
    }

    #endregion

    #region ToStringError Tests

    [Fact]
    public void ToStringError_OnSuccess_PreservesValue()
    {
        // Arrange
        var result = Result<int, Exception>.Success(42);

        // Act
        var converted = result.ToStringError();

        // Assert
        converted.IsSuccess.Should().BeTrue();
        converted.Value.Should().Be(42);
    }

    [Fact]
    public void ToStringError_OnFailure_ExtractsMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");
        var result = Result<int, Exception>.Failure(exception);

        // Act
        var converted = result.ToStringError();

        // Assert
        converted.IsFailure.Should().BeTrue();
        converted.Error.Should().Be("Something went wrong");
    }

    #endregion

    #region Pipe Tests

    [Fact]
    public void Pipe_AllSuccessful_AppliesAllTransformations()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var piped = result.Pipe(
            x => Result<int, string>.Success(x + 1),
            x => Result<int, string>.Success(x * 2),
            x => Result<int, string>.Success(x - 3));

        // Assert
        piped.IsSuccess.Should().BeTrue();
        piped.Value.Should().Be(9); // ((5 + 1) * 2) - 3 = 9
    }

    [Fact]
    public void Pipe_FirstTransformationFails_ShortCircuits()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var piped = result.Pipe(
            _ => Result<int, string>.Failure("first error"),
            x => Result<int, string>.Success(x * 2),
            x => Result<int, string>.Success(x - 3));

        // Assert
        piped.IsFailure.Should().BeTrue();
        piped.Error.Should().Be("first error");
    }

    [Fact]
    public void Pipe_MiddleTransformationFails_ShortCircuits()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var piped = result.Pipe(
            x => Result<int, string>.Success(x + 1),
            _ => Result<int, string>.Failure("middle error"),
            x => Result<int, string>.Success(x - 3));

        // Assert
        piped.IsFailure.Should().BeTrue();
        piped.Error.Should().Be("middle error");
    }

    [Fact]
    public void Pipe_InitialFailure_PropagatesError()
    {
        // Arrange
        var result = Result<int, string>.Failure("initial error");

        // Act
        var piped = result.Pipe(
            x => Result<int, string>.Success(x + 1),
            x => Result<int, string>.Success(x * 2));

        // Assert
        piped.IsFailure.Should().BeTrue();
        piped.Error.Should().Be("initial error");
    }

    [Fact]
    public void Pipe_WithEmptyTransformations_ReturnsOriginal()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var piped = result.Pipe();

        // Assert
        piped.IsSuccess.Should().BeTrue();
        piped.Value.Should().Be(42);
    }

    #endregion

    #region LINQ Query Syntax Tests

    [Fact]
    public void LinqQuerySyntax_SelectMany_Works()
    {
        // Arrange
        var result1 = Result<int, string>.Success(5);
        var result2 = Result<int, string>.Success(3);

        // Act - using query syntax
        var combined =
            from x in result1
            from y in result2
            select x + y;

        // Assert
        combined.IsSuccess.Should().BeTrue();
        combined.Value.Should().Be(8);
    }

    [Fact]
    public void LinqQuerySyntax_Select_Works()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act - using query syntax
        var mapped =
            from x in result
            select x * 2;

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    #endregion
}
