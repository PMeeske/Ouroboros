// <copyright file="IGraphExtractor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Pipeline.GraphRAG;

/// <summary>
/// Interface for extracting knowledge graphs from documents.
/// </summary>
public interface IGraphExtractor
{
    /// <summary>
    /// Extracts a knowledge graph from text content.
    /// </summary>
    /// <param name="content">The text content to extract from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the extracted knowledge graph or an error.</returns>
    Task<Result<KnowledgeGraph, string>> ExtractAsync(string content, CancellationToken ct = default);

    /// <summary>
    /// Extracts a knowledge graph from multiple documents.
    /// </summary>
    /// <param name="documents">The documents to extract from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the merged knowledge graph or an error.</returns>
    Task<Result<KnowledgeGraph, string>> ExtractFromDocumentsAsync(
        IEnumerable<(string Id, string Content)> documents,
        CancellationToken ct = default);

    /// <summary>
    /// Adds extracted entities to an existing graph.
    /// </summary>
    /// <param name="existingGraph">The existing graph to augment.</param>
    /// <param name="content">New content to extract from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the augmented graph or an error.</returns>
    Task<Result<KnowledgeGraph, string>> AugmentGraphAsync(
        KnowledgeGraph existingGraph,
        string content,
        CancellationToken ct = default);
}
