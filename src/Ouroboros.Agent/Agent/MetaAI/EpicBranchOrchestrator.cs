#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Epic Branch Orchestration - Manage sub-issues with agents and branches
// ==========================================================

using System.Collections.Concurrent;
using LangChain.DocumentLoaders;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a sub-issue in an epic with its assigned agent and branch.
/// </summary>
public sealed record SubIssueAssignment(
    int IssueNumber,
    string Title,
    string Description,
    string AssignedAgentId,
    string BranchName,
    PipelineBranch? Branch,
    SubIssueStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt = null,
    string? ErrorMessage = null);

/// <summary>
/// Status of a sub-issue.
/// </summary>
public enum SubIssueStatus
{
    Pending,
    BranchCreated,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Represents an epic with its sub-issues.
/// </summary>
public sealed record Epic(
    int EpicNumber,
    string Title,
    string Description,
    List<int> SubIssueNumbers,
    DateTime CreatedAt);

/// <summary>
/// Configuration for epic branch orchestration.
/// </summary>
public sealed record EpicBranchConfig(
    string BranchPrefix = "epic",
    string AgentPoolPrefix = "sub-issue-agent",
    bool AutoCreateBranches = true,
    bool AutoAssignAgents = true,
    int MaxConcurrentSubIssues = 5);

/// <summary>
/// Interface for epic branch orchestration.
/// </summary>
public interface IEpicBranchOrchestrator
{
    /// <summary>
    /// Registers an epic and creates assignments for all sub-issues.
    /// </summary>
    Task<Result<Epic, string>> RegisterEpicAsync(
        int epicNumber,
        string epicTitle,
        string epicDescription,
        List<int> subIssueNumbers,
        CancellationToken ct = default);

    /// <summary>
    /// Assigns an agent to a sub-issue and creates a dedicated branch.
    /// </summary>
    Task<Result<SubIssueAssignment, string>> AssignSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        string? preferredAgentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all sub-issue assignments for an epic.
    /// </summary>
    IReadOnlyList<SubIssueAssignment> GetSubIssueAssignments(int epicNumber);

    /// <summary>
    /// Gets a specific sub-issue assignment.
    /// </summary>
    SubIssueAssignment? GetSubIssueAssignment(int epicNumber, int subIssueNumber);

    /// <summary>
    /// Updates the status of a sub-issue.
    /// </summary>
    Result<SubIssueAssignment, string> UpdateSubIssueStatus(
        int epicNumber,
        int subIssueNumber,
        SubIssueStatus status,
        string? errorMessage = null);

    /// <summary>
    /// Executes work on a sub-issue using its assigned agent and branch.
    /// </summary>
    Task<Result<SubIssueAssignment, string>> ExecuteSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        Func<SubIssueAssignment, Task<Result<SubIssueAssignment, string>>> workFunc,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of epic branch orchestration for managing sub-issues with dedicated agents and branches.
/// </summary>
public sealed class EpicBranchOrchestrator : IEpicBranchOrchestrator
{
    private readonly ConcurrentDictionary<int, Epic> _epics = new();
    private readonly ConcurrentDictionary<string, SubIssueAssignment> _assignments = new();
    private readonly IDistributedOrchestrator _distributor;
    private readonly EpicBranchConfig _config;

    public EpicBranchOrchestrator(
        IDistributedOrchestrator distributor,
        EpicBranchConfig? config = null)
    {
        _distributor = distributor ?? throw new ArgumentNullException(nameof(distributor));
        _config = config ?? new EpicBranchConfig();
    }

    /// <summary>
    /// Registers an epic and creates assignments for all sub-issues.
    /// </summary>
    public async Task<Result<Epic, string>> RegisterEpicAsync(
        int epicNumber,
        string epicTitle,
        string epicDescription,
        List<int> subIssueNumbers,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(epicTitle))
            return Result<Epic, string>.Failure("Epic title cannot be empty");

        if (subIssueNumbers == null || subIssueNumbers.Count == 0)
            return Result<Epic, string>.Failure("Epic must have at least one sub-issue");

        try
        {
            Epic epic = new Epic(
                epicNumber,
                epicTitle,
                epicDescription ?? string.Empty,
                subIssueNumbers,
                DateTime.UtcNow);

            _epics[epicNumber] = epic;

            // Auto-assign agents to sub-issues if configured
            if (_config.AutoAssignAgents)
            {
                IEnumerable<Task> tasks = subIssueNumbers.Select(async subIssueNumber =>
                {
                    await AssignSubIssueAsync(epicNumber, subIssueNumber, null, ct);
                });

                await Task.WhenAll(tasks);
            }

            return Result<Epic, string>.Success(epic);
        }
        catch (Exception ex)
        {
            return Result<Epic, string>.Failure($"Failed to register epic: {ex.Message}");
        }
    }

    /// <summary>
    /// Assigns an agent to a sub-issue and creates a dedicated branch.
    /// </summary>
    public async Task<Result<SubIssueAssignment, string>> AssignSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        string? preferredAgentId = null,
        CancellationToken ct = default)
    {
        if (!_epics.ContainsKey(epicNumber))
            return Result<SubIssueAssignment, string>.Failure($"Epic #{epicNumber} not found");

        Epic epic = _epics[epicNumber];
        if (!epic.SubIssueNumbers.Contains(subIssueNumber))
            return Result<SubIssueAssignment, string>.Failure($"Sub-issue #{subIssueNumber} not part of epic #{epicNumber}");

        try
        {
            // Generate unique agent ID if not provided
            string agentId = preferredAgentId ?? $"{_config.AgentPoolPrefix}-{epicNumber}-{subIssueNumber}";

            // Generate branch name
            string branchName = $"{_config.BranchPrefix}-{epicNumber}/sub-issue-{subIssueNumber}";

            // Create pipeline branch if configured
            PipelineBranch? branch = null;
            if (_config.AutoCreateBranches)
            {
                TrackedVectorStore store = new TrackedVectorStore();
                DataSource source = DataSource.FromPath(Environment.CurrentDirectory);
                branch = new PipelineBranch(branchName, store, source);
            }

            // Register agent in distributed orchestrator
            AgentInfo agent = new AgentInfo(
                agentId,
                $"Agent for sub-issue #{subIssueNumber}",
                new HashSet<string> { $"epic-{epicNumber}", $"sub-issue-{subIssueNumber}" },
                AgentStatus.Available,
                DateTime.UtcNow);

            _distributor.RegisterAgent(agent);

            // Create assignment
            SubIssueAssignment assignment = new SubIssueAssignment(
                subIssueNumber,
                $"Sub-issue #{subIssueNumber}",
                $"Work item for epic #{epicNumber}",
                agentId,
                branchName,
                branch,
                _config.AutoCreateBranches ? SubIssueStatus.BranchCreated : SubIssueStatus.Pending,
                DateTime.UtcNow);

            string key = GetAssignmentKey(epicNumber, subIssueNumber);
            _assignments[key] = assignment;

            await Task.CompletedTask; // For async compliance
            return Result<SubIssueAssignment, string>.Success(assignment);
        }
        catch (Exception ex)
        {
            return Result<SubIssueAssignment, string>.Failure($"Failed to assign sub-issue: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all sub-issue assignments for an epic.
    /// </summary>
    public IReadOnlyList<SubIssueAssignment> GetSubIssueAssignments(int epicNumber)
    {
        return _assignments.Values
            .Where(a => _epics.TryGetValue(epicNumber, out Epic? epic) && epic.SubIssueNumbers.Contains(a.IssueNumber))
            .ToList();
    }

    /// <summary>
    /// Gets a specific sub-issue assignment.
    /// </summary>
    public SubIssueAssignment? GetSubIssueAssignment(int epicNumber, int subIssueNumber)
    {
        string key = GetAssignmentKey(epicNumber, subIssueNumber);
        return _assignments.TryGetValue(key, out SubIssueAssignment? assignment) ? assignment : null;
    }

    /// <summary>
    /// Updates the status of a sub-issue.
    /// </summary>
    public Result<SubIssueAssignment, string> UpdateSubIssueStatus(
        int epicNumber,
        int subIssueNumber,
        SubIssueStatus status,
        string? errorMessage = null)
    {
        string key = GetAssignmentKey(epicNumber, subIssueNumber);
        if (!_assignments.TryGetValue(key, out SubIssueAssignment? assignment))
            return Result<SubIssueAssignment, string>.Failure($"Assignment for sub-issue #{subIssueNumber} not found");

        try
        {
            SubIssueAssignment updatedAssignment = assignment with
            {
                Status = status,
                CompletedAt = status == SubIssueStatus.Completed ? DateTime.UtcNow : assignment.CompletedAt,
                ErrorMessage = errorMessage
            };

            _assignments[key] = updatedAssignment;
            return Result<SubIssueAssignment, string>.Success(updatedAssignment);
        }
        catch (Exception ex)
        {
            return Result<SubIssueAssignment, string>.Failure($"Failed to update status: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes work on a sub-issue using its assigned agent and branch.
    /// </summary>
    public async Task<Result<SubIssueAssignment, string>> ExecuteSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        Func<SubIssueAssignment, Task<Result<SubIssueAssignment, string>>> workFunc,
        CancellationToken ct = default)
    {
        string key = GetAssignmentKey(epicNumber, subIssueNumber);
        if (!_assignments.TryGetValue(key, out SubIssueAssignment? assignment))
            return Result<SubIssueAssignment, string>.Failure($"Assignment for sub-issue #{subIssueNumber} not found");

        if (assignment.Status == SubIssueStatus.InProgress)
            return Result<SubIssueAssignment, string>.Failure($"Sub-issue #{subIssueNumber} is already in progress");

        try
        {
            // Update status to in progress
            Result<SubIssueAssignment, string> inProgressResult = UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.InProgress);
            if (!inProgressResult.IsSuccess)
                return inProgressResult;

            // Update agent heartbeat
            _distributor.UpdateHeartbeat(assignment.AssignedAgentId);

            // Execute work function
            Result<SubIssueAssignment, string> result = await workFunc(assignment);

            if (result.IsSuccess)
            {
                // Update status to completed
                UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.Completed);
                return result;
            }
            else
            {
                // Update status to failed
                UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.Failed, result.Error);
                return result;
            }
        }
        catch (Exception ex)
        {
            UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.Failed, ex.Message);
            return Result<SubIssueAssignment, string>.Failure($"Execution failed: {ex.Message}");
        }
    }

    private static string GetAssignmentKey(int epicNumber, int subIssueNumber)
        => $"epic-{epicNumber}-issue-{subIssueNumber}";
}
