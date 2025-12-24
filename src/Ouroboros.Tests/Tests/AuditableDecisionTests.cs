// <copyright file="AuditableDecisionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using FluentAssertions;
using LangChainPipeline.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for AuditableDecision type.
/// Validates decision creation, evidence tracking, and compliance features.
/// </summary>
public class AuditableDecisionTests
{
    [Fact]
    public void Approve_CreatesMarkState()
    {
        // Arrange & Act
        var decision = AuditableDecision<string>.Approve(
            "test-value",
            "All checks passed",
            new[] { "Evidence 1", "Evidence 2" });

        // Assert
        decision.State.Should().Be(Form.Mark);
        decision.Value.HasValue.Should().BeTrue();
        decision.Value.Value.Should().Be("test-value");
        decision.Reasoning.Should().Be("All checks passed");
        decision.Evidence.Should().HaveCount(2);
        decision.RequiresHumanReview.Should().BeFalse();
        decision.ComplianceStatus.Should().Be("APPROVED");
    }

    [Fact]
    public void Reject_CreatesVoidState()
    {
        // Arrange & Act
        var decision = AuditableDecision<string>.Reject(
            "Failed validation",
            new[] { "Error 1", "Error 2" });

        // Assert
        decision.State.Should().Be(Form.Void);
        decision.Value.HasValue.Should().BeFalse();
        decision.Reasoning.Should().Be("Failed validation");
        decision.Evidence.Should().HaveCount(2);
        decision.RequiresHumanReview.Should().BeFalse();
        decision.ComplianceStatus.Should().Be("REJECTED");
    }

    [Fact]
    public void Inconclusive_CreatesImaginaryState()
    {
        // Arrange & Act
        var decision = AuditableDecision<string>.Inconclusive(
            0.65,
            "Uncertain result",
            new[] { "Signal 1", "Signal 2" });

        // Assert
        decision.State.Should().Be(Form.Imaginary);
        decision.Value.HasValue.Should().BeFalse();
        decision.ConfidencePhase.Should().Be(0.65);
        decision.Reasoning.Should().Be("Uncertain result");
        decision.Evidence.Should().HaveCount(2);
        decision.RequiresHumanReview.Should().BeTrue();
        decision.ComplianceStatus.Should().Contain("INCONCLUSIVE");
        decision.ComplianceStatus.Should().Contain("0.65");
    }

    [Fact]
    public void Inconclusive_ThrowsForInvalidPhase()
    {
        // Arrange & Act
        var act = () => AuditableDecision<string>.Inconclusive(
            1.5,
            "Invalid phase",
            null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("confidencePhase");
    }

    [Fact]
    public void ToAuditEntry_FormatsCorrectly()
    {
        // Arrange
        var decision = AuditableDecision<int>.Approve(
            42,
            "Test reasoning",
            new[] { "Evidence A", "Evidence B" });

        // Act
        var auditEntry = decision.ToAuditEntry();

        // Assert
        auditEntry.Should().Contain("APPROVED");
        auditEntry.Should().Contain("Test reasoning");
        auditEntry.Should().Contain("Evidence A");
        auditEntry.Should().Contain("Evidence B");
    }

    [Fact]
    public void Map_TransformsApprovedValue()
    {
        // Arrange
        var decision = AuditableDecision<int>.Approve(42, "Approved", null);

        // Act
        var mapped = decision.Map(x => x.ToString());

        // Assert
        mapped.State.Should().Be(Form.Mark);
        mapped.Value.HasValue.Should().BeTrue();
        mapped.Value.Value.Should().Be("42");
    }

    [Fact]
    public void Map_PreservesRejectedState()
    {
        // Arrange
        var decision = AuditableDecision<int>.Reject("Rejected", null);

        // Act
        var mapped = decision.Map(x => x.ToString());

        // Assert
        mapped.State.Should().Be(Form.Void);
        mapped.Value.HasValue.Should().BeFalse();
    }

    [Fact]
    public void And_BothApproved_ReturnsApproved()
    {
        // Arrange
        var decision1 = AuditableDecision<string>.Approve("A", "Check 1 passed", new[] { "Evidence 1" });
        var decision2 = AuditableDecision<string>.Approve("B", "Check 2 passed", new[] { "Evidence 2" });

        // Act
        var combined = decision1.And(decision2);

        // Assert
        combined.State.Should().Be(Form.Mark);
        combined.Value.HasValue.Should().BeTrue();
        combined.Value.Value.Should().Be(("A", "B"));
        combined.Evidence.Should().HaveCount(2);
    }

    [Fact]
    public void And_OneRejected_ReturnsRejected()
    {
        // Arrange
        var decision1 = AuditableDecision<string>.Approve("A", "Check 1 passed", null);
        var decision2 = AuditableDecision<string>.Reject("Check 2 failed", null);

        // Act
        var combined = decision1.And(decision2);

        // Assert
        combined.State.Should().Be(Form.Void);
        combined.Value.HasValue.Should().BeFalse();
    }

    [Fact]
    public void And_OneInconclusive_ReturnsInconclusive()
    {
        // Arrange
        var decision1 = AuditableDecision<string>.Approve("A", "Check 1 passed", null);
        var decision2 = AuditableDecision<string>.Inconclusive(0.7, "Check 2 uncertain", null);

        // Act
        var combined = decision1.And(decision2);

        // Assert
        combined.State.Should().Be(Form.Imaginary);
        combined.Value.HasValue.Should().BeFalse();
        combined.ConfidencePhase.Should().Be(0.7);
    }

    [Fact]
    public void And_CombinesEvidence()
    {
        // Arrange
        var decision1 = AuditableDecision<string>.Approve("A", "Reason 1", new[] { "Ev1", "Ev2" });
        var decision2 = AuditableDecision<string>.Approve("B", "Reason 2", new[] { "Ev3" });

        // Act
        var combined = decision1.And(decision2);

        // Assert
        combined.Evidence.Should().HaveCount(3);
        combined.Evidence.Should().Contain("Ev1");
        combined.Evidence.Should().Contain("Ev2");
        combined.Evidence.Should().Contain("Ev3");
    }

    [Fact]
    public void Timestamp_IsSet()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;

        // Act
        var decision = AuditableDecision<int>.Approve(42, "Test", null);

        // Assert
        var after = DateTimeOffset.UtcNow;
        decision.Timestamp.Should().BeOnOrAfter(before);
        decision.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void RequiresHumanReview_OnlyTrueForInconclusive()
    {
        // Arrange & Act
        var approved = AuditableDecision<int>.Approve(1, "Approved", null);
        var rejected = AuditableDecision<int>.Reject("Rejected", null);
        var inconclusive = AuditableDecision<int>.Inconclusive(0.5, "Uncertain", null);

        // Assert
        approved.RequiresHumanReview.Should().BeFalse();
        rejected.RequiresHumanReview.Should().BeFalse();
        inconclusive.RequiresHumanReview.Should().BeTrue();
    }
}
