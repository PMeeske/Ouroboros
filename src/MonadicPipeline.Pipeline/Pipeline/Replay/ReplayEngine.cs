#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace LangChainPipeline.Pipeline.Replay;

/// <summary>
/// Engine for replaying pipeline execution with fresh context and tool re-execution.
/// </summary>
public sealed class ReplayEngine(ToolAwareChatModel llm, IEmbeddingModel embed)
{
    /// <summary>
    /// Replays a pipeline branch, re-executing all reasoning steps with fresh context.
    /// </summary>
    /// <param name="branch">The branch to replay.</param>
    /// <param name="topic">The topic for context.</param>
    /// <param name="query">The query for retrieving relevant documents.</param>
    /// <param name="tools">The tool registry for schema generation.</param>
    /// <param name="k">Number of documents to retrieve for context.</param>
    /// <returns>A new branch with replayed execution.</returns>
    public async Task<PipelineBranch> ReplayAsync(PipelineBranch branch, string topic, string query, ToolRegistry tools, int k = 8)
    {
        PipelineBranch replayed = new PipelineBranch(branch.Name + "_replay", new TrackedVectorStore(), DataSource.FromPath(Environment.CurrentDirectory));

        // Copy vectors from the original branch
        BranchSnapshot snapshot = await BranchSnapshot.Capture(branch);
        await replayed.Store.AddAsync(snapshot.Vectors.Select(v => new Vector
        {
            Id = v.Id,
            Text = v.Text,
            Metadata = v.Metadata,
            Embedding = v.Embedding
        }));

        // Replay each reasoning step with fresh context
        foreach (ReasoningStep ev in branch.Events.OfType<ReasoningStep>())
        {
            // Refresh context with current state
            IReadOnlyCollection<Document> docs = await replayed.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            // Update prompt with fresh context and tool schemas
            string toolsSchemas = tools.ExportSchemas();
            string prompt = ev.Prompt
                .Replace("{context}", context)
                .Replace("{tools_schemas}", toolsSchemas);

            (string newText, List<ToolExecution> newTools) = await llm.GenerateWithToolsAsync(prompt);

            ReasoningState newState = ev.StepKind switch
            {
                "Draft" => new Draft(newText),
                "Critique" => new Critique(newText),
                "Final" => new FinalSpec(newText),
                _ => new Draft(newText)
            };

            replayed = replayed.WithReasoning(newState, prompt, newTools);
        }

        return replayed;
    }
}
