// <copyright file="HybridSearchConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Configuration for hybrid vector-symbolic retrieval.
/// </summary>
/// <param name="VectorWeight">Weight for vector similarity search results (0.0 to 1.0).</param>
/// <param name="SymbolicWeight">Weight for symbolic reasoning results (0.0 to 1.0).</param>
/// <param name="MaxResults">Maximum number of results to return.</param>
/// <param name="MaxHops">Maximum number of graph traversal hops for multi-hop queries.</param>
public sealed record HybridSearchConfig(
    double VectorWeight = 0.5,
    double SymbolicWeight = 0.5,
    int MaxResults = 10,
    int MaxHops = 2)
{
    /// <summary>
    /// Gets the default hybrid search configuration.
    /// </summary>
    public static HybridSearchConfig Default => new();

    /// <summary>
    /// Gets a vector-focused configuration.
    /// </summary>
    public static HybridSearchConfig VectorFocused => new(VectorWeight: 0.8, SymbolicWeight: 0.2);

    /// <summary>
    /// Gets a symbolic-focused configuration.
    /// </summary>
    public static HybridSearchConfig SymbolicFocused => new(VectorWeight: 0.2, SymbolicWeight: 0.8);

    /// <summary>
    /// Gets a deep traversal configuration for complex multi-hop queries.
    /// </summary>
    public static HybridSearchConfig DeepTraversal => new(MaxHops: 5, MaxResults: 20);

    /// <summary>
    /// Gets the similarity threshold for vector matches.
    /// </summary>
    public double SimilarityThreshold { get; init; } = 0.7;

    /// <summary>
    /// Gets whether to include explanation chains in results.
    /// </summary>
    public bool IncludeExplanation { get; init; } = true;
}
