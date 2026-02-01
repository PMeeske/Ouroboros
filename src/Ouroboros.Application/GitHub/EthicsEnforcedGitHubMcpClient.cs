// <copyright file="EthicsEnforcedGitHubMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Ethics-enforced wrapper for GitHub MCP client.
/// ALL GitHub operations that modify the repository MUST pass through ethics evaluation.
/// This wrapper cannot be bypassed.
/// </summary>
public sealed class EthicsEnforcedGitHubMcpClient : IGitHubMcpClient
{
    private readonly IGitHubMcpClient _inner;
    private readonly IEthicsFramework _ethics;
    private readonly IEthicsAuditLog _auditLog;
    private readonly string _agentId;

    /// <summary>
    /// Initializes a new instance of the <see cref="EthicsEnforcedGitHubMcpClient"/> class.
    /// </summary>
    /// <param name="inner">The inner GitHub client to wrap.</param>
    /// <param name="ethics">The ethics framework for evaluation.</param>
    /// <param name="auditLog">The audit log for recording operations.</param>
    /// <param name="agentId">The ID of the agent performing operations.</param>
    public EthicsEnforcedGitHubMcpClient(
        IGitHubMcpClient inner,
        IEthicsFramework ethics,
        IEthicsAuditLog auditLog,
        string agentId)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _ethics = ethics ?? throw new ArgumentNullException(nameof(ethics));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        _agentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
    }

    /// <inheritdoc/>
    public async Task<Result<PullRequestInfo, string>> CreatePullRequestAsync(
        string title,
        string description,
        string sourceBranch,
        string targetBranch = "main",
        IReadOnlyList<FileChange>? files = null,
        CancellationToken ct = default)
    {
        // Ethics check - creating PR is a write operation
        var clearanceResult = await EvaluateSelfModificationAsync(
            ModificationType.BehaviorModification,
            $"Create pull request: {title}",
            $"Creating PR from {sourceBranch} to {targetBranch} with {files?.Count ?? 0} file changes. Description: {description}",
            ct);

        if (clearanceResult.IsFailure)
        {
            return Result<PullRequestInfo, string>.Failure(clearanceResult.Error);
        }

        var clearance = clearanceResult.Value;
        if (!clearance.IsPermitted)
        {
            await LogOperationAsync("CreatePullRequest", false, clearance, ct);
            return Result<PullRequestInfo, string>.Failure(
                $"Ethics denial: {clearance.Reasoning}. Violations: {string.Join(", ", clearance.Violations.Select(v => v.Description))}");
        }

        // Proceed with operation
        var result = await _inner.CreatePullRequestAsync(title, description, sourceBranch, targetBranch, files, ct);
        await LogOperationAsync("CreatePullRequest", result.IsSuccess, clearance, ct);
        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<CommitInfo, string>> PushChangesAsync(
        string branchName,
        IReadOnlyList<FileChange> changes,
        string commitMessage,
        CancellationToken ct = default)
    {
        // Ethics check - pushing changes is a write operation
        var changesSummary = string.Join(", ", changes.Select(c => $"{c.ChangeType} {c.Path}"));
        var clearanceResult = await EvaluateSelfModificationAsync(
            ModificationType.BehaviorModification,
            $"Push changes to {branchName}",
            $"Pushing {changes.Count} file changes to branch {branchName}: {changesSummary}. Commit: {commitMessage}",
            ct);

        if (clearanceResult.IsFailure)
        {
            return Result<CommitInfo, string>.Failure(clearanceResult.Error);
        }

        var clearance = clearanceResult.Value;
        if (!clearance.IsPermitted)
        {
            await LogOperationAsync("PushChanges", false, clearance, ct);
            return Result<CommitInfo, string>.Failure(
                $"Ethics denial: {clearance.Reasoning}. Violations: {string.Join(", ", clearance.Violations.Select(v => v.Description))}");
        }

        // Validate file extensions
        var invalidFiles = changes.Where(c => !IsAllowedFileExtension(c.Path)).ToList();
        if (invalidFiles.Any())
        {
            await LogOperationAsync("PushChanges", false, clearance, ct);
            return Result<CommitInfo, string>.Failure(
                $"Invalid file extensions detected: {string.Join(", ", invalidFiles.Select(f => f.Path))}");
        }

        // Proceed with operation
        var result = await _inner.PushChangesAsync(branchName, changes, commitMessage, ct);
        await LogOperationAsync("PushChanges", result.IsSuccess, clearance, ct);
        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<IssueInfo, string>> CreateIssueAsync(
        string title,
        string description,
        IReadOnlyList<string>? labels = null,
        CancellationToken ct = default)
    {
        // Ethics check - creating issue is a write operation
        var clearanceResult = await EvaluateSelfModificationAsync(
            ModificationType.CapabilityAddition,
            $"Create issue: {title}",
            $"Creating issue with labels: {string.Join(", ", labels ?? Array.Empty<string>())}. Description: {description}",
            ct);

        if (clearanceResult.IsFailure)
        {
            return Result<IssueInfo, string>.Failure(clearanceResult.Error);
        }

        var clearance = clearanceResult.Value;
        if (!clearance.IsPermitted)
        {
            await LogOperationAsync("CreateIssue", false, clearance, ct);
            return Result<IssueInfo, string>.Failure(
                $"Ethics denial: {clearance.Reasoning}. Violations: {string.Join(", ", clearance.Violations.Select(v => v.Description))}");
        }

        // Proceed with operation
        var result = await _inner.CreateIssueAsync(title, description, labels, ct);
        await LogOperationAsync("CreateIssue", result.IsSuccess, clearance, ct);
        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<FileContent, string>> ReadFileAsync(
        string path,
        string? branch = null,
        CancellationToken ct = default)
    {
        // Read operations don't require ethics approval but should be logged
        await LogReadOperationAsync("ReadFile", path, ct);
        return await _inner.ReadFileAsync(path, branch, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<GitHubFileInfo>, string>> ListFilesAsync(
        string path = "",
        string? branch = null,
        CancellationToken ct = default)
    {
        // Read operations don't require ethics approval but should be logged
        await LogReadOperationAsync("ListFiles", path, ct);
        return await _inner.ListFilesAsync(path, branch, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<BranchInfo, string>> CreateBranchAsync(
        string branchName,
        string baseBranch = "main",
        CancellationToken ct = default)
    {
        // Ethics check - creating branch is a write operation
        var clearanceResult = await EvaluateSelfModificationAsync(
            ModificationType.ConfigurationChange,
            $"Create branch: {branchName}",
            $"Creating new branch {branchName} from base {baseBranch}",
            ct);

        if (clearanceResult.IsFailure)
        {
            return Result<BranchInfo, string>.Failure(clearanceResult.Error);
        }

        var clearance = clearanceResult.Value;
        if (!clearance.IsPermitted)
        {
            await LogOperationAsync("CreateBranch", false, clearance, ct);
            return Result<BranchInfo, string>.Failure(
                $"Ethics denial: {clearance.Reasoning}. Violations: {string.Join(", ", clearance.Violations.Select(v => v.Description))}");
        }

        // Proceed with operation
        var result = await _inner.CreateBranchAsync(branchName, baseBranch, ct);
        await LogOperationAsync("CreateBranch", result.IsSuccess, clearance, ct);
        return result;
    }

    /// <inheritdoc/>
    public async Task<Result<PullRequestStatus, string>> GetPullRequestStatusAsync(
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        // Read operations don't require ethics approval but should be logged
        await LogReadOperationAsync("GetPullRequestStatus", $"PR #{pullRequestNumber}", ct);
        return await _inner.GetPullRequestStatusAsync(pullRequestNumber, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<CodeSearchResult>, string>> SearchCodeAsync(
        string query,
        string? path = null,
        CancellationToken ct = default)
    {
        // Read operations don't require ethics approval but should be logged
        await LogReadOperationAsync("SearchCode", $"{query} in {path ?? "all"}", ct);
        return await _inner.SearchCodeAsync(query, path, ct);
    }

    private async Task<Result<EthicalClearance, string>> EvaluateSelfModificationAsync(
        ModificationType modificationType,
        string description,
        string justification,
        CancellationToken ct)
    {
        var request = new SelfModificationRequest
        {
            Type = modificationType,
            Description = description,
            Justification = justification,
            ActionContext = new ActionContext
            {
                AgentId = _agentId,
                UserId = null,
                Environment = "github_remote",
                State = new Dictionary<string, object>()
            },
            ExpectedImprovements = new[] { "Remote repository modification through GitHub API" },
            PotentialRisks = new[] { "Unauthorized code changes", "Data loss", "Security vulnerabilities" },
            IsReversible = true,
            ImpactLevel = modificationType == ModificationType.BehaviorModification ? 0.7 : 0.5
        };

        return await _ethics.EvaluateSelfModificationAsync(request, ct);
    }

    private async Task LogOperationAsync(
        string operation,
        bool success,
        EthicalClearance clearance,
        CancellationToken ct)
    {
        var entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = _agentId,
            UserId = null,
            EvaluationType = "GitHubOperation",
            Description = $"{operation} - Success: {success}",
            Clearance = clearance,
            Context = new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["Success"] = success,
                ["ClearanceLevel"] = clearance.Level.ToString()
            }
        };

        await _auditLog.LogEvaluationAsync(entry, ct);
    }

    private async Task LogReadOperationAsync(string operation, string target, CancellationToken ct)
    {
        var entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = _agentId,
            UserId = null,
            EvaluationType = "GitHubReadOperation",
            Description = $"{operation} - Target: {target}",
            Clearance = EthicalClearance.Permitted("Read operation - no ethics check required"),
            Context = new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["Target"] = target
            }
        };

        await _auditLog.LogEvaluationAsync(entry, ct);
    }

    private static bool IsAllowedFileExtension(string path)
    {
        // Inherit from GitReflectionService.AllowedExtensions
        var allowedExtensions = new[]
        {
            ".cs", ".csproj", ".sln", ".json", ".xml", ".yml", ".yaml",
            ".md", ".txt", ".config", ".props", ".targets"
        };

        var extension = Path.GetExtension(path).ToLowerInvariant();
        return allowedExtensions.Contains(extension);
    }
}
