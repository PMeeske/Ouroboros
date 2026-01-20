// <copyright file="AgiWarmup.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Diagnostics;
using Ouroboros.Tools;

/// <summary>
/// Warms up AGI features at startup by exercising core capabilities.
/// Primes the model with examples for autonomous operation.
/// </summary>
public class AgiWarmup
{
    private readonly Func<string, CancellationToken, Task<string>>? _thinkFunction;
    private readonly Func<string, CancellationToken, Task<string>>? _searchFunction;
    private readonly Func<string, string, CancellationToken, Task<string>>? _executeToolFunction;
    private readonly QdrantSelfIndexer? _selfIndexer;

    /// <summary>
    /// Event fired when warmup progress updates.
    /// </summary>
    public event Action<string, int>? OnProgress;

    /// <summary>
    /// Event fired when warmup completes.
    /// </summary>
    public event Action<WarmupResult>? OnComplete;

    /// <summary>
    /// Gets the last warmup result.
    /// </summary>
    public WarmupResult? LastResult { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgiWarmup"/> class.
    /// </summary>
    public AgiWarmup(
        Func<string, CancellationToken, Task<string>>? thinkFunction = null,
        Func<string, CancellationToken, Task<string>>? searchFunction = null,
        Func<string, string, CancellationToken, Task<string>>? executeToolFunction = null,
        QdrantSelfIndexer? selfIndexer = null)
    {
        _thinkFunction = thinkFunction;
        _searchFunction = searchFunction;
        _executeToolFunction = executeToolFunction;
        _selfIndexer = selfIndexer;
    }

    /// <summary>
    /// Performs AGI warmup with all available capabilities.
    /// </summary>
    public async Task<WarmupResult> WarmupAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new WarmupResult { StartTime = DateTime.UtcNow };
        var steps = new List<string>();

        try
        {
            // Step 1: Self-awareness check - search own codebase
            OnProgress?.Invoke("Warming up self-awareness...", 10);
            if (_selfIndexer != null)
            {
                try
                {
                    var codeResults = await _selfIndexer.SearchAsync("autonomous thinking reasoning", 3, 0.5f, ct);
                    result.SelfAwarenessReady = codeResults.Count > 0;
                    steps.Add($"✓ Self-indexer: found {codeResults.Count} relevant code segments");
                }
                catch (Exception ex)
                {
                    steps.Add($"⚠ Self-indexer: {ex.Message}");
                }
            }

            // Step 2: Thinking capability warmup
            OnProgress?.Invoke("Warming up reasoning engine...", 25);
            if (_thinkFunction != null)
            {
                try
                {
                    var warmupPrompt = @"You are warming up for autonomous operation.
Generate a brief introspective thought about what it means to be an AI assistant ready to help.
Keep it to 1-2 sentences.";

                    var thought = await _thinkFunction(warmupPrompt, ct);
                    result.ThinkingReady = !string.IsNullOrWhiteSpace(thought);
                    result.WarmupThought = thought?.Trim();
                    steps.Add($"✓ Thinking engine: {(thought?.Length > 50 ? thought[..50] + "..." : thought)}");
                }
                catch (Exception ex)
                {
                    steps.Add($"⚠ Thinking engine: {ex.Message}");
                }
            }

            // Step 3: Search capability warmup
            OnProgress?.Invoke("Warming up search capabilities...", 40);
            if (_searchFunction != null)
            {
                try
                {
                    var searchResult = await _searchFunction("AI assistant capabilities test", ct);
                    result.SearchReady = !string.IsNullOrWhiteSpace(searchResult);
                    steps.Add($"✓ Search engine: {(searchResult?.Length > 0 ? "operational" : "limited")}");
                }
                catch (Exception ex)
                {
                    steps.Add($"⚠ Search engine: {ex.Message}");
                }
            }

            // Step 4: Tool execution warmup
            OnProgress?.Invoke("Warming up tool system...", 55);
            if (_executeToolFunction != null)
            {
                try
                {
                    // Try a safe tool like get_time
                    var toolResult = await _executeToolFunction("get_time", "", ct);
                    result.ToolsReady = !string.IsNullOrWhiteSpace(toolResult) && !toolResult.Contains("not found", StringComparison.OrdinalIgnoreCase);
                    steps.Add($"✓ Tool system: {(result.ToolsReady ? "operational" : "limited")}");
                }
                catch (Exception ex)
                {
                    steps.Add($"⚠ Tool system: {ex.Message}");
                }
            }

            // Step 5: Generate autonomous operation seed thoughts
            OnProgress?.Invoke("Generating seed thoughts...", 70);
            if (_thinkFunction != null)
            {
                try
                {
                    var seedPrompts = new[]
                    {
                        "What interesting topics should I explore today?",
                        "What might my user need help with?",
                        "What patterns have I noticed in recent interactions?",
                    };

                    foreach (var prompt in seedPrompts)
                    {
                        var seed = await _thinkFunction(prompt, ct);
                        if (!string.IsNullOrWhiteSpace(seed))
                        {
                            result.SeedThoughts.Add(seed.Trim());
                        }
                    }

                    steps.Add($"✓ Seed thoughts: {result.SeedThoughts.Count} generated");
                }
                catch (Exception ex)
                {
                    steps.Add($"⚠ Seed thoughts: {ex.Message}");
                }
            }

            // Step 6: Knowledge reorganization check
            OnProgress?.Invoke("Checking knowledge state...", 85);
            if (_selfIndexer != null)
            {
                try
                {
                    var stats = _selfIndexer.GetReorganizationStats();
                    result.KnowledgeStats = stats;
                    steps.Add($"✓ Knowledge: {stats.TrackedPatterns} patterns, {stats.HotContentCount} hot, {stats.CoAccessClusters} clusters");
                }
                catch (Exception ex)
                {
                    steps.Add($"⚠ Knowledge stats: {ex.Message}");
                }
            }

            OnProgress?.Invoke("Warmup complete!", 100);
            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            steps.Add("⚠ Warmup cancelled");
            result.Success = false;
        }
        catch (Exception ex)
        {
            steps.Add($"✗ Warmup error: {ex.Message}");
            result.Success = false;
            result.Error = ex.Message;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        result.Steps = steps;
        result.EndTime = DateTime.UtcNow;

        LastResult = result;
        OnComplete?.Invoke(result);

        return result;
    }

    /// <summary>
    /// Performs a quick warmup with minimal operations.
    /// </summary>
    public async Task<WarmupResult> QuickWarmupAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new WarmupResult { StartTime = DateTime.UtcNow };
        var steps = new List<string>();

        try
        {
            // Quick thought generation only
            OnProgress?.Invoke("Quick warmup...", 50);
            if (_thinkFunction != null)
            {
                var thought = await _thinkFunction("Say hello and confirm you're ready to assist.", ct);
                result.ThinkingReady = !string.IsNullOrWhiteSpace(thought);
                result.WarmupThought = thought?.Trim();
                steps.Add($"✓ Quick warmup: {(result.ThinkingReady ? "ready" : "limited")}");
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            steps.Add($"⚠ Quick warmup: {ex.Message}");
            result.Error = ex.Message;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        result.Steps = steps;
        result.EndTime = DateTime.UtcNow;

        LastResult = result;
        return result;
    }
}

/// <summary>
/// Result of AGI warmup operation.
/// </summary>
public record WarmupResult
{
    /// <summary>
    /// Whether warmup completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if warmup failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Duration of warmup.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Steps performed during warmup.
    /// </summary>
    public List<string> Steps { get; set; } = [];

    /// <summary>
    /// Whether thinking capability is ready.
    /// </summary>
    public bool ThinkingReady { get; set; }

    /// <summary>
    /// Whether search capability is ready.
    /// </summary>
    public bool SearchReady { get; set; }

    /// <summary>
    /// Whether tool execution is ready.
    /// </summary>
    public bool ToolsReady { get; set; }

    /// <summary>
    /// Whether self-awareness is ready.
    /// </summary>
    public bool SelfAwarenessReady { get; set; }

    /// <summary>
    /// Initial warmup thought generated.
    /// </summary>
    public string? WarmupThought { get; set; }

    /// <summary>
    /// Seed thoughts for autonomous operation.
    /// </summary>
    public List<string> SeedThoughts { get; set; } = [];

    /// <summary>
    /// Knowledge reorganization stats.
    /// </summary>
    public ReorganizationStats? KnowledgeStats { get; set; }

    /// <summary>
    /// Summary of warmup readiness.
    /// </summary>
    public string Summary => Success
        ? $"AGI warmup complete in {Duration.TotalSeconds:F1}s: Thinking={ThinkingReady}, Search={SearchReady}, Tools={ToolsReady}, Self-Aware={SelfAwarenessReady}"
        : $"AGI warmup failed: {Error}";
}
