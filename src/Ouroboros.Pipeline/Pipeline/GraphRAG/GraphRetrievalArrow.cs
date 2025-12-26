// <copyright file="GraphRetrievalArrow.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Pipeline.GraphRAG;

/// <summary>
/// Provides arrow functions for graph-based retrieval operations in the pipeline.
/// </summary>
public static class GraphRetrievalArrow
{
    /// <summary>
    /// Creates a graph retrieval arrow that performs hybrid search and adds results to the pipeline.
    /// </summary>
    /// <param name="retriever">The hybrid retriever to use.</param>
    /// <param name="query">The search query.</param>
    /// <param name="config">Optional search configuration.</param>
    /// <returns>A step that transforms a pipeline branch with graph retrieval results.</returns>
    public static Step<PipelineBranch, (PipelineBranch Branch, HybridSearchResult Result)> Search(
        IHybridRetriever retriever,
        string query,
        HybridSearchConfig? config = null)
        => async branch =>
        {
            config ??= HybridSearchConfig.Default;
            var result = await retriever.SearchAsync(query, config);

            return result.Match(
                searchResult => (branch, searchResult),
                _ => (branch, HybridSearchResult.Empty));
        };

    /// <summary>
    /// Creates a Result-safe graph retrieval arrow with comprehensive error handling.
    /// </summary>
    /// <param name="retriever">The hybrid retriever to use.</param>
    /// <param name="query">The search query.</param>
    /// <param name="config">Optional search configuration.</param>
    /// <returns>A Kleisli arrow that returns a Result with the search results or error.</returns>
    public static KleisliResult<PipelineBranch, (PipelineBranch Branch, HybridSearchResult Result), string> SearchSafe(
        IHybridRetriever retriever,
        string query,
        HybridSearchConfig? config = null)
        => async branch =>
        {
            try
            {
                config ??= HybridSearchConfig.Default;
                var result = await retriever.SearchAsync(query, config);

                return result.Match(
                    searchResult => Result<(PipelineBranch, HybridSearchResult), string>.Success((branch, searchResult)),
                    error => Result<(PipelineBranch, HybridSearchResult), string>.Failure(error));
            }
            catch (Exception ex)
            {
                return Result<(PipelineBranch, HybridSearchResult), string>.Failure($"Graph retrieval failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a graph extraction arrow that extracts a knowledge graph from documents.
    /// </summary>
    /// <param name="extractor">The graph extractor to use.</param>
    /// <param name="contentSelector">Function to select content from the branch.</param>
    /// <returns>A step that extracts a knowledge graph.</returns>
    public static Step<PipelineBranch, (PipelineBranch Branch, KnowledgeGraph Graph)> Extract(
        IGraphExtractor extractor,
        Func<PipelineBranch, string> contentSelector)
        => async branch =>
        {
            var content = contentSelector(branch);
            var result = await extractor.ExtractAsync(content);

            return result.Match(
                graph => (branch, graph),
                _ => (branch, KnowledgeGraph.Empty));
        };

    /// <summary>
    /// Creates a multi-hop traversal arrow that explores the knowledge graph.
    /// </summary>
    /// <param name="retriever">The hybrid retriever to use.</param>
    /// <param name="startEntityId">The starting entity ID.</param>
    /// <param name="maxHops">Maximum number of hops.</param>
    /// <param name="relationshipTypes">Optional relationship type filter.</param>
    /// <returns>A step that returns a subgraph from traversal.</returns>
    public static Step<PipelineBranch, (PipelineBranch Branch, KnowledgeGraph Subgraph)> Traverse(
        IHybridRetriever retriever,
        string startEntityId,
        int maxHops = 2,
        IEnumerable<string>? relationshipTypes = null)
        => async branch =>
        {
            var result = await retriever.TraverseAsync(startEntityId, relationshipTypes, maxHops);

            return result.Match(
                subgraph => (branch, subgraph),
                _ => (branch, KnowledgeGraph.Empty));
        };

    /// <summary>
    /// Creates a composed pipeline that decomposes a query, executes the plan, and returns results.
    /// </summary>
    /// <param name="decomposer">The query decomposer.</param>
    /// <param name="retriever">The hybrid retriever.</param>
    /// <param name="query">The natural language query.</param>
    /// <param name="config">Optional search configuration.</param>
    /// <returns>A step that executes a full query pipeline.</returns>
    public static Step<PipelineBranch, (PipelineBranch Branch, HybridSearchResult Result, QueryPlan Plan)> ExecuteQuery(
        IQueryDecomposer decomposer,
        IHybridRetriever retriever,
        string query,
        HybridSearchConfig? config = null)
        => async branch =>
        {
            config ??= HybridSearchConfig.Default;

            // Decompose the query into a plan
            var planResult = await decomposer.DecomposeAsync(query);
            if (planResult.IsFailure)
            {
                return (branch, HybridSearchResult.Empty, QueryPlan.SingleHop(query));
            }

            var plan = planResult.Value;

            // Execute the plan
            var searchResult = await retriever.ExecutePlanAsync(plan, config);

            return searchResult.Match(
                result => (branch, result, plan),
                _ => (branch, HybridSearchResult.Empty, plan));
        };

    /// <summary>
    /// Formats search results as context for LLM prompts.
    /// </summary>
    /// <param name="result">The search result to format.</param>
    /// <param name="maxMatches">Maximum number of matches to include.</param>
    /// <returns>Formatted context string.</returns>
    public static string FormatAsContext(HybridSearchResult result, int maxMatches = 5)
    {
        var matches = result.TopMatches(maxMatches).ToList();
        if (matches.Count == 0)
        {
            return "No relevant information found.";
        }

        var sections = matches.Select((m, i) =>
            $"[{i + 1}] {m.EntityName} ({m.EntityType})\n" +
            $"Relevance: {m.Relevance:P0}\n" +
            $"Content: {m.Content}");

        return string.Join("\n\n", sections);
    }

    /// <summary>
    /// Formats the reasoning chain as an explanation.
    /// </summary>
    /// <param name="result">The search result containing the reasoning chain.</param>
    /// <returns>Human-readable explanation of how the result was derived.</returns>
    public static string FormatReasoningChain(HybridSearchResult result)
    {
        if (result.ReasoningChain.Count == 0)
        {
            return "No reasoning chain available.";
        }

        var steps = result.ReasoningChain.Select(step =>
            $"Step {step.StepNumber}: {step.Operation}\n  {step.Description}");

        return "Reasoning Chain:\n" + string.Join("\n", steps);
    }
}
