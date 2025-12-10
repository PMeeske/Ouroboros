// <copyright file="IQueryDecomposer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Pipeline.GraphRAG.Models;

namespace LangChainPipeline.Pipeline.GraphRAG;

/// <summary>
/// Interface for decomposing natural language queries into executable plans.
/// </summary>
public interface IQueryDecomposer
{
    /// <summary>
    /// Decomposes a natural language query into a query plan.
    /// </summary>
    /// <param name="query">The natural language query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the query plan or an error.</returns>
    Task<Result<QueryPlan, string>> DecomposeAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Classifies the type of query.
    /// </summary>
    /// <param name="query">The query to classify.</param>
    /// <returns>The classified query type.</returns>
    QueryType ClassifyQuery(string query);

    /// <summary>
    /// Extracts entity mentions from a query.
    /// </summary>
    /// <param name="query">The query to analyze.</param>
    /// <returns>List of entity mentions with their types.</returns>
    IReadOnlyList<(string Mention, string? ExpectedType)> ExtractEntityMentions(string query);

    /// <summary>
    /// Extracts relationship patterns from a query.
    /// </summary>
    /// <param name="query">The query to analyze.</param>
    /// <returns>List of relationship patterns.</returns>
    IReadOnlyList<string> ExtractRelationshipPatterns(string query);
}
