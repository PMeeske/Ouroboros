// <copyright file="AuditableDecisionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for AuditableDecision and Evidence types.
/// </summary>
[Trait("Category", "Unit")]
public class AuditableDecisionTests
{
    [Fact]
    public void Approve_CreatesApprovedDecision()
    {
        // Act
        var decision = AuditableDecision<string>.Approve(
            "test result",
            "All criteria passed",
            new Evidence("test", Form.Mark, "Test passed"));

        // Assert
        decision.Result.IsSuccess.Should().BeTrue();
        decision.Result.Value.Should().Be("test result");
        decision.Certainty.IsMark().Should().BeTrue();
        decision.Reasoning.Should().Be("All criteria passed");
        decision.EvidenceTrail.Should().HaveCount(1);
    }

    [Fact]
    public void Reject_CreatesRejectedDecision()
    {
        // Act
        var decision = AuditableDecision<string>.Reject(
            "Not authorized",
            "User lacks permissions",
            new Evidence("auth", Form.Void, "No permission"));

        // Assert
        decision.Result.IsFailure.Should().BeTrue();
        decision.Result.Error.Should().Be("Not authorized");
        decision.Certainty.IsVoid().Should().BeTrue();
        decision.Reasoning.Should().Be("User lacks permissions");
        decision.EvidenceTrail.Should().HaveCount(1);
    }

    [Fact]
    public void Uncertain_CreatesUncertainDecision()
    {
        // Act
        var decision = AuditableDecision<string>.Uncertain(
            "Unable to determine",
            "Conflicting criteria",
            new Evidence("check1", Form.Mark, "Passed"),
            new Evidence("check2", Form.Void, "Failed"));

        // Assert
        decision.Result.IsFailure.Should().BeTrue();
        decision.Certainty.IsImaginary().Should().BeTrue();
        decision.Reasoning.Should().Be("Conflicting criteria");
        decision.EvidenceTrail.Should().HaveCount(2);
    }

    [Fact]
    public void WithEvidence_AddsEvidenceToDecision()
    {
        // Arrange
        var decision = AuditableDecision<string>.Approve(
            "test",
            "initial",
            new Evidence("first", Form.Mark, "First check"));

        // Act
        var updated = decision.WithEvidence(
            new Evidence("second", Form.Mark, "Second check"));

        // Assert
        updated.EvidenceTrail.Should().HaveCount(2);
        updated.EvidenceTrail[0].CriterionName.Should().Be("first");
        updated.EvidenceTrail[1].CriterionName.Should().Be("second");
    }

    [Fact]
    public void WithMetadata_AddsMetadataToDecision()
    {
        // Arrange
        var decision = AuditableDecision<string>.Approve(
            "test",
            "reasoning");

        // Act
        var updated = decision.WithMetadata("key1", "value1")
                              .WithMetadata("key2", "value2");

        // Assert
        updated.Metadata.Should().HaveCount(2);
        updated.Metadata["key1"].Should().Be("value1");
        updated.Metadata["key2"].Should().Be("value2");
    }

    [Fact]
    public void ToAuditEntry_GeneratesFormattedLog()
    {
        // Arrange
        var decision = AuditableDecision<string>.Approve(
            "result",
            "test reasoning",
            new Evidence("criterion1", Form.Mark, "Passed"),
            new Evidence("criterion2", Form.Mark, "Also passed"));

        var updated = decision.WithMetadata("user", "admin");

        // Act
        var auditEntry = updated.ToAuditEntry();

        // Assert
        auditEntry.Should().Contain("Decision: ⌐");
        auditEntry.Should().Contain("Result: Success");
        auditEntry.Should().Contain("Reasoning: test reasoning");
        auditEntry.Should().Contain("criterion1: ⌐ (Passed)");
        auditEntry.Should().Contain("criterion2: ⌐ (Also passed)");
        auditEntry.Should().Contain("user: admin");
    }

    [Fact]
    public void Match_OnApproved_ExecutesCertainFunction()
    {
        // Arrange
        var decision = AuditableDecision<string>.Approve(
            "success",
            "test");

        // Act
        var result = decision.Match(
            onCertain: value => $"Approved: {value}",
            onRejected: error => $"Rejected: {error}",
            onUncertain: error => $"Uncertain: {error}");

        // Assert
        result.Should().Be("Approved: success");
    }

    [Fact]
    public void Match_OnRejected_ExecutesRejectedFunction()
    {
        // Arrange
        var decision = AuditableDecision<string>.Reject(
            "unauthorized",
            "test");

        // Act
        var result = decision.Match(
            onCertain: value => $"Approved: {value}",
            onRejected: error => $"Rejected: {error}",
            onUncertain: error => $"Uncertain: {error}");

        // Assert
        result.Should().Be("Rejected: unauthorized");
    }

    [Fact]
    public void Match_OnUncertain_ExecutesUncertainFunction()
    {
        // Arrange
        var decision = AuditableDecision<string>.Uncertain(
            "needs review",
            "test");

        // Act
        var result = decision.Match(
            onCertain: value => $"Approved: {value}",
            onRejected: error => $"Rejected: {error}",
            onUncertain: error => $"Uncertain: {error}");

        // Assert
        result.Should().Be("Uncertain: needs review");
    }

    [Fact]
    public void Evidence_StoresAllProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var evidence = new Evidence(
            "test_criterion",
            Form.Mark,
            "Test description",
            timestamp);

        // Assert
        evidence.CriterionName.Should().Be("test_criterion");
        evidence.Evaluation.IsMark().Should().BeTrue();
        evidence.Description.Should().Be("Test description");
        evidence.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Evidence_DefaultTimestamp_UsesUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var evidence = new Evidence(
            "test",
            Form.Mark,
            "description");

        var after = DateTime.UtcNow;

        // Assert
        evidence.Timestamp.Should().BeOnOrAfter(before);
        evidence.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Decision_DefaultTimestamp_UsesUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var decision = AuditableDecision<string>.Approve(
            "test",
            "reasoning");

        var after = DateTime.UtcNow;

        // Assert
        decision.Timestamp.Should().BeOnOrAfter(before);
        decision.Timestamp.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Decision_WithEmptyMetadata_HasEmptyDictionary()
    {
        // Act
        var decision = AuditableDecision<string>.Approve(
            "test",
            "reasoning");

        // Assert
        decision.Metadata.Should().NotBeNull();
        decision.Metadata.Should().BeEmpty();
    }
}
