// <copyright file="ResultTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Core;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Comprehensive tests for the Result monad implementation.
/// Tests monadic laws, error handling patterns, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
public class ResultTests
{
    #region Creation Tests

    [Fact]
    public void Ok_CreatesSuccessResult()
    {
        // Arrange & Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Error_CreatesFailureResult()
    {
        // Arrange & Act
        var result = Result<int>.Failure("Something went wrong");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public void Success_WithNullValue_CreatesSuccessWithNullValue()
    {
        // Arrange & Act
        var result = Result<string?>.Success(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Failure_WithEmptyError_CreatesFailureWithEmptyError()
    {
        // Arrange & Act
        var result = Result<int>.Failure(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeEmpty();
    }

    #endregion

    #region Value Access Tests

    [Fact]
    public void Value_OnSuccess_ReturnsValue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var value = result.Value;

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result<int>.Failure("error");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_OnFailure_ReturnsError()
    {
        // Arrange
        var result = Result<int>.Failure("error message");

        // Act
        var error = result.Error;

        // Assert
        error.Should().Be("error message");
    }

    [Fact]
    public void Error_OnSuccess_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        // Arrange
        var result = Result<int>.Failure("original error");

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be("original error");
    }

    [Fact]
    public void Map_WithTypeConversion_ConvertsSuccessfully()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0, 0)]
    [InlineData(-5, -10)]
    [InlineData(100, 200)]
    public void Map_WithVariousValues_TransformsCorrectly(int input, int expected)
    {
        // Arrange
        var result = Result<int>.Success(input);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.Value.Should().Be(expected);
    }

    #endregion

    #region Bind Tests

    [Fact]
    public void Bind_OnSuccess_AppliesFunction()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var bound = result.Bind(x => Result<int>.Success(x * 2));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be(10);
    }

    [Fact]
    public void Bind_OnFailure_ShortCircuits()
    {
        // Arrange
        var result = Result<int>.Failure("original error");

        // Act
        var bound = result.Bind(x => Result<int>.Success(x * 2));

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be("original error");
    }

    [Fact]
    public void Bind_CanChainOperations()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var chained = result
            .Bind(x => Result<int>.Success(x * 2))
            .Bind(x => Result<int>.Success(x + 3))
            .Bind(x => Result<int>.Success(x - 1));

        // Assert
        chained.IsSuccess.Should().BeTrue();
        chained.Value.Should().Be(12); // (5 * 2) + 3 - 1
    }

    [Fact]
    public void Bind_ShortCircuitsOnFirstError()
    {
        // Arrange
        var result = Result<int>.Success(5);

        // Act
        var chained = result
            .Bind(x => Result<int>.Success(x * 2))
            .Bind(x => Result<int>.Failure("first error"))
            .Bind(x => Result<int>.Success(x + 100)); // Should not execute

        // Assert
        chained.IsFailure.Should().BeTrue();
        chained.Error.Should().Be("first error");
    }

    [Fact]
    public void Bind_WithTypeConversion_WorksCorrectly()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var bound = result.Bind(x => Result<string>.Success(x.ToString()));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("42");
    }

    #endregion

    #region Match Tests

    [Fact]
    public void Match_OnSuccess_ExecutesSuccessFunction()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var output = result.Match(
            onSuccess: x => $"Value: {x}",
            onFailure: err => $"Error: {err}");

        // Assert
        output.Should().Be("Value: 42");
    }

    [Fact]
    public void Match_OnFailure_ExecutesFailureFunction()
    {
        // Arrange
        var result = Result<int>.Failure("something went wrong");

        // Act
        var output = result.Match(
            onSuccess: x => $"Value: {x}",
            onFailure: err => $"Error: {err}");

        // Assert
        output.Should().Be("Error: something went wrong");
    }

    [Fact]
    public void Match_WithAction_OnSuccess_ExecutesSuccessAction()
    {
        // Arrange
        var result = Result<int>.Success(42);
        var wasCalled = false;
        var capturedValue = 0;

        // Act
        result.Match(
            onSuccess: x => { wasCalled = true; capturedValue = x; },
            onFailure: _ => { });

        // Assert
        wasCalled.Should().BeTrue();
        capturedValue.Should().Be(42);
    }

    [Fact]
    public void Match_WithAction_OnFailure_ExecutesFailureAction()
    {
        // Arrange
        var result = Result<int>.Failure("error");
        var wasCalled = false;
        var capturedError = string.Empty;

        // Act
        result.Match(
            onSuccess: _ => { },
            onFailure: err => { wasCalled = true; capturedError = err; });

        // Assert
        wasCalled.Should().BeTrue();
        capturedError.Should().Be("error");
    }

    #endregion

    #region GetValueOrDefault Tests

    [Fact]
    public void GetValueOrDefault_OnSuccess_ReturnsValue()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var value = result.GetValueOrDefault(0);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void GetValueOrDefault_OnFailure_ReturnsDefault()
    {
        // Arrange
        var result = Result<int>.Failure("error");

        // Act
        var value = result.GetValueOrDefault(99);

        // Assert
        value.Should().Be(99);
    }

    [Fact]
    public void GetValueOrDefault_OnFailure_ReturnsSpecifiedDefault()
    {
        // Arrange
        var result = Result<string>.Failure("error");

        // Act
        var value = result.GetValueOrDefault("fallback");

        // Assert
        value.Should().Be("fallback");
    }

    #endregion

    #region ToOption Tests

    [Fact]
    public void ToOption_OnSuccess_ReturnsSome()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var option = result.ToOption();

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(42);
    }

    [Fact]
    public void ToOption_OnFailure_ReturnsNone()
    {
        // Arrange
        var result = Result<int>.Failure("error");

        // Act
        var option = result.ToOption();

        // Assert
        option.HasValue.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_TwoSuccessResultsWithSameValue_ReturnsTrue()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(42);

        // Assert
        result1.Equals(result2).Should().BeTrue();
        (result1 == result2).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoSuccessResultsWithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(43);

        // Assert
        result1.Equals(result2).Should().BeFalse();
        (result1 != result2).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoFailureResultsWithSameError_ReturnsTrue()
    {
        // Arrange
        var result1 = Result<int>.Failure("error");
        var result2 = Result<int>.Failure("error");

        // Assert
        result1.Equals(result2).Should().BeTrue();
    }

    [Fact]
    public void Equals_SuccessAndFailure_ReturnsFalse()
    {
        // Arrange
        var success = Result<int>.Success(42);
        var failure = Result<int>.Failure("error");

        // Assert
        success.Equals(failure).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_TwoEqualResults_ReturnSameHashCode()
    {
        // Arrange
        var result1 = Result<int>.Success(42);
        var result2 = Result<int>.Success(42);

        // Assert
        result1.GetHashCode().Should().Be(result2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_OnSuccess_ReturnsSuccessString()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var str = result.ToString();

        // Assert
        str.Should().Contain("Success");
        str.Should().Contain("42");
    }

    [Fact]
    public void ToString_OnFailure_ReturnsFailureString()
    {
        // Arrange
        var result = Result<int>.Failure("error message");

        // Act
        var str = result.ToString();

        // Assert
        str.Should().Contain("Failure");
        str.Should().Contain("error message");
    }

    #endregion

    #region Monadic Laws Tests

    [Fact]
    public void Result_LeftIdentity_Holds()
    {
        // Left identity: return a >>= f ≡ f a
        var a = 42;
        Func<int, Result<int>> f = x => Result<int>.Success(x * 2);

        var left = Result<int>.Success(a).Bind(f);
        var right = f(a);

        left.Value.Should().Be(right.Value);
        left.IsSuccess.Should().Be(right.IsSuccess);
    }

    [Fact]
    public void Result_RightIdentity_Holds()
    {
        // Right identity: m >>= return ≡ m
        var m = Result<int>.Success(42);

        var result = m.Bind(x => Result<int>.Success(x));

        result.Value.Should().Be(m.Value);
        result.IsSuccess.Should().Be(m.IsSuccess);
    }

    [Fact]
    public void Result_Associativity_Holds()
    {
        // Associativity: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Result<int>.Success(5);
        Func<int, Result<int>> f = x => Result<int>.Success(x * 2);
        Func<int, Result<int>> g = x => Result<int>.Success(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.Value.Should().Be(right.Value);
    }

    [Fact]
    public void Result_AssociativityWithFailure_Holds()
    {
        // Associativity should hold even when f returns failure
        var m = Result<int>.Success(5);
        Func<int, Result<int>> f = x => x > 3
            ? Result<int>.Failure("too large")
            : Result<int>.Success(x * 2);
        Func<int, Result<int>> g = x => Result<int>.Success(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.IsFailure.Should().Be(right.IsFailure);
        left.Error.Should().Be(right.Error);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccessResult()
    {
        // Arrange & Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    #endregion

    #region Result<TValue, TError> Tests

    [Fact]
    public void GenericResult_Success_CreatesSuccessfulResult()
    {
        // Arrange & Act
        var result = Result<int, Exception>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericResult_Failure_CreatesFailedResult()
    {
        // Arrange & Act
        var exception = new InvalidOperationException("test error");
        var result = Result<int, Exception>.Failure(exception);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(exception);
    }

    [Fact]
    public void GenericResult_MapError_TransformsErrorType()
    {
        // Arrange
        var result = Result<int, string>.Failure("error text");

        // Act
        var mapped = result.MapError(err => new Exception(err));

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Message.Should().Be("error text");
    }

    [Fact]
    public void GenericResult_MapError_PreservesSuccessValue()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var mapped = result.MapError(err => new Exception(err));

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(42);
    }

    #endregion
}
