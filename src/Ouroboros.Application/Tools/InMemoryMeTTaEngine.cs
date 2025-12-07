// <copyright file="InMemoryMeTTaEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// In-memory MeTTa engine implementation for when Docker/subprocess is unavailable.
/// Provides simplified symbolic reasoning for tool selection without full MeTTa capabilities.
/// </summary>
public sealed class InMemoryMeTTaEngine : IMeTTaEngine
{
    private readonly ConcurrentDictionary<string, List<string>> _facts = new();
    private readonly ConcurrentDictionary<string, string> _rules = new();
    private bool _disposed;

    /// <inheritdoc/>
    public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<string, string>.Failure("Engine disposed"));

        try
        {
            // Parse simple match queries
            // Example: !(match &self (goal $plan $goal) $goal)
            var matchPattern = Regex.Match(query, @"\(goal\s+\S+\s+""([^""]+)""\)");
            if (matchPattern.Success)
            {
                string goal = matchPattern.Groups[1].Value.ToLowerInvariant();
                var recommendations = InferToolsForGoal(goal);
                return Task.FromResult(Result<string, string>.Success(string.Join("\n", recommendations)));
            }

            // Check for capability queries
            var capabilityMatch = Regex.Match(query, @"\(capability\s+(\S+)\s+(\S+)\)");
            if (capabilityMatch.Success)
            {
                return Task.FromResult(Result<string, string>.Success("[capability-match]"));
            }

            // Default: return all tools
            if (_facts.TryGetValue("tools", out var tools))
            {
                return Task.FromResult(Result<string, string>.Success(string.Join("\n", tools)));
            }

            return Task.FromResult(Result<string, string>.Success("[]"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Query failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<Unit, string>.Failure("Engine disposed"));

        try
        {
            // Parse and store facts
            // Example: (tool tool_search "search")
            var toolMatch = Regex.Match(fact, @"\(tool\s+(\S+)\s+""([^""]+)""\)");
            if (toolMatch.Success)
            {
                string toolId = toolMatch.Groups[1].Value;
                _facts.AddOrUpdate("tools",
                    _ => new List<string> { toolId },
                    (_, list) => { list.Add(toolId); return list; });
            }

            // Store capability
            // Example: (capability tool_search information-retrieval)
            var capMatch = Regex.Match(fact, @"\(capability\s+(\S+)\s+(\S+)\)");
            if (capMatch.Success)
            {
                string toolId = capMatch.Groups[1].Value;
                string capability = capMatch.Groups[2].Value;
                _facts.AddOrUpdate($"capability:{toolId}",
                    _ => new List<string> { capability },
                    (_, list) => { list.Add(capability); return list; });
            }

            // Store goal
            var goalMatch = Regex.Match(fact, @"\(goal\s+(\S+)\s+""([^""]+)""\)");
            if (goalMatch.Success)
            {
                string goalId = goalMatch.Groups[1].Value;
                string goalText = goalMatch.Groups[2].Value;
                _facts.AddOrUpdate("goals",
                    _ => new List<string> { $"{goalId}:{goalText}" },
                    (_, list) => { list.Add($"{goalId}:{goalText}"); return list; });
            }

            // Store rules
            var ruleMatch = Regex.Match(fact, @"\(=\s+\(([^)]+)\)\s+(.+)\)", RegexOptions.Singleline);
            if (ruleMatch.Success)
            {
                string ruleName = ruleMatch.Groups[1].Value.Trim();
                string ruleBody = ruleMatch.Groups[2].Value.Trim();
                _rules[ruleName] = ruleBody;
            }

            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to add fact: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<string, string>.Failure("Engine disposed"));

        // Store the rule
        var match = Regex.Match(rule, @"\(=\s+\(([^)]+)\)\s+(.+)\)", RegexOptions.Singleline);
        if (match.Success)
        {
            string ruleName = match.Groups[1].Value.Trim();
            string ruleBody = match.Groups[2].Value.Trim();
            _rules[ruleName] = ruleBody;
            return Task.FromResult(Result<string, string>.Success($"Rule '{ruleName}' applied"));
        }

        return Task.FromResult(Result<string, string>.Success("Rule stored"));
    }

    /// <inheritdoc/>
    public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<bool, string>.Failure("Engine disposed"));

        // Simple verification: check if plan contains steps
        bool hasSteps = plan.Contains("step") || plan.Contains("goal");
        return Task.FromResult(Result<bool, string>.Success(hasSteps));
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        _facts.Clear();
        _rules.Clear();
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    /// <summary>
    /// Infers tool recommendations for a given goal using keyword matching.
    /// </summary>
    private List<string> InferToolsForGoal(string goal)
    {
        var recommendations = new List<string>();

        // Check keyword patterns
        if (ContainsAny(goal, "search", "find", "look", "query", "google", "bing"))
        {
            recommendations.Add("tool_search");
            recommendations.Add("tool_duckduckgo-search");
            recommendations.Add("tool_google-search");
        }

        if (ContainsAny(goal, "fetch", "download", "get", "url", "web", "http"))
        {
            recommendations.Add("tool_fetch-url");
        }

        if (ContainsAny(goal, "calculate", "compute", "math", "number", "add", "subtract"))
        {
            recommendations.Add("tool_calculator");
        }

        if (ContainsAny(goal, "analyze", "understand", "reason", "think", "infer"))
        {
            recommendations.Add("tool_metta");
            recommendations.Add("tool_symbolic-reasoning");
        }

        // Check registered tools for capability matches
        foreach (var kvp in _facts.Where(k => k.Key.StartsWith("capability:")))
        {
            string toolId = kvp.Key.Replace("capability:", string.Empty);
            foreach (var capability in kvp.Value)
            {
                if (goal.Contains(capability.Replace("-", " "), StringComparison.OrdinalIgnoreCase))
                {
                    if (!recommendations.Contains(toolId))
                        recommendations.Add(toolId);
                }
            }
        }

        return recommendations.Distinct().ToList();
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _facts.Clear();
        _rules.Clear();
    }
}
