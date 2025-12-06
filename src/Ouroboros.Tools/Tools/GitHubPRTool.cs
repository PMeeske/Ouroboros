// <copyright file="GitHubPRTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using Octokit;

/// <summary>
/// Tool for creating and managing GitHub pull requests.
/// </summary>
public sealed class GitHubPRTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubPRTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token with repo permissions.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public GitHubPRTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_create_pr";

    /// <inheritdoc />
    public string Description => "Create a new GitHub pull request from a branch. Returns the PR URL.";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubPRArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubPRArgs args = ToolJson.Deserialize<GitHubPRArgs>(input);

            if (string.IsNullOrWhiteSpace(args.Title))
            {
                return Result<string, string>.Failure("PR title cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(args.HeadBranch))
            {
                return Result<string, string>.Failure("Head branch cannot be empty");
            }

            string baseBranch = args.BaseBranch ?? "main";

            NewPullRequest newPr = new NewPullRequest(args.Title, args.HeadBranch, baseBranch)
            {
                Body = args.Body ?? string.Empty,
                Draft = args.Draft ?? false,
            };

            PullRequest pr = await this.client.PullRequest.Create(this.owner, this.repo, newPr);

            // Add labels if provided
            if (args.Labels != null && args.Labels.Length > 0)
            {
                await this.client.Issue.Labels.AddToIssue(
                    this.owner,
                    this.repo,
                    pr.Number,
                    args.Labels);
            }

            return Result<string, string>.Success(
                $"✅ Pull Request #{pr.Number} created successfully\n" +
                $"Title: {pr.Title}\n" +
                $"From: {pr.Head.Ref} → To: {pr.Base.Ref}\n" +
                $"State: {pr.State}\n" +
                $"Draft: {pr.Draft}\n" +
                $"URL: {pr.HtmlUrl}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to create PR: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for creating a GitHub pull request.
/// </summary>
public sealed class GitHubPRArgs
{
    /// <summary>
    /// Gets or sets the PR title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the head branch (source branch with changes).
    /// </summary>
    public string HeadBranch { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base branch (target branch, defaults to "main").
    /// </summary>
    public string? BaseBranch { get; set; }

    /// <summary>
    /// Gets or sets the PR body/description.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the PR should be created as a draft.
    /// </summary>
    public bool? Draft { get; set; }

    /// <summary>
    /// Gets or sets the labels to apply to the PR.
    /// </summary>
    public string[]? Labels { get; set; }
}
