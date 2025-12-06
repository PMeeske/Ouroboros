// <copyright file="GitHubCommentTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using Octokit;

/// <summary>
/// Tool for adding comments to GitHub issues and pull requests.
/// </summary>
public sealed class GitHubCommentTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubCommentTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token with repo permissions.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public GitHubCommentTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_add_comment";

    /// <inheritdoc />
    public string Description => "Add a comment to a GitHub issue or pull request. Supports markdown formatting.";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubCommentArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubCommentArgs args = ToolJson.Deserialize<GitHubCommentArgs>(input);

            if (string.IsNullOrWhiteSpace(args.Body))
            {
                return Result<string, string>.Failure("Comment body cannot be empty");
            }

            IssueComment comment = await this.client.Issue.Comment.Create(
                this.owner,
                this.repo,
                args.IssueNumber,
                args.Body);

            return Result<string, string>.Success(
                $"✅ Comment added successfully to issue #{args.IssueNumber}\n" +
                $"Comment ID: {comment.Id}\n" +
                $"URL: {comment.HtmlUrl}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to add comment: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for adding a comment to a GitHub issue/PR.
/// </summary>
public sealed class GitHubCommentArgs
{
    /// <summary>
    /// Gets or sets the issue or pull request number.
    /// </summary>
    public int IssueNumber { get; set; }

    /// <summary>
    /// Gets or sets the comment body (supports markdown).
    /// </summary>
    public string Body { get; set; } = string.Empty;
}
