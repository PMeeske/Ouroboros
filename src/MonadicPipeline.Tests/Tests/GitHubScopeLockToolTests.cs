using FluentAssertions;
using LangChainPipeline.Tools;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the GitHubScopeLockTool implementation.
/// Note: These tests validate the tool structure and basic behavior.
/// Integration tests with actual GitHub API require credentials and are tested separately.
/// </summary>
public class GitHubScopeLockToolTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var tool = new GitHubScopeLockTool("test-token", "test-owner", "test-repo");

        // Assert
        tool.Should().NotBeNull();
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        // Arrange
        var tool = new GitHubScopeLockTool("test-token", "test-owner", "test-repo");

        // Act
        var name = tool.Name;

        // Assert
        name.Should().Be("github_scope_lock");
    }

    [Fact]
    public void Description_ContainsRelevantKeywords()
    {
        // Arrange
        var tool = new GitHubScopeLockTool("test-token", "test-owner", "test-repo");

        // Act
        var description = tool.Description;

        // Assert
        description.Should().NotBeNullOrWhiteSpace();
        description.Should().Contain("scope");
        description.Should().Contain("lock");
        description.Should().Contain("scope-locked");
    }

    [Fact]
    public void JsonSchema_ReturnsValidSchema()
    {
        // Arrange
        var tool = new GitHubScopeLockTool("test-token", "test-owner", "test-repo");

        // Act
        var schema = tool.JsonSchema;

        // Assert
        schema.Should().NotBeNullOrWhiteSpace();
        schema.Should().Contain("IssueNumber");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        var tool = new GitHubScopeLockTool("test-token", "test-owner", "test-repo");

        // Act
        var result = await tool.InvokeAsync("invalid-json");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyJson_ReturnsFailure()
    {
        // Arrange
        var tool = new GitHubScopeLockTool("test-token", "test-owner", "test-repo");

        // Act
        var result = await tool.InvokeAsync("{}");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GitHubScopeLockArgs_CanBeInstantiated()
    {
        // Arrange & Act
        var args = new GitHubScopeLockArgs
        {
            IssueNumber = 138,
            Milestone = "v1.0"
        };

        // Assert
        args.IssueNumber.Should().Be(138);
        args.Milestone.Should().Be("v1.0");
    }

    [Fact]
    public void GitHubScopeLockArgs_MilestoneIsOptional()
    {
        // Arrange & Act
        var args = new GitHubScopeLockArgs
        {
            IssueNumber = 138
        };

        // Assert
        args.IssueNumber.Should().Be(138);
        args.Milestone.Should().BeNull();
    }
}
