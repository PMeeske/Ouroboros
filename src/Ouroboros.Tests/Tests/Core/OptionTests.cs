// <copyright file="OptionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Core;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Comprehensive tests for the Option monad implementation.
/// Tests monadic laws, null safety, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
public class OptionTests
{
    #region Creation Tests

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
    public void Some_WithZero_CreatesOptionWithZero()
    {
        // Arrange & Act
        var option = Option<int>.Some(0);

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(0);
    }

    [Fact]
    public void Some_WithEmptyString_CreatesOptionWithEmptyString()
    {
        // Arrange & Act
        var option = Option<string>.Some(string.Empty);

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(string.Empty);
    }

    #endregion

    #region IsSome/IsNone Tests

    [Fact]
    public void HasValue_OnSome_ReturnsTrue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Assert
        option.HasValue.Should().BeTrue();
    }

    [Fact]
    public void HasValue_OnNone_ReturnsFalse()
    {
        // Arrange
        var option = Option<int>.None();

        // Assert
        option.HasValue.Should().BeFalse();
    }

    #endregion

    #region Map Tests

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
    public void Map_OnNone_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var mapped = option.Map(x => x * 2);

        // Assert
        mapped.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Map_WithTypeConversion_WorksCorrectly()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var mapped = option.Map(x => x.ToString());

        // Assert
        mapped.HasValue.Should().BeTrue();
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
        var option = Option<int>.Some(input);

        // Act
        var mapped = option.Map(x => x * 2);

        // Assert
        mapped.Value.Should().Be(expected);
    }

    [Fact]
    public void Map_ChainedOperations_TransformsCorrectly()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var result = option
            .Map(x => x * 2)
            .Map(x => x + 3)
            .Map(x => x.ToString());

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("13");
    }

    #endregion

    #region Bind Tests

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
    public void Bind_OnNone_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var bound = option.Bind(x => Option<int>.Some(x * 2));

        // Assert
        bound.HasValue.Should().BeFalse();
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
            .Bind(x => Option<string>.Some(x + " value")); // Should not execute

        // Assert
        chained.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Bind_WithTypeConversion_WorksCorrectly()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var bound = option.Bind(x => Option<string>.Some(x.ToString()));

        // Assert
        bound.HasValue.Should().BeTrue();
        bound.Value.Should().Be("42");
    }

    [Fact]
    public void Bind_WithConditionalLogic_ReturnsCorrectResult()
    {
        // Arrange
        var option = Option<int>.Some(10);
        Func<int, Option<int>> divideByTwo = x =>
            x % 2 == 0 ? Option<int>.Some(x / 2) : Option<int>.None();

        // Act
        var result = option.Bind(divideByTwo);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public void Bind_WithConditionalLogic_OnOddNumber_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.Some(7);
        Func<int, Option<int>> divideByTwo = x =>
            x % 2 == 0 ? Option<int>.Some(x / 2) : Option<int>.None();

        // Act
        var result = option.Bind(divideByTwo);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region Match Tests

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
        var capturedValue = 0;

        // Act
        option.Match(
            onSome: x => { wasCalled = true; capturedValue = x; },
            onNone: () => { });

        // Assert
        wasCalled.Should().BeTrue();
        capturedValue.Should().Be(42);
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

    #endregion

    #region GetValueOrDefault Tests

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

    [Fact]
    public void GetValueOrDefault_OnNone_ReturnsSpecifiedDefault()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var value = option.GetValueOrDefault(99);

        // Assert
        value.Should().Be(99);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_TwoSomeWithSameValue_ReturnsTrue()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(42);

        // Assert
        option1.Equals(option2).Should().BeTrue();
        (option1 == option2).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoSomeWithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(43);

        // Assert
        option1.Equals(option2).Should().BeFalse();
        (option1 != option2).Should().BeTrue();
    }

    [Fact]
    public void Equals_TwoNone_ReturnsTrue()
    {
        // Arrange
        var option1 = Option<int>.None();
        var option2 = Option<int>.None();

        // Assert
        option1.Equals(option2).Should().BeTrue();
    }

    [Fact]
    public void Equals_SomeAndNone_ReturnsFalse()
    {
        // Arrange
        var some = Option<int>.Some(42);
        var none = Option<int>.None();

        // Assert
        some.Equals(none).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_TwoEqualOptions_ReturnSameHashCode()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(42);

        // Assert
        option1.GetHashCode().Should().Be(option2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_TwoNoneOptions_ReturnSameHashCode()
    {
        // Arrange
        var option1 = Option<int>.None();
        var option2 = Option<int>.None();

        // Assert
        option1.GetHashCode().Should().Be(option2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_OnSome_ReturnsSomeString()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var str = option.ToString();

        // Assert
        str.Should().Contain("Some");
        str.Should().Contain("42");
    }

    [Fact]
    public void ToString_OnNone_ReturnsNoneString()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var str = option.ToString();

        // Assert
        str.Should().Be("None");
    }

    #endregion

    #region Monadic Laws Tests

    [Fact]
    public void Option_LeftIdentity_Holds()
    {
        // Left identity: return a >>= f ≡ f a
        var a = 42;
        Func<int, Option<string>> f = x => Option<string>.Some(x.ToString());

        var left = Option<int>.Some(a).Bind(f);
        var right = f(a);

        left.HasValue.Should().Be(right.HasValue);
        left.Value.Should().Be(right.Value);
    }

    [Fact]
    public void Option_RightIdentity_Holds()
    {
        // Right identity: m >>= return ≡ m
        var m = Option<int>.Some(42);

        var result = m.Bind(x => Option<int>.Some(x));

        result.HasValue.Should().Be(m.HasValue);
        result.Value.Should().Be(m.Value);
    }

    [Fact]
    public void Option_Associativity_Holds()
    {
        // Associativity: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Option<int>.Some(5);
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        Func<int, Option<int>> g = x => Option<int>.Some(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.HasValue.Should().Be(right.HasValue);
        left.Value.Should().Be(right.Value);
    }

    [Fact]
    public void Option_AssociativityWithNone_Holds()
    {
        // Associativity should hold even when f returns None
        var m = Option<int>.Some(5);
        Func<int, Option<int>> f = x => x > 3
            ? Option<int>.None()
            : Option<int>.Some(x * 2);
        Func<int, Option<int>> g = x => Option<int>.Some(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.HasValue.Should().Be(right.HasValue);
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSomeOption()
    {
        // Arrange & Act
        Option<int> option = 42;

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromNull_CreatesNoneOption()
    {
        // Arrange & Act
        Option<string?> option = (string?)null;

        // Assert
        option.HasValue.Should().BeFalse();
    }

    #endregion

    #region Real-World Usage Tests

    [Fact]
    public void Option_SafelyHandlesNullableChain()
    {
        // Simulating a chain of potentially null operations
        var data = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };

        // Safe lookup using Option
        Option<string> TryGetValue(string key) =>
            data.TryGetValue(key, out var value) ? Option<string>.Some(value) : Option<string>.None();

        // Act - chain lookups
        var result = TryGetValue("key1")
            .Map(v => v.ToUpper())
            .Bind(v => TryGetValue("key2").Map(v2 => $"{v}-{v2}"));

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("VALUE1-value2");
    }

    [Fact]
    public void Option_HandlesParsingScenario()
    {
        // Safe parsing helper
        Option<int> TryParseInt(string s) =>
            int.TryParse(s, out var result) ? Option<int>.Some(result) : Option<int>.None();

        // Valid input
        var validResult = TryParseInt("42");
        validResult.HasValue.Should().BeTrue();
        validResult.Value.Should().Be(42);

        // Invalid input
        var invalidResult = TryParseInt("not a number");
        invalidResult.HasValue.Should().BeFalse();
    }

    #endregion
}
