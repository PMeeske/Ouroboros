#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Capability Registry Implementation
// Agent self-model with capability tracking and assessment
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

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
    private readonly ConcurrentDictionary<string, AgentCapability> _capabilities = new();
    private readonly IChatCompletionModel _llm;
    private readonly ToolRegistry _tools;
    private readonly CapabilityRegistryConfig _config;

    public CapabilityRegistry(
        IChatCompletionModel llm,
        ToolRegistry tools,
        CapabilityRegistryConfig? config = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _config = config ?? new CapabilityRegistryConfig(
            CapabilityExpirationTime: TimeSpan.FromDays(30));
    }

    /// <summary>
    /// Gets all capabilities the agent possesses.
    /// </summary>
    public async Task<List<AgentCapability>> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return _capabilities.Values
            .OrderByDescending(c => c.SuccessRate)
            .ThenByDescending(c => c.UsageCount)
            .ToList();
    }

    /// <summary>
    /// Checks if the agent can handle a given task.
    /// </summary>
    public async Task<bool> CanHandleAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(task))
            return false;

        // Check against known capabilities
        List<AgentCapability> relevantCapabilities = await FindRelevantCapabilitiesAsync(task, ct);

        if (relevantCapabilities.Any())
        {
            // If we have capabilities with good success rates, we can handle it
            IEnumerable<AgentCapability> reliableCapabilities = relevantCapabilities
                .Where(c => c.SuccessRate >= _config.MinSuccessRateThreshold
                         || c.UsageCount < _config.MinUsageCountForReliability);

            if (reliableCapabilities.Any())
                return true;
        }

        // Check if we have the required tools
        List<string> requiredTools = await AnalyzeRequiredToolsAsync(task, ct);
        HashSet<string> availableTools = _tools.All.Select(t => t.Name).ToHashSet();

        return requiredTools.All(t => availableTools.Contains(t));
    }

    /// <summary>
    /// Gets a specific capability by name.
    /// </summary>
    public AgentCapability? GetCapability(string name)
    {
        _capabilities.TryGetValue(name, out AgentCapability? capability);
        return capability;
    }

    /// <summary>
    /// Updates capability metrics after execution.
    /// </summary>
    public async Task UpdateCapabilityAsync(
        string name,
        ExecutionResult result,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_capabilities.TryGetValue(name, out AgentCapability? existing))
            return;

        int newUsageCount = existing.UsageCount + 1;
        double newSuccessRate = ((existing.SuccessRate * existing.UsageCount) + (result.Success ? 1.0 : 0.0)) / newUsageCount;
        double newAvgLatency = ((existing.AverageLatency * existing.UsageCount) + result.Duration.TotalMilliseconds) / newUsageCount;

        AgentCapability updated = existing with
        {
            SuccessRate = newSuccessRate,
            AverageLatency = newAvgLatency,
            UsageCount = newUsageCount,
            LastUsed = DateTime.UtcNow
        };

        _capabilities[name] = updated;
    }

    /// <summary>
    /// Registers a new capability.
    /// </summary>
    public void RegisterCapability(AgentCapability capability)
    {
        if (capability == null)
            throw new ArgumentNullException(nameof(capability));

        _capabilities[capability.Name] = capability;
    }

    /// <summary>
    /// Identifies capability gaps for a given task.
    /// </summary>
    public async Task<List<string>> IdentifyCapabilityGapsAsync(
        string task,
        CancellationToken ct = default)
    {
        List<string> gaps = new List<string>();

        // Analyze what the task requires
        List<string> requiredTools = await AnalyzeRequiredToolsAsync(task, ct);
        HashSet<string> availableTools = _tools.All.Select(t => t.Name).ToHashSet();

        // Identify missing tools
        List<string> missingTools = requiredTools.Where(t => !availableTools.Contains(t)).ToList();
        if (missingTools.Any())
        {
            gaps.Add($"Missing tools: {string.Join(", ", missingTools)}");
        }

        // Check if task complexity exceeds current capabilities
        List<AgentCapability> relevantCapabilities = await FindRelevantCapabilitiesAsync(task, ct);
        if (!relevantCapabilities.Any())
        {
            gaps.Add("No experience with similar tasks");
        }
        else
        {
            IEnumerable<AgentCapability> lowPerformingCapabilities = relevantCapabilities
                .Where(c => c.SuccessRate < _config.MinSuccessRateThreshold
                         && c.UsageCount >= _config.MinUsageCountForReliability);

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
    public async Task<List<string>> SuggestAlternativesAsync(
        string task,
        CancellationToken ct = default)
    {
        List<string> suggestions = new List<string>();

        // Identify what's missing
        List<string> gaps = await IdentifyCapabilityGapsAsync(task, ct);

        if (gaps.Any())
        {
            // Use LLM to generate alternative approaches
            string prompt = $@"Given this task: {task}

The agent has identified these capability gaps:
{string.Join("\n", gaps.Select(g => $"- {g}"))}

Available capabilities:
{string.Join("\n", _capabilities.Values.Take(5).Select(c => $"- {c.Name}: {c.Description} (Success: {c.SuccessRate:P0})"))}

Suggest 3-5 alternative approaches to accomplish this task or similar outcomes with available capabilities.
Format each suggestion on a new line starting with '- '";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse suggestions
            List<string> lines = response.Split('\n')
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
        string taskLower = task.ToLowerInvariant();
        string[] keywords = taskLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        List<AgentCapability> relevant = _capabilities.Values
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
        string availableTools = string.Join("\n", _tools.All.Select(t => $"- {t.Name}: {t.Description}"));

        string prompt = $@"Analyze this task and identify which tools would be needed:

Task: {task}

Available tools:
{availableTools}

List only the tool names that are required, one per line.";

        try
        {
            string response = await _llm.GenerateTextAsync(prompt, ct);
            List<string> toolNames = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
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
