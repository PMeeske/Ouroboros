#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Identity Graph Implementation
// Phase 2: Agent identity with capabilities, resources, commitments, performance
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;

namespace LangChainPipeline.Agent.MetaAI.SelfModel;

/// <summary>
/// Implementation of identity graph for agent self-modeling.
/// </summary>
public sealed class IdentityGraph : IIdentityGraph
{
    private readonly Guid _agentId;
    private readonly string _agentName;
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly ConcurrentDictionary<string, AgentResource> _resources = new();
    private readonly ConcurrentDictionary<Guid, AgentCommitment> _commitments = new();
    private readonly ConcurrentBag<(DateTime Time, ExecutionResult Result)> _taskHistory = new();
    private readonly string? _persistencePath;

    public IdentityGraph(
        Guid agentId,
        string agentName,
        ICapabilityRegistry capabilityRegistry,
        string? persistencePath = null)
    {
        _agentId = agentId;
        _agentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        _capabilityRegistry = capabilityRegistry ?? throw new ArgumentNullException(nameof(capabilityRegistry));
        _persistencePath = persistencePath;

        // Initialize default resources
        InitializeDefaultResources();
    }

    public async Task<AgentIdentityState> GetStateAsync(CancellationToken ct = default)
    {
        List<AgentCapability> capabilities = await _capabilityRegistry.GetCapabilitiesAsync(ct);
        List<AgentResource> resources = _resources.Values.ToList();
        List<AgentCommitment> commitments = _commitments.Values.OrderByDescending(c => c.Priority).ToList();
        AgentPerformance performance = GetPerformanceSummary(TimeSpan.FromDays(30));

        return new AgentIdentityState(
            _agentId,
            _agentName,
            capabilities,
            resources,
            commitments,
            performance,
            DateTime.UtcNow,
            new Dictionary<string, object>());
    }

    public void RegisterResource(AgentResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _resources[resource.Name] = resource;
    }

    public AgentResource? GetResource(string resourceName)
    {
        _resources.TryGetValue(resourceName, out AgentResource? resource);
        return resource;
    }

    public AgentCommitment CreateCommitment(
        string description,
        DateTime deadline,
        double priority,
        List<string>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(description);
        
        var commitment = new AgentCommitment(
            Guid.NewGuid(),
            description,
            deadline,
            Math.Clamp(priority, 0.0, 1.0),
            CommitmentStatus.Planned,
            0.0,
            dependencies ?? new List<string>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow,
            null);

        _commitments[commitment.Id] = commitment;
        return commitment;
    }

    public void UpdateCommitment(Guid commitmentId, CommitmentStatus status, double progressPercent)
    {
        if (_commitments.TryGetValue(commitmentId, out AgentCommitment? existing))
        {
            DateTime? completedAt = status is CommitmentStatus.Completed or CommitmentStatus.Failed or CommitmentStatus.Cancelled
                ? DateTime.UtcNow
                : existing.CompletedAt;

            AgentCommitment updated = existing with
            {
                Status = status,
                ProgressPercent = Math.Clamp(progressPercent, 0.0, 100.0),
                CompletedAt = completedAt
            };

            _commitments[commitmentId] = updated;
        }
    }

    public List<AgentCommitment> GetActiveCommitments()
    {
        return _commitments.Values
            .Where(c => c.Status is CommitmentStatus.Planned or CommitmentStatus.InProgress or CommitmentStatus.AtRisk)
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Deadline)
            .ToList();
    }

    public List<AgentCommitment> GetAtRiskCommitments()
    {
        DateTime now = DateTime.UtcNow;
        return _commitments.Values
            .Where(c => c.Status == CommitmentStatus.InProgress &&
                       c.Deadline < now.AddHours(24) &&
                       c.ProgressPercent < 80.0)
            .OrderBy(c => c.Deadline)
            .ToList();
    }

    public void RecordTaskResult(ExecutionResult taskResult)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        _taskHistory.Add((DateTime.UtcNow, taskResult));
    }

    public AgentPerformance GetPerformanceSummary(TimeSpan timeWindow)
    {
        DateTime cutoff = DateTime.UtcNow - timeWindow;
        List<(DateTime Time, ExecutionResult Result)> recentTasks = _taskHistory
            .Where(t => t.Time >= cutoff)
            .ToList();

        if (!recentTasks.Any())
        {
            return new AgentPerformance(
                0.0,
                0.0,
                0,
                0,
                0,
                new Dictionary<string, double>(),
                new Dictionary<string, double>(),
                cutoff,
                DateTime.UtcNow);
        }

        int totalTasks = recentTasks.Count;
        int successfulTasks = recentTasks.Count(t => t.Result.Success);
        int failedTasks = totalTasks - successfulTasks;
        double overallSuccessRate = totalTasks > 0 ? successfulTasks / (double)totalTasks : 0.0;
        double averageResponseTime = recentTasks.Average(t => t.Result.Duration.TotalMilliseconds);

        // Calculate resource utilization
        Dictionary<string, double> resourceUtilization = _resources.Values
            .ToDictionary(
                r => r.Name,
                r => r.Total > 0 ? (r.Total - r.Available) / r.Total : 0.0);

        return new AgentPerformance(
            overallSuccessRate,
            averageResponseTime,
            totalTasks,
            successfulTasks,
            failedTasks,
            new Dictionary<string, double>(), // Capability success rates computed elsewhere
            resourceUtilization,
            cutoff,
            DateTime.UtcNow);
    }

    public async Task SaveStateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_persistencePath))
            return;

        AgentIdentityState state = await GetStateAsync(ct);
        string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        
        string directory = Path.GetDirectoryName(_persistencePath)!;
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await File.WriteAllTextAsync(_persistencePath, json, ct);
    }

    public async Task LoadStateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_persistencePath) || !File.Exists(_persistencePath))
            return;

        string json = await File.ReadAllTextAsync(_persistencePath, ct);
        AgentIdentityState? state = JsonSerializer.Deserialize<AgentIdentityState>(json);

        if (state == null)
            return;

        // Restore resources
        foreach (AgentResource resource in state.Resources)
        {
            RegisterResource(resource);
        }

        // Restore commitments
        foreach (AgentCommitment commitment in state.Commitments)
        {
            _commitments[commitment.Id] = commitment;
        }

        // Restore capabilities
        foreach (AgentCapability capability in state.Capabilities)
        {
            _capabilityRegistry.RegisterCapability(capability);
        }
    }

    private void InitializeDefaultResources()
    {
        // Initialize default computational resources
        RegisterResource(new AgentResource(
            "CPU",
            "Computation",
            Environment.ProcessorCount,
            Environment.ProcessorCount,
            "cores",
            DateTime.UtcNow,
            new Dictionary<string, object>()));

        RegisterResource(new AgentResource(
            "Memory",
            "Storage",
            1024.0,
            1024.0,
            "MB",
            DateTime.UtcNow,
            new Dictionary<string, object>()));

        RegisterResource(new AgentResource(
            "Attention",
            "Cognitive",
            100.0,
            100.0,
            "units",
            DateTime.UtcNow,
            new Dictionary<string, object>()));
    }
}
