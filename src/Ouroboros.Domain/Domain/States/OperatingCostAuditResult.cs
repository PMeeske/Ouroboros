// <copyright file="OperatingCostAuditResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.States;

using System.Text.Json.Serialization;

/// <summary>
/// Evaluation status for a data field in the operating cost audit.
/// </summary>
public enum FieldStatus
{
    /// <summary>Directly visible on the main statement.</summary>
    OK,

    /// <summary>Derivable only from attachments via manual reconstruction.</summary>
    INDIRECT,

    /// <summary>Metric present but not identified as living area/MEA/unit/person.</summary>
    UNCLEAR,

    /// <summary>Not provided anywhere.</summary>
    MISSING,

    /// <summary>Conflicting data between documents.</summary>
    INCONSISTENT,
}

/// <summary>
/// Overall formal status of the operating cost statement audit.
/// </summary>
public enum FormalStatus
{
    /// <summary>All required fields are present and properly documented.</summary>
    Complete,

    /// <summary>Some required fields are missing or unclear.</summary>
    Incomplete,

    /// <summary>Critical information missing, cannot perform audit.</summary>
    NotAuditable,
}

/// <summary>
/// Represents the audit results for a single cost category.
/// </summary>
/// <param name="Category">The cost category name (e.g., heating, water, garbage).</param>
/// <param name="TotalCosts">Status of total costs visibility.</param>
/// <param name="ReferenceMetric">Status of declared allocation key/reference metric.</param>
/// <param name="TotalReferenceValue">Status of total reference metric value.</param>
/// <param name="TenantShare">Status of allocated share for the tenant.</param>
/// <param name="TenantCost">Status of calculated cost portion for the tenant.</param>
/// <param name="Balance">Status of resulting balance (credit/amount due).</param>
/// <param name="Comment">Optional comment explaining the evaluation.</param>
public sealed record CostCategoryAudit(
    string Category,
    FieldStatus TotalCosts,
    FieldStatus ReferenceMetric,
    FieldStatus TotalReferenceValue,
    FieldStatus TenantShare,
    FieldStatus TenantCost,
    FieldStatus Balance,
    string? Comment = null);

/// <summary>
/// Represents the complete audit result for an operating cost statement.
/// This is a non-legal, formal completeness check.
/// </summary>
/// <param name="DocumentsAnalyzed">Whether documents were successfully analyzed.</param>
/// <param name="OverallFormalStatus">Overall status of the audit.</param>
/// <param name="Categories">Audit results for each cost category.</param>
/// <param name="CriticalGaps">List of critical gaps identified.</param>
/// <param name="SummaryShort">Brief summary of the audit findings.</param>
/// <param name="Note">Legal disclaimer note.</param>
public sealed record OperatingCostAuditResult(
    bool DocumentsAnalyzed,
    FormalStatus OverallFormalStatus,
    IReadOnlyList<CostCategoryAudit> Categories,
    IReadOnlyList<string> CriticalGaps,
    string SummaryShort,
    string Note = "This output does not contain legal evaluation or statements on validity or enforceability.")
    : ReasoningState("OperatingCostAudit", SummaryShort)
{
    /// <summary>
    /// Gets the JSON representation of the audit result.
    /// </summary>
    [JsonIgnore]
    public string AsJson => System.Text.Json.JsonSerializer.Serialize(
        new
        {
            documents_analyzed = DocumentsAnalyzed,
            overall_formal_status = OverallFormalStatus.ToString().ToLowerInvariant(),
            categories = Categories.Select(c => new
            {
                category = c.Category,
                total_costs = c.TotalCosts.ToString(),
                reference_metric = c.ReferenceMetric.ToString(),
                total_reference_value = c.TotalReferenceValue.ToString(),
                tenant_share = c.TenantShare.ToString(),
                tenant_cost = c.TenantCost.ToString(),
                balance = c.Balance.ToString(),
                comment = c.Comment,
            }),
            critical_gaps = CriticalGaps,
            summary_short = SummaryShort,
            note = Note,
        },
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
}
