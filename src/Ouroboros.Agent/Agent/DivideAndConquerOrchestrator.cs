#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Divide and Conquer Orchestrator
// High-performance orchestrator for parallel task execution
// Splits work into chunks, processes in parallel, merges results
// ==========================================================

using System.Collections.Concurrent;
using System.Text;

namespace LangChainPipeline.Agent;

/// <summary>
/// Configuration for divide-and-conquer execution.
/// </summary>
public sealed record DivideAndConquerConfig(
    int MaxParallelism = 4,
    int ChunkSize = 500,
    bool MergeResults = true,
    string MergeSeparator = "\n\n---\n\n");

/// <summary>
/// Result of a chunk execution in divide-and-conquer pattern.
/// </summary>
public sealed record ChunkResult(
    int ChunkIndex,
    string Input,
    string Output,
    TimeSpan ExecutionTime,
    bool Success,
    string? Error = null);

/// <summary>
/// Orchestrator implementing divide-and-conquer strategy for high-performance parallel execution.
/// Splits tasks into chunks, processes them in parallel, and merges results into a unified stream.
/// Optimized for use with lightweight models like TinyLlama.
/// </summary>
public sealed class DivideAndConquerOrchestrator
{
    private readonly IChatCompletionModel _model;
    private readonly DivideAndConquerConfig _config;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();

    public DivideAndConquerOrchestrator(
        IChatCompletionModel model,
        DivideAndConquerConfig? config = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _config = config ?? new DivideAndConquerConfig();
    }

    /// <summary>
    /// Executes a task by dividing it into chunks, processing in parallel, and merging results.
    /// </summary>
    public async Task<Result<string, string>> ExecuteAsync(
        string task,
        List<string> chunks,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(task))
            return Result<string, string>.Failure("Task cannot be empty");

        if (chunks == null || chunks.Count == 0)
            return Result<string, string>.Failure("No chunks provided");

        System.Diagnostics.Stopwatch totalTimer = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Process chunks in parallel with configured degree of parallelism
            ConcurrentBag<ChunkResult> results = new ConcurrentBag<ChunkResult>();
            
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.MaxParallelism,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(
                chunks.Select((chunk, index) => (Chunk: chunk, Index: index)),
                parallelOptions,
                async (item, token) =>
                {
                    ChunkResult result = await ProcessChunkAsync(task, item.Chunk, item.Index, token);
                    results.Add(result);
                });

            totalTimer.Stop();

            // Sort results by chunk index to maintain order
            List<ChunkResult> sortedResults = results.OrderBy(r => r.ChunkIndex).ToList();

            // Check for failures
            List<ChunkResult> failures = sortedResults.Where(r => !r.Success).ToList();
            if (failures.Any())
            {
                string errorMsg = $"Failed to process {failures.Count} of {chunks.Count} chunks: " +
                    string.Join("; ", failures.Select(f => $"Chunk {f.ChunkIndex}: {f.Error}"));
                return Result<string, string>.Failure(errorMsg);
            }

            // Merge results
            string mergedResult = _config.MergeResults
                ? MergeResults(sortedResults)
                : string.Join(_config.MergeSeparator, sortedResults.Select(r => r.Output));

            // Record overall metrics
            RecordMetric("divide_and_conquer_orchestrator", totalTimer.Elapsed.TotalMilliseconds, true);

            return Result<string, string>.Success(mergedResult);
        }
        catch (Exception ex)
        {
            totalTimer.Stop();
            RecordMetric("divide_and_conquer_orchestrator", totalTimer.Elapsed.TotalMilliseconds, false);
            return Result<string, string>.Failure($"Execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Divides text into chunks based on configured chunk size.
    /// </summary>
    public List<string> DivideIntoChunks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        List<string> chunks = new List<string>();
        
        // Split by paragraphs first to maintain semantic boundaries
        string[] paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        StringBuilder currentChunk = new StringBuilder();
        int currentSize = 0;

        foreach (string paragraph in paragraphs)
        {
            int paragraphSize = paragraph.Length;

            // If adding this paragraph exceeds chunk size and we have content, start new chunk
            if (currentSize > 0 && currentSize + paragraphSize > _config.ChunkSize)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
                currentSize = 0;
            }

            // If single paragraph exceeds chunk size, split it by sentences
            if (paragraphSize > _config.ChunkSize)
            {
                string[] sentences = paragraph.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string sentence in sentences)
                {
                    if (currentSize + sentence.Length > _config.ChunkSize && currentSize > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                        currentSize = 0;
                    }
                    currentChunk.Append(sentence).Append(". ");
                    currentSize += sentence.Length + 2;
                }
            }
            else
            {
                currentChunk.Append(paragraph).Append("\n\n");
                currentSize += paragraphSize + 2;
            }
        }

        // Add remaining content
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }

    /// <summary>
    /// Processes a single chunk with the model.
    /// </summary>
    private async Task<ChunkResult> ProcessChunkAsync(
        string task,
        string chunk,
        int index,
        CancellationToken ct)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Construct prompt for the chunk
            string prompt = $"{task}\n\nContent:\n{chunk}";
            
            // Execute with model
            string output = await _model.GenerateTextAsync(prompt, ct);
            
            sw.Stop();

            // Record metrics
            RecordMetric($"chunk_{index}", sw.Elapsed.TotalMilliseconds, true);

            return new ChunkResult(
                ChunkIndex: index,
                Input: chunk,
                Output: output,
                ExecutionTime: sw.Elapsed,
                Success: true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordMetric($"chunk_{index}", sw.Elapsed.TotalMilliseconds, false);

            return new ChunkResult(
                ChunkIndex: index,
                Input: chunk,
                Output: string.Empty,
                ExecutionTime: sw.Elapsed,
                Success: false,
                Error: ex.Message);
        }
    }

    /// <summary>
    /// Merges chunk results into a unified output.
    /// </summary>
    private string MergeResults(List<ChunkResult> results)
    {
        if (results.Count == 0)
            return string.Empty;

        if (results.Count == 1)
            return results[0].Output;

        StringBuilder merged = new StringBuilder();
        
        // Simple concatenation with separator
        for (int i = 0; i < results.Count; i++)
        {
            merged.Append(results[i].Output);
            
            if (i < results.Count - 1)
            {
                merged.Append(_config.MergeSeparator);
            }
        }

        return merged.ToString();
    }

    /// <summary>
    /// Records performance metrics for tracking.
    /// </summary>
    private void RecordMetric(string resourceName, double latencyMs, bool success)
    {
        _metrics.AddOrUpdate(
            resourceName,
            // Add new
            _ => new PerformanceMetrics(
                resourceName,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),
            // Update existing
            (_, existing) =>
            {
                int newCount = existing.ExecutionCount + 1;
                double newAvgLatency = ((existing.AverageLatencyMs * existing.ExecutionCount) + latencyMs) / newCount;
                double newSuccessRate = ((existing.SuccessRate * existing.ExecutionCount) + (success ? 1.0 : 0.0)) / newCount;

                return new PerformanceMetrics(
                    resourceName,
                    ExecutionCount: newCount,
                    AverageLatencyMs: newAvgLatency,
                    SuccessRate: newSuccessRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: existing.CustomMetrics);
            });
    }

    /// <summary>
    /// Gets current performance metrics.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        => new Dictionary<string, PerformanceMetrics>(_metrics);
}
