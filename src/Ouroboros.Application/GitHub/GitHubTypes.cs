// <copyright file="GitHubTypes.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Represents a file change for GitHub operations.
/// </summary>
public sealed record FileChange
{
    /// <summary>
    /// Gets the file path relative to repository root.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the new content of the file.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the type of change being made.
    /// </summary>
    public required FileChangeType ChangeType { get; init; }
}

/// <summary>
/// Type of file change operation.
/// </summary>
public enum FileChangeType
{
    /// <summary>Create a new file</summary>
    Create,

    /// <summary>Update an existing file</summary>
    Update,

    /// <summary>Delete a file</summary>
    Delete
}

/// <summary>
/// Information about a pull request.
/// </summary>
public sealed record PullRequestInfo
{
    /// <summary>
    /// Gets the pull request number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the URL of the pull request.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the title of the pull request.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the state of the pull request (open, closed, merged).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the head branch name.
    /// </summary>
    public required string HeadBranch { get; init; }

    /// <summary>
    /// Gets the base branch name.
    /// </summary>
    public required string BaseBranch { get; init; }

    /// <summary>
    /// Gets when the pull request was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Information about a commit.
/// </summary>
public sealed record CommitInfo
{
    /// <summary>
    /// Gets the commit SHA.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// Gets the commit message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the URL of the commit.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets when the commit was made.
    /// </summary>
    public required DateTime CommittedAt { get; init; }
}

/// <summary>
/// Information about an issue.
/// </summary>
public sealed record IssueInfo
{
    /// <summary>
    /// Gets the issue number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the URL of the issue.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets the title of the issue.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the state of the issue (open or closed).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets when the issue was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Information about file content from GitHub.
/// </summary>
public sealed record FileContent
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the file content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public required int Size { get; init; }

    /// <summary>
    /// Gets the SHA of the file.
    /// </summary>
    public required string Sha { get; init; }
}

/// <summary>
/// Information about a file or directory in GitHub.
/// </summary>
public sealed record GitHubFileInfo
{
    /// <summary>
    /// Gets the name of the file or directory.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the path of the file or directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the type (file or dir).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the size in bytes (null for directories).
    /// </summary>
    public int? Size { get; init; }
}

/// <summary>
/// Information about a branch.
/// </summary>
public sealed record BranchInfo
{
    /// <summary>
    /// Gets the branch name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the commit SHA at the head of the branch.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// Gets whether the branch is protected.
    /// </summary>
    public required bool IsProtected { get; init; }
}

/// <summary>
/// Status information for a pull request.
/// </summary>
public sealed record PullRequestStatus
{
    /// <summary>
    /// Gets the pull request number.
    /// </summary>
    public required int Number { get; init; }

    /// <summary>
    /// Gets the state (open, closed, merged).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets whether the PR is mergeable.
    /// </summary>
    public bool? Mergeable { get; init; }

    /// <summary>
    /// Gets the number of additions.
    /// </summary>
    public required int Additions { get; init; }

    /// <summary>
    /// Gets the number of deletions.
    /// </summary>
    public required int Deletions { get; init; }

    /// <summary>
    /// Gets the number of changed files.
    /// </summary>
    public required int ChangedFiles { get; init; }
}

/// <summary>
/// Result of a code search operation.
/// </summary>
public sealed record CodeSearchResult
{
    /// <summary>
    /// Gets the file path where the code was found.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the filename.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Gets the matched content snippet.
    /// </summary>
    public required string MatchedContent { get; init; }

    /// <summary>
    /// Gets the line number where the match was found.
    /// </summary>
    public int? LineNumber { get; init; }
}
