using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers.Ollama;
using LangChainPipeline.Core;
using LangChainPipeline.Tools;

namespace LangChainPipeline.Pipeline.Reasoning;

/// <summary>
/// Provides arrow functions for reasoning operations in the pipeline.
/// </summary>
public static class ReasoningArrows
{
    private static string ToolSchemasOrEmpty(ToolRegistry registry)
        => registry.ExportSchemas();

    /// <summary>
    /// Creates a draft arrow that generates an initial response.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> DraftArrow(
        ToolAwareChatModel llm, ToolRegistry tools, OllamaEmbeddingModel embed, string topic, string query, int k = 8)
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
            branch.AddReasoning(new Draft(text), prompt, toolCalls);
            return branch;
        };

    /// <summary>
    /// Creates a critique arrow that analyzes and critiques the draft.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> CritiqueArrow(
        ToolAwareChatModel llm, ToolRegistry tools, OllamaEmbeddingModel embed, string topic, string query, int k = 8)
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
            branch.AddReasoning(new Critique(text), prompt, toolCalls);
            return branch;
        };

    /// <summary>
    /// Creates an improvement arrow that generates a final improved version.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> ImproveArrow(
        ToolAwareChatModel llm, ToolRegistry tools, OllamaEmbeddingModel embed, string topic, string query, int k = 8)
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
            branch.AddReasoning(new FinalSpec(text), prompt, toolCalls);
            return branch;
        };
}
