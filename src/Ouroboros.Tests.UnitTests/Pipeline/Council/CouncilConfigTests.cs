// <copyright file="CouncilConfigTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Pipeline.Council;

using FluentAssertions;
using Ouroboros.Pipeline.Council;
using Xunit;

/// <summary>
/// Tests for CouncilConfig and related council components.
/// </summary>
[Trait("Category", "Unit")]
public class CouncilConfigTests
{
    #region Default Configuration Tests

    [Fact]
    public void Default_HasExpectedMaxRoundsPerPhase()
    {
        // Act
        var config = CouncilConfig.Default;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(3);
    }

    [Fact]
    public void Default_HasExpectedConsensusThreshold()
    {
        // Act
        var config = CouncilConfig.Default;

        // Assert
        config.ConsensusThreshold.Should().Be(0.7);
    }

    [Fact]
    public void Default_HasNullTimeoutPerAgent()
    {
        // Act
        var config = CouncilConfig.Default;

        // Assert
        config.TimeoutPerAgent.Should().BeNull();
    }

    [Fact]
    public void Default_DoesNotRequireUnanimity()
    {
        // Act
        var config = CouncilConfig.Default;

        // Assert
        config.RequireUnanimity.Should().BeFalse();
    }

    [Fact]
    public void Default_EnablesMinorityReport()
    {
        // Act
        var config = CouncilConfig.Default;

        // Assert
        config.EnableMinorityReport.Should().BeTrue();
    }

    #endregion

    #region Strict Configuration Tests

    [Fact]
    public void Strict_HasHigherMaxRoundsPerPhase()
    {
        // Act
        var config = CouncilConfig.Strict;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(5);
    }

    [Fact]
    public void Strict_HasFullConsensusThreshold()
    {
        // Act
        var config = CouncilConfig.Strict;

        // Assert
        config.ConsensusThreshold.Should().Be(1.0);
    }

    [Fact]
    public void Strict_RequiresUnanimity()
    {
        // Act
        var config = CouncilConfig.Strict;

        // Assert
        config.RequireUnanimity.Should().BeTrue();
    }

    [Fact]
    public void Strict_EnablesMinorityReport()
    {
        // Act
        var config = CouncilConfig.Strict;

        // Assert
        config.EnableMinorityReport.Should().BeTrue();
    }

    #endregion

    #region Fast Configuration Tests

    [Fact]
    public void Fast_HasMinimalRounds()
    {
        // Act
        var config = CouncilConfig.Fast;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(1);
    }

    [Fact]
    public void Fast_HasLowConsensusThreshold()
    {
        // Act
        var config = CouncilConfig.Fast;

        // Assert
        config.ConsensusThreshold.Should().Be(0.5);
    }

    [Fact]
    public void Fast_DoesNotRequireUnanimity()
    {
        // Act
        var config = CouncilConfig.Fast;

        // Assert
        config.RequireUnanimity.Should().BeFalse();
    }

    [Fact]
    public void Fast_DisablesMinorityReport()
    {
        // Act
        var config = CouncilConfig.Fast;

        // Assert
        config.EnableMinorityReport.Should().BeFalse();
    }

    #endregion

    #region Custom Configuration Tests

    [Fact]
    public void Constructor_WithCustomValues_SetsAllProperties()
    {
        // Act
        var config = new CouncilConfig(
            MaxRoundsPerPhase: 10,
            ConsensusThreshold: 0.8,
            TimeoutPerAgent: TimeSpan.FromMinutes(5),
            RequireUnanimity: true,
            EnableMinorityReport: false);

        // Assert
        config.MaxRoundsPerPhase.Should().Be(10);
        config.ConsensusThreshold.Should().Be(0.8);
        config.TimeoutPerAgent.Should().Be(TimeSpan.FromMinutes(5));
        config.RequireUnanimity.Should().BeTrue();
        config.EnableMinorityReport.Should().BeFalse();
    }

    [Fact]
    public void ParameterlessConstructor_UsesDefaults()
    {
        // Act
        var config = new CouncilConfig();

        // Assert - Should match Default
        config.MaxRoundsPerPhase.Should().Be(3);
        config.ConsensusThreshold.Should().Be(0.7);
        config.TimeoutPerAgent.Should().BeNull();
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void WithExpression_ModifiesSingleProperty()
    {
        // Arrange
        var original = CouncilConfig.Default;

        // Act
        var modified = original with { MaxRoundsPerPhase = 5 };

        // Assert
        modified.MaxRoundsPerPhase.Should().Be(5);
        modified.ConsensusThreshold.Should().Be(original.ConsensusThreshold);
        modified.RequireUnanimity.Should().Be(original.RequireUnanimity);
        original.MaxRoundsPerPhase.Should().Be(3); // Original unchanged
    }

    [Fact]
    public void WithExpression_ModifiesMultipleProperties()
    {
        // Arrange
        var original = CouncilConfig.Default;

        // Act
        var modified = original with
        {
            MaxRoundsPerPhase = 10,
            ConsensusThreshold = 0.9,
            RequireUnanimity = true
        };

        // Assert
        modified.MaxRoundsPerPhase.Should().Be(10);
        modified.ConsensusThreshold.Should().Be(0.9);
        modified.RequireUnanimity.Should().BeTrue();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var config1 = new CouncilConfig(3, 0.7, null, false, true);
        var config2 = new CouncilConfig(3, 0.7, null, false, true);

        // Assert
        config1.Should().Be(config2);
        (config1 == config2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentMaxRounds_AreNotEqual()
    {
        // Arrange
        var config1 = new CouncilConfig(3, 0.7, null, false, true);
        var config2 = new CouncilConfig(5, 0.7, null, false, true);

        // Assert
        config1.Should().NotBe(config2);
        (config1 != config2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentThreshold_AreNotEqual()
    {
        // Arrange
        var config1 = CouncilConfig.Default;
        var config2 = config1 with { ConsensusThreshold = 0.9 };

        // Assert
        config1.Should().NotBe(config2);
    }

    [Fact]
    public void GetHashCode_SameValues_SameHash()
    {
        // Arrange
        var config1 = CouncilConfig.Default;
        var config2 = CouncilConfig.Default;

        // Assert
        config1.GetHashCode().Should().Be(config2.GetHashCode());
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(100)]
    public void MaxRoundsPerPhase_AcceptsPositiveValues(int rounds)
    {
        // Act
        var config = new CouncilConfig(MaxRoundsPerPhase: rounds);

        // Assert
        config.MaxRoundsPerPhase.Should().Be(rounds);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void ConsensusThreshold_AcceptsValidValues(double threshold)
    {
        // Act
        var config = new CouncilConfig(ConsensusThreshold: threshold);

        // Assert
        config.ConsensusThreshold.Should().Be(threshold);
    }

    [Fact]
    public void TimeoutPerAgent_AcceptsTimeSpan()
    {
        // Act
        var config = new CouncilConfig(TimeoutPerAgent: TimeSpan.FromSeconds(30));

        // Assert
        config.TimeoutPerAgent.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void TimeoutPerAgent_AcceptsNull()
    {
        // Act
        var config = new CouncilConfig(TimeoutPerAgent: null);

        // Assert
        config.TimeoutPerAgent.Should().BeNull();
    }

    #endregion

    #region Preset Comparison Tests

    [Fact]
    public void Strict_IsMoreConservativeThanDefault()
    {
        // Arrange
        var defaultConfig = CouncilConfig.Default;
        var strictConfig = CouncilConfig.Strict;

        // Assert
        strictConfig.MaxRoundsPerPhase.Should().BeGreaterThan(defaultConfig.MaxRoundsPerPhase);
        strictConfig.ConsensusThreshold.Should().BeGreaterThan(defaultConfig.ConsensusThreshold);
        strictConfig.RequireUnanimity.Should().BeTrue();
    }

    [Fact]
    public void Fast_IsLessConservativeThanDefault()
    {
        // Arrange
        var defaultConfig = CouncilConfig.Default;
        var fastConfig = CouncilConfig.Fast;

        // Assert
        fastConfig.MaxRoundsPerPhase.Should().BeLessThan(defaultConfig.MaxRoundsPerPhase);
        fastConfig.ConsensusThreshold.Should().BeLessThan(defaultConfig.ConsensusThreshold);
    }

    [Fact]
    public void AllPresets_AreDistinct()
    {
        // Arrange
        var configs = new[]
        {
            CouncilConfig.Default,
            CouncilConfig.Strict,
            CouncilConfig.Fast
        };

        // Assert
        configs.Distinct().Should().HaveCount(3);
    }

    #endregion
}
