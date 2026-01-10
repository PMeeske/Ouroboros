// <copyright file="QuaternionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for Quaternion type.
/// </summary>
[Trait("Category", "Unit")]
public sealed class QuaternionTests
{
    [Fact]
    public void Quaternion_Identity_ShouldReturnIdentityQuaternion()
    {
        // Act
        var identity = Quaternion.Identity;

        // Assert
        identity.X.Should().Be(0f);
        identity.Y.Should().Be(0f);
        identity.Z.Should().Be(0f);
        identity.W.Should().Be(1f);
    }

    [Fact]
    public void Magnitude_WithKnownQuaternion_ShouldCalculateCorrectly()
    {
        // Arrange
        var q = new Quaternion(1f, 0f, 0f, 1f);

        // Act
        var magnitude = q.Magnitude();

        // Assert
        magnitude.Should().BeApproximately(MathF.Sqrt(2f), 0.001f);
    }

    [Fact]
    public void Normalized_ShouldReturnUnitQuaternion()
    {
        // Arrange
        var q = new Quaternion(1f, 0f, 0f, 1f);

        // Act
        var normalized = q.Normalized();

        // Assert
        normalized.Magnitude().Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void Conjugate_ShouldNegateVectorPart()
    {
        // Arrange
        var q = new Quaternion(1f, 2f, 3f, 4f);

        // Act
        var conjugate = q.Conjugate();

        // Assert
        conjugate.X.Should().Be(-1f);
        conjugate.Y.Should().Be(-2f);
        conjugate.Z.Should().Be(-3f);
        conjugate.W.Should().Be(4f);
    }

    [Fact]
    public void Multiplication_WithIdentity_ShouldReturnOriginal()
    {
        // Arrange
        var q = new Quaternion(1f, 2f, 3f, 4f);
        var identity = Quaternion.Identity;

        // Act
        var result = q * identity;

        // Assert
        result.Should().Be(q);
    }

    [Fact]
    public void Multiplication_ShouldCombineRotations()
    {
        // Arrange - Two 90-degree rotations around Z axis should equal 180 degrees
        var halfSqrt2 = MathF.Sqrt(2f) / 2f;
        var q1 = new Quaternion(0f, 0f, halfSqrt2, halfSqrt2); // 90 degrees around Z
        var q2 = new Quaternion(0f, 0f, halfSqrt2, halfSqrt2); // 90 degrees around Z

        // Act
        var combined = q1 * q2;

        // Assert - Should be 180 degrees around Z (0, 0, 1, 0)
        combined.X.Should().BeApproximately(0f, 0.001f);
        combined.Y.Should().BeApproximately(0f, 0.001f);
        combined.Z.Should().BeApproximately(1f, 0.001f);
        combined.W.Should().BeApproximately(0f, 0.001f);
    }
}
