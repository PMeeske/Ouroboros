// <copyright file="AdapterIdTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Ouroboros.Core.Learning;
using Xunit;

/// <summary>
/// Unit tests for AdapterId type.
/// </summary>
[Trait("Category", "Unit")]
public class AdapterIdTests
{
    [Fact]
    public void NewId_CreatesUniqueIds()
    {
        // Act
        var id1 = AdapterId.NewId();
        var id2 = AdapterId.NewId();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(Guid.Empty);
        id2.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void FromString_WithValidGuid_ReturnsAdapterId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var guidString = guid.ToString();

        // Act
        var result = AdapterId.FromString(guidString);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Value.Should().Be(guid);
    }

    [Fact]
    public void FromString_WithInvalidString_ReturnsNone()
    {
        // Act
        var result = AdapterId.FromString("not-a-guid");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var adapterId = new AdapterId(guid);

        // Act
        var result = adapterId.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void Records_WithSameGuid_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new AdapterId(guid);
        var id2 = new AdapterId(guid);

        // Act & Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Records_WithDifferentGuid_AreNotEqual()
    {
        // Arrange
        var id1 = new AdapterId(Guid.NewGuid());
        var id2 = new AdapterId(Guid.NewGuid());

        // Act & Assert
        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }
}
