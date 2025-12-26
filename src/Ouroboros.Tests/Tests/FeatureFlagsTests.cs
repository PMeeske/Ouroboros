using FluentAssertions;
using Ouroboros.Core.Configuration;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Comprehensive tests for FeatureFlags following functional programming principles.
/// Tests focus on immutability, property correctness, and helper method behavior.
/// </summary>
public sealed class FeatureFlagsTests
{
    #region Constructor and Default Tests

    [Fact]
    public void Constructor_ShouldCreateWithAllFlagsDisabled()
    {
        // Act
        var flags = new FeatureFlags();

        // Assert
        flags.Embodiment.Should().BeFalse();
        flags.SelfModel.Should().BeFalse();
        flags.Affect.Should().BeFalse();
    }

    [Fact]
    public void AllOff_ShouldReturnInstanceWithAllFlagsDisabled()
    {
        // Act
        var flags = FeatureFlags.AllOff();

        // Assert
        flags.Embodiment.Should().BeFalse();
        flags.SelfModel.Should().BeFalse();
        flags.Affect.Should().BeFalse();
    }

    [Fact]
    public void AllOn_ShouldReturnInstanceWithAllFlagsEnabled()
    {
        // Act
        var flags = FeatureFlags.AllOn();

        // Assert
        flags.Embodiment.Should().BeTrue();
        flags.SelfModel.Should().BeTrue();
        flags.Affect.Should().BeTrue();
    }

    #endregion

    #region AnyEnabled Tests

    [Fact]
    public void AnyEnabled_WhenAllDisabled_ShouldReturnFalse()
    {
        // Arrange
        var flags = FeatureFlags.AllOff();

        // Act
        var result = flags.AnyEnabled();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AnyEnabled_WhenEmbodimentEnabled_ShouldReturnTrue()
    {
        // Arrange
        var flags = new FeatureFlags { Embodiment = true };

        // Act
        var result = flags.AnyEnabled();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AnyEnabled_WhenSelfModelEnabled_ShouldReturnTrue()
    {
        // Arrange
        var flags = new FeatureFlags { SelfModel = true };

        // Act
        var result = flags.AnyEnabled();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AnyEnabled_WhenAffectEnabled_ShouldReturnTrue()
    {
        // Arrange
        var flags = new FeatureFlags { Affect = true };

        // Act
        var result = flags.AnyEnabled();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AnyEnabled_WhenAllEnabled_ShouldReturnTrue()
    {
        // Arrange
        var flags = FeatureFlags.AllOn();

        // Act
        var result = flags.AnyEnabled();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region AllEnabled Tests

    [Fact]
    public void AllEnabled_WhenAllDisabled_ShouldReturnFalse()
    {
        // Arrange
        var flags = FeatureFlags.AllOff();

        // Act
        var result = flags.AllEnabled();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AllEnabled_WhenOnlyEmbodimentEnabled_ShouldReturnFalse()
    {
        // Arrange
        var flags = new FeatureFlags { Embodiment = true };

        // Act
        var result = flags.AllEnabled();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AllEnabled_WhenTwoEnabled_ShouldReturnFalse()
    {
        // Arrange
        var flags = new FeatureFlags { Embodiment = true, SelfModel = true };

        // Act
        var result = flags.AllEnabled();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AllEnabled_WhenAllEnabled_ShouldReturnTrue()
    {
        // Arrange
        var flags = FeatureFlags.AllOn();

        // Act
        var result = flags.AllEnabled();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetEnabledFeatures Tests

    [Fact]
    public void GetEnabledFeatures_WhenAllDisabled_ShouldReturnEmptyList()
    {
        // Arrange
        var flags = FeatureFlags.AllOff();

        // Act
        var result = flags.GetEnabledFeatures();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetEnabledFeatures_WhenEmbodimentEnabled_ShouldReturnSingleItem()
    {
        // Arrange
        var flags = new FeatureFlags { Embodiment = true };

        // Act
        var result = flags.GetEnabledFeatures();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("Embodiment");
    }

    [Fact]
    public void GetEnabledFeatures_WhenSelfModelEnabled_ShouldReturnSingleItem()
    {
        // Arrange
        var flags = new FeatureFlags { SelfModel = true };

        // Act
        var result = flags.GetEnabledFeatures();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("SelfModel");
    }

    [Fact]
    public void GetEnabledFeatures_WhenAffectEnabled_ShouldReturnSingleItem()
    {
        // Arrange
        var flags = new FeatureFlags { Affect = true };

        // Act
        var result = flags.GetEnabledFeatures();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("Affect");
    }

    [Fact]
    public void GetEnabledFeatures_WhenTwoEnabled_ShouldReturnTwoItems()
    {
        // Arrange
        var flags = new FeatureFlags { Embodiment = true, SelfModel = true };

        // Act
        var result = flags.GetEnabledFeatures();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Embodiment");
        result.Should().Contain("SelfModel");
    }

    [Fact]
    public void GetEnabledFeatures_WhenAllEnabled_ShouldReturnAllThree()
    {
        // Arrange
        var flags = FeatureFlags.AllOn();

        // Act
        var result = flags.GetEnabledFeatures();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Embodiment");
        result.Should().Contain("SelfModel");
        result.Should().Contain("Affect");
    }

    [Fact]
    public void GetEnabledFeatures_ShouldReturnReadOnlyList()
    {
        // Arrange
        var flags = FeatureFlags.AllOn();

        // Act
        var result = flags.GetEnabledFeatures();

        // Assert
        result.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void RecordEquality_WhenSameValues_ShouldBeEqual()
    {
        // Arrange
        var flags1 = new FeatureFlags { Embodiment = true, SelfModel = true };
        var flags2 = new FeatureFlags { Embodiment = true, SelfModel = true };

        // Act & Assert
        flags1.Should().Be(flags2);
        (flags1 == flags2).Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_WhenDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var flags1 = new FeatureFlags { Embodiment = true };
        var flags2 = new FeatureFlags { SelfModel = true };

        // Act & Assert
        flags1.Should().NotBe(flags2);
        (flags1 != flags2).Should().BeTrue();
    }

    [Fact]
    public void With_Expression_ShouldCreateNewInstanceWithModifiedValue()
    {
        // Arrange
        var original = new FeatureFlags { Embodiment = true };

        // Act
        var modified = original with { SelfModel = true };

        // Assert
        original.SelfModel.Should().BeFalse(); // Original unchanged
        modified.Embodiment.Should().BeTrue();
        modified.SelfModel.Should().BeTrue();
    }

    #endregion

    #region Configuration Integration Tests

    [Fact]
    public void SectionName_ShouldBeFeatureFlags()
    {
        // Assert
        FeatureFlags.SectionName.Should().Be("FeatureFlags");
    }

    #endregion
}
