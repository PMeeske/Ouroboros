// <copyright file="IngestBatch.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Events;

/// <summary>
/// Event representing a batch of documents ingested into the pipeline.
/// Tracks the source and document IDs for vector store operations.
/// </summary>
/// <param name="Id">Unique identifier for this ingestion event</param>
/// <param name="Source">The source path or identifier of the ingested data</param>
/// <param name="Ids">List of document IDs that were ingested</param>
/// <param name="Timestamp">When the ingestion occurred</param>
public sealed record IngestBatch(
    Guid Id,
    string Source,
    IReadOnlyList<string> Ids,
    DateTime Timestamp) : PipelineEvent(Id, "Ingest", Timestamp);
