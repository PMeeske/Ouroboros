// <copyright file="OrchestratorToolExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

/// <summary>
/// Extensions for creating performance-aware composable tools that
/// integrate with the orchestrator system.
/// </summary>
public static class OrchestratorToolExtensions
{
    /// <summary>
    /// Creates a performance-tracked wrapper around a tool.
    /// </summary>
    /// <returns></returns>
    public static ITool WithPerformanceTracking(
        this ITool tool,
        Action<string, double, bool> metricsCallback)
    {
        return new DelegateTool(
            $"{tool.Name}",
            tool.Description,
            async (input, ct) =>
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    Result<string, string> result = await tool.InvokeAsync(input, ct);
                    sw.Stop();
                    metricsCallback(tool.Name, sw.Elapsed.TotalMilliseconds, result.IsSuccess);
                    return result;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    metricsCallback(tool.Name, sw.Elapsed.TotalMilliseconds, false);
                    return Result<string, string>.Failure($"Tool error: {ex.Message}");
                }
            },
            tool.JsonSchema);
    }

    /// <summary>
    /// Creates a tool that selects between multiple implementations based on input.
    /// </summary>
    /// <returns></returns>
    public static ITool WithDynamicSelection(
        string name,
        string description,
        Func<string, ITool> selector,
        params ITool[] availableTools)
    {
        return new DelegateTool(
            name,
            description,
            async (input, ct) =>
            {
                try
                {
                    ITool? selected = selector(input);
                    if (selected is null)
                    {
                        return Result<string, string>.Failure(
                            "No suitable tool selected for input");
                    }

                    return await selected.InvokeAsync(input, ct);
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure(
                        $"Tool selection failed: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// Creates a tool that executes tools in parallel and combines results.
    /// </summary>
    /// <returns></returns>
    public static ITool Parallel(
        string name,
        string description,
        Func<string[], string> combiner,
        params ITool[] tools)
    {
        return new DelegateTool(
            name,
            description,
            async (input, ct) =>
            {
                try
                {
                    Task<Result<string, string>>[] tasks = tools.Select(t => t.InvokeAsync(input, ct)).ToArray();
                    Result<string, string>[] results = await Task.WhenAll(tasks);

                    string[] successes = results
                        .Where(r => r.IsSuccess)
                        .Select(r => r.Match(s => s, _ => string.Empty))
                        .ToArray();

                    if (successes.Length == 0)
                    {
                        return Result<string, string>.Failure(
                            "All parallel tool executions failed");
                    }

                    string combined = combiner(successes);
                    return Result<string, string>.Success(combined);
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure(
                        $"Parallel execution failed: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// Creates a tool with retry logic.
    /// </summary>
    /// <returns></returns>
    public static ITool WithRetry(
        this ITool tool,
        int maxRetries = 3,
        int delayMs = 100)
    {
        return new DelegateTool(
            tool.Name,
            tool.Description,
            async (input, ct) =>
            {
                Result<string, string> lastResult = Result<string, string>.Failure("Not executed");

                for (int i = 0; i < maxRetries; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return Result<string, string>.Failure("Operation cancelled");
                    }

                    lastResult = await tool.InvokeAsync(input, ct);

                    if (lastResult.IsSuccess)
                    {
                        return lastResult;
                    }

                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs * (i + 1), ct);
                    }
                }

                return lastResult;
            },
            tool.JsonSchema);
    }

    /// <summary>
    /// Creates a tool with caching of results.
    /// </summary>
    /// <returns></returns>
    public static ITool WithCaching(
        this ITool tool,
        TimeSpan cacheDuration)
    {
        System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime, string)> cache = new System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime, string)>();

        return new DelegateTool(
            tool.Name,
            tool.Description,
            async (input, ct) =>
            {
                DateTime now = DateTime.UtcNow;

                // Check cache
                if (cache.TryGetValue(input, out (DateTime, string) cached))
                {
                    if (now - cached.Item1 < cacheDuration)
                    {
                        return Result<string, string>.Success(cached.Item2);
                    }

                    // Remove stale entry
                    cache.TryRemove(input, out _);
                }

                // Execute tool
                Result<string, string> result = await tool.InvokeAsync(input, ct);

                // Cache successful results
                if (result.IsSuccess)
                {
                    result.Match(
                        success => cache[input] = (now, success),
                        _ => { });
                }

                return result;
            },
            tool.JsonSchema);
    }

    /// <summary>
    /// Creates a tool with timeout protection.
    /// </summary>
    /// <returns></returns>
    public static ITool WithTimeout(
        this ITool tool,
        TimeSpan timeout)
    {
        return new DelegateTool(
            tool.Name,
            tool.Description,
            async (input, ct) =>
            {
                using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);

                try
                {
                    return await tool.InvokeAsync(input, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return Result<string, string>.Failure(
                        $"Tool execution timed out after {timeout.TotalSeconds}s");
                }
            },
            tool.JsonSchema);
    }

    /// <summary>
    /// Creates a tool that falls back to another tool on failure.
    /// </summary>
    /// <returns></returns>
    public static ITool WithFallback(
        this ITool primary,
        ITool fallback)
    {
        return new DelegateTool(
            primary.Name,
            primary.Description,
            async (input, ct) =>
            {
                Result<string, string> result = await primary.InvokeAsync(input, ct);

                if (result.IsSuccess)
                {
                    return result;
                }

                // Try fallback
                return await fallback.InvokeAsync(input, ct);
            },
            primary.JsonSchema);
    }
}

/// <summary>
/// Advanced tool builder for creating composable, performance-aware tools.
/// </summary>
public static class AdvancedToolBuilder
{
    /// <summary>
    /// Creates a pipeline of tools that execute sequentially.
    /// </summary>
    /// <returns></returns>
    public static ITool Pipeline(
        string name,
        string description,
        params ITool[] tools)
    {
        return ToolBuilder.Chain(name, description, tools);
    }

    /// <summary>
    /// Creates a conditional tool that routes to different tools based on predicate.
    /// </summary>
    /// <returns></returns>
    public static ITool Switch(
        string name,
        string description,
        params (Func<string, bool> Predicate, ITool Tool)[] cases)
    {
        return new DelegateTool(
            name,
            description,
            async (input, ct) =>
            {
                foreach ((Func<string, bool> predicate, ITool tool) in cases)
                {
                    if (predicate(input))
                    {
                        return await tool.InvokeAsync(input, ct);
                    }
                }

                return Result<string, string>.Failure(
                    "No matching condition found for input");
            });
    }

    /// <summary>
    /// Creates a tool that aggregates results from multiple tools.
    /// </summary>
    /// <returns></returns>
    public static ITool Aggregate(
        string name,
        string description,
        Func<List<string>, string> aggregator,
        params ITool[] tools)
    {
        return new DelegateTool(
            name,
            description,
            async (input, ct) =>
            {
                List<string> results = new List<string>();

                foreach (ITool tool in tools)
                {
                    Result<string, string> result = await tool.InvokeAsync(input, ct);
                    result.Match(
                        success => results.Add(success),
                        failure => { /* Skip failed tools */ });
                }

                if (results.Count == 0)
                {
                    return Result<string, string>.Failure(
                        "All tools in aggregate failed");
                }

                try
                {
                    string aggregated = aggregator(results);
                    return Result<string, string>.Success(aggregated);
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure(
                        $"Aggregation failed: {ex.Message}");
                }
            });
    }
}
