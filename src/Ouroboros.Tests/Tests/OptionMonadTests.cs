// <copyright file="OptionMonadTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the Option monad implementation.
/// Validates monadic laws and null safety.
/// </summary>
[Trait("Category", "Unit")]
public class OptionMonadTests
{
    [Fact]
    public void Some_CreatesOptionWithValue()
    {
        // Arrange & Act
        var option = Option<int>.Some(42);

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(42);
    }

    [Fact]
    public void None_CreatesEmptyOption()
    {
        // Arrange & Act
        var option = Option<string>.None();

        // Assert
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNull_CreatesEmptyOption()
    {
        // Arrange & Act
        var option = new Option<string>(null);

        // Assert
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithValue_CreatesOptionWithValue()
    {
        // Arrange & Act
        var option = new Option<string>("test");

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be("test");
    }

    [Fact]
    public void Map_OnSome_TransformsValue()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var mapped = option.Map(x => x * 2);

        // Assert
        mapped.HasValue.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Bind_OnSome_AppliesFunction()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var bound = option.Bind(x => Option<int>.Some(x * 2));

        // Assert
        bound.HasValue.Should().BeTrue();
        bound.Value.Should().Be(10);
    }

    [Fact]
    public void Bind_CanChainOperations()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var chained = option
            .Bind(x => Option<int>.Some(x * 2))
            .Bind(x => Option<int>.Some(x + 3));

        // Assert
        chained.HasValue.Should().BeTrue();
        chained.Value.Should().Be(13); // (5 * 2) + 3
    }

    [Fact]
    public void Bind_ShortCircuitsOnNone()
    {
        // Arrange
        var option = Option<string>.Some("test");

        // Act
        var chained = option
            .Bind(x => Option<string>.None())
            .Bind(x => Option<string>.Some(x + " value"));

        // Assert
        chained.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Match_OnSome_ExecutesSomeFunction()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var output = option.Match(
            func: x => $"Value: {x}",
            defaultValue: "No value");

        // Assert
        output.Should().Be("Value: 42");
    }

    [Fact]
    public void Match_OnNone_ReturnsDefaultValue()
    {
        // Arrange
        var option = Option<string>.None();

        // Act
        var output = option.Match(
            func: x => $"Value: {x}",
            defaultValue: "No value");

        // Assert
        output.Should().Be("No value");
    }

    [Fact]
    public void MatchAction_OnSome_ExecutesSomeAction()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var wasCalled = false;
        var value = 0;

        // Act
        option.Match(
            onSome: x => { wasCalled = true;
                value = x; },
            onNone: () => { wasCalled = false; });

        // Assert
        wasCalled.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void MatchAction_OnNone_ExecutesNoneAction()
    {
        // Arrange
        var option = Option<string>.None();
        var wasNoneCalled = false;

        // Act
        option.Match(
            onSome: x => { },
            onNone: () => { wasNoneCalled = true; });

        // Assert
        wasNoneCalled.Should().BeTrue();
    }

    [Fact]
    public void GetValueOrDefault_OnSome_ReturnsValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var value = option.GetValueOrDefault(0);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void GetValueOrDefault_OnNone_ReturnsDefault()
    {
        // Arrange
        var option = Option<string>.None();

        // Act
        var value = option.GetValueOrDefault("default");

        // Assert
        value.Should().Be("default");
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(0, 0)]
    [InlineData(-3, -6)]
    public void Map_CorrectlyTransformsDifferentValues(int input, int expected)
    {
        // Arrange
        var option = Option<int>.Some(input);

        // Act
        var mapped = option.Map(x => x * 2);

        // Assert
        mapped.Value.Should().Be(expected);
    }

    [Fact]
    public void Option_FollowsMonadicIdentityLaw()
    {
        // Left identity: return a >>= f ≡ f a
        var a = 42;
        Func<int, Option<string>> f = x => Option<string>.Some(x.ToString());

        var left = Option<int>.Some(a).Bind(f);
        var right = f(a);

        left.HasValue.Should().Be(right.HasValue);
        if (left.HasValue)
        {
            left.Value.Should().Be(right.Value);
        }
    }

    [Fact]
    public void Option_FollowsMonadicAssociativityLaw()
    {
        // Associativity: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Option<int>.Some(5);
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        Func<int, Option<int>> g = x => Option<int>.Some(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.HasValue.Should().Be(right.HasValue);
        if (left.HasValue)
        {
            left.Value.Should().Be(right.Value);
        }
    }

    [Fact]
    public void Map_OnNone_RemainsNone()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var mapped = option.Map(x => x * 2);

        // Assert
        mapped.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Bind_OnNone_RemainsNone()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var bound = option.Bind(x => Option<int>.Some(x * 2));

        // Assert
        bound.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToString_OnSome_ReturnsSomeFormat()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.ToString();

        // Assert
        result.Should().Be("Some(42)");
    }

    [Fact]
    public void ToString_OnNone_ReturnsNone()
    {
        // Arrange
        var option = Option<string>.None();

        // Act
        var result = option.ToString();

        // Assert
        result.Should().Be("None");
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(42);

        // Act & Assert
        option1.Equals(option2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(99);

        // Act & Assert
        option1.Equals(option2).Should().BeFalse();
    }

    [Fact]
    public void Equals_BothNone_ReturnsTrue()
    {
        // Arrange
        var option1 = Option<string>.None();
        var option2 = Option<string>.None();

        // Act & Assert
        option1.Equals(option2).Should().BeTrue();
    }

    [Fact]
    public void Equals_SomeAndNone_ReturnsFalse()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.None();

        // Act & Assert
        option1.Equals(option2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithObject_ReturnsCorrectResult()
    {
        // Arrange
        var option = Option<int>.Some(42);
        object boxed = Option<int>.Some(42);

        // Act & Assert
        option.Equals(boxed).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithInvalidObject_ReturnsFalse()
    {
        // Arrange
        var option = Option<int>.Some(42);
        object invalid = "not an option";

        // Act & Assert
        option.Equals(invalid).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(42);

        // Act & Assert
        option1.GetHashCode().Should().Be(option2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_None_ReturnsZero()
    {
        // Arrange
        var option = Option<string>.None();

        // Act & Assert
        option.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(42);

        // Act & Assert
        (option1 == option2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(99);

        // Act & Assert
        (option1 != option2).Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesOption()
    {
        // Arrange & Act
        Option<int> option = 42;

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromNull_CreatesNone()
    {
        // Arrange & Act
        Option<string> option = null!;

        // Assert
        option.HasValue.Should().BeFalse();
    }
}
