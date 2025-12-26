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
            new Evidence("Evidence 1", Form.Mark, "Evidence 1"),
            new Evidence("Evidence 2", Form.Mark, "Evidence 2"));

        // Assert
        decision.State.Should().Be(Form.Mark);
        (decision.Value != null).Should().BeTrue();
        decision.Value.Should().Be("test-value");
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
            "Failed validation",
            new Evidence("Error 1", Form.Void, "Error 1"),
            new Evidence("Error 2", Form.Void, "Error 2"));

        // Assert
        decision.State.Should().Be(Form.Void);
        (decision.Value == null).Should().BeTrue();
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
            new Evidence("Signal 1", Form.Imaginary, "Signal 1"),
            new Evidence("Signal 2", Form.Imaginary, "Signal 2"));

        // Assert
        decision.State.Should().Be(Form.Imaginary);
        (decision.Value == null).Should().BeTrue();
        decision.ConfidencePhase.Should().Be(0.65);
        decision.Reasoning.Should().Be("Uncertain result");
        decision.Evidence.Should().HaveCount(2);
        decision.RequiresHumanReview.Should().BeTrue();
        decision.ComplianceStatus.Should().Contain("INCONCLUSIVE");
        decision.ComplianceStatus.Should().Contain("65");
    }

    [Fact]
    public void Inconclusive_ThrowsForInvalidPhase()
    {
        // Arrange & Act
        var act = () => AuditableDecision<string>.Inconclusive(
            1.5,
            "Invalid phase");

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
            new Evidence("Evidence A", Form.Mark, "Evidence A"),
            new Evidence("Evidence B", Form.Mark, "Evidence B"));

        // Act
        var auditEntry = decision.ToAuditEntry();

        // Assert
        auditEntry.Should().Contain("Test reasoning");
        auditEntry.Should().Contain("Evidence A");
        auditEntry.Should().Contain("Evidence B");
    }

    [Fact]
    public void Timestamp_IsSet()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var decision = AuditableDecision<int>.Approve(42, "Test");

        // Assert
        var after = DateTime.UtcNow;
        decision.Timestamp.Should().BeOnOrAfter(before);
        decision.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void RequiresHumanReview_OnlyTrueForInconclusive()
    {
        // Arrange & Act
        var approved = AuditableDecision<int>.Approve(1, "Approved");
        var rejected = AuditableDecision<int>.Reject("error", "Rejected");
        var inconclusive = AuditableDecision<int>.Inconclusive(0.5, "Uncertain");

        // Assert
        approved.RequiresHumanReview.Should().BeFalse();
        rejected.RequiresHumanReview.Should().BeFalse();
        inconclusive.RequiresHumanReview.Should().BeTrue();
    }
}
