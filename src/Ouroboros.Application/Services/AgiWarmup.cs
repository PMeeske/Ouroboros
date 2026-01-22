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
    private readonly ToolRegistry? _toolRegistry;

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
        QdrantSelfIndexer? selfIndexer = null,
        ToolRegistry? toolRegistry = null)
    {
        _thinkFunction = thinkFunction;
        _searchFunction = searchFunction;
        _executeToolFunction = executeToolFunction;
        _selfIndexer = selfIndexer;
        _toolRegistry = toolRegistry;
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

            // Step 1b: Comprehensive self-index warmup
            OnProgress?.Invoke("Warming up self-index...", 15);
            if (_selfIndexer != null)
            {
                await WarmupSelfIndexAsync(result, steps, ct);
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

            // Step 4b: Comprehensive tool warmup - test all registered tools
            OnProgress?.Invoke("Testing all registered tools...", 60);
            if (_toolRegistry != null)
            {
                await WarmupAllToolsAsync(result, steps, ct);
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
    /// Warms up the self-index by testing search, stats, and reorganization features.
    /// </summary>
    private async Task WarmupSelfIndexAsync(WarmupResult result, List<string> steps, CancellationToken ct)
    {
        if (_selfIndexer == null) return;

        try
        {
            // 1. Get index stats
            var stats = await _selfIndexer.GetStatsAsync(ct);
            result.SelfIndexStats = new SelfIndexWarmupStats
            {
                TotalVectors = stats.TotalVectors,
                IndexedFiles = stats.IndexedFiles,
                CollectionName = stats.CollectionName
            };

            // 2. Test various search queries to warm up the vector index
            var searchQueries = new[]
            {
                "error handling exception",
                "async await Task",
                "configuration settings",
                "tool registration",
                "LLM model inference"
            };

            int successfulSearches = 0;
            foreach (var query in searchQueries)
            {
                try
                {
                    using var searchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    searchCts.CancelAfter(TimeSpan.FromSeconds(5));

                    var searchResults = await _selfIndexer.SearchAsync(query, 2, 0.4f, searchCts.Token);
                    if (searchResults.Count > 0)
                    {
                        successfulSearches++;
                        // Record access to train the reorganization system
                        _selfIndexer.RecordAccess(searchResults);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Search timed out, continue with next
                }
                catch
                {
                    // Individual search failed, continue
                }
            }

            result.SelfIndexStats.SearchQueriesTested = searchQueries.Length;
            result.SelfIndexStats.SearchQueriesSucceeded = successfulSearches;

            // 3. Get reorganization stats
            var reorgStats = _selfIndexer.GetReorganizationStats();
            result.SelfIndexStats.TrackedPatterns = reorgStats.TrackedPatterns;
            result.SelfIndexStats.HotContentCount = reorgStats.HotContentCount;
            result.SelfIndexStats.CoAccessClusters = reorgStats.CoAccessClusters;

            // 4. Trigger quick reorganization if there's enough access data
            if (reorgStats.TrackedPatterns > 10)
            {
                try
                {
                    using var reorgCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    reorgCts.CancelAfter(TimeSpan.FromSeconds(10));

                    var reorgCount = await _selfIndexer.QuickReorganizeAsync(reorgCts.Token);
                    result.SelfIndexStats.ReorganizedChunks = reorgCount;
                    if (reorgCount > 0)
                    {
                        steps.Add($"  ↻ Reorganized {reorgCount} knowledge chunks for faster access");
                    }
                }
                catch
                {
                    // Reorganization failed or timed out
                }
            }

            result.SelfIndexReady = stats.TotalVectors > 0 && successfulSearches > 0;
            steps.Add($"✓ Self-index: {stats.IndexedFiles} files, {stats.TotalVectors} vectors, {successfulSearches}/{searchQueries.Length} searches OK");

            if (reorgStats.TrackedPatterns > 0)
            {
                steps.Add($"  📊 Knowledge patterns: {reorgStats.TrackedPatterns} tracked, {reorgStats.HotContentCount} hot, {reorgStats.CoAccessClusters} clusters");
            }
        }
        catch (Exception ex)
        {
            steps.Add($"⚠ Self-index warmup: {ex.Message}");
            result.SelfIndexReady = false;
        }
    }

    /// <summary>
    /// Warms up all registered tools by testing them with safe inputs.
    /// </summary>
    private async Task WarmupAllToolsAsync(WarmupResult result, List<string> steps, CancellationToken ct)
    {
        if (_toolRegistry == null) return;

        var toolTests = new Dictionary<string, string>
        {
            // Roslyn tools - test with simple C# code
            ["analyze_csharp_code"] = """{"code":"public class Test { public void Hello() { } }"}""",
            ["get_code_structure"] = """{"code":"namespace Foo { public class Bar { public int X { get; set; } } }"}""",
            ["format_csharp_code"] = """{"code":"public class Test{public void Hello(){}}"}""",
            ["create_csharp_class"] = """{"className":"WarmupTest","namespaceName":"Ouroboros.Warmup"}""",

            // System tools - safe operations
            ["system_info"] = "{}",
            ["environment"] = """{"action":"get","name":"PATH"}""",
            ["disk_info"] = "{}",
            ["network_info"] = "{}",

            // File tools - read-only operations
            ["file_system"] = """{"action":"exists","path":"."}""",
            ["directory_list"] = """{"path":".","pattern":"*.cs","maxDepth":1}""",

            // Calculator - simple math
            ["calculator"] = """{"expression":"2+2"}""",

            // Self-introspection - search only
            ["search_my_code"] = """{"query":"warmup","maxResults":1}""",
        };

        int testedCount = 0;
        int successCount = 0;
        var failedTools = new List<string>();

        foreach (var (toolName, testInput) in toolTests)
        {
            var toolOption = _toolRegistry.GetTool(toolName);
            if (!toolOption.HasValue) continue;

            try
            {
                var tool = toolOption.GetValueOrDefault(null!);
                if (tool == null) continue;

                testedCount++;
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10)); // Timeout per tool

                var toolResult = await tool.InvokeAsync(testInput, cts.Token);
                if (toolResult.IsSuccess)
                {
                    successCount++;
                    result.ToolWarmupResults[toolName] = true;
                }
                else
                {
                    failedTools.Add($"{toolName}: {toolResult.Error}");
                    result.ToolWarmupResults[toolName] = false;
                }
            }
            catch (OperationCanceledException)
            {
                failedTools.Add($"{toolName}: timeout");
                result.ToolWarmupResults[toolName] = false;
            }
            catch (Exception ex)
            {
                failedTools.Add($"{toolName}: {ex.Message}");
                result.ToolWarmupResults[toolName] = false;
            }
        }

        // Also count tools not explicitly tested
        result.TotalToolsRegistered = _toolRegistry.Count;

        if (testedCount > 0)
        {
            var percentage = (successCount * 100) / testedCount;
            steps.Add($"✓ Tool warmup: {successCount}/{testedCount} tools tested OK ({percentage}%)");

            if (failedTools.Count > 0 && failedTools.Count <= 3)
            {
                steps.Add($"  ⚠ Failed: {string.Join(", ", failedTools.Take(3))}");
            }
            else if (failedTools.Count > 3)
            {
                steps.Add($"  ⚠ {failedTools.Count} tools had issues");
            }

            result.ToolsTestedCount = testedCount;
            result.ToolsSuccessCount = successCount;
        }
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
    /// Total number of tools registered in the system.
    /// </summary>
    public int TotalToolsRegistered { get; set; }

    /// <summary>
    /// Number of tools that were tested during warmup.
    /// </summary>
    public int ToolsTestedCount { get; set; }

    /// <summary>
    /// Number of tools that passed warmup testing.
    /// </summary>
    public int ToolsSuccessCount { get; set; }

    /// <summary>
    /// Per-tool warmup results (tool name -> success).
    /// </summary>
    public Dictionary<string, bool> ToolWarmupResults { get; set; } = [];

    /// <summary>
    /// Whether self-index is warmed up and ready.
    /// </summary>
    public bool SelfIndexReady { get; set; }

    /// <summary>
    /// Self-index warmup statistics.
    /// </summary>
    public SelfIndexWarmupStats? SelfIndexStats { get; set; }

    /// <summary>
    /// Summary of warmup readiness.
    /// </summary>
    public string Summary => Success
        ? $"AGI warmup complete in {Duration.TotalSeconds:F1}s: Thinking={ThinkingReady}, Search={SearchReady}, Tools={ToolsSuccessCount}/{ToolsTestedCount}, Self-Index={SelfIndexReady}, Self-Aware={SelfAwarenessReady}"
        : $"AGI warmup failed: {Error}";
}

/// <summary>
/// Statistics from self-index warmup.
/// </summary>
public record SelfIndexWarmupStats
{
    /// <summary>Total vectors in the index.</summary>
    public long TotalVectors { get; set; }

    /// <summary>Number of indexed files.</summary>
    public int IndexedFiles { get; set; }

    /// <summary>Collection name in Qdrant.</summary>
    public string? CollectionName { get; set; }

    /// <summary>Number of search queries tested.</summary>
    public int SearchQueriesTested { get; set; }

    /// <summary>Number of successful search queries.</summary>
    public int SearchQueriesSucceeded { get; set; }

    /// <summary>Number of tracked access patterns.</summary>
    public int TrackedPatterns { get; set; }

    /// <summary>Number of hot (frequently accessed) content items.</summary>
    public int HotContentCount { get; set; }

    /// <summary>Number of co-access clusters identified.</summary>
    public int CoAccessClusters { get; set; }

    /// <summary>Whether reorganization was triggered.</summary>
    public bool ReorganizationTriggered { get; set; }

    /// <summary>Number of chunks reorganized during warmup.</summary>
    public int ReorganizedChunks { get; set; }
}
