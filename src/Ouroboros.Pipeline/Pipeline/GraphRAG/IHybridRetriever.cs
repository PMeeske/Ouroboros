// <copyright file="IHybridRetriever.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Pipeline.GraphRAG;

/// <summary>
/// Interface for hybrid vector-symbolic retrieval combining dense embeddings with symbolic reasoning.
/// </summary>
public interface IHybridRetriever
{
    /// <summary>
    /// Performs a hybrid search combining vector similarity and symbolic reasoning.
    /// </summary>
    /// <param name="query">The natural language query.</param>
    /// <param name="config">Search configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the search results or an error.</returns>
    Task<Result<HybridSearchResult, string>> SearchAsync(
        string query,
        HybridSearchConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Performs a hybrid search using a pre-computed query plan.
    /// </summary>
    /// <param name="plan">The query plan to execute.</param>
    /// <param name="config">Search configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the search results or an error.</returns>
    Task<Result<HybridSearchResult, string>> ExecutePlanAsync(
        QueryPlan plan,
        HybridSearchConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Performs multi-hop graph traversal to answer complex queries.
    /// </summary>
    /// <param name="startEntityId">The starting entity ID.</param>
    /// <param name="relationshipTypes">Types of relationships to follow.</param>
    /// <param name="maxHops">Maximum number of hops.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the subgraph or an error.</returns>
    Task<Result<KnowledgeGraph, string>> TraverseAsync(
        string startEntityId,
        IEnumerable<string>? relationshipTypes = null,
        int maxHops = 2,
        CancellationToken ct = default);
}
