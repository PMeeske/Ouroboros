// <copyright file="DecisionPipelineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for DecisionPipeline composition and evaluation.
/// Validates KYC-style scenarios and multi-criteria decision-making.
/// </summary>
[Trait("Category", "Unit")]
public class DecisionPipelineTests
{
    private record Application(string Name, int CreditScore, bool IdVerified, bool AddressVerified);

    [Fact]
    public void Evaluate_AllCriteriaPass_ReturnsApproved()
    {
        // Arrange
        var app = new Application("John Doe", 750, true, true);

        var criteria = new Func<Application, AuditableDecision<Application>>[]
        {
            a => CheckCreditScore(a),
            a => CheckIdVerification(a),
            a => CheckAddressVerification(a),
        };

        // Act
        var decision = DecisionPipeline.Evaluate(
            app,
            criteria,
            a => a);

        // Assert
        decision.State.Should().Be(Form.Mark);
        (decision.Value != null).Should().BeTrue();
        decision.RequiresHumanReview.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OneCriterionFails_ReturnsRejected()
    {
        // Arrange
        var app = new Application("Jane Doe", 550, true, true);

        var criteria = new Func<Application, AuditableDecision<Application>>[]
        {
            a => CheckCreditScore(a),
            a => CheckIdVerification(a),
            a => CheckAddressVerification(a),
        };

        // Act
        var decision = DecisionPipeline.Evaluate(
            app,
            criteria,
            a => a);

        // Assert
        decision.State.Should().Be(Form.Void);
        (decision.Value == null).Should().BeTrue();
        decision.Reasoning.Should().Contain("Failed criteria");
    }

    [Fact]
    public void Evaluate_OneCriterionInconclusive_ReturnsInconclusive()
    {
        // Arrange
        var app = new Application("Bob Smith", 650, true, true);

        var criteria = new Func<Application, AuditableDecision<Application>>[]
        {
            a => CheckCreditScore(a),
            a => CheckIdVerification(a),
            a => CheckAddressVerification(a),
        };

        // Act
        var decision = DecisionPipeline.Evaluate(
            app,
            criteria,
            a => a);

        // Assert
        decision.State.Should().Be(Form.Imaginary);
        (decision.Value == null).Should().BeTrue();
        decision.RequiresHumanReview.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CombinesAllEvidence()
    {
        // Arrange
        var app = new Application("Alice", 750, true, true);

        var criteria = new Func<Application, AuditableDecision<Application>>[]
        {
            a => CheckCreditScore(a),
            a => CheckIdVerification(a),
        };

        // Act
        var decision = DecisionPipeline.Evaluate(
            app,
            criteria,
            a => a);

        // Assert
        decision.Evidence.Should().Contain(e => e.CriterionName.Contains("Credit"));
        decision.Evidence.Should().Contain(e => e.CriterionName.Contains("Id"));
    }

    [Fact]
    public void EvaluateAny_AtLeastOnePasses_ReturnsApproved()
    {
        // Arrange
        var app = new Application("Charlie", 550, true, false);

        var criteria = new Func<Application, AuditableDecision<Application>>[]
        {
            a => CheckCreditScore(a),      // Fails
            a => CheckIdVerification(a),   // Passes
        };

        // Act
        var decision = DecisionPipeline.EvaluateAny(app, criteria);

        // Assert
        decision.State.Should().Be(Form.Mark);
        (decision.Value != null).Should().BeTrue();
    }

    [Fact]
    public void EvaluateAny_AllFail_ReturnsRejected()
    {
        // Arrange
        var app = new Application("David", 550, false, false);

        var criteria = new Func<Application, AuditableDecision<Application>>[]
        {
            a => CheckCreditScore(a),      // Fails
            a => CheckIdVerification(a),   // Fails
        };

        // Act
        var decision = DecisionPipeline.EvaluateAny(app, criteria);

        // Assert
        decision.State.Should().Be(Form.Void);
        (decision.Value == null).Should().BeTrue();
    }

    [Fact]
    public void Chain_StopsAtFirstFailure()
    {
        // Arrange
        var initial = AuditableDecision<int>.Approve(10, "Initial");

        var step1 = (int x) => AuditableDecision<int>.Approve(x * 2, "Step 1");
        var step2 = (int x) => AuditableDecision<int>.Reject("error", "Step 2 failed");
        var step3 = (int x) => AuditableDecision<int>.Approve(x * 3, "Step 3");

        // Act
        var result = DecisionPipeline.Chain(initial, step1, step2, step3);

        // Assert
        result.State.Should().Be(Form.Void);
        result.Reasoning.Should().Contain("Step 2 failed");
        // Step 3 should not have been executed
    }

    [Fact]
    public void Chain_AllStepsPass_CombinesReasoning()
    {
        // Arrange
        var initial = AuditableDecision<int>.Approve(10, "Initial");

        var step1 = (int x) => AuditableDecision<int>.Approve(x * 2, "Doubled");
        var step2 = (int x) => AuditableDecision<int>.Approve(x + 5, "Added 5");

        // Act
        var result = DecisionPipeline.Chain(initial, step1, step2);

        // Assert
        result.State.Should().Be(Form.Mark);
        result.Value.Should().Be(25); // (10 * 2) + 5
        result.Reasoning.Should().Contain("Initial");
        result.Reasoning.Should().Contain("Doubled");
        result.Reasoning.Should().Contain("Added 5");
    }

    [Fact]
    public void Evaluate_ThrowsForNoCriteria()
    {
        // Arrange
        var app = new Application("Test", 700, true, true);
        var emptyCriteria = Array.Empty<Func<Application, AuditableDecision<Application>>>();

        // Act
        var act = () => DecisionPipeline.Evaluate(app, emptyCriteria, a => a);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("criteria");
    }

    [Fact]
    public void KycScenario_CompleteApprovalFlow()
    {
        // Arrange - Full KYC scenario
        var perfectApp = new Application("Perfect Customer", 800, true, true);

        var kycCriteria = new Func<Application, AuditableDecision<Application>>[]
        {
            a => CheckCreditScore(a),
            a => CheckIdVerification(a),
            a => CheckAddressVerification(a),
        };

        // Act
        var decision = DecisionPipeline.Evaluate(
            perfectApp,
            kycCriteria,
            a => a);

        // Assert
        decision.State.Should().Be(Form.Mark);
        decision.ComplianceStatus.Should().Be("APPROVED");
        decision.Evidence.Should().HaveCountGreaterThanOrEqualTo(3);
        decision.RequiresHumanReview.Should().BeFalse();

        var auditLog = decision.ToAuditEntry();
        auditLog.Should().Contain("Credit");
    }

    // Helper methods simulating KYC checks
    private static AuditableDecision<Application> CheckCreditScore(Application app)
    {
        if (app.CreditScore >= 700)
        {
            return AuditableDecision<Application>.Approve(
                app,
                "Credit score acceptable",
                new Evidence("CreditScore", Form.Mark, $"Credit score: {app.CreditScore}"));
        }

        if (app.CreditScore >= 600)
        {
            return AuditableDecision<Application>.Inconclusive(
                0.5,
                "Borderline credit score - manual review needed",
                new Evidence("CreditScore", Form.Imaginary, $"Credit score: {app.CreditScore}"));
        }

        return AuditableDecision<Application>.Reject(
            "Credit score too low",
            "Credit score below minimum threshold",
            new Evidence("CreditScore", Form.Void, $"Credit score: {app.CreditScore}"));
    }

    private static AuditableDecision<Application> CheckIdVerification(Application app)
    {
        return app.IdVerified
            ? AuditableDecision<Application>.Approve(app, "ID verified", new Evidence("IdVerification", Form.Mark, "ID check passed"))
            : AuditableDecision<Application>.Reject("ID not verified", "ID verification failed", new Evidence("IdVerification", Form.Void, "ID check failed"));
    }

    private static AuditableDecision<Application> CheckAddressVerification(Application app)
    {
        return app.AddressVerified
            ? AuditableDecision<Application>.Approve(app, "Address verified", new Evidence("AddressVerification", Form.Mark, "Address check passed"))
            : AuditableDecision<Application>.Reject("Address not verified", "Address verification failed", new Evidence("AddressVerification", Form.Void, "Address check failed"));
    }
}
