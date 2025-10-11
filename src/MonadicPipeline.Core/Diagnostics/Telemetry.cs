using System.Collections.Concurrent;

namespace LangChainPipeline.Diagnostics;

/// <summary>
/// Telemetry collector for tracking pipeline execution metrics and performance data.
/// </summary>
public static class Telemetry
{
    private static long _embeddings;
    private static long _embFailures;
    private static long _vectors;
    private static long _approxTokens;
    private static readonly ConcurrentDictionary<int, long> Dims = new();
    private static long _agentIterations;
    private static long _agentToolCalls;
    private static long _agentRetries;
    private static long _streamChunks;
    private static long _toolLatencyMicros;
    private static long _toolLatencySamples;
    private static readonly ConcurrentDictionary<string, long> ToolNameCounts = new();

    /// <summary>
    /// Records a single agent iteration.
    /// </summary>
    public static void RecordAgentIteration() => Interlocked.Increment(ref _agentIterations);

    /// <summary>
    /// Records the number of tool calls made by the agent.
    /// </summary>
    /// <param name="n">Number of tool calls.</param>
    public static void RecordAgentToolCalls(int n) => Interlocked.Add(ref _agentToolCalls, n);

    /// <summary>
    /// Records a single agent retry attempt.
    /// </summary>
    public static void RecordAgentRetry() => Interlocked.Increment(ref _agentRetries);

    /// <summary>
    /// Records a single stream chunk received.
    /// </summary>
    public static void RecordStreamChunk() => Interlocked.Increment(ref _streamChunks);

    /// <summary>
    /// Records the latency of a tool execution.
    /// </summary>
    /// <param name="elapsed">The elapsed time for tool execution.</param>
    public static void RecordToolLatency(TimeSpan elapsed)
    {
        Interlocked.Add(ref _toolLatencyMicros, (long)(elapsed.TotalMilliseconds * 1000));
        Interlocked.Increment(ref _toolLatencySamples);
    }

    /// <summary>
    /// Records the usage of a specific tool by name.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    public static void RecordToolName(string name) => ToolNameCounts.AddOrUpdate(name, 1, (_, v) => v + 1);

    /// <summary>
    /// Records embedding inputs for tracking token usage.
    /// </summary>
    /// <param name="inputs">The input strings to be embedded.</param>
    public static void RecordEmbeddingInput(IEnumerable<string> inputs)
    {
        var list = inputs as ICollection<string> ?? inputs.ToList();
        Interlocked.Increment(ref _embeddings);
        long t = 0;
        foreach (var s in list) t += s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Interlocked.Add(ref _approxTokens, t);
    }

    /// <summary>
    /// Records a successful embedding operation with the vector dimension.
    /// </summary>
    /// <param name="dimension">The dimension of the embedding vector.</param>
    public static void RecordEmbeddingSuccess(int dimension)
        => Dims.AddOrUpdate(dimension, 1, (_, v) => v + 1);

    /// <summary>
    /// Records a failed embedding operation.
    /// </summary>
    public static void RecordEmbeddingFailure() => Interlocked.Increment(ref _embFailures);

    /// <summary>
    /// Records the number of vectors stored or processed.
    /// </summary>
    /// <param name="count">Number of vectors.</param>
    public static void RecordVectors(int count) => Interlocked.Add(ref _vectors, count);

    /// <summary>
    /// Prints a summary of all collected telemetry data to the console.
    /// Only prints when MONADIC_DEBUG environment variable is set to "1".
    /// </summary>
    public static void PrintSummary()
    {
        if (Environment.GetEnvironmentVariable("MONADIC_DEBUG") != "1") return;
        var dims = string.Join(';', Dims.OrderBy(kv => kv.Key).Select(kv => $"d{kv.Key}={kv.Value}"));
        double avgToolMicros = _toolLatencySamples == 0 ? 0 : (double)_toolLatencyMicros / _toolLatencySamples;
        var toolTop = string.Join(',', ToolNameCounts.OrderByDescending(kv => kv.Value).Take(5).Select(kv => $"{kv.Key}={kv.Value}"));
        Console.WriteLine($"[telemetry] embReq={_embeddings} embFail={_embFailures} vectors={_vectors} approxTokens={_approxTokens} agentIters={_agentIterations} agentTools={_agentToolCalls} agentRetries={_agentRetries} streamChunks={_streamChunks} avgToolUs={avgToolMicros:F1} tools[{toolTop}] {dims}");
    }
}
