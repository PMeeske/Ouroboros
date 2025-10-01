using LangChain.DocumentLoaders;
using LangChain.Providers;
using IEmbeddingModel = LangChain.Providers.IEmbeddingModel;

namespace LangChainPipeline.Pipeline.Reasoning;

/// <summary>
/// Provides arrow functions for reasoning operations in the pipeline.
/// Enhanced with Result monad for better error handling.
/// </summary>
public static class ReasoningArrows
{
    private static string ToolSchemasOrEmpty(ToolRegistry registry)
        => registry.ExportSchemas();

    /// <summary>
    /// Creates a draft arrow that generates an initial response.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> DraftArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Draft.Format(new()
            {
                ["context"] = context,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Draft(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe draft arrow that generates an initial response with error handling.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeDraftArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            try
            {
                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = Prompts.Draft.Format(new()
                {
                    ["context"] = context,
                    ["topic"] = topic,
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools)
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                var result = branch.WithReasoning(new Draft(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Draft generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a critique arrow that analyzes and critiques the draft.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> CritiqueArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            Draft? draft = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Draft>().LastOrDefault();
            if (draft is null) return branch;

            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Critique.Format(new()
            {
                ["context"] = context,
                ["draft"] = draft.DraftText,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Critique(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe critique arrow with proper error handling and validation.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeCritiqueArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            try
            {
                Draft? draft = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Draft>().LastOrDefault();
                if (draft is null) 
                    return Result<PipelineBranch, string>.Failure("No draft found to critique");

                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = Prompts.Critique.Format(new()
                {
                    ["context"] = context,
                    ["draft"] = draft.DraftText,
                    ["topic"] = topic,
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools)
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                var result = branch.WithReasoning(new Critique(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Critique generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates an improvement arrow that generates a final improved version.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> ImproveArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            Draft? draft = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Draft>().LastOrDefault();
            Critique? critique = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Critique>().LastOrDefault();
            if (draft is null || critique is null) return branch;

            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Improve.Format(new()
            {
                ["context"] = context,
                ["draft"] = draft.DraftText,
                ["critique"] = critique.CritiqueText,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new FinalSpec(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe improvement arrow with comprehensive error handling.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeImproveArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            try
            {
                Draft? draft = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Draft>().LastOrDefault();
                Critique? critique = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Critique>().LastOrDefault();
                
                if (draft is null) 
                    return Result<PipelineBranch, string>.Failure("No draft found for improvement");
                if (critique is null) 
                    return Result<PipelineBranch, string>.Failure("No critique found for improvement");

                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = Prompts.Improve.Format(new()
                {
                    ["context"] = context,
                    ["draft"] = draft.DraftText,
                    ["critique"] = critique.CritiqueText,
                    ["topic"] = topic,
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools)
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                var result = branch.WithReasoning(new FinalSpec(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Improvement generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a complete safe reasoning pipeline that chains draft -> critique -> improve with error handling.
    /// Demonstrates monadic composition for robust pipeline execution.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeReasoningPipeline(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => SafeDraftArrow(llm, tools, embed, topic, query, k)
            .Then(SafeCritiqueArrow(llm, tools, embed, topic, query, k))
            .Then(SafeImproveArrow(llm, tools, embed, topic, query, k));
}
