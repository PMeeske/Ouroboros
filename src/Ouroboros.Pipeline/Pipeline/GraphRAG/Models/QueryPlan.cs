// <copyright file="QueryPlan.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a decomposed query plan for hybrid retrieval.
/// </summary>
/// <param name="OriginalQuery">The original natural language query.</param>
/// <param name="QueryType">The type of query (SingleHop, MultiHop, Aggregation, Comparison).</param>
/// <param name="Steps">The sequence of steps to execute.</param>
public sealed record QueryPlan(
    string OriginalQuery,
    QueryType QueryType,
    IReadOnlyList<QueryStep> Steps)
{
    /// <summary>
    /// Creates a simple single-hop query plan.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <returns>A single-hop query plan.</returns>
    public static QueryPlan SingleHop(string query) =>
        new(query, QueryType.SingleHop, [new QueryStep(1, QueryStepType.VectorSearch, query, [])]);

    /// <summary>
    /// Gets the estimated complexity of executing this plan.
    /// </summary>
    public int EstimatedComplexity => Steps.Count + (QueryType == QueryType.MultiHop ? 2 : 0);
}

/// <summary>
/// Represents a step in a query plan.
/// </summary>
/// <param name="Order">The execution order of this step.</param>
/// <param name="StepType">The type of operation to perform.</param>
/// <param name="Query">The query or expression for this step.</param>
/// <param name="Dependencies">IDs of steps that must complete before this one.</param>
public sealed record QueryStep(
    int Order,
    QueryStepType StepType,
    string Query,
    IReadOnlyList<int> Dependencies)
{
    /// <summary>
    /// Gets the entity types to filter for this step.
    /// </summary>
    public IReadOnlyList<string>? EntityTypeFilter { get; init; }

    /// <summary>
    /// Gets the relationship types to traverse for this step.
    /// </summary>
    public IReadOnlyList<string>? RelationshipTypeFilter { get; init; }

    /// <summary>
    /// Gets the maximum number of hops for graph traversal.
    /// </summary>
    public int? MaxHops { get; init; }
}

/// <summary>
/// Types of queries supported by the hybrid retriever.
/// </summary>
public enum QueryType
{
    /// <summary>Direct entity lookup or simple similarity search.</summary>
    SingleHop,

    /// <summary>Query requiring traversal through multiple relationships.</summary>
    MultiHop,

    /// <summary>Query requiring aggregation of multiple entities.</summary>
    Aggregation,

    /// <summary>Query comparing properties of entities.</summary>
    Comparison
}

/// <summary>
/// Types of steps in a query plan.
/// </summary>
public enum QueryStepType
{
    /// <summary>Vector similarity search.</summary>
    VectorSearch,

    /// <summary>Graph traversal to find related entities.</summary>
    GraphTraversal,

    /// <summary>Symbolic pattern matching.</summary>
    SymbolicMatch,

    /// <summary>Entity type filtering.</summary>
    TypeFilter,

    /// <summary>Property-based filtering.</summary>
    PropertyFilter,

    /// <summary>Result aggregation.</summary>
    Aggregate,

    /// <summary>Result ranking and sorting.</summary>
    Rank,

    /// <summary>Logical inference step.</summary>
    Inference
}
