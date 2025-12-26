// <copyright file="Telemetry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Diagnostics;

using System.Collections.Concurrent;

/// <summary>
/// Telemetry collector for tracking pipeline execution metrics and performance data.
/// </summary>
public static class Telemetry
{
    private static long embeddings;
    private static long embFailures;
    private static long vectors;
    private static long approxTokens;
    private static readonly ConcurrentDictionary<int, long> Dims = new();
    private static long agentIterations;
    private static long agentToolCalls;
    private static long agentRetries;
    private static long streamChunks;
    private static long toolLatencyMicros;
    private static long toolLatencySamples;
    private static readonly ConcurrentDictionary<string, long> ToolNameCounts = new();

    /// <summary>
    /// Records a single agent iteration.
    /// </summary>
    public static void RecordAgentIteration() => Interlocked.Increment(ref agentIterations);

    /// <summary>
    /// Records the number of tool calls made by the agent.
    /// </summary>
    /// <param name="n">Number of tool calls.</param>
    public static void RecordAgentToolCalls(int n) => Interlocked.Add(ref agentToolCalls, n);

    /// <summary>
    /// Records a single agent retry attempt.
    /// </summary>
    public static void RecordAgentRetry() => Interlocked.Increment(ref agentRetries);

    /// <summary>
    /// Records a single stream chunk received.
    /// </summary>
    public static void RecordStreamChunk() => Interlocked.Increment(ref streamChunks);

    /// <summary>
    /// Records the latency of a tool execution.
    /// </summary>
    /// <param name="elapsed">The elapsed time for tool execution.</param>
    public static void RecordToolLatency(TimeSpan elapsed)
    {
        Interlocked.Add(ref toolLatencyMicros, (long)(elapsed.TotalMilliseconds * 1000));
        Interlocked.Increment(ref toolLatencySamples);
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
        ICollection<string> list = inputs as ICollection<string> ?? inputs.ToList();
        Interlocked.Increment(ref embeddings);
        long t = 0;
        foreach (string s in list)
        {
            t += s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        Interlocked.Add(ref approxTokens, t);
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
    public static void RecordEmbeddingFailure() => Interlocked.Increment(ref embFailures);

    /// <summary>
    /// Records the number of vectors stored or processed.
    /// </summary>
    /// <param name="count">Number of vectors.</param>
    public static void RecordVectors(int count) => Interlocked.Add(ref vectors, count);

    /// <summary>
    /// Prints a summary of all collected telemetry data to the console.
    /// Only prints when MONADIC_DEBUG environment variable is set to "1".
    /// </summary>
    public static void PrintSummary()
    {
        if (Environment.GetEnvironmentVariable("MONADIC_DEBUG") != "1")
        {
            return;
        }

        string dims = string.Join(';', Dims.OrderBy(kv => kv.Key).Select(kv => $"d{kv.Key}={kv.Value}"));
        double avgToolMicros = toolLatencySamples == 0 ? 0 : (double)toolLatencyMicros / toolLatencySamples;
        string toolTop = string.Join(',', ToolNameCounts.OrderByDescending(kv => kv.Value).Take(5).Select(kv => $"{kv.Key}={kv.Value}"));
        Console.WriteLine($"[telemetry] embReq={embeddings} embFail={embFailures} vectors={vectors} approxTokens={approxTokens} agentIters={agentIterations} agentTools={agentToolCalls} agentRetries={agentRetries} streamChunks={streamChunks} avgToolUs={avgToolMicros:F1} tools[{toolTop}] {dims}");
    }
}
