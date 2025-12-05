// <copyright file="GitHubIssueTools.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using Octokit;

/// <summary>
/// Tool for creating new GitHub issues.
/// </summary>
public sealed class GitHubIssueCreateTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubIssueCreateTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token with repo permissions.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public GitHubIssueCreateTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_create_issue";

    /// <inheritdoc />
    public string Description => "Create a new GitHub issue with title, body, labels, and optional assignees. Returns the created issue URL.";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubIssueCreateArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubIssueCreateArgs args = ToolJson.Deserialize<GitHubIssueCreateArgs>(input);

            if (string.IsNullOrWhiteSpace(args.Title))
            {
                return Result<string, string>.Failure("Issue title cannot be empty");
            }

            NewIssue newIssue = new NewIssue(args.Title)
            {
                Body = args.Body ?? string.Empty,
            };

            // Add labels if provided
            if (args.Labels != null)
            {
                foreach (string label in args.Labels)
                {
                    newIssue.Labels.Add(label);
                }
            }

            // Add assignees if provided
            if (args.Assignees != null)
            {
                foreach (string assignee in args.Assignees)
                {
                    newIssue.Assignees.Add(assignee);
                }
            }

            Issue issue = await this.client.Issue.Create(this.owner, this.repo, newIssue);

            return Result<string, string>.Success(
                $"✅ Issue #{issue.Number} created successfully\n" +
                $"Title: {issue.Title}\n" +
                $"URL: {issue.HtmlUrl}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to create issue: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for creating a GitHub issue.
/// </summary>
public sealed class GitHubIssueCreateArgs
{
    /// <summary>
    /// Gets or sets the issue title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issue body/description.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the labels to apply to the issue.
    /// </summary>
    public string[]? Labels { get; set; }

    /// <summary>
    /// Gets or sets the usernames to assign to the issue.
    /// </summary>
    public string[]? Assignees { get; set; }
}

/// <summary>
/// Tool for reading GitHub issue details.
/// </summary>
public sealed class GitHubIssueReadTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubIssueReadTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token.</param>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    public GitHubIssueReadTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_read_issue";

    /// <inheritdoc />
    public string Description => "Read detailed information about a specific GitHub issue including title, body, state, labels, and comments.";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubIssueReadArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubIssueReadArgs args = ToolJson.Deserialize<GitHubIssueReadArgs>(input);

            Issue issue = await this.client.Issue.Get(this.owner, this.repo, args.IssueNumber);

            string result = $"Issue #{issue.Number}: {issue.Title}\n" +
                          $"State: {issue.State}\n" +
                          $"Author: {issue.User.Login}\n" +
                          $"Created: {issue.CreatedAt:yyyy-MM-dd HH:mm}\n" +
                          $"Updated: {issue.UpdatedAt:yyyy-MM-dd HH:mm}\n" +
                          $"Labels: {string.Join(", ", issue.Labels.Select(l => l.Name))}\n" +
                          $"Assignees: {string.Join(", ", issue.Assignees.Select(a => a.Login))}\n" +
                          $"Comments: {issue.Comments}\n" +
                          $"\nBody:\n{issue.Body ?? "(no body)"}\n" +
                          $"\nURL: {issue.HtmlUrl}";

            return Result<string, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to read issue: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for reading a GitHub issue.
/// </summary>
public sealed class GitHubIssueReadArgs
{
    /// <summary>
    /// Gets or sets the issue number.
    /// </summary>
    public int IssueNumber { get; set; }
}

/// <summary>
/// Tool for updating GitHub issues (state, labels, assignees).
/// </summary>
public sealed class GitHubIssueUpdateTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubIssueUpdateTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token.</param>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    public GitHubIssueUpdateTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_update_issue";

    /// <inheritdoc />
    public string Description => "Update a GitHub issue's state (open/closed), title, body, labels, or assignees.";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubIssueUpdateArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubIssueUpdateArgs args = ToolJson.Deserialize<GitHubIssueUpdateArgs>(input);

            IssueUpdate update = new IssueUpdate();

            if (args.State != null)
            {
                update.State = args.State.ToLower() == "closed" ? ItemState.Closed : ItemState.Open;
            }

            if (args.Title != null)
            {
                update.Title = args.Title;
            }

            if (args.Body != null)
            {
                update.Body = args.Body;
            }

            if (args.Labels != null)
            {
                update.ClearLabels();
                foreach (string label in args.Labels)
                {
                    update.AddLabel(label);
                }
            }

            if (args.Assignees != null)
            {
                update.ClearAssignees();
                foreach (string assignee in args.Assignees)
                {
                    update.AddAssignee(assignee);
                }
            }

            Issue updatedIssue = await this.client.Issue.Update(this.owner, this.repo, args.IssueNumber, update);

            return Result<string, string>.Success(
                $"✅ Issue #{updatedIssue.Number} updated successfully\n" +
                $"Title: {updatedIssue.Title}\n" +
                $"State: {updatedIssue.State}\n" +
                $"URL: {updatedIssue.HtmlUrl}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to update issue: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for updating a GitHub issue.
/// </summary>
public sealed class GitHubIssueUpdateArgs
{
    /// <summary>
    /// Gets or sets the issue number.
    /// </summary>
    public int IssueNumber { get; set; }

    /// <summary>
    /// Gets or sets the new state (open or closed).
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the new title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the new body.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the labels (replaces all existing labels).
    /// </summary>
    public string[]? Labels { get; set; }

    /// <summary>
    /// Gets or sets the assignees (replaces all existing assignees).
    /// </summary>
    public string[]? Assignees { get; set; }
}
