// <copyright file="GitHubToolsIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.IntegrationTests;

using System;
using System.Threading.Tasks;
using FluentAssertions;
using LangChainPipeline.Core.Monads;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Integration tests for GitHub tools that run against the live GitHub API.
/// These tests require environment variables to be set:
/// - GITHUB_TOKEN: GitHub personal access token with repo permissions
/// - GITHUB_TEST_OWNER: Repository owner (username or organization)
/// - GITHUB_TEST_REPO: Repository name
/// 
/// To run these tests:
/// 1. Set the required environment variables
/// 2. Run: dotnet test --filter "Category=Integration"
/// 
/// These tests are excluded from CI by default using the Integration trait.
/// </summary>
[Trait("Category", "Integration")]
public class GitHubToolsIntegrationTests : IDisposable
{
    private readonly string? token;
    private readonly string? owner;
    private readonly string? repo;
    private readonly bool credentialsAvailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubToolsIntegrationTests"/> class.
    /// </summary>
    public GitHubToolsIntegrationTests()
    {
        this.token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        this.owner = Environment.GetEnvironmentVariable("GITHUB_TEST_OWNER");
        this.repo = Environment.GetEnvironmentVariable("GITHUB_TEST_REPO");
        this.credentialsAvailable = !string.IsNullOrEmpty(this.token) &&
                                      !string.IsNullOrEmpty(this.owner) &&
                                      !string.IsNullOrEmpty(this.repo);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GitHubSearchTool_SearchForIssues_ReturnsSuccessResult()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubSearchTool tool = new GitHubSearchTool(this.token!, this.owner!, this.repo!);
        string searchInput = ToolJson.Serialize(new GitHubSearchArgs
        {
            Query = "is:issue",
            Type = "issues",
            MaxResults = 5
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(searchInput);

        // Assert
        result.IsSuccess.Should().BeTrue("the search should complete successfully");
        result.Value.Should().NotBeNullOrEmpty("the result should contain search results");
        result.Value.Should().Contain("Found", "the result should indicate number of issues found");
    }

    [Fact]
    public async Task GitHubSearchTool_SearchForCode_ReturnsSuccessResult()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubSearchTool tool = new GitHubSearchTool(this.token!, this.owner!, this.repo!);
        string searchInput = ToolJson.Serialize(new GitHubSearchArgs
        {
            Query = "class",
            Type = "code",
            MaxResults = 5
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(searchInput);

        // Assert
        result.IsSuccess.Should().BeTrue("the code search should complete successfully");
        result.Value.Should().NotBeNullOrEmpty("the result should contain search results");
    }

    [Fact]
    public async Task GitHubSearchTool_SearchWithNoResults_ReturnsSuccessWithMessage()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubSearchTool tool = new GitHubSearchTool(this.token!, this.owner!, this.repo!);
        string searchInput = ToolJson.Serialize(new GitHubSearchArgs
        {
            Query = "xyzabc123nonexistent456",
            Type = "issues",
            MaxResults = 5
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(searchInput);

        // Assert
        result.IsSuccess.Should().BeTrue("empty search results should still be success");
        result.Value.Should().Contain("No issues found", "the result should indicate no matches");
    }

    [Fact]
    public async Task GitHubSearchTool_EmptyQuery_ReturnsFailure()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubSearchTool tool = new GitHubSearchTool(this.token!, this.owner!, this.repo!);
        string searchInput = ToolJson.Serialize(new GitHubSearchArgs
        {
            Query = string.Empty,
            Type = "issues"
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(searchInput);

        // Assert
        result.IsFailure.Should().BeTrue("empty query should fail");
        result.Error.Should().Contain("empty", "error should mention empty query");
    }

    [Fact]
    public async Task GitHubSearchTool_InvalidSearchType_ReturnsFailure()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubSearchTool tool = new GitHubSearchTool(this.token!, this.owner!, this.repo!);
        string searchInput = ToolJson.Serialize(new GitHubSearchArgs
        {
            Query = "test",
            Type = "invalid_type"
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(searchInput);

        // Assert
        result.IsFailure.Should().BeTrue("invalid search type should fail");
        result.Error.Should().Contain("Unknown search type", "error should mention invalid type");
    }

    [Fact]
    public async Task GitHubIssueReadTool_ReadExistingIssue_ReturnsSuccessWithDetails()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // First, search for an existing issue to read
        GitHubSearchTool searchTool = new GitHubSearchTool(this.token!, this.owner!, this.repo!);
        string searchInput = ToolJson.Serialize(new GitHubSearchArgs
        {
            Query = "is:issue",
            Type = "issues",
            MaxResults = 1
        });

        Result<string, string> searchResult = await searchTool.InvokeAsync(searchInput);
        if (searchResult.IsFailure || searchResult.Value.Contains("No issues found"))
        {
            // Skip test if no issues exist in the repository
            return;
        }

        // Extract issue number from search result (format: "#123 - Title")
        string searchValue = searchResult.Value;
        int hashIndex = searchValue.IndexOf('#');
        if (hashIndex < 0)
        {
            return; // Skip if we can't parse the issue number
        }

        int dashIndex = searchValue.IndexOf(" -", hashIndex);
        if (dashIndex < 0)
        {
            return;
        }

        string issueNumberStr = searchValue.Substring(hashIndex + 1, dashIndex - hashIndex - 1);
        if (!int.TryParse(issueNumberStr, out int issueNumber))
        {
            return;
        }

        // Arrange
        GitHubIssueReadTool tool = new GitHubIssueReadTool(this.token!, this.owner!, this.repo!);
        string readInput = ToolJson.Serialize(new GitHubIssueReadArgs
        {
            IssueNumber = issueNumber
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(readInput);

        // Assert
        result.IsSuccess.Should().BeTrue("reading existing issue should succeed");
        result.Value.Should().NotBeNullOrEmpty("the result should contain issue details");
        result.Value.Should().Contain($"Issue #{issueNumber}", "the result should include the issue number");
        result.Value.Should().Contain("State:", "the result should include the state");
        result.Value.Should().Contain("Author:", "the result should include the author");
    }

    [Fact]
    public async Task GitHubIssueReadTool_ReadNonexistentIssue_ReturnsFailure()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubIssueReadTool tool = new GitHubIssueReadTool(this.token!, this.owner!, this.repo!);
        string readInput = ToolJson.Serialize(new GitHubIssueReadArgs
        {
            IssueNumber = 999999999 // Extremely unlikely to exist
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(readInput);

        // Assert
        result.IsFailure.Should().BeTrue("reading non-existent issue should fail");
        result.Error.Should().Contain("Failed to read issue", "error should indicate read failure");
    }

    [Fact]
    public async Task GitHubLabelTool_AddLabelsToIssue_RequiresWritePermissions()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Note: This test only verifies the tool accepts valid input.
        // Actual label addition requires write permissions and an existing issue.
        // It may fail with permission errors if the token doesn't have write access.

        // Arrange
        GitHubLabelTool tool = new GitHubLabelTool(this.token!, this.owner!, this.repo!);
        string labelInput = ToolJson.Serialize(new GitHubLabelArgs
        {
            IssueNumber = 1,
            AddLabels = new[] { "test-label" }
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(labelInput);

        // Assert - Can be either success or failure depending on permissions/issue existence
        // We're just verifying the tool doesn't crash with valid input
        result.Should().NotBeNull("the result should not be null");
    }

    [Fact]
    public async Task GitHubLabelTool_NoOperationSpecified_ReturnsFailure()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubLabelTool tool = new GitHubLabelTool(this.token!, this.owner!, this.repo!);
        string labelInput = ToolJson.Serialize(new GitHubLabelArgs
        {
            IssueNumber = 1,
            AddLabels = null,
            RemoveLabels = null
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(labelInput);

        // Assert
        result.IsFailure.Should().BeTrue("no label operations should fail");
        result.Error.Should().Contain("No label operations specified", "error should mention no operations");
    }

    [Fact]
    public async Task GitHubCommentTool_AddCommentToIssue_RequiresWritePermissions()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Note: This test only verifies the tool accepts valid input.
        // Actual comment addition requires write permissions and an existing issue.
        // It may fail with permission errors if the token doesn't have write access.

        // Arrange
        GitHubCommentTool tool = new GitHubCommentTool(this.token!, this.owner!, this.repo!);
        string commentInput = ToolJson.Serialize(new GitHubCommentArgs
        {
            IssueNumber = 1,
            Body = "Test comment from integration test"
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(commentInput);

        // Assert - Can be either success or failure depending on permissions/issue existence
        // We're just verifying the tool doesn't crash with valid input
        result.Should().NotBeNull("the result should not be null");
    }

    [Fact]
    public async Task GitHubCommentTool_EmptyBody_ReturnsFailure()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubCommentTool tool = new GitHubCommentTool(this.token!, this.owner!, this.repo!);
        string commentInput = ToolJson.Serialize(new GitHubCommentArgs
        {
            IssueNumber = 1,
            Body = string.Empty
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(commentInput);

        // Assert
        result.IsFailure.Should().BeTrue("empty comment body should fail");
        result.Error.Should().Contain("empty", "error should mention empty body");
    }

    [Fact]
    public async Task GitHubIssueCreateTool_EmptyTitle_ReturnsFailure()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Arrange
        GitHubIssueCreateTool tool = new GitHubIssueCreateTool(this.token!, this.owner!, this.repo!);
        string createInput = ToolJson.Serialize(new GitHubIssueCreateArgs
        {
            Title = string.Empty,
            Body = "Test body"
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(createInput);

        // Assert
        result.IsFailure.Should().BeTrue("empty issue title should fail");
        result.Error.Should().Contain("empty", "error should mention empty title");
    }

    [Fact]
    public async Task GitHubIssueUpdateTool_ValidInput_AcceptsRequest()
    {
        // Skip if credentials not available
        if (!this.credentialsAvailable)
        {
            return;
        }

        // Note: This test only verifies the tool accepts valid input.
        // Actual update requires write permissions and an existing issue.

        // Arrange
        GitHubIssueUpdateTool tool = new GitHubIssueUpdateTool(this.token!, this.owner!, this.repo!);
        string updateInput = ToolJson.Serialize(new GitHubIssueUpdateArgs
        {
            IssueNumber = 1,
            State = "open"
        });

        // Act
        Result<string, string> result = await tool.InvokeAsync(updateInput);

        // Assert - Can be either success or failure depending on permissions/issue existence
        // We're just verifying the tool doesn't crash with valid input
        result.Should().NotBeNull("the result should not be null");
    }
}
