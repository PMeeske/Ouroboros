// <copyright file="PolicyEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.Governance;

namespace Ouroboros.Tests.Governance;

/// <summary>
/// Tests for the Policy Engine.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
[Trait("Category", "Unit")]
public class PolicyEngineTests
{
    [Fact]
    public void RegisterPolicy_WithValidPolicy_ShouldSucceed()
    {
        // Arrange
        var engine = new PolicyEngine();
        var policy = Policy.Create("TestPolicy", "A test policy");

        // Act
        var result = engine.RegisterPolicy(policy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(policy);
    }

    [Fact]
    public void RegisterPolicy_WithDuplicateId_ShouldFail()
    {
        // Arrange
        var engine = new PolicyEngine();
        var policy = Policy.Create("TestPolicy", "A test policy");

        // Act
        engine.RegisterPolicy(policy);
        var result = engine.RegisterPolicy(policy);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public void GetPolicies_WithActiveFilter_ShouldReturnOnlyActivePolicies()
    {
        // Arrange
        var engine = new PolicyEngine();
        var activePolicy = Policy.Create("Active", "Active policy") with { IsActive = true };
        var inactivePolicy = Policy.Create("Inactive", "Inactive policy") with { IsActive = false };
        
        engine.RegisterPolicy(activePolicy);
        engine.RegisterPolicy(inactivePolicy);

        // Act
        var policies = engine.GetPolicies(activeOnly: true);

        // Assert
        policies.Should().ContainSingle();
        policies.First().Name.Should().Be("Active");
    }

    [Fact]
    public async Task EvaluatePolicyAsync_WithNoViolations_ShouldBeCompliant()
    {
        // Arrange
        var engine = new PolicyEngine();
        var policy = Policy.Create("TestPolicy", "Test policy");
        var context = new { value = 100 };

        // Act
        var result = await engine.EvaluatePolicyAsync(policy, context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task SimulatePolicyAsync_ShouldProvideSimulationDetails()
    {
        // Arrange
        var engine = new PolicyEngine();
        var policy = Policy.Create("TestPolicy", "Test policy");
        var context = new { value = 100 };

        // Act
        var result = await engine.SimulatePolicyAsync(policy, context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Policy.Should().Be(policy);
        result.Value.SimulatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SubmitApproval_ForNonExistentRequest_ShouldFail()
    {
        // Arrange
        var engine = new PolicyEngine();
        var approval = new Approval
        {
            ApproverId = "user1",
            Decision = ApprovalDecision.Approve
        };

        // Act
        var result = engine.SubmitApproval(Guid.NewGuid(), approval);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void GetAuditTrail_ShouldReturnRegisteredPolicies()
    {
        // Arrange
        var engine = new PolicyEngine();
        var policy = Policy.Create("TestPolicy", "Test policy");
        engine.RegisterPolicy(policy);

        // Act
        var auditTrail = engine.GetAuditTrail(limit: 10);

        // Assert
        auditTrail.Should().NotBeEmpty();
        auditTrail.First().Action.Should().Be("RegisterPolicy");
    }

    [Fact]
    public void GetPendingApprovals_WhenNoApprovals_ShouldReturnEmpty()
    {
        // Arrange
        var engine = new PolicyEngine();

        // Act
        var pending = engine.GetPendingApprovals();

        // Assert
        pending.Should().BeEmpty();
    }

    [Fact]
    public void RemovePolicy_WithValidId_ShouldSucceed()
    {
        // Arrange
        var engine = new PolicyEngine();
        var policy = Policy.Create("TestPolicy", "Test policy");
        engine.RegisterPolicy(policy);

        // Act
        var result = engine.RemovePolicy(policy.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.GetPolicies(activeOnly: false).Should().BeEmpty();
    }

    [Fact]
    public void RegisterConditionEvaluator_ShouldAllowCustomConditions()
    {
        // Arrange
        var engine = new PolicyEngine();
        var called = false;

        // Act
        engine.RegisterConditionEvaluator("test_condition", ctx =>
        {
            called = true;
            return true;
        });

        // Assert - Just verify no exception thrown
        called.Should().BeFalse(); // Not called yet
    }
}
