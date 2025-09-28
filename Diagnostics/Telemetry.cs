using System.Collections.Concurrent;

namespace LangChainPipeline.Diagnostics;

internal static class Telemetry
{
    private static long _embeddings;
    private static long _embFailures;
    private static long _vectors;
    private static long _approxTokens;
    private static readonly ConcurrentDictionary<int,long> Dims = new();
    private static long _agentIterations;
    private static long _agentToolCalls;
    private static long _agentRetries;
    private static long _streamChunks;
    private static long _toolLatencyMicros;
    private static long _toolLatencySamples;
    private static readonly ConcurrentDictionary<string,long> ToolNameCounts = new();

    public static void RecordAgentIteration() => Interlocked.Increment(ref _agentIterations);
    public static void RecordAgentToolCalls(int n) => Interlocked.Add(ref _agentToolCalls, n);
    public static void RecordAgentRetry() => Interlocked.Increment(ref _agentRetries);
    public static void RecordStreamChunk() => Interlocked.Increment(ref _streamChunks);
    public static void RecordToolLatency(TimeSpan elapsed)
    {
        Interlocked.Add(ref _toolLatencyMicros, (long)(elapsed.TotalMilliseconds * 1000));
        Interlocked.Increment(ref _toolLatencySamples);
    }
    public static void RecordToolName(string name) => ToolNameCounts.AddOrUpdate(name, 1, (_, v) => v + 1);

    public static void RecordEmbeddingInput(IEnumerable<string> inputs)
    {
        var list = inputs as ICollection<string> ?? inputs.ToList();
        Interlocked.Increment(ref _embeddings);
        long t = 0;
        foreach (var s in list) t += s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Interlocked.Add(ref _approxTokens, t);
    }

    public static void RecordEmbeddingSuccess(int dimension)
        => Dims.AddOrUpdate(dimension, 1, (_, v) => v + 1);

    public static void RecordEmbeddingFailure() => Interlocked.Increment(ref _embFailures);
    public static void RecordVectors(int count) => Interlocked.Add(ref _vectors, count);

    public static void PrintSummary()
    {
        if (Environment.GetEnvironmentVariable("MONADIC_DEBUG") != "1") return;
        var dims = string.Join(';', Dims.OrderBy(kv => kv.Key).Select(kv => $"d{kv.Key}={kv.Value}"));
        double avgToolMicros = _toolLatencySamples == 0 ? 0 : (double)_toolLatencyMicros / _toolLatencySamples;
        var toolTop = string.Join(',', ToolNameCounts.OrderByDescending(kv=>kv.Value).Take(5).Select(kv=>$"{kv.Key}={kv.Value}"));
        Console.WriteLine($"[telemetry] embReq={_embeddings} embFail={_embFailures} vectors={_vectors} approxTokens={_approxTokens} agentIters={_agentIterations} agentTools={_agentToolCalls} agentRetries={_agentRetries} streamChunks={_streamChunks} avgToolUs={avgToolMicros:F1} tools[{toolTop}] {dims}");
    }
}
