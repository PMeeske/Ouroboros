// <copyright file="HomeostasisPolicyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Affect;

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;
using Xunit;

/// <summary>
/// Tests for HomeostasisPolicy - Phase 3 Affective Dynamics.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HomeostasisPolicyTests
{
    [Fact]
    public void Constructor_InitializesDefaultRules()
    {
        // Arrange & Act
        var policy = new HomeostasisPolicy();
        var rules = policy.GetRules(activeOnly: false);

        // Assert
        rules.Should().NotBeEmpty();
        rules.Should().Contain(r => r.Name == "MaxStress");
        rules.Should().Contain(r => r.Name == "MinConfidence");
        rules.Should().Contain(r => r.Name == "CuriosityBalance");
    }

    [Fact]
    public void AddRule_CreatesNewRule()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        int initialCount = policy.GetRules(activeOnly: false).Count;

        // Act
        var rule = policy.AddRule(
            "TestRule",
            "A test rule",
            SignalType.Valence,
            -0.5,
            0.5,
            0.0,
            HomeostasisAction.Log);

        // Assert
        rule.Should().NotBeNull();
        rule.Name.Should().Be("TestRule");
        rule.TargetSignal.Should().Be(SignalType.Valence);
        rule.IsActive.Should().BeTrue();
        policy.GetRules(activeOnly: false).Should().HaveCount(initialCount + 1);
    }

    [Fact]
    public void UpdateRule_ModifiesBounds()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var rule = policy.AddRule("Test", "Test", SignalType.Stress, 0.0, 0.5, 0.2, HomeostasisAction.Log);

        // Act
        policy.UpdateRule(rule.Id, lowerBound: 0.1, upperBound: 0.8);

        // Assert
        var updated = policy.GetRules(activeOnly: false).First(r => r.Id == rule.Id);
        updated.LowerBound.Should().Be(0.1);
        updated.UpperBound.Should().Be(0.8);
    }

    [Fact]
    public void SetRuleActive_TogglesRule()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var rule = policy.AddRule("Test", "Test", SignalType.Stress, 0.0, 0.5, 0.2, HomeostasisAction.Log);

        // Act
        policy.SetRuleActive(rule.Id, false);

        // Assert
        var updated = policy.GetRules(activeOnly: false).First(r => r.Id == rule.Id);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public void GetRules_ActiveOnly_FiltersInactive()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var rule = policy.AddRule("Test", "Test", SignalType.Stress, 0.0, 0.5, 0.2, HomeostasisAction.Log);
        policy.SetRuleActive(rule.Id, false);

        // Act
        var activeRules = policy.GetRules(activeOnly: true);
        var allRules = policy.GetRules(activeOnly: false);

        // Assert
        activeRules.Should().NotContain(r => r.Id == rule.Id);
        allRules.Should().Contain(r => r.Id == rule.Id);
    }

    [Fact]
    public void EvaluatePolicies_DetectsViolation_AboveUpperBound()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        // MaxStress rule has upper bound of 0.8
        var state = new AffectiveState(
            Guid.NewGuid(),
            0.0, // valence
            0.95, // stress - above 0.8
            0.5, // confidence
            0.3, // curiosity
            0.5, // arousal
            DateTime.UtcNow,
            new Dictionary<string, object>());

        // Act
        var violations = policy.EvaluatePolicies(state);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain(v => v.RuleName == "MaxStress");
        violations.First(v => v.RuleName == "MaxStress").ViolationType.Should().Be("AboveUpperBound");
    }

    [Fact]
    public void EvaluatePolicies_DetectsViolation_BelowLowerBound()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        // MinConfidence rule has lower bound of 0.2
        var state = new AffectiveState(
            Guid.NewGuid(),
            0.0,
            0.3,
            0.1, // confidence - below 0.2
            0.3,
            0.5,
            DateTime.UtcNow,
            new Dictionary<string, object>());

        // Act
        var violations = policy.EvaluatePolicies(state);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain(v => v.RuleName == "MinConfidence");
    }

    [Fact]
    public void EvaluatePolicies_NoViolation_WithinBounds()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var state = new AffectiveState(
            Guid.NewGuid(),
            0.0,
            0.4, // stress - within bounds
            0.5, // confidence - within bounds
            0.4, // curiosity - within bounds
            0.5,
            DateTime.UtcNow,
            new Dictionary<string, object>());

        // Act
        var violations = policy.EvaluatePolicies(state);

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyCorrectionAsync_Log_Succeeds()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var monitor = new ValenceMonitor();
        var violation = new PolicyViolation(
            Guid.NewGuid(),
            "TestRule",
            SignalType.Stress,
            0.9,
            0.0,
            0.8,
            "AboveUpperBound",
            HomeostasisAction.Log,
            0.5,
            DateTime.UtcNow);

        // Act
        var result = await policy.ApplyCorrectionAsync(violation, monitor);

        // Assert
        result.Success.Should().BeTrue();
        result.ActionTaken.Should().Be(HomeostasisAction.Log);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_Reset_ResetsMonitor()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var monitor = new ValenceMonitor();
        monitor.RecordSignal("test", 0.9, SignalType.Stress);

        var violation = new PolicyViolation(
            Guid.NewGuid(),
            "TestRule",
            SignalType.Stress,
            0.9,
            0.0,
            0.8,
            "AboveUpperBound",
            HomeostasisAction.Reset,
            0.5,
            DateTime.UtcNow);

        // Act
        var result = await policy.ApplyCorrectionAsync(violation, monitor);
        var state = monitor.GetCurrentState();

        // Assert
        result.Success.Should().BeTrue();
        state.Stress.Should().Be(0.0);
    }

    [Fact]
    public void GetViolationHistory_ReturnsHistory()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var state = new AffectiveState(
            Guid.NewGuid(), 0.0, 0.95, 0.1, 0.3, 0.5, DateTime.UtcNow, new Dictionary<string, object>());

        // Act
        _ = policy.EvaluatePolicies(state);
        var history = policy.GetViolationHistory(10);

        // Assert
        history.Should().NotBeEmpty();
    }

    [Fact]
    public void GetHealthSummary_ReturnsValidSummary()
    {
        // Arrange
        var policy = new HomeostasisPolicy();

        // Act
        var summary = policy.GetHealthSummary();

        // Assert
        summary.TotalRules.Should().BeGreaterThan(0);
        summary.ActiveRules.Should().BeGreaterThan(0);
        summary.CorrectionSuccessRate.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void RegisterCustomHandler_AllowsCustomCorrections()
    {
        // Arrange
        var policy = new HomeostasisPolicy();
        var customCalled = false;

        policy.RegisterCustomHandler("CustomRule", async (violation, monitor) =>
        {
            customCalled = true;
            return new CorrectionResult(
                violation.RuleId,
                HomeostasisAction.Custom,
                true,
                "Custom handler executed",
                0.0,
                0.0,
                DateTime.UtcNow);
        });

        // Assert
        customCalled.Should().BeFalse(); // Just registering shouldn't call it
    }
}
