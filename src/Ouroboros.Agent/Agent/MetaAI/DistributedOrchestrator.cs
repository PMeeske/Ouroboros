#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Distributed Orchestration - Coordinate multiple agents
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents an agent in the distributed system.
/// </summary>
public sealed record AgentInfo(
    string AgentId,
    string Name,
    HashSet<string> Capabilities,
    AgentStatus Status,
    DateTime LastHeartbeat);

/// <summary>
/// Agent status enumeration.
/// </summary>
public enum AgentStatus
{
    Available,
    Busy,
    Offline
}

/// <summary>
/// Represents a distributed task assignment.
/// </summary>
public sealed record TaskAssignment(
    string TaskId,
    string AgentId,
    PlanStep Step,
    DateTime AssignedAt,
    TaskAssignmentStatus Status);

/// <summary>
/// Task assignment status.
/// </summary>
public enum TaskAssignmentStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Configuration for distributed orchestration.
/// </summary>
public sealed record DistributedOrchestrationConfig(
    int MaxAgents = 10,
    TimeSpan HeartbeatTimeout = default,
    bool EnableLoadBalancing = true);

/// <summary>
/// Interface for distributed orchestration capabilities.
/// </summary>
public interface IDistributedOrchestrator
{
    /// <summary>
    /// Registers an agent in the distributed system.
    /// </summary>
    void RegisterAgent(AgentInfo agent);

    /// <summary>
    /// Unregisters an agent from the system.
    /// </summary>
    void UnregisterAgent(string agentId);

    /// <summary>
    /// Executes a plan across multiple agents.
    /// </summary>
    Task<Result<ExecutionResult, string>> ExecuteDistributedAsync(
        Plan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Gets status of all registered agents.
    /// </summary>
    IReadOnlyList<AgentInfo> GetAgentStatus();

    /// <summary>
    /// Updates agent heartbeat.
    /// </summary>
    void UpdateHeartbeat(string agentId);
}

/// <summary>
/// Implementation of distributed orchestration for multi-agent coordination.
/// </summary>
public sealed class DistributedOrchestrator : IDistributedOrchestrator
{
    private readonly ConcurrentDictionary<string, AgentInfo> _agents = new();
    private readonly ConcurrentDictionary<string, TaskAssignment> _assignments = new();
    private readonly ISafetyGuard _safety;
    private readonly DistributedOrchestrationConfig _config;

    public DistributedOrchestrator(
        ISafetyGuard safety,
        DistributedOrchestrationConfig? config = null)
    {
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _config = config ?? new DistributedOrchestrationConfig(
            HeartbeatTimeout: TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Registers an agent in the distributed system.
    /// </summary>
    public void RegisterAgent(AgentInfo agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (_agents.Count >= _config.MaxAgents)
            throw new InvalidOperationException($"Maximum number of agents ({_config.MaxAgents}) reached");

        _agents[agent.AgentId] = agent;
    }

    /// <summary>
    /// Unregisters an agent from the system.
    /// </summary>
    public void UnregisterAgent(string agentId)
    {
        _agents.TryRemove(agentId, out _);
    }

    /// <summary>
    /// Executes a plan across multiple agents.
    /// </summary>
    public async Task<Result<ExecutionResult, string>> ExecuteDistributedAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        if (plan == null)
            return Result<ExecutionResult, string>.Failure("Plan cannot be null");

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        List<StepResult> stepResults = new List<StepResult>();
        bool overallSuccess = true;

        try
        {
            // Remove offline agents
            CleanupOfflineAgents();

            List<AgentInfo> availableAgents = GetAvailableAgents();
            if (availableAgents.Count == 0)
            {
                return Result<ExecutionResult, string>.Failure("No agents available for execution");
            }

            // Assign steps to agents
            List<TaskAssignment> assignments = AssignStepsToAgents(plan.Steps, availableAgents);

            // Execute steps in parallel across agents
            IEnumerable<Task<StepResult>> tasks = assignments.Select(async assignment =>
            {
                AgentInfo agent = _agents[assignment.AgentId];
                PlanStep step = assignment.Step;

                // Mark agent as busy
                _agents[assignment.AgentId] = agent with { Status = AgentStatus.Busy };

                try
                {
                    // Simulate step execution (in real implementation, would delegate to actual agent)
                    StepResult result = await ExecuteStepOnAgentAsync(assignment, ct);

                    // Update assignment status
                    _assignments[assignment.TaskId] = assignment with
                    {
                        Status = result.Success ? TaskAssignmentStatus.Completed : TaskAssignmentStatus.Failed
                    };

                    return result;
                }
                finally
                {
                    // Mark agent as available
                    _agents[assignment.AgentId] = agent with { Status = AgentStatus.Available };
                }
            });

            stepResults.AddRange(await Task.WhenAll(tasks));

            overallSuccess = stepResults.All(r => r.Success);
            sw.Stop();

            string finalOutput = string.Join("\n", stepResults.Select(r => r.Output));

            ExecutionResult execution = new ExecutionResult(
                plan,
                stepResults,
                overallSuccess,
                finalOutput,
                new Dictionary<string, object>
                {
                    ["agents_used"] = assignments.Select(a => a.AgentId).Distinct().Count(),
                    ["distributed"] = true
                },
                sw.Elapsed);

            return Result<ExecutionResult, string>.Success(execution);
        }
        catch (Exception ex)
        {
            return Result<ExecutionResult, string>.Failure($"Distributed execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets status of all registered agents.
    /// </summary>
    public IReadOnlyList<AgentInfo> GetAgentStatus()
        => _agents.Values.ToList();

    /// <summary>
    /// Updates agent heartbeat.
    /// </summary>
    public void UpdateHeartbeat(string agentId)
    {
        if (_agents.TryGetValue(agentId, out AgentInfo? agent))
        {
            _agents[agentId] = agent with { LastHeartbeat = DateTime.UtcNow };
        }
    }

    private List<AgentInfo> GetAvailableAgents()
    {
        return _agents.Values
            .Where(a => a.Status == AgentStatus.Available)
            .ToList();
    }

    private List<TaskAssignment> AssignStepsToAgents(List<PlanStep> steps, List<AgentInfo> agents)
    {
        List<TaskAssignment> assignments = new List<TaskAssignment>();

        if (_config.EnableLoadBalancing)
        {
            // Round-robin load balancing
            for (int i = 0; i < steps.Count; i++)
            {
                AgentInfo agent = agents[i % agents.Count];
                TaskAssignment assignment = new TaskAssignment(
                    Guid.NewGuid().ToString(),
                    agent.AgentId,
                    steps[i],
                    DateTime.UtcNow,
                    TaskAssignmentStatus.Pending);

                assignments.Add(assignment);
                _assignments[assignment.TaskId] = assignment;
            }
        }
        else
        {
            // Capability-based assignment
            foreach (PlanStep step in steps)
            {
                AgentInfo? suitableAgent = FindSuitableAgent(step, agents);
                if (suitableAgent != null)
                {
                    TaskAssignment assignment = new TaskAssignment(
                        Guid.NewGuid().ToString(),
                        suitableAgent.AgentId,
                        step,
                        DateTime.UtcNow,
                        TaskAssignmentStatus.Pending);

                    assignments.Add(assignment);
                    _assignments[assignment.TaskId] = assignment;
                }
            }
        }

        return assignments;
    }

    private AgentInfo? FindSuitableAgent(PlanStep step, List<AgentInfo> agents)
    {
        // Find agent with matching capabilities
        return agents.FirstOrDefault(a => a.Capabilities.Contains(step.Action)) ?? agents.FirstOrDefault();
    }

    private async Task<StepResult> ExecuteStepOnAgentAsync(TaskAssignment assignment, CancellationToken ct)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Apply safety checks
            PlanStep sandboxedStep = _safety.SandboxStep(assignment.Step);

            // In real implementation, this would delegate to the actual agent
            // For now, simulate execution
            await Task.Delay(100, ct); // Simulate work

            sw.Stop();

            return new StepResult(
                sandboxedStep,
                true,
                $"Executed by agent {assignment.AgentId}",
                null,
                sw.Elapsed,
                new Dictionary<string, object>
                {
                    ["agent_id"] = assignment.AgentId,
                    ["task_id"] = assignment.TaskId
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StepResult(
                assignment.Step,
                false,
                "",
                ex.Message,
                sw.Elapsed,
                new Dictionary<string, object>
                {
                    ["agent_id"] = assignment.AgentId,
                    ["error"] = ex.Message
                });
        }
    }

    private void CleanupOfflineAgents()
    {
        TimeSpan timeout = _config.HeartbeatTimeout;
        DateTime now = DateTime.UtcNow;

        List<AgentInfo> offlineAgents = _agents.Values
            .Where(a => now - a.LastHeartbeat > timeout)
            .ToList();

        foreach (AgentInfo? agent in offlineAgents)
        {
            _agents[agent.AgentId] = agent with { Status = AgentStatus.Offline };
        }
    }
}
