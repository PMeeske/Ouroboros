// <copyright file="GitHubSelfModificationWorkflow.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Complete workflow for autonomous self-modification through GitHub.
/// Orchestrates: Analyze → Propose → Branch → Push → PR → (Optional: Request review)
/// </summary>
public sealed class GitHubSelfModificationWorkflow
{
    private readonly IGitHubMcpClient _client;
    private readonly IEthicsAuditLog _auditLog;
    private readonly string _agentId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubSelfModificationWorkflow"/> class.
    /// </summary>
    /// <param name="client">The GitHub MCP client (should be ethics-enforced).</param>
    /// <param name="auditLog">The audit log for recording workflow steps.</param>
    /// <param name="agentId">The ID of the agent executing the workflow.</param>
    public GitHubSelfModificationWorkflow(
        IGitHubMcpClient client,
        IEthicsAuditLog auditLog,
        string agentId)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        _agentId = agentId ?? throw new ArgumentNullException(nameof(agentId));
    }

    /// <summary>
    /// Executes the complete self-modification workflow.
    /// </summary>
    /// <param name="proposal">The self-modification proposal.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the workflow outcome.</returns>
    public async Task<Result<SelfModificationResult, string>> ExecuteAsync(
        SelfModificationProposal proposal,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Validate proposal
            if (!ValidateProposal(proposal, out var validationError))
            {
                return Result<SelfModificationResult, string>.Failure(validationError);
            }

            await LogWorkflowStepAsync("Validation", "Proposal validated successfully", ct);

            // 2. Create branch
            var branchName = GenerateBranchName(proposal.Category);
            var branchResult = await _client.CreateBranchAsync(branchName, ct: ct);

            if (branchResult.IsFailure)
            {
                await LogWorkflowStepAsync("CreateBranch", $"Failed: {branchResult.Error}", ct);
                return Result<SelfModificationResult, string>.Failure(
                    $"Failed to create branch: {branchResult.Error}");
            }

            await LogWorkflowStepAsync("CreateBranch", $"Created branch: {branchName}", ct);

            // 3. Push changes
            var commitMessage = $"[{proposal.Category}] {proposal.Title}\n\n{proposal.Rationale}";
            var pushResult = await _client.PushChangesAsync(
                branchName,
                proposal.Changes,
                commitMessage,
                ct);

            if (pushResult.IsFailure)
            {
                await LogWorkflowStepAsync("PushChanges", $"Failed: {pushResult.Error}", ct);
                return Result<SelfModificationResult, string>.Failure(
                    $"Failed to push changes: {pushResult.Error}");
            }

            await LogWorkflowStepAsync("PushChanges", $"Pushed changes: {pushResult.Value.Sha}", ct);

            // 4. Create pull request
            var prTitle = $"[{proposal.Category}] {proposal.Title}";
            var prDescription = BuildPullRequestDescription(proposal);
            var prResult = await _client.CreatePullRequestAsync(
                prTitle,
                prDescription,
                branchName,
                ct: ct);

            if (prResult.IsFailure)
            {
                await LogWorkflowStepAsync("CreatePullRequest", $"Failed: {prResult.Error}", ct);
                return Result<SelfModificationResult, string>.Failure(
                    $"Failed to create pull request: {prResult.Error}");
            }

            await LogWorkflowStepAsync(
                "CreatePullRequest",
                $"Created PR #{prResult.Value.Number}: {prResult.Value.Url}",
                ct);

            // 5. Success
            var result = new SelfModificationResult(
                Success: true,
                PullRequest: prResult.Value,
                BranchName: branchName,
                Error: null,
                EthicsClearance: EthicalClearance.Permitted(
                    "Self-modification workflow completed successfully",
                    Array.Empty<EthicalPrinciple>()));

            return Result<SelfModificationResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            await LogWorkflowStepAsync("Exception", $"Workflow failed: {ex.Message}", ct);
            return Result<SelfModificationResult, string>.Failure($"Workflow exception: {ex.Message}");
        }
    }

    private static bool ValidateProposal(SelfModificationProposal proposal, out string error)
    {
        if (string.IsNullOrWhiteSpace(proposal.Title))
        {
            error = "Proposal title is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(proposal.Description))
        {
            error = "Proposal description is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(proposal.Rationale))
        {
            error = "Proposal rationale is required";
            return false;
        }

        if (proposal.Changes == null || proposal.Changes.Count == 0)
        {
            error = "At least one file change is required";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string GenerateBranchName(ChangeCategory category)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var categoryName = category.ToString().ToLowerInvariant();
        return $"self-modify/{categoryName}/{timestamp}";
    }

    private static string BuildPullRequestDescription(SelfModificationProposal proposal)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Self-Modification Proposal");
        sb.AppendLine();
        sb.AppendLine($"**Category**: {proposal.Category}");
        sb.AppendLine();
        sb.AppendLine("### Description");
        sb.AppendLine(proposal.Description);
        sb.AppendLine();
        sb.AppendLine("### Rationale");
        sb.AppendLine(proposal.Rationale);
        sb.AppendLine();
        sb.AppendLine("### Changes");
        foreach (var change in proposal.Changes)
        {
            sb.AppendLine($"- `{change.Path}` ({change.ChangeType})");
        }

        if (proposal.RequestReview)
        {
            sb.AppendLine();
            sb.AppendLine("### Review Requested");
            sb.AppendLine("This is an autonomous self-modification proposal. Please review carefully before merging.");
        }

        return sb.ToString();
    }

    private async Task LogWorkflowStepAsync(string step, string message, CancellationToken ct)
    {
        var entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = _agentId,
            UserId = null,
            EvaluationType = "GitHubWorkflowStep",
            Description = $"{step}: {message}",
            Clearance = EthicalClearance.Permitted($"Workflow step: {step}"),
            Context = new Dictionary<string, object>
            {
                ["Step"] = step,
                ["Message"] = message
            }
        };

        await _auditLog.LogEvaluationAsync(entry, ct);
    }
}

/// <summary>
/// Represents a proposal for self-modification.
/// </summary>
public sealed record SelfModificationProposal
{
    /// <summary>
    /// Gets the title of the modification.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the description of what will be changed.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the rationale for the modification.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets the list of file changes.
    /// </summary>
    public required IReadOnlyList<FileChange> Changes { get; init; }

    /// <summary>
    /// Gets the category of change.
    /// </summary>
    public required ChangeCategory Category { get; init; }

    /// <summary>
    /// Gets a value indicating whether to request human review.
    /// </summary>
    public bool RequestReview { get; init; } = true;
}

/// <summary>
/// Result of a self-modification workflow execution.
/// </summary>
public sealed record SelfModificationResult(
    bool Success,
    PullRequestInfo? PullRequest,
    string? BranchName,
    string? Error,
    EthicalClearance EthicsClearance);
