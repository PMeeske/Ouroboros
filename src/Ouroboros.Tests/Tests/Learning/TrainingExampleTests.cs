// <copyright file="TrainingExampleTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Ouroboros.Core.Learning;
using Xunit;

/// <summary>
/// Unit tests for TrainingExample type.
/// </summary>
[Trait("Category", "Unit")]
public class TrainingExampleTests
{
    [Fact]
    public void Validate_WithValidExample_ReturnsSuccess()
    {
        // Arrange
        var example = new TrainingExample("input", "output", 1.0);

        // Act
        var result = example.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyInput_ReturnsFailure()
    {
        // Arrange
        var example = new TrainingExample(string.Empty, "output", 1.0);

        // Act
        var result = example.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Input cannot be empty");
    }

    [Fact]
    public void Validate_WithEmptyOutput_ReturnsFailure()
    {
        // Arrange
        var example = new TrainingExample("input", string.Empty, 1.0);

        // Act
        var result = example.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Output cannot be empty");
    }

    [Fact]
    public void Validate_WithZeroWeight_ReturnsFailure()
    {
        // Arrange
        var example = new TrainingExample("input", "output", 0.0);

        // Act
        var result = example.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Weight must be positive");
    }

    [Fact]
    public void Validate_WithNegativeWeight_ReturnsFailure()
    {
        // Arrange
        var example = new TrainingExample("input", "output", -0.5);

        // Act
        var result = example.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Weight must be positive");
    }
}
