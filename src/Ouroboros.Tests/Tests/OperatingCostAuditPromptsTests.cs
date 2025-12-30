// <copyright file="OperatingCostAuditPromptsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

/// <summary>
/// Tests for the OperatingCostAuditPrompts templates.
/// </summary>
public class OperatingCostAuditPromptsTests
{
    [Fact]
    public void SystemPrompt_IsNotNull()
    {
        // Act
        var systemPrompt = OperatingCostAuditPrompts.SystemPrompt;

        // Assert
        systemPrompt.Should().NotBeNull();
    }

    [Fact]
    public void SystemPrompt_ContainsKeyConstraints()
    {
        // Act
        var systemPrompt = OperatingCostAuditPrompts.SystemPrompt.ToString();

        // Assert
        systemPrompt.Should().Contain("formal completeness");
        systemPrompt.Should().Contain("No legal advice");
        systemPrompt.Should().Contain("No statements about enforceability");
        systemPrompt.Should().Contain("Only formal completeness");
    }

    [Fact]
    public void AnalyzeMainStatement_IsNotNull()
    {
        // Act
        var prompt = OperatingCostAuditPrompts.AnalyzeMainStatement;

        // Assert
        prompt.Should().NotBeNull();
    }

    [Fact]
    public void AnalyzeMainStatement_Format_ReplacesPlaceholders()
    {
        // Arrange
        var prompt = OperatingCostAuditPrompts.AnalyzeMainStatement;

        // Act
        var formatted = prompt.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "test-schema" },
            { "main_statement", "Test operating cost statement" },
            { "context", "Additional context here" },
        });

        // Assert
        formatted.Should().Contain("test-schema");
        formatted.Should().Contain("Test operating cost statement");
        formatted.Should().Contain("Additional context here");
    }

    [Fact]
    public void AnalyzeMainStatement_ContainsRequiredDataPoints()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.AnalyzeMainStatement.ToString();

        // Assert - Check for all 7 required minimum data points
        promptText.Should().Contain("Total costs per cost category");
        promptText.Should().Contain("allocation key");
        promptText.Should().Contain("Total reference metric");
        promptText.Should().Contain("Allocated share for the tenant");
        promptText.Should().Contain("Calculated cost portion");
        promptText.Should().Contain("Deducted advance payments");
        promptText.Should().Contain("Resulting balance");
    }

    [Fact]
    public void AnalyzeMainStatement_ContainsEvaluationCategories()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.AnalyzeMainStatement.ToString();

        // Assert - Check for evaluation statuses
        promptText.Should().Contain("OK");
        promptText.Should().Contain("INDIRECT");
        promptText.Should().Contain("UNCLEAR");
        promptText.Should().Contain("MISSING");
        promptText.Should().Contain("INCONSISTENT");
    }

    [Fact]
    public void CompareWithHoaStatement_IsNotNull()
    {
        // Act
        var prompt = OperatingCostAuditPrompts.CompareWithHoaStatement;

        // Assert
        prompt.Should().NotBeNull();
    }

    [Fact]
    public void CompareWithHoaStatement_Format_ReplacesPlaceholders()
    {
        // Arrange
        var prompt = OperatingCostAuditPrompts.CompareWithHoaStatement;

        // Act
        var formatted = prompt.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "schema" },
            { "main_statement", "main-statement-text" },
            { "hoa_statement", "hoa-statement-text" },
            { "previous_analysis", "previous-analysis-text" },
        });

        // Assert
        formatted.Should().Contain("schema");
        formatted.Should().Contain("main-statement-text");
        formatted.Should().Contain("hoa-statement-text");
        formatted.Should().Contain("previous-analysis-text");
    }

    [Fact]
    public void CompareWithHoaStatement_ContainsComparisonTasks()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.CompareWithHoaStatement.ToString();

        // Assert
        promptText.Should().Contain("Verify that total costs");
        promptText.Should().Contain("discrepancies");
        promptText.Should().Contain("mathematical inconsistencies");
        promptText.Should().Contain("formal completeness check only");
    }

    [Fact]
    public void CheckAllocationRules_IsNotNull()
    {
        // Act
        var prompt = OperatingCostAuditPrompts.CheckAllocationRules;

        // Assert
        prompt.Should().NotBeNull();
    }

    [Fact]
    public void CheckAllocationRules_Format_ReplacesPlaceholders()
    {
        // Arrange
        var prompt = OperatingCostAuditPrompts.CheckAllocationRules;

        // Act
        var formatted = prompt.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "schema" },
            { "main_statement", "main-statement" },
            { "rental_agreement_rules", "rental-rules" },
            { "previous_analysis", "previous" },
        });

        // Assert
        formatted.Should().Contain("schema");
        formatted.Should().Contain("main-statement");
        formatted.Should().Contain("rental-rules");
        formatted.Should().Contain("previous");
    }

    [Fact]
    public void CheckAllocationRules_ContainsVerificationTasks()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.CheckAllocationRules.ToString();

        // Assert
        promptText.Should().Contain("Extract allocation rules");
        promptText.Should().Contain("Compare: applied allocation key vs contractual allocation key");
        promptText.Should().Contain("MATCH");
        promptText.Should().Contain("MISMATCH");
        promptText.Should().Contain("do not assess legal validity");
    }

    [Fact]
    public void GenerateAuditReport_IsNotNull()
    {
        // Act
        var prompt = OperatingCostAuditPrompts.GenerateAuditReport;

        // Assert
        prompt.Should().NotBeNull();
    }

    [Fact]
    public void GenerateAuditReport_Format_ReplacesPlaceholders()
    {
        // Arrange
        var prompt = OperatingCostAuditPrompts.GenerateAuditReport;

        // Act
        var formatted = prompt.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "schema" },
            { "analysis_results", "analysis-results" },
            { "critical_gaps", "gaps" },
        });

        // Assert
        formatted.Should().Contain("schema");
        formatted.Should().Contain("analysis-results");
        formatted.Should().Contain("gaps");
    }

    [Fact]
    public void GenerateAuditReport_ContainsJsonStructure()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.GenerateAuditReport.ToString();

        // Assert
        promptText.Should().Contain("documents_analyzed");
        promptText.Should().Contain("overall_formal_status");
        promptText.Should().Contain("categories");
        promptText.Should().Contain("critical_gaps");
        promptText.Should().Contain("summary_short");
        promptText.Should().Contain("note");
    }

    [Fact]
    public void GenerateAuditReport_ContainsStatusDefinitions()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.GenerateAuditReport.ToString();

        // Assert
        promptText.Should().Contain("complete");
        promptText.Should().Contain("incomplete");
        promptText.Should().Contain("not_auditable");
    }

    [Fact]
    public void GenerateAuditReport_ContainsLegalDisclaimer()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.GenerateAuditReport.ToString();

        // Assert
        promptText.Should().Contain("does not contain legal evaluation");
        promptText.Should().Contain("no legal advice");
    }

    [Fact]
    public void ExtractCostCategories_IsNotNull()
    {
        // Act
        var prompt = OperatingCostAuditPrompts.ExtractCostCategories;

        // Assert
        prompt.Should().NotBeNull();
    }

    [Fact]
    public void ExtractCostCategories_Format_ReplacesPlaceholders()
    {
        // Arrange
        var prompt = OperatingCostAuditPrompts.ExtractCostCategories;

        // Act
        var formatted = prompt.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "schema" },
            { "main_statement", "statement" },
            { "context", "context" },
        });

        // Assert
        formatted.Should().Contain("schema");
        formatted.Should().Contain("statement");
        formatted.Should().Contain("context");
    }

    [Fact]
    public void ExtractCostCategories_ContainsCategoryExamples()
    {
        // Act
        var promptText = OperatingCostAuditPrompts.ExtractCostCategories.ToString();

        // Assert
        promptText.Should().Contain("Heating");
        promptText.Should().Contain("Water");
        promptText.Should().Contain("Garbage");
        promptText.Should().Contain("Property tax");
        promptText.Should().Contain("Building insurance");
    }

    [Fact]
    public void AllPrompts_AreDistinct()
    {
        // Act
        var systemPrompt = OperatingCostAuditPrompts.SystemPrompt;
        var analyzePrompt = OperatingCostAuditPrompts.AnalyzeMainStatement;
        var comparePrompt = OperatingCostAuditPrompts.CompareWithHoaStatement;
        var checkPrompt = OperatingCostAuditPrompts.CheckAllocationRules;
        var reportPrompt = OperatingCostAuditPrompts.GenerateAuditReport;
        var extractPrompt = OperatingCostAuditPrompts.ExtractCostCategories;

        // Assert
        new[] { systemPrompt, analyzePrompt, comparePrompt, checkPrompt, reportPrompt, extractPrompt }
            .Select(p => p.ToString())
            .Distinct()
            .Should().HaveCount(6);
    }

    [Fact]
    public void AnalyzeMainStatement_RequiredVariables_AreCorrect()
    {
        // Act
        var required = OperatingCostAuditPrompts.AnalyzeMainStatement.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("main_statement");
        required.Should().Contain("context");
    }

    [Fact]
    public void CompareWithHoaStatement_RequiredVariables_AreCorrect()
    {
        // Act
        var required = OperatingCostAuditPrompts.CompareWithHoaStatement.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("main_statement");
        required.Should().Contain("hoa_statement");
        required.Should().Contain("previous_analysis");
    }

    [Fact]
    public void CheckAllocationRules_RequiredVariables_AreCorrect()
    {
        // Act
        var required = OperatingCostAuditPrompts.CheckAllocationRules.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("main_statement");
        required.Should().Contain("rental_agreement_rules");
        required.Should().Contain("previous_analysis");
    }

    [Fact]
    public void GenerateAuditReport_RequiredVariables_AreCorrect()
    {
        // Act
        var required = OperatingCostAuditPrompts.GenerateAuditReport.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("analysis_results");
        required.Should().Contain("critical_gaps");
    }

    [Fact]
    public void ExtractCostCategories_RequiredVariables_AreCorrect()
    {
        // Act
        var required = OperatingCostAuditPrompts.ExtractCostCategories.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("main_statement");
        required.Should().Contain("context");
    }
}
