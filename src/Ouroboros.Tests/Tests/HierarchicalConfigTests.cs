// <copyright file="HierarchicalConfigTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using FluentAssertions;
using LangChainPipeline.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for HierarchicalConfig multi-level configuration resolution.
/// Validates feature flag scenarios and organizational hierarchy.
/// </summary>
public class HierarchicalConfigTests
{
    [Fact]
    public void ResolveForUser_AllInherit_UsesSystemDefault()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: true,
            organizationOverride: TriState.Inherit,
            teamOverride: TriState.Inherit,
            userOverride: TriState.Inherit);

        // Act
        var result = config.ResolveForUser();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveForUser_UserOverrideWins()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.On,
            teamOverride: TriState.On,
            userOverride: TriState.Off);

        // Act
        var result = config.ResolveForUser();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ResolveForUser_InheritsFromTeam()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.Inherit,
            teamOverride: TriState.On,
            userOverride: TriState.Inherit);

        // Act
        var result = config.ResolveForUser();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveForUser_InheritsFromOrganization()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.On,
            teamOverride: TriState.Inherit,
            userOverride: TriState.Inherit);

        // Act
        var result = config.ResolveForUser();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveForTeam_IgnoresUserOverride()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.Off,
            teamOverride: TriState.On,
            userOverride: TriState.Off);

        // Act
        var result = config.ResolveForTeam();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveForOrganization_IgnoresTeamAndUser()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.On,
            teamOverride: TriState.Off,
            userOverride: TriState.Off);

        // Act
        var result = config.ResolveForOrganization();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetResolutionChain_ShowsAllLevels()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.On,
            teamOverride: TriState.Inherit,
            userOverride: TriState.Inherit);

        // Act
        var chain = config.GetResolutionChain();

        // Assert
        chain.Should().ContainKey("System");
        chain.Should().ContainKey("Organization");
        chain.Should().ContainKey("Team");
        chain.Should().ContainKey("User");

        chain["System"].Should().BeFalse();
        chain["Organization"].Should().BeTrue();
        chain["Team"].Should().BeTrue();
        chain["User"].Should().BeTrue();
    }

    [Fact]
    public void WithUserOverride_CreatesNewInstance()
    {
        // Arrange
        var original = new HierarchicalConfig(true, TriState.Inherit, TriState.Inherit, TriState.Inherit);

        // Act
        var modified = original.WithUserOverride(TriState.Off);

        // Assert
        original.UserOverride.Should().Be(TriState.Inherit);
        modified.UserOverride.Should().Be(TriState.Off);
        modified.SystemDefault.Should().Be(original.SystemDefault);
    }

    [Fact]
    public void WithTeamOverride_CreatesNewInstance()
    {
        // Arrange
        var original = new HierarchicalConfig(true, TriState.Inherit, TriState.Inherit, TriState.Inherit);

        // Act
        var modified = original.WithTeamOverride(TriState.On);

        // Assert
        original.TeamOverride.Should().Be(TriState.Inherit);
        modified.TeamOverride.Should().Be(TriState.On);
    }

    [Fact]
    public void WithOrganizationOverride_CreatesNewInstance()
    {
        // Arrange
        var original = new HierarchicalConfig(false, TriState.Inherit, TriState.Inherit, TriState.Inherit);

        // Act
        var modified = original.WithOrganizationOverride(TriState.On);

        // Assert
        original.OrganizationOverride.Should().Be(TriState.Inherit);
        modified.OrganizationOverride.Should().Be(TriState.On);
    }

    [Fact]
    public void ToString_FormatsHierarchy()
    {
        // Arrange
        var config = new HierarchicalConfig(
            systemDefault: true,
            organizationOverride: TriState.Off,
            teamOverride: TriState.Inherit,
            userOverride: TriState.On);

        // Act
        var result = config.ToString();

        // Assert
        result.Should().Contain("System");
        result.Should().Contain("Organization");
        result.Should().Contain("Team");
        result.Should().Contain("User");
    }

    [Fact]
    public void FeatureFlagScenario_OrganizationDisablesForAll()
    {
        // Arrange - Organization explicitly disables feature
        var config = new HierarchicalConfig(
            systemDefault: true,
            organizationOverride: TriState.Off,
            teamOverride: TriState.Inherit,
            userOverride: TriState.Inherit);

        // Act & Assert
        config.ResolveForUser().Should().BeFalse();
        config.ResolveForTeam().Should().BeFalse();
        config.ResolveForOrganization().Should().BeFalse();
    }

    [Fact]
    public void FeatureFlagScenario_TeamOverridesOrganization()
    {
        // Arrange - Team enables despite org settings
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.Off,
            teamOverride: TriState.On,
            userOverride: TriState.Inherit);

        // Act & Assert
        config.ResolveForOrganization().Should().BeFalse();
        config.ResolveForTeam().Should().BeTrue();
        config.ResolveForUser().Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagScenario_UserOverridesAll()
    {
        // Arrange - User explicitly disables
        var config = new HierarchicalConfig(
            systemDefault: true,
            organizationOverride: TriState.On,
            teamOverride: TriState.On,
            userOverride: TriState.Off);

        // Act & Assert
        config.ResolveForUser().Should().BeFalse();
        config.ResolveForTeam().Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagScenario_ComplexInheritanceChain()
    {
        // Arrange - System=Off, Org=Inherit, Team=On, User=Inherit
        // Expected: User should be On (inherits from Team)
        var config = new HierarchicalConfig(
            systemDefault: false,
            organizationOverride: TriState.Inherit,
            teamOverride: TriState.On,
            userOverride: TriState.Inherit);

        // Act
        var chain = config.GetResolutionChain();

        // Assert
        chain["System"].Should().BeFalse();
        chain["Organization"].Should().BeFalse();  // Inherits from System
        chain["Team"].Should().BeTrue();            // Explicitly On
        chain["User"].Should().BeTrue();            // Inherits from Team
    }

    [Fact]
    public void MultipleConfigs_IndependentInstances()
    {
        // Arrange
        var config1 = new HierarchicalConfig(true, TriState.Off, TriState.Inherit, TriState.Inherit);
        var config2 = new HierarchicalConfig(false, TriState.On, TriState.Inherit, TriState.Inherit);

        // Act & Assert
        config1.ResolveForUser().Should().BeFalse();
        config2.ResolveForUser().Should().BeTrue();
    }
}
