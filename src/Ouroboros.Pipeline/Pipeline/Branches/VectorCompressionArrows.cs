// <copyright file="VectorCompressionArrows.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.VectorCompression;

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Pipeline arrows for vector compression operations with event sourcing.
/// Wraps VectorCompressionService with PipelineBranch integration.
/// </summary>
public static class VectorCompressionArrows
{
    /// <summary>
    /// Compresses a vector and returns both compressed data and updated branch with tracking event.
    /// Pure functional arrow pattern following Kleisli composition.
    /// </summary>
    /// <param name="vector">Input embedding vector.</param>
    /// <param name="config">Compression configuration.</param>
    /// <param name="method">Compression method (null = use config default).</param>
    /// <returns>A step that returns a Result containing compressed data and updated branch.</returns>
    public static Step<PipelineBranch, Result<(byte[] CompressedData, PipelineBranch UpdatedBranch)>> CompressArrow(
        float[] vector,
        CompressionConfig config,
        CompressionMethod? method = null) =>
        async branch =>
        {
            var result = VectorCompressionService.Compress(vector, config, method);

            if (result.IsFailure)
            {
                return Result<(byte[] CompressedData, PipelineBranch UpdatedBranch)>.Failure(result.Error);
            }

            // Convert VectorCompressionEvent to PipelineEvent
            var pipelineEvent = VectorCompressionEvent.FromDomainEvent(result.Value.Event);
            var updatedBranch = branch.WithEvent(pipelineEvent);

            return Result<(byte[] CompressedData, PipelineBranch UpdatedBranch)>.Success(
                (result.Value.CompressedData, updatedBranch));
        };

    /// <summary>
    /// Batch compress multiple vectors with event tracking.
    /// Returns compressed data and updated branch with all compression events.
    /// </summary>
    public static async Task<Result<(IReadOnlyList<byte[]> CompressedData, PipelineBranch UpdatedBranch)>> BatchCompressAsync(
        PipelineBranch branch,
        IEnumerable<float[]> vectors,
        CompressionConfig config,
        CompressionMethod? method = null)
    {
        try
        {
            var result = await VectorCompressionService.BatchCompressAsync(vectors, config, method);

            if (result.IsFailure)
            {
                return Result<(IReadOnlyList<byte[]>, PipelineBranch)>.Failure(result.Error);
            }

            var (compressedData, events) = result.Value;

            // Add all events to the branch
            var updatedBranch = branch;
            foreach (var domainEvent in events)
            {
                var pipelineEvent = VectorCompressionEvent.FromDomainEvent(domainEvent);
                updatedBranch = updatedBranch.WithEvent(pipelineEvent);
            }

            return Result<(IReadOnlyList<byte[]>, PipelineBranch)>.Success((compressedData, updatedBranch));
        }
        catch (Exception ex)
        {
            return Result<(IReadOnlyList<byte[]>, PipelineBranch)>.Failure($"Batch compression failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets compression statistics from branch events.
    /// </summary>
    public static Result<VectorCompressionStats> GetCompressionStats(PipelineBranch branch)
    {
        var events = branch.Events
            .OfType<VectorCompressionEvent>()
            .Select(e => e.ToDomainEvent())
            .ToList();

        return VectorCompressionService.GetStats(events);
    }
}
