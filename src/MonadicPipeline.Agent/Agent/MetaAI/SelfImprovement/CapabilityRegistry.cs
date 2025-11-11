// <copyright file="CapabilityRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using System.Collections.Concurrent;

/// <summary>
/// Configuration for capability registry behavior.
/// </summary>
public sealed record CapabilityRegistryConfig(
    double MinSuccessRateThreshold = 0.6,
    int MinUsageCountForReliability = 5,
    TimeSpan CapabilityExpirationTime = default);

/// <summary>
/// Implementation of capability registry for agent self-modeling.
/// Tracks what the agent can do, success rates, and limitations.
/// </summary>
public sealed class CapabilityRegistry : ICapabilityRegistry
{
    private readonly ConcurrentDictionary<string, AgentCapability> capabilities = new();
    private readonly IChatCompletionModel llm;
    private readonly ToolRegistry tools;
    private readonly CapabilityRegistryConfig config;

    public CapabilityRegistry(
        IChatCompletionModel llm,
        ToolRegistry tools,
        CapabilityRegistryConfig? config = null)
    {
        this.llm = llm ?? throw new ArgumentNullException(nameof(llm));
        this.tools = tools ?? throw new ArgumentNullException(nameof(tools));
        this.config = config ?? new CapabilityRegistryConfig(
            CapabilityExpirationTime: TimeSpan.FromDays(30));
    }

    /// <summary>
    /// Gets all capabilities the agent possesses.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<List<AgentCapability>> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return this.capabilities.Values
            .OrderByDescending(c => c.SuccessRate)
            .ThenByDescending(c => c.UsageCount)
            .ToList();
    }

    /// <summary>
    /// Checks if the agent can handle a given task.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<bool> CanHandleAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(task))
        {
            return false;
        }

        // Check against known capabilities
        var relevantCapabilities = await this.FindRelevantCapabilitiesAsync(task, ct);

        if (relevantCapabilities.Any())
        {
            // If we have capabilities with good success rates, we can handle it
            var reliableCapabilities = relevantCapabilities
                .Where(c => c.SuccessRate >= this.config.MinSuccessRateThreshold
                         || c.UsageCount < this.config.MinUsageCountForReliability);

            if (reliableCapabilities.Any())
            {
                return true;
            }
        }

        // Check if we have the required tools
        var requiredTools = await this.AnalyzeRequiredToolsAsync(task, ct);
        var availableTools = this.tools.All.Select(t => t.Name).ToHashSet();

        return requiredTools.All(t => availableTools.Contains(t));
    }

    /// <summary>
    /// Gets a specific capability by name.
    /// </summary>
    /// <returns></returns>
    public AgentCapability? GetCapability(string name)
    {
        this.capabilities.TryGetValue(name, out var capability);
        return capability;
    }

    /// <summary>
    /// Updates capability metrics after execution.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task UpdateCapabilityAsync(
        string name,
        ExecutionResult result,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!this.capabilities.TryGetValue(name, out var existing))
        {
            return;
        }

        var newUsageCount = existing.UsageCount + 1;
        var newSuccessRate = ((existing.SuccessRate * existing.UsageCount) + (result.Success ? 1.0 : 0.0)) / newUsageCount;
        var newAvgLatency = ((existing.AverageLatency * existing.UsageCount) + result.Duration.TotalMilliseconds) / newUsageCount;

        var updated = existing with
        {
            SuccessRate = newSuccessRate,
            AverageLatency = newAvgLatency,
            UsageCount = newUsageCount,
            LastUsed = DateTime.UtcNow,
        };

        this.capabilities[name] = updated;
    }

    /// <summary>
    /// Registers a new capability.
    /// </summary>
    public void RegisterCapability(AgentCapability capability)
    {
        if (capability == null)
        {
            throw new ArgumentNullException(nameof(capability));
        }

        this.capabilities[capability.Name] = capability;
    }

    /// <summary>
    /// Identifies capability gaps for a given task.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<List<string>> IdentifyCapabilityGapsAsync(
        string task,
        CancellationToken ct = default)
    {
        var gaps = new List<string>();

        // Analyze what the task requires
        var requiredTools = await this.AnalyzeRequiredToolsAsync(task, ct);
        var availableTools = this.tools.All.Select(t => t.Name).ToHashSet();

        // Identify missing tools
        var missingTools = requiredTools.Where(t => !availableTools.Contains(t)).ToList();
        if (missingTools.Any())
        {
            gaps.Add($"Missing tools: {string.Join(", ", missingTools)}");
        }

        // Check if task complexity exceeds current capabilities
        var relevantCapabilities = await this.FindRelevantCapabilitiesAsync(task, ct);
        if (!relevantCapabilities.Any())
        {
            gaps.Add("No experience with similar tasks");
        }
        else
        {
            var lowPerformingCapabilities = relevantCapabilities
                .Where(c => c.SuccessRate < this.config.MinSuccessRateThreshold
                         && c.UsageCount >= this.config.MinUsageCountForReliability);

            if (lowPerformingCapabilities.Any())
            {
                gaps.Add($"Low success rate in: {string.Join(", ", lowPerformingCapabilities.Select(c => c.Name))}");
            }
        }

        return gaps;
    }

    /// <summary>
    /// Suggests alternatives when a task cannot be handled.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<List<string>> SuggestAlternativesAsync(
        string task,
        CancellationToken ct = default)
    {
        var suggestions = new List<string>();

        // Identify what's missing
        var gaps = await this.IdentifyCapabilityGapsAsync(task, ct);

        if (gaps.Any())
        {
            // Use LLM to generate alternative approaches
            var prompt = $@"Given this task: {task}

The agent has identified these capability gaps:
{string.Join("\n", gaps.Select(g => $"- {g}"))}

Available capabilities:
{string.Join("\n", this.capabilities.Values.Take(5).Select(c => $"- {c.Name}: {c.Description} (Success: {c.SuccessRate:P0})"))}

Suggest 3-5 alternative approaches to accomplish this task or similar outcomes with available capabilities.
Format each suggestion on a new line starting with '- '";

            var response = await this.llm.GenerateTextAsync(prompt, ct);

            // Parse suggestions
            var lines = response.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("- "))
                .Select(l => l.Substring(2).Trim())
                .ToList();

            suggestions.AddRange(lines);
        }

        return suggestions;
    }

    // Private helper methods
    private async Task<List<AgentCapability>> FindRelevantCapabilitiesAsync(
        string task,
        CancellationToken ct)
    {
        // Simple keyword matching - in production, use embedding similarity
        var taskLower = task.ToLowerInvariant();
        var keywords = taskLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var relevant = this.capabilities.Values
            .Where(c => keywords.Any(k =>
                c.Name.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Task.CompletedTask;
        return relevant;
    }

    private async Task<List<string>> AnalyzeRequiredToolsAsync(
        string task,
        CancellationToken ct)
    {
        // Use LLM to analyze what tools are needed
        var availableTools = string.Join("\n", this.tools.All.Select(t => $"- {t.Name}: {t.Description}"));

        var prompt = $@"Analyze this task and identify which tools would be needed:

Task: {task}

Available tools:
{availableTools}

List only the tool names that are required, one per line.";

        try
        {
            var response = await this.llm.GenerateTextAsync(prompt, ct);
            var toolNames = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                .ToList();

            return toolNames;
        }
        catch
        {
            // Fallback: return empty list
            return new List<string>();
        }
    }
}
