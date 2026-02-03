#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Diagnostics;

namespace Ouroboros.Providers;

/// <summary>
/// Tracks usage, costs, and timing for LLM API calls across multiple providers.
/// Pricing based on published rates (USD per 1M tokens).
/// </summary>
public sealed class LlmCostTracker
{
    /// <summary>
    /// Pricing tier for a model.
    /// </summary>
    public sealed record ModelPricing(
        string Provider,
        decimal InputCostPer1M,
        decimal OutputCostPer1M,
        string? Notes = null);

    // Pricing per 1M tokens (USD) - Updated for 2025
    private static readonly Dictionary<string, ModelPricing> KnownPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        // === ANTHROPIC ===
        ["claude-opus-4-5-20251101"] = new("Anthropic", 15.00m, 75.00m),
        ["claude-opus-4-5"] = new("Anthropic", 15.00m, 75.00m),
        ["claude-sonnet-4-20250514"] = new("Anthropic", 3.00m, 15.00m),
        ["claude-sonnet-4"] = new("Anthropic", 3.00m, 15.00m),
        ["claude-3-5-sonnet-20241022"] = new("Anthropic", 3.00m, 15.00m),
        ["claude-3-5-sonnet"] = new("Anthropic", 3.00m, 15.00m),
        ["claude-3-5-haiku-20241022"] = new("Anthropic", 0.25m, 1.25m),
        ["claude-3-5-haiku"] = new("Anthropic", 0.25m, 1.25m),
        ["claude-3-opus"] = new("Anthropic", 15.00m, 75.00m),
        ["claude-3-sonnet"] = new("Anthropic", 3.00m, 15.00m),
        ["claude-3-haiku"] = new("Anthropic", 0.25m, 1.25m),

        // === OPENAI ===
        ["gpt-4o"] = new("OpenAI", 2.50m, 10.00m),
        ["gpt-4o-mini"] = new("OpenAI", 0.15m, 0.60m),
        ["gpt-4-turbo"] = new("OpenAI", 10.00m, 30.00m),
        ["gpt-4"] = new("OpenAI", 30.00m, 60.00m),
        ["gpt-3.5-turbo"] = new("OpenAI", 0.50m, 1.50m),
        ["o1"] = new("OpenAI", 15.00m, 60.00m),
        ["o1-mini"] = new("OpenAI", 3.00m, 12.00m),
        ["o1-preview"] = new("OpenAI", 15.00m, 60.00m),

        // === DEEPSEEK ===
        ["deepseek-chat"] = new("DeepSeek", 0.14m, 0.28m, "Cache miss pricing"),
        ["deepseek-coder"] = new("DeepSeek", 0.14m, 0.28m),
        ["deepseek-reasoner"] = new("DeepSeek", 0.55m, 2.19m, "R1 reasoning model"),
        ["deepseek-v3"] = new("DeepSeek", 0.27m, 1.10m),
        ["deepseek-v3.1:671b-cloud"] = new("DeepSeek", 0.27m, 1.10m, "Ollama Cloud hosted"),

        // === GOOGLE ===
        ["gemini-2.0-flash"] = new("Google", 0.10m, 0.40m),
        ["gemini-1.5-pro"] = new("Google", 1.25m, 5.00m),
        ["gemini-1.5-flash"] = new("Google", 0.075m, 0.30m),

        // === MISTRAL ===
        ["mistral-large"] = new("Mistral", 2.00m, 6.00m),
        ["mistral-small"] = new("Mistral", 0.20m, 0.60m),
        ["codestral"] = new("Mistral", 0.20m, 0.60m),

        // === LOCAL/FREE ===
        ["llama3"] = new("Local", 0m, 0m, "Free (local)"),
        ["llama3.1"] = new("Local", 0m, 0m, "Free (local)"),
        ["llama3.2"] = new("Local", 0m, 0m, "Free (local)"),
        ["qwen2.5"] = new("Local", 0m, 0m, "Free (local)"),
        ["phi3"] = new("Local", 0m, 0m, "Free (local)"),
        ["gemma2"] = new("Local", 0m, 0m, "Free (local)"),
        ["mistral"] = new("Local", 0m, 0m, "Free (local)"),
        ["mixtral"] = new("Local", 0m, 0m, "Free (local)"),
        ["nomic-embed-text"] = new("Local", 0m, 0m, "Free (local embeddings)"),
    };

    private readonly string _model;
    private readonly string _provider;
    private readonly object _lock = new();

    // Session totals
    private long _totalInputTokens;
    private long _totalOutputTokens;
    private int _totalRequests;
    private TimeSpan _totalLatency;
    private decimal _totalCost;
    private readonly List<RequestMetrics> _requestHistory = new();

    // Current request tracking
    private Stopwatch? _currentRequestTimer;

    // Global tracker for cross-session statistics
    private static readonly LlmCostTracker GlobalTracker = new("*global*", "*all*");
    private static readonly Dictionary<string, LlmCostTracker> ProviderTrackers = new();

    public LlmCostTracker(string model, string? provider = null)
    {
        _model = model;
        _provider = provider ?? GetProvider(model);
    }

    /// <summary>
    /// Get the provider name for a model.
    /// </summary>
    public static string GetProvider(string model)
    {
        if (KnownPricing.TryGetValue(model, out var pricing))
            return pricing.Provider;

        // Infer from model name
        if (model.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
            return "Anthropic";
        if (model.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) || model.StartsWith("o1", StringComparison.OrdinalIgnoreCase))
            return "OpenAI";
        if (model.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
            return "DeepSeek";
        if (model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
            return "Google";
        if (model.StartsWith("mistral", StringComparison.OrdinalIgnoreCase) || model.StartsWith("codestral", StringComparison.OrdinalIgnoreCase))
            return "Mistral";

        return "Unknown";
    }

    /// <summary>
    /// Gets pricing for a model. Returns (0, 0) if model unknown or local.
    /// </summary>
    public static ModelPricing GetPricing(string model)
    {
        // Try exact match first
        if (KnownPricing.TryGetValue(model, out var pricing))
            return pricing;

        // Try prefix match
        foreach (var (key, value) in KnownPricing)
        {
            if (model.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        // Default to free (assume local/unknown)
        return new ModelPricing(GetProvider(model), 0m, 0m, "Unknown pricing");
    }

    /// <summary>
    /// Calculates cost for a given token count.
    /// </summary>
    public static decimal CalculateCost(string model, int inputTokens, int outputTokens)
    {
        var pricing = GetPricing(model);
        return (inputTokens / 1_000_000m * pricing.InputCostPer1M) +
               (outputTokens / 1_000_000m * pricing.OutputCostPer1M);
    }

    /// <summary>
    /// Start timing a request.
    /// </summary>
    public void StartRequest()
    {
        _currentRequestTimer = Stopwatch.StartNew();
    }

    /// <summary>
    /// Record completion of a request with token counts.
    /// </summary>
    public RequestMetrics EndRequest(int inputTokens, int outputTokens)
    {
        _currentRequestTimer?.Stop();
        var latency = _currentRequestTimer?.Elapsed ?? TimeSpan.Zero;
        var cost = CalculateCost(_model, inputTokens, outputTokens);

        var metrics = new RequestMetrics(_model, inputTokens, outputTokens, latency, cost, DateTime.UtcNow);

        lock (_lock)
        {
            _totalInputTokens += inputTokens;
            _totalOutputTokens += outputTokens;
            _totalRequests++;
            _totalLatency += latency;
            _totalCost += cost;

            if (_requestHistory.Count < 1000) // Limit history size
                _requestHistory.Add(metrics);
        }

        // Update global tracker
        GlobalTracker.RecordFromChild(metrics);

        return metrics;
    }

    private void RecordFromChild(RequestMetrics metrics)
    {
        lock (_lock)
        {
            _totalInputTokens += metrics.InputTokens;
            _totalOutputTokens += metrics.OutputTokens;
            _totalRequests++;
            _totalLatency += metrics.Latency;
            _totalCost += metrics.Cost;
        }
    }

    /// <summary>
    /// Get session totals.
    /// </summary>
    public SessionMetrics GetSessionMetrics()
    {
        lock (_lock)
        {
            return new SessionMetrics(
                _model,
                _provider,
                _totalRequests,
                _totalInputTokens,
                _totalOutputTokens,
                _totalLatency,
                _totalCost,
                _totalRequests > 0 ? _totalLatency / _totalRequests : TimeSpan.Zero
            );
        }
    }

    /// <summary>
    /// Get global metrics across all trackers.
    /// </summary>
    public static SessionMetrics GetGlobalMetrics() => GlobalTracker.GetSessionMetrics();

    /// <summary>
    /// Reset session totals.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _totalInputTokens = 0;
            _totalOutputTokens = 0;
            _totalRequests = 0;
            _totalLatency = TimeSpan.Zero;
            _totalCost = 0;
            _requestHistory.Clear();
        }
    }

    /// <summary>
    /// Format a cost summary for display.
    /// </summary>
    public string FormatSessionSummary()
    {
        var metrics = GetSessionMetrics();
        var pricing = GetPricing(_model);

        return $"""
            LLM Usage Summary:
              Provider: {metrics.Provider}
              Model: {metrics.Model}
              Pricing: ${pricing.InputCostPer1M}/1M in, ${pricing.OutputCostPer1M}/1M out
              Requests: {metrics.TotalRequests}
              Tokens: {metrics.TotalInputTokens:N0} in / {metrics.TotalOutputTokens:N0} out ({metrics.TotalTokens:N0} total)
              Cost: ${metrics.TotalCost:F4}
              Total Time: {metrics.TotalLatency.TotalSeconds:F1}s
              Avg Latency: {metrics.AverageLatency.TotalSeconds:F2}s/request
            """;
    }

    /// <summary>
    /// Get a cost-awareness prompt that can be injected into system messages.
    /// </summary>
    public static string GetCostAwarenessPrompt(string model)
    {
        var pricing = GetPricing(model);

        if (pricing.InputCostPer1M == 0 && pricing.OutputCostPer1M == 0)
        {
            return $"""
                MODEL INFO: You are running on {model} ({pricing.Provider}).
                This is a local/free model - no API costs.
                Focus on quality and helpfulness without token constraints.
                """;
        }

        return $"""
            COST AWARENESS: You are running on {model} ({pricing.Provider}).
            - Input tokens: ${pricing.InputCostPer1M}/1M tokens
            - Output tokens: ${pricing.OutputCostPer1M}/1M tokens
            Guidelines for cost efficiency:
            - Be concise - avoid unnecessary verbosity
            - Use structured formats (lists, tables) for clarity
            - Don't repeat information already in the conversation
            - If asked to explain, be thorough but not redundant
            """;
    }

    /// <summary>
    /// Get a brief cost string for display.
    /// </summary>
    public string GetCostString()
    {
        var metrics = GetSessionMetrics();
        if (metrics.TotalCost == 0)
            return $"{metrics.TotalTokens:N0} tokens";
        return $"{metrics.TotalTokens:N0} tokens (${metrics.TotalCost:F4})";
    }
}

/// <summary>
/// Metrics for a single request.
/// </summary>
public sealed record RequestMetrics(
    string Model,
    int InputTokens,
    int OutputTokens,
    TimeSpan Latency,
    decimal Cost,
    DateTime Timestamp)
{
    public int TotalTokens => InputTokens + OutputTokens;
    public double TokensPerSecond => Latency.TotalSeconds > 0 ? OutputTokens / Latency.TotalSeconds : 0;

    public override string ToString()
    {
        if (Cost == 0)
            return $"{InputTokens}→{OutputTokens} tokens, {Latency.TotalSeconds:F2}s ({TokensPerSecond:F0} tok/s)";
        return $"{InputTokens}→{OutputTokens} tokens, ${Cost:F4}, {Latency.TotalSeconds:F2}s ({TokensPerSecond:F0} tok/s)";
    }
}

/// <summary>
/// Aggregate metrics for a session.
/// </summary>
public sealed record SessionMetrics(
    string Model,
    string Provider,
    int TotalRequests,
    long TotalInputTokens,
    long TotalOutputTokens,
    TimeSpan TotalLatency,
    decimal TotalCost,
    TimeSpan AverageLatency)
{
    public long TotalTokens => TotalInputTokens + TotalOutputTokens;

    public string ToCostString()
    {
        if (TotalCost == 0)
            return $"{TotalTokens:N0} tokens";
        return $"{TotalTokens:N0} tokens (${TotalCost:F4})";
    }
}
