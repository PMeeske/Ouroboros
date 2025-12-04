// <copyright file="RecursiveChunkProcessor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Processing;

using System.Collections.Concurrent;
using System.Diagnostics;
using LangChainPipeline.Core.Monads;

/// <summary>
/// Interface for processing large contexts by recursively chunking them into smaller pieces.
/// Implements map-reduce pattern for parallel chunk processing with adaptive chunking strategies.
/// </summary>
public interface IRecursiveChunkProcessor
{
    /// <summary>
    /// Processes a large context by chunking it and applying processing to each chunk.
    /// </summary>
    /// <typeparam name="TInput">Type of the input context.</typeparam>
    /// <typeparam name="TOutput">Type of the output result.</typeparam>
    /// <param name="largeContext">The large context to process.</param>
    /// <param name="maxChunkSize">Maximum chunk size in tokens (default: 512).</param>
    /// <param name="strategy">Chunking strategy to use (default: Adaptive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the processed output or error.</returns>
    Task<Result<TOutput>> ProcessLargeContextAsync<TInput, TOutput>(
        TInput largeContext,
        int maxChunkSize = 512,
        ChunkingStrategy strategy = ChunkingStrategy.Adaptive,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Recursive chunk processor that splits large contexts into smaller chunks,
/// processes them in parallel using map-reduce pattern, and combines results.
/// Features adaptive chunking with conditioned stimulus learning.
/// </summary>
public sealed class RecursiveChunkProcessor : IRecursiveChunkProcessor
{
    private readonly Func<string, Task<Result<string>>> processChunkFunc;
    private readonly Func<IEnumerable<string>, Task<Result<string>>> combineResultsFunc;
    private readonly ConcurrentDictionary<int, (int successCount, int failureCount)> chunkSizePerformance;
    private const int MinChunkSize = 256;
    private const int MaxChunkSize = 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecursiveChunkProcessor"/> class.
    /// Initializes a new instance of RecursiveChunkProcessor.
    /// </summary>
    /// <param name="processChunkFunc">Function to process a single chunk of text.</param>
    /// <param name="combineResultsFunc">Function to combine multiple chunk results into final output.</param>
    public RecursiveChunkProcessor(
        Func<string, Task<Result<string>>> processChunkFunc,
        Func<IEnumerable<string>, Task<Result<string>>> combineResultsFunc)
    {
        this.processChunkFunc = processChunkFunc ?? throw new ArgumentNullException(nameof(processChunkFunc));
        this.combineResultsFunc = combineResultsFunc ?? throw new ArgumentNullException(nameof(combineResultsFunc));
        this.chunkSizePerformance = new ConcurrentDictionary<int, (int successCount, int failureCount)>();
    }

    /// <inheritdoc/>
    public async Task<Result<TOutput>> ProcessLargeContextAsync<TInput, TOutput>(
        TInput largeContext,
        int maxChunkSize = 512,
        ChunkingStrategy strategy = ChunkingStrategy.Adaptive,
        CancellationToken cancellationToken = default)
    {
        if (largeContext is not string textContext)
        {
            return Result<TOutput>.Failure("Only string input is currently supported");
        }

        try
        {
            // Determine optimal chunk size
            int chunkSize = strategy == ChunkingStrategy.Adaptive
                ? this.GetAdaptiveChunkSize(maxChunkSize)
                : maxChunkSize;

            // Split context into chunks
            List<string> chunks = this.SplitIntoChunks(textContext, chunkSize);

            if (chunks.Count == 0)
            {
                return Result<TOutput>.Failure("Failed to split context into chunks");
            }

            // Process chunks in parallel (map phase)
            List<ChunkResult<string>> chunkResults = await this.ProcessChunksInParallelAsync(chunks, strategy, cancellationToken);

            // Check for failures
            List<ChunkResult<string>> failures = chunkResults.Where(r => !r.Success).ToList();
            if (failures.Any())
            {
                // Update performance metrics for failures
                if (strategy == ChunkingStrategy.Adaptive)
                {
                    this.UpdatePerformanceMetrics(chunkSize, false);
                }

                return Result<TOutput>.Failure(
                    $"Failed to process {failures.Count} out of {chunkResults.Count} chunks");
            }

            // Update performance metrics for successes
            if (strategy == ChunkingStrategy.Adaptive)
            {
                this.UpdatePerformanceMetrics(chunkSize, true);
            }

            // Combine results (reduce phase)
            Result<string> combinedResult = await this.CombineChunkResultsAsync(
                chunkResults.Select(r => r.Output).ToList());

            if (combinedResult.IsFailure)
            {
                return Result<TOutput>.Failure($"Failed to combine chunk results: {combinedResult.Error}");
            }

            // Convert result to TOutput
            if (combinedResult.Value is TOutput output)
            {
                return Result<TOutput>.Success(output);
            }

            return Result<TOutput>.Failure(
                $"Cannot convert combined result of type {combinedResult.Value?.GetType()} to {typeof(TOutput)}");
        }
        catch (OperationCanceledException)
        {
            return Result<TOutput>.Failure("Processing was cancelled");
        }
        catch (Exception ex)
        {
            return Result<TOutput>.Failure($"Unexpected error during recursive processing: {ex.Message}");
        }
    }

    /// <summary>
    /// Splits text into chunks of approximately the specified size.
    /// Uses token-aware splitting to avoid breaking semantic units.
    /// </summary>
    private List<string> SplitIntoChunks(string text, int chunkSize)
    {
        List<string> chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        // Simple token approximation: ~4 characters per token
        int chunkCharSize = chunkSize * 4;
        int overlap = chunkSize / 4; // 25% overlap to maintain context
        int overlapCharSize = overlap * 4;

        int position = 0;
        while (position < text.Length)
        {
            int remainingLength = text.Length - position;
            int currentChunkSize = Math.Min(chunkCharSize, remainingLength);

            // Try to break at sentence boundaries
            string chunk = text.Substring(position, currentChunkSize);

            // If not at the end, try to find a good break point
            if (position + currentChunkSize < text.Length)
            {
                int lastPeriod = chunk.LastIndexOf(". ");
                int lastNewline = chunk.LastIndexOf('\n');
                int breakPoint = Math.Max(lastPeriod, lastNewline);

                if (breakPoint > currentChunkSize / 2) // Only break if we're past halfway
                {
                    currentChunkSize = breakPoint + 1;
                    chunk = text.Substring(position, currentChunkSize);
                }
            }

            chunks.Add(chunk.Trim());

            // Move position forward with overlap
            position += currentChunkSize - overlapCharSize;

            // Ensure we make progress even with overlap
            if (position <= 0 || position >= text.Length - overlapCharSize)
            {
                position = text.Length;
            }
        }

        return chunks;
    }

    /// <summary>
    /// Processes chunks in parallel using the map pattern.
    /// </summary>
    private async Task<List<ChunkResult<string>>> ProcessChunksInParallelAsync(
        List<string> chunks,
                ChunkingStrategy strategy,
        CancellationToken cancellationToken)
    {
        ConcurrentBag<ChunkResult<string>> results = new ConcurrentBag<ChunkResult<string>>();
        ParallelOptions options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4), // Limit parallelism
        };

        await Parallel.ForEachAsync(
            chunks.Select((chunk, index) => (chunk, index)),
            options,
            async (item, ct) =>
            {
                (string? chunk, int index) = item;
                Stopwatch stopwatch = Stopwatch.StartNew();

                try
                {
                    Result<string> result = await this.processChunkFunc(chunk);
                    stopwatch.Stop();

                    ChunkMetadata metadata = new ChunkMetadata(
                        Index: index,
                        TotalChunks: chunks.Count,
                        TokenCount: EstimateTokenCount(chunk),
                        Strategy: strategy);

                    ChunkResult<string> chunkResult = new ChunkResult<string>(
                        Output: result.IsSuccess ? result.Value : string.Empty,
                        Metadata: metadata,
                        ProcessingTime: stopwatch.Elapsed,
                        Success: result.IsSuccess);

                    results.Add(chunkResult);
                }
                catch
                {
                    stopwatch.Stop();

                    ChunkMetadata metadata = new ChunkMetadata(
                        Index: index,
                        TotalChunks: chunks.Count,
                        TokenCount: EstimateTokenCount(chunk),
                        Strategy: strategy);

                    ChunkResult<string> failedResult = new ChunkResult<string>(
                        Output: string.Empty,
                        Metadata: metadata,
                        ProcessingTime: stopwatch.Elapsed,
                        Success: false);

                    results.Add(failedResult);
                }
            });

        return results.OrderBy(r => r.Metadata.Index).ToList();
    }

    /// <summary>
    /// Combines chunk results using the reduce pattern with hierarchical joining.
    /// </summary>
    private async Task<Result<string>> CombineChunkResultsAsync(
        List<string> chunkOutputs
        )
    {
        try
        {
            // Use the provided combine function
            Result<string> result = await this.combineResultsFunc(chunkOutputs);
            return result;
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Error combining results: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the adaptive chunk size based on historical performance using conditioned stimulus learning.
    /// </summary>
    private int GetAdaptiveChunkSize(int requestedMaxSize)
    {
        // Start with requested size
        int candidateSize = Math.Clamp(requestedMaxSize, MinChunkSize, MaxChunkSize);

        // If we have performance data, optimize based on success rates
        if (this.chunkSizePerformance.Any())
        {
            var bestPerforming = this.chunkSizePerformance
                .Where(kvp => kvp.Value.successCount > 0)
                .Select(kvp => new
                {
                    Size = kvp.Key,
                    SuccessRate = (double)kvp.Value.successCount / (kvp.Value.successCount + kvp.Value.failureCount),
                })
                .OrderByDescending(x => x.SuccessRate)
                .ThenByDescending(x => x.Size) // Prefer larger chunks when success rates are equal
                .FirstOrDefault();

            if (bestPerforming != null && bestPerforming.SuccessRate > 0.8)
            {
                candidateSize = bestPerforming.Size;
            }
        }

        return candidateSize;
    }

    /// <summary>
    /// Updates performance metrics for adaptive learning.
    /// </summary>
    private void UpdatePerformanceMetrics(int chunkSize, bool success)
    {
        this.chunkSizePerformance.AddOrUpdate(
            chunkSize,
            _ => success ? (1, 0) : (0, 1),
            (_, current) => success
                ? (current.successCount + 1, current.failureCount)
                : (current.successCount, current.failureCount + 1));
    }

    /// <summary>
    /// Estimates token count from text (rough approximation).
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        // Rough approximation: ~4 characters per token on average
        return text.Length / 4;
    }
}
