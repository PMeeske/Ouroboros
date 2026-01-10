// <copyright file="UnitTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;
using Unit = Ouroboros.Core.Synthesis.Unit;

namespace Ouroboros.Tests.Synthesis;

/// <summary>
/// Tests for the Unit type.
/// </summary>
public class UnitTests
{
    [Fact]
    public void Unit_DefaultValue_ShouldBeEqual()
    {
        // Arrange & Act
        var unit1 = Unit.Value;
        var unit2 = default(Unit);

        // Assert
        unit1.Should().Be(unit2);
        (unit1 == unit2).Should().BeTrue();
        (unit1 != unit2).Should().BeFalse();
    }

    [Fact]
    public void Unit_Equals_ShouldAlwaysReturnTrue()
    {
        // Arrange
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        // Act & Assert
        unit1.Equals(unit2).Should().BeTrue();
        unit1.Equals((object)unit2).Should().BeTrue();
    }

    [Fact]
    public void Unit_GetHashCode_ShouldAlwaysReturnZero()
    {
        // Arrange
        var unit = Unit.Value;

        // Act
        var hashCode = unit.GetHashCode();

        // Assert
        hashCode.Should().Be(0);
    }

    [Fact]
    public void Unit_ToString_ShouldReturnEmptyParentheses()
    {
        // Arrange
        var unit = Unit.Value;

        // Act
        var result = unit.ToString();

        // Assert
        result.Should().Be("()");
    }

    [Fact]
    public void Unit_Operators_ShouldWorkCorrectly()
    {
        // Arrange
        var unit1 = Unit.Value;
        var unit2 = default(Unit);

        // Act & Assert
        (unit1 == unit2).Should().BeTrue();
        (unit1 != unit2).Should().BeFalse();
    }
}
