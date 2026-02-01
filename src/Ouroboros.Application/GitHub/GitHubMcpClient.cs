// <copyright file="GitHubMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ouroboros.Core.Monads;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Implementation of GitHub MCP client using GitHub REST API.
/// </summary>
public sealed class GitHubMcpClient : IGitHubMcpClient
{
    private readonly GitHubMcpClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubMcpClient"/> class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <param name="httpClient">Optional HTTP client (will create one if not provided).</param>
    public GitHubMcpClient(GitHubMcpClientOptions options, HttpClient? httpClient = null)
    {
        if (!options.IsValid())
        {
            throw new ArgumentException("Invalid GitHubMcpClientOptions", nameof(options));
        }

        _options = options;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ouroboros", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.Timeout = _options.Timeout;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
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
        try
        {
            var payload = new
            {
                title,
                body = description,
                head = sourceBranch,
                @base = targetBranch
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/pulls",
                content,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<PullRequestInfo, string>.Failure(
                    $"Failed to create pull request: {response.StatusCode} - {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var prInfo = new PullRequestInfo
            {
                Number = root.GetProperty("number").GetInt32(),
                Url = root.GetProperty("html_url").GetString() ?? string.Empty,
                Title = root.GetProperty("title").GetString() ?? string.Empty,
                State = root.GetProperty("state").GetString() ?? string.Empty,
                HeadBranch = root.GetProperty("head").GetProperty("ref").GetString() ?? string.Empty,
                BaseBranch = root.GetProperty("base").GetProperty("ref").GetString() ?? string.Empty,
                CreatedAt = root.GetProperty("created_at").GetDateTime()
            };

            return Result<PullRequestInfo, string>.Success(prInfo);
        }
        catch (Exception ex)
        {
            return Result<PullRequestInfo, string>.Failure($"Exception creating pull request: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<CommitInfo, string>> PushChangesAsync(
        string branchName,
        IReadOnlyList<FileChange> changes,
        string commitMessage,
        CancellationToken ct = default)
    {
        try
        {
            // Get the current branch reference
            var refResponse = await _httpClient.GetAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/ref/heads/{branchName}",
                ct);

            if (!refResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to get branch reference: {refResponse.StatusCode}");
            }

            var refBody = await refResponse.Content.ReadAsStringAsync(ct);
            using var refDoc = JsonDocument.Parse(refBody);
            var currentSha = refDoc.RootElement.GetProperty("object").GetProperty("sha").GetString();

            // Create a tree with the file changes
            var treeItems = new List<object>();
            foreach (var change in changes)
            {
                treeItems.Add(new
                {
                    path = change.Path,
                    mode = "100644",
                    type = "blob",
                    content = change.Content
                });
            }

            var treePayload = new
            {
                base_tree = currentSha,
                tree = treeItems
            };

            var treeContent = new StringContent(
                JsonSerializer.Serialize(treePayload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var treeResponse = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/trees",
                treeContent,
                ct);

            if (!treeResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to create tree: {treeResponse.StatusCode}");
            }

            var treeBody = await treeResponse.Content.ReadAsStringAsync(ct);
            using var treeDoc = JsonDocument.Parse(treeBody);
            var treeSha = treeDoc.RootElement.GetProperty("sha").GetString();

            // Create commit
            var commitPayload = new
            {
                message = commitMessage,
                tree = treeSha,
                parents = new[] { currentSha }
            };

            var commitContent = new StringContent(
                JsonSerializer.Serialize(commitPayload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var commitResponse = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/commits",
                commitContent,
                ct);

            if (!commitResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to create commit: {commitResponse.StatusCode}");
            }

            var commitBody = await commitResponse.Content.ReadAsStringAsync(ct);
            using var commitDoc = JsonDocument.Parse(commitBody);
            var newCommitSha = commitDoc.RootElement.GetProperty("sha").GetString();

            // Update branch reference
            var updatePayload = new
            {
                sha = newCommitSha,
                force = false
            };

            var updateContent = new StringContent(
                JsonSerializer.Serialize(updatePayload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var updateResponse = await _httpClient.PatchAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/refs/heads/{branchName}",
                updateContent,
                ct);

            if (!updateResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to update branch: {updateResponse.StatusCode}");
            }

            var commitInfo = new CommitInfo
            {
                Sha = newCommitSha ?? string.Empty,
                Message = commitMessage,
                Url = commitDoc.RootElement.GetProperty("html_url").GetString() ?? string.Empty,
                CommittedAt = commitDoc.RootElement.GetProperty("committer").GetProperty("date").GetDateTime()
            };

            return Result<CommitInfo, string>.Success(commitInfo);
        }
        catch (Exception ex)
        {
            return Result<CommitInfo, string>.Failure($"Exception pushing changes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IssueInfo, string>> CreateIssueAsync(
        string title,
        string description,
        IReadOnlyList<string>? labels = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                title,
                body = description,
                labels = labels ?? Array.Empty<string>()
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/issues",
                content,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<IssueInfo, string>.Failure(
                    $"Failed to create issue: {response.StatusCode} - {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var issueInfo = new IssueInfo
            {
                Number = root.GetProperty("number").GetInt32(),
                Url = root.GetProperty("html_url").GetString() ?? string.Empty,
                Title = root.GetProperty("title").GetString() ?? string.Empty,
                State = root.GetProperty("state").GetString() ?? string.Empty,
                CreatedAt = root.GetProperty("created_at").GetDateTime()
            };

            return Result<IssueInfo, string>.Success(issueInfo);
        }
        catch (Exception ex)
        {
            return Result<IssueInfo, string>.Failure($"Exception creating issue: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<FileContent, string>> ReadFileAsync(
        string path,
        string? branch = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/repos/{_options.Owner}/{_options.Repository}/contents/{path}";
            if (!string.IsNullOrWhiteSpace(branch))
            {
                url += $"?ref={branch}";
            }

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<FileContent, string>.Failure(
                    $"Failed to read file: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var encoding = root.GetProperty("encoding").GetString();
            var contentEncoded = root.GetProperty("content").GetString() ?? string.Empty;

            string decodedContent;
            if (encoding == "base64")
            {
                var bytes = Convert.FromBase64String(contentEncoded.Replace("\n", string.Empty));
                decodedContent = Encoding.UTF8.GetString(bytes);
            }
            else
            {
                decodedContent = contentEncoded;
            }

            var fileContent = new FileContent
            {
                Path = root.GetProperty("path").GetString() ?? string.Empty,
                Content = decodedContent,
                Size = root.GetProperty("size").GetInt32(),
                Sha = root.GetProperty("sha").GetString() ?? string.Empty
            };

            return Result<FileContent, string>.Success(fileContent);
        }
        catch (Exception ex)
        {
            return Result<FileContent, string>.Failure($"Exception reading file: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<GitHubFileInfo>, string>> ListFilesAsync(
        string path = "",
        string? branch = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/repos/{_options.Owner}/{_options.Repository}/contents/{path}";
            if (!string.IsNullOrWhiteSpace(branch))
            {
                url += $"?ref={branch}";
            }

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<GitHubFileInfo>, string>.Failure(
                    $"Failed to list files: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);

            var fileInfos = new List<GitHubFileInfo>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                fileInfos.Add(new GitHubFileInfo
                {
                    Name = item.GetProperty("name").GetString() ?? string.Empty,
                    Path = item.GetProperty("path").GetString() ?? string.Empty,
                    Type = item.GetProperty("type").GetString() ?? string.Empty,
                    Size = item.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt32() : (int?)null
                });
            }

            return Result<IReadOnlyList<GitHubFileInfo>, string>.Success(fileInfos);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<GitHubFileInfo>, string>.Failure($"Exception listing files: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<BranchInfo, string>> CreateBranchAsync(
        string branchName,
        string baseBranch = "main",
        CancellationToken ct = default)
    {
        try
        {
            // Get the base branch SHA
            var baseRefResponse = await _httpClient.GetAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/ref/heads/{baseBranch}",
                ct);

            if (!baseRefResponse.IsSuccessStatusCode)
            {
                return Result<BranchInfo, string>.Failure(
                    $"Failed to get base branch: {baseRefResponse.StatusCode}");
            }

            var baseRefBody = await baseRefResponse.Content.ReadAsStringAsync(ct);
            using var baseDoc = JsonDocument.Parse(baseRefBody);
            var baseSha = baseDoc.RootElement.GetProperty("object").GetProperty("sha").GetString();

            // Create the new branch
            var payload = new
            {
                @ref = $"refs/heads/{branchName}",
                sha = baseSha
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/refs",
                content,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<BranchInfo, string>.Failure(
                    $"Failed to create branch: {response.StatusCode} - {error}");
            }

            var branchInfo = new BranchInfo
            {
                Name = branchName,
                Sha = baseSha ?? string.Empty,
                IsProtected = false
            };

            return Result<BranchInfo, string>.Success(branchInfo);
        }
        catch (Exception ex)
        {
            return Result<BranchInfo, string>.Failure($"Exception creating branch: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<PullRequestStatus, string>> GetPullRequestStatusAsync(
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/pulls/{pullRequestNumber}",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<PullRequestStatus, string>.Failure(
                    $"Failed to get pull request status: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var status = new PullRequestStatus
            {
                Number = root.GetProperty("number").GetInt32(),
                State = root.GetProperty("state").GetString() ?? string.Empty,
                Mergeable = root.TryGetProperty("mergeable", out var mergeableElement)
                    ? mergeableElement.GetBoolean()
                    : (bool?)null,
                Additions = root.GetProperty("additions").GetInt32(),
                Deletions = root.GetProperty("deletions").GetInt32(),
                ChangedFiles = root.GetProperty("changed_files").GetInt32()
            };

            return Result<PullRequestStatus, string>.Success(status);
        }
        catch (Exception ex)
        {
            return Result<PullRequestStatus, string>.Failure($"Exception getting pull request status: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<CodeSearchResult>, string>> SearchCodeAsync(
        string query,
        string? path = null,
        CancellationToken ct = default)
    {
        try
        {
            var searchQuery = $"{query} repo:{_options.Owner}/{_options.Repository}";
            if (!string.IsNullOrWhiteSpace(path))
            {
                searchQuery += $" path:{path}";
            }

            var encodedQuery = Uri.EscapeDataString(searchQuery);
            var response = await _httpClient.GetAsync($"/search/code?q={encodedQuery}", ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<CodeSearchResult>, string>.Failure(
                    $"Failed to search code: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var items = doc.RootElement.GetProperty("items");

            var results = new List<CodeSearchResult>();
            foreach (var item in items.EnumerateArray())
            {
                results.Add(new CodeSearchResult
                {
                    Path = item.GetProperty("path").GetString() ?? string.Empty,
                    Filename = item.GetProperty("name").GetString() ?? string.Empty,
                    MatchedContent = item.TryGetProperty("text_matches", out var matches) && matches.GetArrayLength() > 0
                        ? matches[0].GetProperty("fragment").GetString() ?? string.Empty
                        : string.Empty,
                    LineNumber = null
                });
            }

            return Result<IReadOnlyList<CodeSearchResult>, string>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<CodeSearchResult>, string>.Failure($"Exception searching code: {ex.Message}");
        }
    }
}
