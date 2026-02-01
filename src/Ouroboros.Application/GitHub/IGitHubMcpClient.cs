// <copyright file="IGitHubMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Interface for GitHub MCP (Model Context Protocol) client operations.
/// Provides methods for interacting with GitHub repositories remotely.
/// </summary>
public interface IGitHubMcpClient
{
    /// <summary>
    /// Creates a pull request with proposed code changes.
    /// </summary>
    /// <param name="title">The title of the pull request.</param>
    /// <param name="description">The description/body of the pull request.</param>
    /// <param name="sourceBranch">The source branch containing changes.</param>
    /// <param name="targetBranch">The target branch (default: "main").</param>
    /// <param name="files">Optional list of files to include in the PR.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing pull request information or error.</returns>
    Task<Result<PullRequestInfo, string>> CreatePullRequestAsync(
        string title,
        string description,
        string sourceBranch,
        string targetBranch = "main",
        IReadOnlyList<FileChange>? files = null,
        CancellationToken ct = default);

    /// <summary>
    /// Pushes changes to a branch.
    /// </summary>
    /// <param name="branchName">The branch to push changes to.</param>
    /// <param name="changes">The list of file changes to push.</param>
    /// <param name="commitMessage">The commit message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing commit information or error.</returns>
    Task<Result<CommitInfo, string>> PushChangesAsync(
        string branchName,
        IReadOnlyList<FileChange> changes,
        string commitMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Creates an issue in the repository.
    /// </summary>
    /// <param name="title">The title of the issue.</param>
    /// <param name="description">The description/body of the issue.</param>
    /// <param name="labels">Optional list of labels to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing issue information or error.</returns>
    Task<Result<IssueInfo, string>> CreateIssueAsync(
        string title,
        string description,
        IReadOnlyList<string>? labels = null,
        CancellationToken ct = default);

    /// <summary>
    /// Reads file content from the repository.
    /// </summary>
    /// <param name="path">The path to the file in the repository.</param>
    /// <param name="branch">The branch to read from (default: repository default branch).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing file content or error.</returns>
    Task<Result<FileContent, string>> ReadFileAsync(
        string path,
        string? branch = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists files in a directory.
    /// </summary>
    /// <param name="path">The path to the directory (default: root).</param>
    /// <param name="branch">The branch to read from (default: repository default branch).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of file information or error.</returns>
    Task<Result<IReadOnlyList<GitHubFileInfo>, string>> ListFilesAsync(
        string path = "",
        string? branch = null,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new branch for modifications.
    /// </summary>
    /// <param name="branchName">The name of the new branch.</param>
    /// <param name="baseBranch">The base branch to create from (default: "main").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing branch information or error.</returns>
    Task<Result<BranchInfo, string>> CreateBranchAsync(
        string branchName,
        string baseBranch = "main",
        CancellationToken ct = default);

    /// <summary>
    /// Gets the status of a pull request.
    /// </summary>
    /// <param name="pullRequestNumber">The pull request number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing pull request status or error.</returns>
    Task<Result<PullRequestStatus, string>> GetPullRequestStatusAsync(
        int pullRequestNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Searches code in the repository.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="path">Optional path filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of search results or error.</returns>
    Task<Result<IReadOnlyList<CodeSearchResult>, string>> SearchCodeAsync(
        string query,
        string? path = null,
        CancellationToken ct = default);
}
