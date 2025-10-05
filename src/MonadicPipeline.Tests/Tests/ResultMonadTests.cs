using FluentAssertions;
using LangChainPipeline.Core.Monads;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the Result monad implementation.
/// Validates monadic laws and error handling.
/// </summary>
public class ResultMonadTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        // Arrange & Act
        var result = Result<int, string>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        // Arrange & Act
        var result = Result<int, string>.Failure("error");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("error");
    }

    [Fact]
    public void Value_OnFailedResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_OnSuccessfulResult_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Map_OnSuccessfulResult_TransformsValue()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnFailedResult_PreservesError()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be("error");
    }

    [Fact]
    public void Bind_OnSuccessfulResult_AppliesFunction()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var bound = result.Bind(x => Result<int, string>.Success(x * 2));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be(10);
    }

    [Fact]
    public void Bind_OnFailedResult_PreservesError()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var bound = result.Bind(x => Result<int, string>.Success(x * 2));

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be("error");
    }

    [Fact]
    public void Bind_CanChainOperations()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var chained = result
            .Bind(x => Result<int, string>.Success(x * 2))
            .Bind(x => Result<int, string>.Success(x + 3));

        // Assert
        chained.IsSuccess.Should().BeTrue();
        chained.Value.Should().Be(13); // (5 * 2) + 3
    }

    [Fact]
    public void Bind_ShortCircuitsOnFirstError()
    {
        // Arrange
        var result = Result<int, string>.Success(5);

        // Act
        var chained = result
            .Bind(x => Result<int, string>.Failure("first error"))
            .Bind(x => Result<int, string>.Success(x + 3));

        // Assert
        chained.IsFailure.Should().BeTrue();
        chained.Error.Should().Be("first error");
    }

    [Fact]
    public void MapError_TransformsErrorType()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var mapped = result.MapError(err => err.Length);

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(5); // "error".Length
    }

    [Fact]
    public void MapError_PreservesSuccessValue()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var mapped = result.MapError(err => err.Length);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(42);
    }

    [Fact]
    public void Match_OnSuccess_ExecutesSuccessFunction()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var output = result.Match(
            onSuccess: x => $"Success: {x}",
            onFailure: err => $"Error: {err}"
        );

        // Assert
        output.Should().Be("Success: 42");
    }

    [Fact]
    public void Match_OnFailure_ExecutesFailureFunction()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var output = result.Match(
            onSuccess: x => $"Success: {x}",
            onFailure: err => $"Error: {err}"
        );

        // Assert
        output.Should().Be("Error: error");
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(0, 0)]
    [InlineData(-3, -6)]
    public void Map_CorrectlyTransformsDifferentValues(int input, int expected)
    {
        // Arrange
        var result = Result<int, string>.Success(input);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.Value.Should().Be(expected);
    }

    [Fact]
    public void Result_FollowsMonadicIdentityLaw()
    {
        // Left identity: return a >>= f ≡ f a
        var a = 42;
        Func<int, Result<string, string>> f = x => Result<string, string>.Success(x.ToString());

        var left = Result<int, string>.Success(a).Bind(f);
        var right = f(a);

        left.IsSuccess.Should().Be(right.IsSuccess);
        left.Value.Should().Be(right.Value);
    }

    [Fact]
    public void Result_FollowsMonadicAssociativityLaw()
    {
        // Associativity: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Result<int, string>.Success(5);
        Func<int, Result<int, string>> f = x => Result<int, string>.Success(x * 2);
        Func<int, Result<int, string>> g = x => Result<int, string>.Success(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.IsSuccess.Should().Be(right.IsSuccess);
        left.Value.Should().Be(right.Value);
    }
}
