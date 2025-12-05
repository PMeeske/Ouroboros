// <copyright file="GitHubSearchTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using Octokit;

/// <summary>
/// Tool for searching GitHub issues, pull requests, and code.
/// </summary>
public sealed class GitHubSearchTool : ITool
{
    private readonly GitHubClient client;
    private readonly string owner;
    private readonly string repo;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubSearchTool"/> class.
    /// </summary>
    /// <param name="token">GitHub personal access token.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public GitHubSearchTool(string token, string owner, string repo)
    {
        this.client = new GitHubClient(new ProductHeaderValue("Ouroboros"))
        {
            Credentials = new Credentials(token),
        };
        this.owner = owner;
        this.repo = repo;
    }

    /// <inheritdoc />
    public string Name => "github_search";

    /// <inheritdoc />
    public string Description => "Search GitHub issues, pull requests, or code within the repository. Returns top matching results.";

    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(GitHubSearchArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            GitHubSearchArgs args = ToolJson.Deserialize<GitHubSearchArgs>(input);

            if (string.IsNullOrWhiteSpace(args.Query))
            {
                return Result<string, string>.Failure("Search query cannot be empty");
            }

            string searchType = args.Type?.ToLower() ?? "issues";

            return searchType switch
            {
                "issues" => await this.SearchIssuesAsync(args.Query, args.MaxResults ?? 10),
                "code" => await this.SearchCodeAsync(args.Query, args.MaxResults ?? 10),
                _ => Result<string, string>.Failure($"Unknown search type: {searchType}. Valid types: issues, code")
            };
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Search failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> SearchIssuesAsync(string query, int maxResults)
    {
        try
        {
            // Add repository qualifier to search query
            string fullQuery = $"{query} repo:{this.owner}/{this.repo}";

            SearchIssuesRequest request = new SearchIssuesRequest(fullQuery)
            {
                PerPage = Math.Min(maxResults, 100),
            };

            SearchIssuesResult result = await this.client.Search.SearchIssues(request);

            if (result.TotalCount == 0)
            {
                return Result<string, string>.Success("No issues found matching your query.");
            }

            List<string> issueResults = new List<string>();
            foreach (Issue issue in result.Items.Take(maxResults))
            {
                string labels = string.Join(", ", issue.Labels.Select(l => l.Name));
                issueResults.Add(
                    $"#{issue.Number} - {issue.Title}\n" +
                    $"  State: {issue.State} | Labels: {labels}\n" +
                    $"  Author: {issue.User.Login} | Created: {issue.CreatedAt:yyyy-MM-dd}\n" +
                    $"  URL: {issue.HtmlUrl}\n");
            }

            return Result<string, string>.Success(
                $"Found {result.TotalCount} issues (showing top {Math.Min(maxResults, result.Items.Count)}):\n\n" +
                string.Join("\n", issueResults));
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Issue search failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> SearchCodeAsync(string query, int maxResults)
    {
        try
        {
            SearchCodeRequest request = new SearchCodeRequest(query, this.owner, this.repo)
            {
                PerPage = Math.Min(maxResults, 100),
            };

            SearchCodeResult result = await this.client.Search.SearchCode(request);

            if (result.TotalCount == 0)
            {
                return Result<string, string>.Success("No code found matching your query.");
            }

            List<string> codeResults = new List<string>();
            foreach (SearchCode code in result.Items.Take(maxResults))
            {
                codeResults.Add(
                    $"📄 {code.Path}\n" +
                    $"  Repository: {code.Repository.FullName}\n" +
                    $"  URL: {code.HtmlUrl}\n");
            }

            return Result<string, string>.Success(
                $"Found {result.TotalCount} code matches (showing top {Math.Min(maxResults, result.Items.Count)}):\n\n" +
                string.Join("\n", codeResults));
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Code search failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Arguments for GitHub search.
/// </summary>
public sealed class GitHubSearchArgs
{
    /// <summary>
    /// Gets or sets the search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the search type (issues, code). Defaults to issues.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results to return (default 10, max 100).
    /// </summary>
    public int? MaxResults { get; set; }
}
