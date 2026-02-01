// <copyright file="GitHubMcpClientOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Configuration options for the GitHub MCP client.
/// </summary>
public sealed record GitHubMcpClientOptions
{
    /// <summary>
    /// Gets the repository owner (username or organization).
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public required string Repository { get; init; }

    /// <summary>
    /// Gets the GitHub Personal Access Token or GitHub App token.
    /// This should be stored securely (environment variable or secure config).
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Gets the base URL for GitHub API (default: "https://api.github.com").
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.github.com";

    /// <summary>
    /// Gets the timeout for HTTP requests (default: 30 seconds).
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the maximum number of retry attempts for failed requests (default: 3).
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Gets a value indicating whether ethics approval is required for write operations.
    /// This cannot be set to false in production environments.
    /// </summary>
    public bool RequireEthicsApproval { get; init; } = true;

    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <returns>True if valid, otherwise false.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Owner)
            && !string.IsNullOrWhiteSpace(Repository)
            && !string.IsNullOrWhiteSpace(Token)
            && !string.IsNullOrWhiteSpace(BaseUrl)
            && Timeout > TimeSpan.Zero
            && MaxRetries >= 0;
    }
}
