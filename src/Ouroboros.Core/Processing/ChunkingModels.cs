// <copyright file="ChunkingModels.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Processing;

/// <summary>
/// Defines the chunking strategy for processing large contexts.
/// </summary>
public enum ChunkingStrategy
{
    /// <summary>
    /// Fixed chunk size - uses the specified maxChunkSize consistently.
    /// </summary>
    Fixed,

    /// <summary>
    /// Adaptive chunking - starts with maxChunkSize and adjusts based on processing success.
    /// Uses conditioned stimulus learning to optimize chunk size over time.
    /// </summary>
    Adaptive,
}

/// <summary>
/// Represents metadata about a chunk in the processing pipeline.
/// </summary>
public sealed record ChunkMetadata(
    int Index,
    int TotalChunks,
    int TokenCount,
    ChunkingStrategy Strategy);

/// <summary>
/// Represents an intermediate chunk result with its metadata.
/// </summary>
/// <typeparam name="TOutput">Type of the chunk output.</typeparam>
public sealed record ChunkResult<TOutput>(
    TOutput Output,
    ChunkMetadata Metadata,
    TimeSpan ProcessingTime,
    bool Success);
