// <copyright file="GitHubMcpClientTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.GitHub;
using Xunit;

namespace Ouroboros.Tests.Tests.GitHub;

/// <summary>
/// Tests for GitHubMcpClient operations.
/// </summary>
public sealed class GitHubMcpClientTests
{
    private readonly GitHubMcpClientOptions _options;

    public GitHubMcpClientTests()
    {
        _options = new GitHubMcpClientOptions
        {
            Owner = "test-owner",
            Repository = "test-repo",
            Token = "test-token",
            BaseUrl = "https://api.github.com",
            Timeout = TimeSpan.FromSeconds(30),
            MaxRetries = 3,
            RequireEthicsApproval = true
        };
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldSucceed()
    {
        // Act
        var client = new GitHubMcpClient(_options);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ShouldThrow()
    {
        // Arrange
        var invalidOptions = _options with { Owner = string.Empty };

        // Act & Assert
        var act = () => new GitHubMcpClient(invalidOptions);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Options_IsValid_WithValidData_ShouldReturnTrue()
    {
        // Act
        var isValid = _options.IsValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Options_IsValid_WithEmptyOwner_ShouldReturnFalse()
    {
        // Arrange
        var options = _options with { Owner = string.Empty };

        // Act
        var isValid = options.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Options_IsValid_WithEmptyRepository_ShouldReturnFalse()
    {
        // Arrange
        var options = _options with { Repository = string.Empty };

        // Act
        var isValid = options.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Options_IsValid_WithEmptyToken_ShouldReturnFalse()
    {
        // Arrange
        var options = _options with { Token = string.Empty };

        // Act
        var isValid = options.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Options_IsValid_WithNegativeTimeout_ShouldReturnFalse()
    {
        // Arrange
        var options = _options with { Timeout = TimeSpan.FromSeconds(-1) };

        // Act
        var isValid = options.IsValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void FileChange_ShouldBeImmutable()
    {
        // Arrange
        var change = new FileChange
        {
            Path = "test.cs",
            Content = "content",
            ChangeType = FileChangeType.Create
        };

        // Assert
        change.Path.Should().Be("test.cs");
        change.Content.Should().Be("content");
        change.ChangeType.Should().Be(FileChangeType.Create);
    }

    [Fact]
    public void PullRequestInfo_ShouldBeImmutable()
    {
        // Arrange
        var pr = new PullRequestInfo
        {
            Number = 1,
            Url = "https://github.com/test/test/pull/1",
            Title = "Test PR",
            State = "open",
            HeadBranch = "feature",
            BaseBranch = "main",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        pr.Number.Should().Be(1);
        pr.State.Should().Be("open");
        pr.HeadBranch.Should().Be("feature");
        pr.BaseBranch.Should().Be("main");
    }

    [Fact]
    public void CommitInfo_ShouldBeImmutable()
    {
        // Arrange
        var commit = new CommitInfo
        {
            Sha = "abc123",
            Message = "Test commit",
            Url = "https://github.com/test/test/commit/abc123",
            CommittedAt = DateTime.UtcNow
        };

        // Assert
        commit.Sha.Should().Be("abc123");
        commit.Message.Should().Be("Test commit");
    }

    [Fact]
    public void BranchInfo_ShouldBeImmutable()
    {
        // Arrange
        var branch = new BranchInfo
        {
            Name = "feature-branch",
            Sha = "abc123",
            IsProtected = false
        };

        // Assert
        branch.Name.Should().Be("feature-branch");
        branch.Sha.Should().Be("abc123");
        branch.IsProtected.Should().BeFalse();
    }

    [Fact]
    public void IssueInfo_ShouldBeImmutable()
    {
        // Arrange
        var issue = new IssueInfo
        {
            Number = 42,
            Url = "https://github.com/test/test/issues/42",
            Title = "Test Issue",
            State = "open",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        issue.Number.Should().Be(42);
        issue.Title.Should().Be("Test Issue");
        issue.State.Should().Be("open");
    }

    [Fact]
    public void FileContent_ShouldBeImmutable()
    {
        // Arrange
        var fileContent = new FileContent
        {
            Path = "src/Test.cs",
            Content = "public class Test {}",
            Size = 20,
            Sha = "def456"
        };

        // Assert
        fileContent.Path.Should().Be("src/Test.cs");
        fileContent.Content.Should().Be("public class Test {}");
        fileContent.Size.Should().Be(20);
    }
}
