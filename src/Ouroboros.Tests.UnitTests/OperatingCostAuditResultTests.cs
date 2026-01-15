// <copyright file="OperatingCostAuditResultTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

/// <summary>
/// Tests for the OperatingCostAuditResult model and related types.
/// </summary>
[Trait("Category", "Unit")]
public class OperatingCostAuditResultTests
{
    [Fact]
    public void FieldStatus_HasAllExpectedValues()
    {
        // Assert
        Enum.GetValues<FieldStatus>().Should().HaveCount(5);
        Enum.IsDefined(FieldStatus.OK).Should().BeTrue();
        Enum.IsDefined(FieldStatus.INDIRECT).Should().BeTrue();
        Enum.IsDefined(FieldStatus.UNCLEAR).Should().BeTrue();
        Enum.IsDefined(FieldStatus.MISSING).Should().BeTrue();
        Enum.IsDefined(FieldStatus.INCONSISTENT).Should().BeTrue();
    }

    [Fact]
    public void FormalStatus_HasAllExpectedValues()
    {
        // Assert
        Enum.GetValues<FormalStatus>().Should().HaveCount(3);
        Enum.IsDefined(FormalStatus.Complete).Should().BeTrue();
        Enum.IsDefined(FormalStatus.Incomplete).Should().BeTrue();
        Enum.IsDefined(FormalStatus.NotAuditable).Should().BeTrue();
    }

    [Fact]
    public void CostCategoryAudit_CreatesWithAllFields()
    {
        // Arrange & Act
        var audit = new CostCategoryAudit(
            Category: "heating",
            TotalCosts: FieldStatus.OK,
            ReferenceMetric: FieldStatus.UNCLEAR,
            TotalReferenceValue: FieldStatus.MISSING,
            TenantShare: FieldStatus.INDIRECT,
            TenantCost: FieldStatus.OK,
            Balance: FieldStatus.OK,
            Comment: "Reference metric not clearly defined");

        // Assert
        audit.Category.Should().Be("heating");
        audit.TotalCosts.Should().Be(FieldStatus.OK);
        audit.ReferenceMetric.Should().Be(FieldStatus.UNCLEAR);
        audit.TotalReferenceValue.Should().Be(FieldStatus.MISSING);
        audit.TenantShare.Should().Be(FieldStatus.INDIRECT);
        audit.TenantCost.Should().Be(FieldStatus.OK);
        audit.Balance.Should().Be(FieldStatus.OK);
        audit.Comment.Should().Be("Reference metric not clearly defined");
    }

    [Fact]
    public void CostCategoryAudit_CommentIsOptional()
    {
        // Arrange & Act
        var audit = new CostCategoryAudit(
            Category: "water",
            TotalCosts: FieldStatus.OK,
            ReferenceMetric: FieldStatus.OK,
            TotalReferenceValue: FieldStatus.OK,
            TenantShare: FieldStatus.OK,
            TenantCost: FieldStatus.OK,
            Balance: FieldStatus.OK);

        // Assert
        audit.Comment.Should().BeNull();
    }

    [Fact]
    public void OperatingCostAuditResult_CreatesWithRequiredFields()
    {
        // Arrange
        var categories = new List<CostCategoryAudit>
        {
            new CostCategoryAudit(
                "heating",
                FieldStatus.OK,
                FieldStatus.OK,
                FieldStatus.OK,
                FieldStatus.OK,
                FieldStatus.OK,
                FieldStatus.OK),
        };
        var criticalGaps = new List<string>();

        // Act
        var result = new OperatingCostAuditResult(
            DocumentsAnalyzed: true,
            OverallFormalStatus: FormalStatus.Complete,
            Categories: categories,
            CriticalGaps: criticalGaps,
            SummaryShort: "All fields complete");

        // Assert
        result.DocumentsAnalyzed.Should().BeTrue();
        result.OverallFormalStatus.Should().Be(FormalStatus.Complete);
        result.Categories.Should().HaveCount(1);
        result.CriticalGaps.Should().BeEmpty();
        result.SummaryShort.Should().Be("All fields complete");
        result.Note.Should().Be("This output does not contain legal evaluation or statements on validity or enforceability.");
    }

    [Fact]
    public void OperatingCostAuditResult_InheritsFromReasoningState()
    {
        // Arrange
        var result = new OperatingCostAuditResult(
            DocumentsAnalyzed: true,
            OverallFormalStatus: FormalStatus.Complete,
            Categories: new List<CostCategoryAudit>(),
            CriticalGaps: new List<string>(),
            SummaryShort: "Test summary");

        // Assert
        result.Should().BeAssignableTo<ReasoningState>();
        result.Kind.Should().Be("OperatingCostAudit");
        result.Text.Should().Be("Test summary");
    }

    [Fact]
    public void OperatingCostAuditResult_AsJson_ReturnsValidJson()
    {
        // Arrange
        var categories = new List<CostCategoryAudit>
        {
            new CostCategoryAudit(
                "heating",
                FieldStatus.OK,
                FieldStatus.UNCLEAR,
                FieldStatus.MISSING,
                FieldStatus.INDIRECT,
                FieldStatus.OK,
                FieldStatus.OK,
                "Reference metric not clearly defined"),
        };
        var criticalGaps = new List<string>
        {
            "reference metrics not labeled",
            "total reference metric not given",
        };
        var result = new OperatingCostAuditResult(
            DocumentsAnalyzed: true,
            OverallFormalStatus: FormalStatus.Incomplete,
            Categories: categories,
            CriticalGaps: criticalGaps,
            SummaryShort: "The statement is not formally auditable");

        // Act
        var json = result.AsJson;

        // Assert
        json.Should().Contain("\"documents_analyzed\": true");
        json.Should().Contain("\"overall_formal_status\": \"incomplete\"");
        json.Should().Contain("\"category\": \"heating\"");
        json.Should().Contain("\"total_costs\": \"OK\"");
        json.Should().Contain("\"reference_metric\": \"UNCLEAR\"");
        json.Should().Contain("\"total_reference_value\": \"MISSING\"");
        json.Should().Contain("\"tenant_share\": \"INDIRECT\"");
        json.Should().Contain("reference metrics not labeled");
        json.Should().Contain("does not contain legal evaluation");
    }

    [Fact]
    public void OperatingCostAuditResult_WithCustomNote_OverridesDefaultNote()
    {
        // Arrange & Act
        var result = new OperatingCostAuditResult(
            DocumentsAnalyzed: true,
            OverallFormalStatus: FormalStatus.Complete,
            Categories: new List<CostCategoryAudit>(),
            CriticalGaps: new List<string>(),
            SummaryShort: "Test",
            Note: "Custom disclaimer");

        // Assert
        result.Note.Should().Be("Custom disclaimer");
    }

    [Fact]
    public void OperatingCostAuditResult_WithIncompleteStatus_IncludesCriticalGaps()
    {
        // Arrange
        var categories = new List<CostCategoryAudit>
        {
            new CostCategoryAudit(
                "garbage",
                FieldStatus.OK,
                FieldStatus.MISSING,
                FieldStatus.MISSING,
                FieldStatus.MISSING,
                FieldStatus.INDIRECT,
                FieldStatus.OK,
                "Allocation key not specified"),
        };
        var criticalGaps = new List<string>
        {
            "No allocation key for garbage collection",
            "Tenant share not explicitly stated",
        };

        // Act
        var result = new OperatingCostAuditResult(
            DocumentsAnalyzed: true,
            OverallFormalStatus: FormalStatus.Incomplete,
            Categories: categories,
            CriticalGaps: criticalGaps,
            SummaryShort: "Statement incomplete due to missing allocation information");

        // Assert
        result.CriticalGaps.Should().HaveCount(2);
        result.OverallFormalStatus.Should().Be(FormalStatus.Incomplete);
    }

    [Fact]
    public void OperatingCostAuditResult_NotAuditableStatus_WhenCriticalFieldsMissing()
    {
        // Arrange
        var categories = new List<CostCategoryAudit>
        {
            new CostCategoryAudit(
                "total",
                FieldStatus.MISSING,
                FieldStatus.MISSING,
                FieldStatus.MISSING,
                FieldStatus.MISSING,
                FieldStatus.MISSING,
                FieldStatus.MISSING,
                "No data available"),
        };
        var criticalGaps = new List<string>
        {
            "No cost categories identified",
            "No amounts visible",
            "Cannot perform meaningful audit",
        };

        // Act
        var result = new OperatingCostAuditResult(
            DocumentsAnalyzed: false,
            OverallFormalStatus: FormalStatus.NotAuditable,
            Categories: categories,
            CriticalGaps: criticalGaps,
            SummaryShort: "Documents could not be analyzed");

        // Assert
        result.DocumentsAnalyzed.Should().BeFalse();
        result.OverallFormalStatus.Should().Be(FormalStatus.NotAuditable);
        result.CriticalGaps.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(FieldStatus.OK, "OK")]
    [InlineData(FieldStatus.INDIRECT, "INDIRECT")]
    [InlineData(FieldStatus.UNCLEAR, "UNCLEAR")]
    [InlineData(FieldStatus.MISSING, "MISSING")]
    [InlineData(FieldStatus.INCONSISTENT, "INCONSISTENT")]
    public void FieldStatus_ToString_ReturnsExpectedValue(FieldStatus status, string expected)
    {
        // Act
        var result = status.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(FormalStatus.Complete, "Complete")]
    [InlineData(FormalStatus.Incomplete, "Incomplete")]
    [InlineData(FormalStatus.NotAuditable, "NotAuditable")]
    public void FormalStatus_ToString_ReturnsExpectedValue(FormalStatus status, string expected)
    {
        // Act
        var result = status.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CostCategoryAudit_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var audit1 = new CostCategoryAudit(
            "heating",
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            "Test");
        var audit2 = new CostCategoryAudit(
            "heating",
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            "Test");
        var audit3 = new CostCategoryAudit(
            "water",
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            FieldStatus.OK,
            "Test");

        // Assert
        audit1.Should().Be(audit2);
        audit1.Should().NotBe(audit3);
    }
}
