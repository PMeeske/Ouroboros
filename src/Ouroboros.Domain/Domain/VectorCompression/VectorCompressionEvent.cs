// <copyright file="VectorCompressionEvent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.VectorCompression;

/// <summary>
/// Event representing a vector compression operation.
/// Immutable record for event sourcing pattern.
/// </summary>
public sealed record VectorCompressionEvent
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
    /// Gets the timestamp of the compression operation.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets optional metadata about the compression.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a new compression event from compression details.
    /// </summary>
    public static VectorCompressionEvent Create(
        string method,
        long originalBytes,
        long compressedBytes,
        double energyRetained,
        Dictionary<string, object>? metadata = null)
    {
        return new VectorCompressionEvent
        {
            Method = method,
            OriginalBytes = originalBytes,
            CompressedBytes = compressedBytes,
            EnergyRetained = energyRetained,
            Timestamp = DateTime.UtcNow,
            Metadata = (metadata as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Statistics computed from compression events.
/// </summary>
public sealed record VectorCompressionStats
{
    /// <summary>
    /// Gets the total number of vectors compressed.
    /// </summary>
    public required int VectorsCompressed { get; init; }

    /// <summary>
    /// Gets the total original size in bytes.
    /// </summary>
    public required long TotalOriginalBytes { get; init; }

    /// <summary>
    /// Gets the total compressed size in bytes.
    /// </summary>
    public required long TotalCompressedBytes { get; init; }

    /// <summary>
    /// Gets the average compression ratio.
    /// </summary>
    public double AverageCompressionRatio =>
        VectorsCompressed > 0 && TotalCompressedBytes > 0
            ? (double)TotalOriginalBytes / TotalCompressedBytes
            : 1.0;

    /// <summary>
    /// Gets the average energy retained.
    /// </summary>
    public required double AverageEnergyRetained { get; init; }

    /// <summary>
    /// Gets the first compression timestamp.
    /// </summary>
    public DateTime? FirstCompressionAt { get; init; }

    /// <summary>
    /// Gets the last compression timestamp.
    /// </summary>
    public DateTime? LastCompressionAt { get; init; }

    /// <summary>
    /// Gets the breakdown by compression method.
    /// </summary>
    public IReadOnlyDictionary<string, int> MethodBreakdown { get; init; } = new Dictionary<string, int>();
}
