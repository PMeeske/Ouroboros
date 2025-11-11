// <copyright file="EpicBranchOrchestrator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using System.Collections.Concurrent;
using LangChain.DocumentLoaders;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Pipeline.Branches;

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
    Failed,
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<Result<Epic, string>> RegisterEpicAsync(
        int epicNumber,
        string epicTitle,
        string epicDescription,
        List<int> subIssueNumbers,
        CancellationToken ct = default);

    /// <summary>
    /// Assigns an agent to a sub-issue and creates a dedicated branch.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<Result<SubIssueAssignment, string>> AssignSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        string? preferredAgentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all sub-issue assignments for an epic.
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<SubIssueAssignment> GetSubIssueAssignments(int epicNumber);

    /// <summary>
    /// Gets a specific sub-issue assignment.
    /// </summary>
    /// <returns></returns>
    SubIssueAssignment? GetSubIssueAssignment(int epicNumber, int subIssueNumber);

    /// <summary>
    /// Updates the status of a sub-issue.
    /// </summary>
    /// <returns></returns>
    Result<SubIssueAssignment, string> UpdateSubIssueStatus(
        int epicNumber,
        int subIssueNumber,
        SubIssueStatus status,
        string? errorMessage = null);

    /// <summary>
    /// Executes work on a sub-issue using its assigned agent and branch.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    private readonly ConcurrentDictionary<int, Epic> epics = new();
    private readonly ConcurrentDictionary<string, SubIssueAssignment> assignments = new();
    private readonly IDistributedOrchestrator distributor;
    private readonly EpicBranchConfig config;

    public EpicBranchOrchestrator(
        IDistributedOrchestrator distributor,
        EpicBranchConfig? config = null)
    {
        this.distributor = distributor ?? throw new ArgumentNullException(nameof(distributor));
        this.config = config ?? new EpicBranchConfig();
    }

    /// <summary>
    /// Registers an epic and creates assignments for all sub-issues.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Epic, string>> RegisterEpicAsync(
        int epicNumber,
        string epicTitle,
        string epicDescription,
        List<int> subIssueNumbers,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(epicTitle))
        {
            return Result<Epic, string>.Failure("Epic title cannot be empty");
        }

        if (subIssueNumbers == null || subIssueNumbers.Count == 0)
        {
            return Result<Epic, string>.Failure("Epic must have at least one sub-issue");
        }

        try
        {
            var epic = new Epic(
                epicNumber,
                epicTitle,
                epicDescription ?? string.Empty,
                subIssueNumbers,
                DateTime.UtcNow);

            this.epics[epicNumber] = epic;

            // Auto-assign agents to sub-issues if configured
            if (this.config.AutoAssignAgents)
            {
                var tasks = subIssueNumbers.Select(async subIssueNumber =>
                {
                    await this.AssignSubIssueAsync(epicNumber, subIssueNumber, null, ct);
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<SubIssueAssignment, string>> AssignSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        string? preferredAgentId = null,
        CancellationToken ct = default)
    {
        if (!this.epics.ContainsKey(epicNumber))
        {
            return Result<SubIssueAssignment, string>.Failure($"Epic #{epicNumber} not found");
        }

        var epic = this.epics[epicNumber];
        if (!epic.SubIssueNumbers.Contains(subIssueNumber))
        {
            return Result<SubIssueAssignment, string>.Failure($"Sub-issue #{subIssueNumber} not part of epic #{epicNumber}");
        }

        try
        {
            // Generate unique agent ID if not provided
            var agentId = preferredAgentId ?? $"{this.config.AgentPoolPrefix}-{epicNumber}-{subIssueNumber}";

            // Generate branch name
            var branchName = $"{this.config.BranchPrefix}-{epicNumber}/sub-issue-{subIssueNumber}";

            // Create pipeline branch if configured
            PipelineBranch? branch = null;
            if (this.config.AutoCreateBranches)
            {
                var store = new TrackedVectorStore();
                var source = DataSource.FromPath(Environment.CurrentDirectory);
                branch = new PipelineBranch(branchName, store, source);
            }

            // Register agent in distributed orchestrator
            var agent = new AgentInfo(
                agentId,
                $"Agent for sub-issue #{subIssueNumber}",
                new HashSet<string> { $"epic-{epicNumber}", $"sub-issue-{subIssueNumber}" },
                AgentStatus.Available,
                DateTime.UtcNow);

            this.distributor.RegisterAgent(agent);

            // Create assignment
            var assignment = new SubIssueAssignment(
                subIssueNumber,
                $"Sub-issue #{subIssueNumber}",
                $"Work item for epic #{epicNumber}",
                agentId,
                branchName,
                branch,
                this.config.AutoCreateBranches ? SubIssueStatus.BranchCreated : SubIssueStatus.Pending,
                DateTime.UtcNow);

            var key = GetAssignmentKey(epicNumber, subIssueNumber);
            this.assignments[key] = assignment;

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
    /// <returns></returns>
    public IReadOnlyList<SubIssueAssignment> GetSubIssueAssignments(int epicNumber)
    {
        return this.assignments.Values
            .Where(a => this.epics.TryGetValue(epicNumber, out var epic) && epic.SubIssueNumbers.Contains(a.IssueNumber))
            .ToList();
    }

    /// <summary>
    /// Gets a specific sub-issue assignment.
    /// </summary>
    /// <returns></returns>
    public SubIssueAssignment? GetSubIssueAssignment(int epicNumber, int subIssueNumber)
    {
        var key = GetAssignmentKey(epicNumber, subIssueNumber);
        return this.assignments.TryGetValue(key, out var assignment) ? assignment : null;
    }

    /// <summary>
    /// Updates the status of a sub-issue.
    /// </summary>
    /// <returns></returns>
    public Result<SubIssueAssignment, string> UpdateSubIssueStatus(
        int epicNumber,
        int subIssueNumber,
        SubIssueStatus status,
        string? errorMessage = null)
    {
        var key = GetAssignmentKey(epicNumber, subIssueNumber);
        if (!this.assignments.TryGetValue(key, out var assignment))
        {
            return Result<SubIssueAssignment, string>.Failure($"Assignment for sub-issue #{subIssueNumber} not found");
        }

        try
        {
            var updatedAssignment = assignment with
            {
                Status = status,
                CompletedAt = status == SubIssueStatus.Completed ? DateTime.UtcNow : assignment.CompletedAt,
                ErrorMessage = errorMessage,
            };

            this.assignments[key] = updatedAssignment;
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<SubIssueAssignment, string>> ExecuteSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        Func<SubIssueAssignment, Task<Result<SubIssueAssignment, string>>> workFunc,
        CancellationToken ct = default)
    {
        var key = GetAssignmentKey(epicNumber, subIssueNumber);
        if (!this.assignments.TryGetValue(key, out var assignment))
        {
            return Result<SubIssueAssignment, string>.Failure($"Assignment for sub-issue #{subIssueNumber} not found");
        }

        if (assignment.Status == SubIssueStatus.InProgress)
        {
            return Result<SubIssueAssignment, string>.Failure($"Sub-issue #{subIssueNumber} is already in progress");
        }

        try
        {
            // Update status to in progress
            var inProgressResult = this.UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.InProgress);
            if (!inProgressResult.IsSuccess)
            {
                return inProgressResult;
            }

            // Update agent heartbeat
            this.distributor.UpdateHeartbeat(assignment.AssignedAgentId);

            // Execute work function
            var result = await workFunc(assignment);

            if (result.IsSuccess)
            {
                // Update status to completed
                this.UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.Completed);
                return result;
            }
            else
            {
                // Update status to failed
                this.UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.Failed, result.Error);
                return result;
            }
        }
        catch (Exception ex)
        {
            this.UpdateSubIssueStatus(epicNumber, subIssueNumber, SubIssueStatus.Failed, ex.Message);
            return Result<SubIssueAssignment, string>.Failure($"Execution failed: {ex.Message}");
        }
    }

    private static string GetAssignmentKey(int epicNumber, int subIssueNumber)
        => $"epic-{epicNumber}-issue-{subIssueNumber}";
}
