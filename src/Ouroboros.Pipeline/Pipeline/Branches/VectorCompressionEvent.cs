// <copyright file="VectorCompressionEvent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.Events;
using DomainCompressionEvent = Ouroboros.Domain.VectorCompression.VectorCompressionEvent;

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Pipeline event for vector compression operations.
/// Wraps the domain event for integration with PipelineBranch event sourcing.
/// </summary>
public sealed record VectorCompressionEvent(Guid Id, DateTime Timestamp) 
    : PipelineEvent(Id, "VectorCompression", Timestamp)
{
    /// <summary>
    /// Gets the compression method used.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets the original size in bytes.
    /// </summary>
    public required long OriginalBytes { get; init; }

    /// <summary>
    /// Gets the compressed size in bytes.
    /// </summary>
    public required long CompressedBytes { get; init; }

    /// <summary>
    /// Gets the energy retained (0.0-1.0).
    /// </summary>
    public required double EnergyRetained { get; init; }

    /// <summary>
    /// Gets the compression ratio.
    /// </summary>
    public double CompressionRatio => OriginalBytes > 0 ? (double)OriginalBytes / CompressedBytes : 1.0;

    /// <summary>
    /// Gets optional metadata about the compression.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a pipeline event from a domain compression event.
    /// </summary>
    public static VectorCompressionEvent FromDomainEvent(DomainCompressionEvent domainEvent)
    {
        return new VectorCompressionEvent(Guid.NewGuid(), domainEvent.Timestamp)
        {
            Method = domainEvent.Method,
            OriginalBytes = domainEvent.OriginalBytes,
            CompressedBytes = domainEvent.CompressedBytes,
            EnergyRetained = domainEvent.EnergyRetained,
            Metadata = domainEvent.Metadata
        };
    }

    /// <summary>
    /// Converts this pipeline event back to a domain event.
    /// </summary>
    public DomainCompressionEvent ToDomainEvent()
    {
        return new DomainCompressionEvent
        {
            Method = Method,
            OriginalBytes = OriginalBytes,
            CompressedBytes = CompressedBytes,
            EnergyRetained = EnergyRetained,
            Timestamp = Timestamp,
            Metadata = Metadata
        };
    }
}


