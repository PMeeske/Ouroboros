// <copyright file="OperatingCostAuditPrompts.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.Pipeline.Reasoning;

/// <summary>
/// Prompt templates for Operating Cost Statement formal completeness audits.
/// These prompts enable non-legal structured completeness analysis of operating cost statements.
/// </summary>
public static class OperatingCostAuditPrompts
{
    /// <summary>
    /// System prompt for the operating cost audit module.
    /// Sets up the LLM to perform formal completeness checks without legal interpretation.
    /// </summary>
    public static readonly PromptTemplate SystemPrompt = new(@"
You are a formal completeness auditor for operating cost statements.

Your role is to analyze documents solely for formal completeness and traceability of minimum information.
You do NOT provide legal interpretation or assess enforceability.

EXPLICIT CONSTRAINTS:
- No legal advice
- No statements about enforceability, validity, deadlines, or payment obligations
- Only formal completeness & auditability checks

Your analysis should identify whether required minimum data points are clearly visible without reconstruction or assumptions.
");

    /// <summary>
    /// Prompt template for analyzing the main operating cost statement.
    /// Checks for the seven required minimum data points.
    /// </summary>
    public static readonly PromptTemplate AnalyzeMainStatement = new(@"
Analyze the provided operating cost statement for formal completeness.

Available tools (JSON Schema):
{tools_schemas}

Operating Cost Statement:
{main_statement}

Additional Context (if provided):
{context}

REQUIRED MINIMUM DATA POINTS TO VERIFY:
1. Total costs per cost category (not reconstructed from attachments)
2. Declared allocation key / reference metric (e.g., living area, ownership shares/MEA, points, occupants, units)
3. Total reference metric (e.g., total living area, total ownership shares, total units)
4. Allocated share for the tenant (as percentage, physical share, or explicit fraction)
5. Calculated cost portion attributable to the tenant (not self-derived)
6. Deducted advance payments
7. Resulting balance (credit / amount due)

For each cost category found, evaluate each required field with one of these statuses:
- OK → directly visible on the main statement
- INDIRECT → derivable only from attachments via manual reconstruction
- UNCLEAR → metric present, but not identified as living area / MEA / unit / person / etc.
- MISSING → not provided anywhere
- INCONSISTENT → conflicting data between documents

If you need to verify calculations or cross-reference data, use available tools.

Provide your analysis in a structured format listing each cost category and its field statuses.
");

    /// <summary>
    /// Prompt template for comparing the main statement with HOA/WEG documents.
    /// </summary>
    public static readonly PromptTemplate CompareWithHoaStatement = new(@"
Compare the operating cost statement with the condominium association (HOA/WEG) cost statement.

Available tools (JSON Schema):
{tools_schemas}

Operating Cost Statement:
{main_statement}

HOA/WEG Cost Statement:
{hoa_statement}

Previous Analysis:
{previous_analysis}

COMPARISON TASKS:
1. Verify that total costs in the main statement match the HOA/WEG statement
2. Identify any discrepancies in allocation keys or reference metrics
3. Check for cost categories present in HOA/WEG but missing in main statement
4. Flag any mathematical inconsistencies between documents

For each discrepancy found, indicate:
- The cost category affected
- The nature of the discrepancy
- Which document contains which value

This is a formal completeness check only - do not provide legal interpretation.
");

    /// <summary>
    /// Prompt template for checking allocation rules against rental agreement.
    /// </summary>
    public static readonly PromptTemplate CheckAllocationRules = new(@"
Compare the allocation rules applied in the operating cost statement against the rental agreement.

Available tools (JSON Schema):
{tools_schemas}

Operating Cost Statement:
{main_statement}

Rental Agreement Allocation Rules:
{rental_agreement_rules}

Previous Analysis:
{previous_analysis}

VERIFICATION TASKS:
1. Extract allocation rules from the rental agreement passages
2. Compare: applied allocation key vs contractual allocation key for each category
3. Flag any mismatches between contractual and applied allocation methods

For each cost category, report:
- Contractual allocation method (from rental agreement)
- Applied allocation method (from operating cost statement)
- Match status: MATCH / MISMATCH / UNCLEAR / NOT_SPECIFIED

This is a formal completeness check only - do not assess legal validity of allocation methods.
");

    /// <summary>
    /// Prompt template for generating the final audit report.
    /// </summary>
    public static readonly PromptTemplate GenerateAuditReport = new(@"
Generate a formal completeness audit report based on the analysis performed.

Available tools (JSON Schema):
{tools_schemas}

Analysis Results:
{analysis_results}

Critical Gaps Identified:
{critical_gaps}

REPORT REQUIREMENTS:
Generate a JSON output with this structure:
{{
  ""documents_analyzed"": true,
  ""overall_formal_status"": ""complete | incomplete | not_auditable"",
  ""categories"": [
    {{
      ""category"": ""[category name]"",
      ""total_costs"": ""[OK|INDIRECT|UNCLEAR|MISSING|INCONSISTENT]"",
      ""reference_metric"": ""[status]"",
      ""total_reference_value"": ""[status]"",
      ""tenant_share"": ""[status]"",
      ""tenant_cost"": ""[status]"",
      ""balance"": ""[status]"",
      ""comment"": ""[optional explanation]""
    }}
  ],
  ""critical_gaps"": [
    ""[list of critical issues]""
  ],
  ""summary_short"": ""[Brief summary of audit findings]"",
  ""note"": ""This output does not contain legal evaluation or statements on validity or enforceability.""
}}

DETERMINE overall_formal_status AS:
- ""complete"": All required fields OK for all categories
- ""incomplete"": Some fields MISSING, UNCLEAR, or INDIRECT
- ""not_auditable"": Critical information missing, cannot perform meaningful audit

Ensure the note disclaimer is always included.
This is a formal completeness check only - no legal advice or enforceability assessment.
");

    /// <summary>
    /// Prompt template for extracting cost categories from documents.
    /// </summary>
    public static readonly PromptTemplate ExtractCostCategories = new(@"
Extract all cost categories from the provided operating cost statement.

Available tools (JSON Schema):
{tools_schemas}

Operating Cost Statement:
{main_statement}

Additional Documents:
{context}

EXTRACTION TASK:
List all operating cost categories found in the documents, such as:
- Heating (Heizkosten)
- Water/Wastewater (Wasser/Abwasser)
- Garbage collection (Müllabfuhr)
- Property tax (Grundsteuer)
- Building insurance (Gebäudeversicherung)
- Property management (Hausverwaltung)
- Cleaning (Reinigung)
- Garden maintenance (Gartenpflege)
- Elevator (Aufzug)
- Common electricity (Allgemeinstrom)
- Other operating costs

For each category found, extract:
1. Category name as stated in the document
2. Total amount shown (if visible)
3. Allocation key mentioned (if any)
4. Tenant's share amount (if shown)

Output as a structured list.
");
}
