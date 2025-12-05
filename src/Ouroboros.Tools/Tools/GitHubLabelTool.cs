// <copyright file="GitHubLabelTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using Octokit;

/// <summary>
/// Tool for managing labels on GitHub issues and pull requests.
/// </summary>
public sealed class GitHubLabelTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubLabelTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token with repo permissions.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public GitHubLabelTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_manage_labels";

    /// <inheritdoc />
    public string Description => "Add or remove labels from a GitHub issue or pull request. Supports multiple operations in one call.";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubLabelArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubLabelArgs args = ToolJson.Deserialize<GitHubLabelArgs>(input);

            List<string> results = new List<string>();

            // Add labels if specified
            if (args.AddLabels != null && args.AddLabels.Length > 0)
            {
                foreach (string label in args.AddLabels)
                {
                    // Ensure label exists in repository
                    await this.EnsureLabelExistsAsync(label);
                }

                await this.client.Issue.Labels.AddToIssue(
                    this.owner,
                    this.repo,
                    args.IssueNumber,
                    args.AddLabels);
                
                results.Add($"Added labels: {string.Join(", ", args.AddLabels)}");
            }

            // Remove labels if specified
            if (args.RemoveLabels != null && args.RemoveLabels.Length > 0)
            {
                foreach (string label in args.RemoveLabels)
                {
                    try
                    {
                        await this.client.Issue.Labels.RemoveFromIssue(
                            this.owner,
                            this.repo,
                            args.IssueNumber,
                            label);
                    }
                    catch (NotFoundException)
                    {
                        // Label wasn't on the issue, continue
                    }
                }
                
                results.Add($"Removed labels: {string.Join(", ", args.RemoveLabels)}");
            }

            if (results.Count == 0)
            {
                return Result<string, string>.Failure("No label operations specified");
            }

            return Result<string, string>.Success(
                $"✅ Labels updated for issue #{args.IssueNumber}\n{string.Join("\n", results)}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to manage labels: {ex.Message}");
        }
    }

    private async Task EnsureLabelExistsAsync(string labelName)
    {
        try
        {
            await this.client.Issue.Labels.Get(this.owner, this.repo, labelName);
        }
        catch (NotFoundException)
        {
            // Create the label with a default color if it doesn't exist
            NewLabel newLabel = new NewLabel(labelName, "EDEDED")
            {
                Description = $"Label created automatically",
            };
            await this.client.Issue.Labels.Create(this.owner, this.repo, newLabel);
        }
    }
}

/// <summary>
/// Arguments for managing GitHub labels.
/// </summary>
public sealed class GitHubLabelArgs
{
    /// <summary>
    /// Gets or sets the issue or pull request number.
    /// </summary>
    public int IssueNumber { get; set; }

    /// <summary>
    /// Gets or sets the labels to add.
    /// </summary>
    public string[]? AddLabels { get; set; }

    /// <summary>
    /// Gets or sets the labels to remove.
    /// </summary>
    public string[]? RemoveLabels { get; set; }
}
