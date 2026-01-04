// <copyright file="CouncilConfigTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

/// <summary>
/// Tests for CouncilConfig record functionality.
/// </summary>
[Trait("Category", "Unit")]
public class CouncilConfigTests
{
    [Fact]
    public void Default_ShouldHaveReasonableDefaults()
    {
        // Act
        var config = CouncilConfig.Default;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(3);
        config.ConsensusThreshold.Should().Be(0.7);
        config.TimeoutPerAgent.Should().BeNull();
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void Strict_ShouldRequireUnanimity()
    {
        // Act
        var config = CouncilConfig.Strict;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(5);
        config.ConsensusThreshold.Should().Be(1.0);
        config.RequireUnanimity.Should().BeTrue();
        config.EnableMinorityReport.Should().BeTrue();
    }

    [Fact]
    public void Fast_ShouldHaveMinimalRounds()
    {
        // Act
        var config = CouncilConfig.Fast;

        // Assert
        config.MaxRoundsPerPhase.Should().Be(1);
        config.ConsensusThreshold.Should().Be(0.5);
        config.RequireUnanimity.Should().BeFalse();
        config.EnableMinorityReport.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldApplyAll()
    {
        // Arrange & Act
        var config = new CouncilConfig(
            MaxRoundsPerPhase: 10,
            ConsensusThreshold: 0.9,
            TimeoutPerAgent: TimeSpan.FromMinutes(5),
            RequireUnanimity: true,
            EnableMinorityReport: false);

        // Assert
        config.MaxRoundsPerPhase.Should().Be(10);
        config.ConsensusThreshold.Should().Be(0.9);
        config.TimeoutPerAgent.Should().Be(TimeSpan.FromMinutes(5));
        config.RequireUnanimity.Should().BeTrue();
        config.EnableMinorityReport.Should().BeFalse();
    }
}
