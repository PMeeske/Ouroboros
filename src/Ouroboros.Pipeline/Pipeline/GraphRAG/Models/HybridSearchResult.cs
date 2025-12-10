// <copyright file="HybridSearchResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents the result of a hybrid vector-symbolic search.
/// </summary>
/// <param name="Matches">The matching entities/documents from the search.</param>
/// <param name="Inferences">Logical inferences made during symbolic reasoning.</param>
/// <param name="ReasoningChain">The chain of reasoning steps that led to the results.</param>
public sealed record HybridSearchResult(
    IReadOnlyList<SearchMatch> Matches,
    IReadOnlyList<Inference> Inferences,
    IReadOnlyList<ReasoningChainStep> ReasoningChain)
{
    /// <summary>
    /// Gets the total relevance score for the result set.
    /// </summary>
    public double TotalRelevance => Matches.Sum(m => m.Relevance);

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    public static HybridSearchResult Empty => new([], [], []);

    /// <summary>
    /// Gets the top N matches by relevance.
    /// </summary>
    /// <param name="n">Number of matches to return.</param>
    /// <returns>Top N matches sorted by relevance.</returns>
    public IEnumerable<SearchMatch> TopMatches(int n) =>
        Matches.OrderByDescending(m => m.Relevance).Take(n);
}

/// <summary>
/// Represents a single match from the search.
/// </summary>
/// <param name="EntityId">The ID of the matched entity.</param>
/// <param name="EntityName">The name of the matched entity.</param>
/// <param name="EntityType">The type of the matched entity.</param>
/// <param name="Content">The text content associated with the match.</param>
/// <param name="Relevance">The combined relevance score (0.0 to 1.0).</param>
/// <param name="VectorScore">The vector similarity score.</param>
/// <param name="SymbolicScore">The symbolic reasoning score.</param>
public sealed record SearchMatch(
    string EntityId,
    string EntityName,
    string EntityType,
    string Content,
    double Relevance,
    double VectorScore,
    double SymbolicScore);

/// <summary>
/// Represents a logical inference made during symbolic reasoning.
/// </summary>
/// <param name="Premise">The premises used for the inference.</param>
/// <param name="Conclusion">The conclusion drawn.</param>
/// <param name="Confidence">Confidence level of the inference (0.0 to 1.0).</param>
/// <param name="Rule">The rule that was applied.</param>
public sealed record Inference(
    IReadOnlyList<string> Premise,
    string Conclusion,
    double Confidence,
    string? Rule = null);

/// <summary>
/// Represents a step in the reasoning chain.
/// </summary>
/// <param name="StepNumber">The step number in the chain.</param>
/// <param name="Operation">The operation performed (e.g., "Traverse", "Match", "Infer").</param>
/// <param name="Description">Human-readable description of the step.</param>
/// <param name="EntitiesInvolved">Entity IDs involved in this step.</param>
public sealed record ReasoningChainStep(
    int StepNumber,
    string Operation,
    string Description,
    IReadOnlyList<string> EntitiesInvolved);
