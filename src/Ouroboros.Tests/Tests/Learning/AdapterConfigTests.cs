// <copyright file="AdapterConfigTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Ouroboros.Core.Learning;
using Xunit;

/// <summary>
/// Unit tests for AdapterConfig type.
/// </summary>
[Trait("Category", "Unit")]
public class AdapterConfigTests
{
    [Fact]
    public void Default_CreatesValidConfig()
    {
        // Act
        var config = AdapterConfig.Default();

        // Assert
        config.Rank.Should().Be(8);
        config.LearningRate.Should().Be(3e-4);
        config.MaxSteps.Should().Be(1000);
        config.TargetModules.Should().Be("q_proj,v_proj");
        config.UseRSLoRA.Should().BeFalse();
    }

    [Fact]
    public void LowRank_CreatesConfigWithRank4()
    {
        // Act
        var config = AdapterConfig.LowRank();

        // Assert
        config.Rank.Should().Be(4);
    }

    [Fact]
    public void HighRank_CreatesConfigWithRank16()
    {
        // Act
        var config = AdapterConfig.HighRank();

        // Assert
        config.Rank.Should().Be(16);
    }

    [Fact]
    public void Validate_WithValidConfig_ReturnsSuccess()
    {
        // Arrange
        var config = AdapterConfig.Default();

        // Act
        var result = config.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(config);
    }

    [Fact]
    public void Validate_WithZeroRank_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { Rank = 0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Rank must be positive");
    }

    [Fact]
    public void Validate_WithNegativeRank_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { Rank = -1 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Rank must be positive");
    }

    [Fact]
    public void Validate_WithZeroLearningRate_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { LearningRate = 0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Learning rate must be positive");
    }

    [Fact]
    public void Validate_WithNegativeLearningRate_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { LearningRate = -0.001 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Learning rate must be positive");
    }

    [Fact]
    public void Validate_WithZeroMaxSteps_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { MaxSteps = 0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Max steps must be positive");
    }

    [Fact]
    public void Validate_WithEmptyTargetModules_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { TargetModules = string.Empty };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Target modules cannot be empty");
    }

    [Fact]
    public void Validate_WithWhitespaceTargetModules_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { TargetModules = "   " };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Target modules cannot be empty");
    }

    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        // Arrange
        var config1 = new AdapterConfig(8, 3e-4, 1000, "q_proj,v_proj", false);
        var config2 = new AdapterConfig(8, 3e-4, 1000, "q_proj,v_proj", false);

        // Act & Assert
        config1.Should().Be(config2);
    }

    [Fact]
    public void Records_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var config1 = AdapterConfig.Default();
        var config2 = config1 with { Rank = 16 };

        // Act & Assert
        config1.Should().NotBe(config2);
    }
}
