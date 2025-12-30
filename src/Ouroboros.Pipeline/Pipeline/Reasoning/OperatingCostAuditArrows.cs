// <copyright file="OperatingCostAuditArrows.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using LangChain.DocumentLoaders;

namespace Ouroboros.Pipeline.Reasoning;

using Ouroboros.Domain.States;

/// <summary>
/// Provides arrow functions for Operating Cost Statement audit operations in the pipeline.
/// These arrows enable formal completeness audits without legal interpretation.
/// </summary>
public static class OperatingCostAuditArrows
{
    private static string ToolSchemasOrEmpty(ToolRegistry registry)
        => registry.ExportSchemas();

    /// <summary>
    /// Creates an arrow that extracts cost categories from the operating cost statement.
    /// This is the first step in the audit pipeline.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <param name="mainStatement">The main operating cost statement text.</param>
    /// <returns>A step that transforms a pipeline branch by adding extracted categories.</returns>
    public static Step<PipelineBranch, PipelineBranch> ExtractCategoriesArrow(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        string mainStatement)
        => async branch =>
        {
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(
                new NoOpEmbeddingModel(),
                "operating cost categories",
                amount: 5);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = OperatingCostAuditPrompts.ExtractCostCategories.Format(new()
            {
                ["tools_schemas"] = ToolSchemasOrEmpty(tools),
                ["main_statement"] = mainStatement,
                ["context"] = context,
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Draft(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates an arrow that analyzes the main operating cost statement for completeness.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <param name="mainStatement">The main operating cost statement text.</param>
    /// <returns>A step that transforms a pipeline branch by adding the main statement analysis.</returns>
    public static Step<PipelineBranch, PipelineBranch> AnalyzeMainStatementArrow(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        string mainStatement)
        => async branch =>
        {
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(
                new NoOpEmbeddingModel(),
                "operating cost analysis",
                amount: 5);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = OperatingCostAuditPrompts.AnalyzeMainStatement.Format(new()
            {
                ["tools_schemas"] = ToolSchemasOrEmpty(tools),
                ["main_statement"] = mainStatement,
                ["context"] = context,
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Draft(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe arrow for analyzing the main operating cost statement.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <param name="mainStatement">The main operating cost statement text.</param>
    /// <returns>A Kleisli arrow with Result-based error handling.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeAnalyzeMainStatementArrow(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        string mainStatement)
        => async branch =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mainStatement))
                {
                    return Result<PipelineBranch, string>.Failure("Main statement cannot be empty");
                }

                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(
                    new NoOpEmbeddingModel(),
                    "operating cost analysis",
                    amount: 5);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = OperatingCostAuditPrompts.AnalyzeMainStatement.Format(new()
                {
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools),
                    ["main_statement"] = mainStatement,
                    ["context"] = context,
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                PipelineBranch result = branch.WithReasoning(new Draft(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Main statement analysis failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates an arrow that compares the main statement with HOA/WEG documents.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <param name="mainStatement">The main operating cost statement text.</param>
    /// <param name="hoaStatement">The HOA/WEG cost statement text.</param>
    /// <returns>A step that transforms a pipeline branch by adding the comparison analysis.</returns>
    public static Step<PipelineBranch, PipelineBranch> CompareWithHoaArrow(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        string mainStatement,
        string hoaStatement)
        => async branch =>
        {
            string previousAnalysis = GetLatestAnalysis(branch);

            string prompt = OperatingCostAuditPrompts.CompareWithHoaStatement.Format(new()
            {
                ["tools_schemas"] = ToolSchemasOrEmpty(tools),
                ["main_statement"] = mainStatement,
                ["hoa_statement"] = hoaStatement,
                ["previous_analysis"] = previousAnalysis,
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Critique(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates an arrow that checks allocation rules against rental agreement.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <param name="mainStatement">The main operating cost statement text.</param>
    /// <param name="rentalAgreementRules">Rental agreement passages about allocation rules.</param>
    /// <returns>A step that transforms a pipeline branch by adding the allocation check.</returns>
    public static Step<PipelineBranch, PipelineBranch> CheckAllocationRulesArrow(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        string mainStatement,
        string rentalAgreementRules)
        => async branch =>
        {
            string previousAnalysis = GetLatestAnalysis(branch);

            string prompt = OperatingCostAuditPrompts.CheckAllocationRules.Format(new()
            {
                ["tools_schemas"] = ToolSchemasOrEmpty(tools),
                ["main_statement"] = mainStatement,
                ["rental_agreement_rules"] = rentalAgreementRules,
                ["previous_analysis"] = previousAnalysis,
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Critique(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates an arrow that generates the final audit report.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <returns>A step that transforms a pipeline branch by adding the final audit report.</returns>
    public static Step<PipelineBranch, PipelineBranch> GenerateAuditReportArrow(
        ToolAwareChatModel llm,
        ToolRegistry tools)
        => async branch =>
        {
            string analysisResults = GetAllAnalysisResults(branch);
            string criticalGaps = ExtractCriticalGaps(branch);

            string prompt = OperatingCostAuditPrompts.GenerateAuditReport.Format(new()
            {
                ["tools_schemas"] = ToolSchemasOrEmpty(tools),
                ["analysis_results"] = analysisResults,
                ["critical_gaps"] = criticalGaps,
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new FinalSpec(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe arrow for generating the final audit report.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <returns>A Kleisli arrow with Result-based error handling.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeGenerateAuditReportArrow(
        ToolAwareChatModel llm,
        ToolRegistry tools)
        => async branch =>
        {
            try
            {
                string analysisResults = GetAllAnalysisResults(branch);
                if (string.IsNullOrWhiteSpace(analysisResults))
                {
                    return Result<PipelineBranch, string>.Failure("No analysis results available to generate report");
                }

                string criticalGaps = ExtractCriticalGaps(branch);

                string prompt = OperatingCostAuditPrompts.GenerateAuditReport.Format(new()
                {
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools),
                    ["analysis_results"] = analysisResults,
                    ["critical_gaps"] = criticalGaps,
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                PipelineBranch result = branch.WithReasoning(new FinalSpec(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Audit report generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a complete safe audit pipeline that chains analysis steps with error handling.
    /// This is a simplified pipeline for basic audits without HOA comparison or rental agreement checks.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <param name="mainStatement">The main operating cost statement text.</param>
    /// <returns>A Kleisli arrow representing the complete audit pipeline.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeBasicAuditPipeline(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        string mainStatement)
        => SafeAnalyzeMainStatementArrow(llm, tools, mainStatement)
            .Then(SafeGenerateAuditReportArrow(llm, tools));

    /// <summary>
    /// Creates a full audit pipeline with HOA comparison and allocation rule checks.
    /// </summary>
    /// <param name="llm">The tool-aware language model for analysis.</param>
    /// <param name="tools">Registry of available tools.</param>
    /// <param name="mainStatement">The main operating cost statement text.</param>
    /// <param name="hoaStatement">Optional HOA/WEG cost statement text.</param>
    /// <param name="rentalAgreementRules">Optional rental agreement allocation rules.</param>
    /// <returns>A step representing the complete audit pipeline.</returns>
    public static Step<PipelineBranch, PipelineBranch> FullAuditPipeline(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        string mainStatement,
        string? hoaStatement = null,
        string? rentalAgreementRules = null)
    {
        // Start with main statement analysis
        Step<PipelineBranch, PipelineBranch> pipeline = AnalyzeMainStatementArrow(llm, tools, mainStatement);

        // Optionally add HOA comparison
        if (!string.IsNullOrWhiteSpace(hoaStatement))
        {
            pipeline = pipeline.Then(CompareWithHoaArrow(llm, tools, mainStatement, hoaStatement));
        }

        // Optionally add allocation rule checks
        if (!string.IsNullOrWhiteSpace(rentalAgreementRules))
        {
            pipeline = pipeline.Then(CheckAllocationRulesArrow(llm, tools, mainStatement, rentalAgreementRules));
        }

        // Generate final report
        pipeline = pipeline.Then(GenerateAuditReportArrow(llm, tools));

        return pipeline;
    }

    /// <summary>
    /// Gets the latest analysis text from the pipeline branch.
    /// </summary>
    private static string GetLatestAnalysis(PipelineBranch branch)
    {
        ReasoningState? latestState = branch.Events
            .OfType<ReasoningStep>()
            .Select(e => e.State)
            .LastOrDefault();

        return latestState?.Text ?? string.Empty;
    }

    /// <summary>
    /// Gets all analysis results from the pipeline branch.
    /// </summary>
    private static string GetAllAnalysisResults(PipelineBranch branch)
    {
        IEnumerable<string> analyses = branch.Events
            .OfType<ReasoningStep>()
            .Select(e => $"[{e.State.Kind}]\n{e.State.Text}");

        return string.Join("\n\n---\n\n", analyses);
    }

    /// <summary>
    /// Extracts critical gaps from critique steps in the pipeline.
    /// </summary>
    private static string ExtractCriticalGaps(PipelineBranch branch)
    {
        IEnumerable<string> critiques = branch.Events
            .OfType<ReasoningStep>()
            .Where(e => e.State is Critique)
            .Select(e => e.State.Text);

        if (!critiques.Any())
        {
            return "No specific gaps identified during analysis.";
        }

        return string.Join("\n", critiques);
    }
}

/// <summary>
/// A no-op embedding model used when actual embedding is not required.
/// </summary>
internal sealed class NoOpEmbeddingModel : IEmbeddingModel
{
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<float>());
}
