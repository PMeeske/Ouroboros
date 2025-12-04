// <copyright file="GitHubScopeLockTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

using Octokit;

/// <summary>
/// A tool for locking GitHub issue scope by adding a scope-locked label and confirmation comment.
/// This prevents uncontrolled scope creep by formally locking the requirements.
/// </summary>
public sealed class GitHubScopeLockTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubScopeLockTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token with repo permissions.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public GitHubScopeLockTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_scope_lock";

    /// <inheritdoc />
    public string Description => "Locks the scope of a GitHub issue by adding 'scope-locked' label and confirmation comment. Args: { issueNumber: number, milestone?: string }";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubScopeLockArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubScopeLockArgs args = ToolJson.Deserialize<GitHubScopeLockArgs>(input);

            // Step 1: Add scope-locked label to issue
            Result<bool, string> labelResult = await this.AddScopeLockedLabelAsync(args.IssueNumber, ct);
            if (!labelResult.IsSuccess)
            {
                return Result<string, string>.Failure($"Failed to add label: {labelResult.Error}");
            }

            // Step 2: Add confirmation comment
            Result<bool, string> commentResult = await this.AddConfirmationCommentAsync(args.IssueNumber, args.Milestone, ct);
            if (!commentResult.IsSuccess)
            {
                return Result<string, string>.Failure($"Failed to add comment: {commentResult.Error}");
            }

            // Step 3: Update milestone if provided
            if (!string.IsNullOrEmpty(args.Milestone))
            {
                Result<bool, string> milestoneResult = await this.UpdateMilestoneAsync(args.IssueNumber, args.Milestone, ct);
                if (!milestoneResult.IsSuccess)
                {
                    return Result<string, string>.Failure($"Failed to update milestone: {milestoneResult.Error}");
                }
            }

            string resultMessage = $"✅ Scope locked for issue #{args.IssueNumber}\n" +
                                 $"  - Label 'scope-locked' added\n" +
                                 $"  - Confirmation comment posted\n" +
                                 (string.IsNullOrEmpty(args.Milestone) ? string.Empty : $"  - Milestone updated to: {args.Milestone}\n");

            return Result<string, string>.Success(resultMessage);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Scope lock failed: {ex.Message}");
        }
    }

    private async Task<Result<bool, string>> AddScopeLockedLabelAsync(int issueNumber, CancellationToken ct)
    {
        try
        {
            // First, ensure the label exists in the repository
            try
            {
                await this.client.Issue.Labels.Get(this.owner, this.repo, "scope-locked");
            }
            catch (NotFoundException)
            {
                // Create the label if it doesn't exist
                NewLabel newLabel = new NewLabel("scope-locked", "D4C5F9")
                {
                    Description = "Scope is locked to prevent uncontrolled scope creep",
                };
                await this.client.Issue.Labels.Create(this.owner, this.repo, newLabel);
            }

            // Add the label to the issue
            IssueUpdate issueUpdate = new IssueUpdate();
            await this.client.Issue.Labels.AddToIssue(this.owner, this.repo, issueNumber, new[] { "scope-locked" });

            return Result<bool, string>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Failed to add scope-locked label: {ex.Message}");
        }
    }

    private async Task<Result<bool, string>> AddConfirmationCommentAsync(int issueNumber, string? milestone, CancellationToken ct)
    {
        try
        {
            string commentBody = "🔒 **Scope Locked**\n\n" +
                               "The scope of this issue has been formally locked to prevent uncontrolled scope creep.\n\n" +
                               "**What this means:**\n" +
                               "- No new requirements can be added without explicit approval\n" +
                               "- Changes to existing requirements require scope change review\n" +
                               "- This ensures predictable delivery timelines\n\n" +
                               (string.IsNullOrEmpty(milestone)
                                   ? string.Empty
                                   : $"**Milestone:** {milestone}\n\n") +
                               "To request scope changes, please open a new issue and reference this locked scope.";

            await this.client.Issue.Comment.Create(this.owner, this.repo, issueNumber, commentBody);

            return Result<bool, string>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Failed to add confirmation comment: {ex.Message}");
        }
    }

    private async Task<Result<bool, string>> UpdateMilestoneAsync(int issueNumber, string milestoneName, CancellationToken ct)
    {
        try
        {
            // Find the milestone by name
            IReadOnlyList<Milestone> milestones = await this.client.Issue.Milestone.GetAllForRepository(this.owner, this.repo);
            Milestone? milestone = milestones.FirstOrDefault(m => m.Title.Equals(milestoneName, StringComparison.OrdinalIgnoreCase));

            if (milestone == null)
            {
                return Result<bool, string>.Failure($"Milestone '{milestoneName}' not found");
            }

            // Update the issue with the milestone
            IssueUpdate issueUpdate = new IssueUpdate
            {
                Milestone = milestone.Number,
            };

            await this.client.Issue.Update(this.owner, this.repo, issueNumber, issueUpdate);

            return Result<bool, string>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Failed to update milestone: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for the GitHubScopeLockTool.
/// </summary>
public sealed class GitHubScopeLockArgs
{
    /// <summary>
    /// Gets or sets the GitHub issue number to lock.
    /// </summary>
    public int IssueNumber { get; set; }

    /// <summary>
    /// Gets or sets the optional milestone name to assign to the issue.
    /// </summary>
    public string? Milestone { get; set; }
}
