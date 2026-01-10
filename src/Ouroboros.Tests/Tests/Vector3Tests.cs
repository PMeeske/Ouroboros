// <copyright file="Vector3Tests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for Vector3 type.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Vector3Tests
{
    [Fact]
    public void Vector3_Zero_ShouldReturnZeroVector()
    {
        // Act
        var zero = Vector3.Zero;

        // Assert
        zero.X.Should().Be(0f);
        zero.Y.Should().Be(0f);
        zero.Z.Should().Be(0f);
    }

    [Fact]
    public void Vector3_UnitVectors_ShouldHaveCorrectValues()
    {
        // Act
        var unitX = Vector3.UnitX;
        var unitY = Vector3.UnitY;
        var unitZ = Vector3.UnitZ;

        // Assert
        unitX.Should().Be(new Vector3(1f, 0f, 0f));
        unitY.Should().Be(new Vector3(0f, 1f, 0f));
        unitZ.Should().Be(new Vector3(0f, 0f, 1f));
    }

    [Fact]
    public void Magnitude_WithKnownVector_ShouldCalculateCorrectly()
    {
        // Arrange
        var vector = new Vector3(3f, 4f, 0f); // 3-4-5 triangle

        // Act
        var magnitude = vector.Magnitude();

        // Assert
        magnitude.Should().BeApproximately(5f, 0.001f);
    }

    [Fact]
    public void Normalized_WithNonZeroVector_ShouldReturnUnitVector()
    {
        // Arrange
        var vector = new Vector3(3f, 4f, 0f);

        // Act
        var normalized = vector.Normalized();

        // Assert
        normalized.Magnitude().Should().BeApproximately(1f, 0.001f);
        normalized.X.Should().BeApproximately(0.6f, 0.001f);
        normalized.Y.Should().BeApproximately(0.8f, 0.001f);
    }

    [Fact]
    public void Normalized_WithZeroVector_ShouldReturnZero()
    {
        // Arrange
        var vector = Vector3.Zero;

        // Act
        var normalized = vector.Normalized();

        // Assert
        normalized.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void Addition_ShouldAddComponentwise()
    {
        // Arrange
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);

        // Act
        var result = a + b;

        // Assert
        result.Should().Be(new Vector3(5f, 7f, 9f));
    }

    [Fact]
    public void Subtraction_ShouldSubtractComponentwise()
    {
        // Arrange
        var a = new Vector3(5f, 7f, 9f);
        var b = new Vector3(1f, 2f, 3f);

        // Act
        var result = a - b;

        // Assert
        result.Should().Be(new Vector3(4f, 5f, 6f));
    }

    [Fact]
    public void ScalarMultiplication_ShouldScaleAllComponents()
    {
        // Arrange
        var vector = new Vector3(1f, 2f, 3f);
        var scalar = 2f;

        // Act
        var result1 = vector * scalar;
        var result2 = scalar * vector;

        // Assert
        result1.Should().Be(new Vector3(2f, 4f, 6f));
        result2.Should().Be(new Vector3(2f, 4f, 6f));
    }

    [Fact]
    public void Dot_ShouldCalculateDotProduct()
    {
        // Arrange
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);

        // Act
        var dot = Vector3.Dot(a, b);

        // Assert
        dot.Should().BeApproximately(32f, 0.001f); // 1*4 + 2*5 + 3*6 = 32
    }

    [Fact]
    public void Cross_ShouldCalculateCrossProduct()
    {
        // Arrange
        var a = new Vector3(1f, 0f, 0f);
        var b = new Vector3(0f, 1f, 0f);

        // Act
        var cross = Vector3.Cross(a, b);

        // Assert
        cross.Should().Be(new Vector3(0f, 0f, 1f));
    }

    [Fact]
    public void Cross_WithParallelVectors_ShouldReturnZero()
    {
        // Arrange
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(2f, 4f, 6f); // Parallel to a

        // Act
        var cross = Vector3.Cross(a, b);

        // Assert
        cross.Magnitude().Should().BeApproximately(0f, 0.001f);
    }
}
