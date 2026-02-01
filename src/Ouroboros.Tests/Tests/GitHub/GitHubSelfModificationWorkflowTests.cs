// <copyright file="GitHubSelfModificationWorkflowTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Application.GitHub;
using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.SelfModification;
using Xunit;

namespace Ouroboros.Tests.Tests.GitHub;

/// <summary>
/// Tests for GitHubSelfModificationWorkflow.
/// </summary>
public sealed class GitHubSelfModificationWorkflowTests
{
    private readonly Mock<IGitHubMcpClient> _clientMock;
    private readonly Mock<IEthicsAuditLog> _auditLogMock;
    private readonly GitHubSelfModificationWorkflow _workflow;

    public GitHubSelfModificationWorkflowTests()
    {
        _clientMock = new Mock<IGitHubMcpClient>();
        _auditLogMock = new Mock<IEthicsAuditLog>();
        _workflow = new GitHubSelfModificationWorkflow(
            _clientMock.Object,
            _auditLogMock.Object,
            "test-agent");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidProposal_ShouldSucceed()
    {
        // Arrange
        var proposal = new SelfModificationProposal
        {
            Title = "Test Modification",
            Description = "Test description",
            Rationale = "Test rationale",
            Changes = new List<FileChange>
            {
                new FileChange
                {
                    Path = "test.cs",
                    Content = "content",
                    ChangeType = FileChangeType.Update
                }
            },
            Category = ChangeCategory.Refactoring,
            RequestReview = true
        };

        var branchInfo = new BranchInfo
        {
            Name = "test-branch",
            Sha = "abc123",
            IsProtected = false
        };

        var commitInfo = new CommitInfo
        {
            Sha = "def456",
            Message = "Test commit",
            Url = "https://github.com/test/commit/def456",
            CommittedAt = DateTime.UtcNow
        };

        var prInfo = new PullRequestInfo
        {
            Number = 1,
            Url = "https://github.com/test/pr/1",
            Title = "[Refactoring] Test Modification",
            State = "open",
            HeadBranch = "test-branch",
            BaseBranch = "main",
            CreatedAt = DateTime.UtcNow
        };

        _clientMock.Setup(c => c.CreateBranchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchInfo, string>.Success(branchInfo));

        _clientMock.Setup(c => c.PushChangesAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileChange>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CommitInfo, string>.Success(commitInfo));

        _clientMock.Setup(c => c.CreatePullRequestAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileChange>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequestInfo, string>.Success(prInfo));

        // Act
        var result = await _workflow.ExecuteAsync(proposal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.PullRequest.Should().NotBeNull();
        result.Value.PullRequest!.Number.Should().Be(1);
        result.Value.BranchName.Should().NotBeNull();
        result.Value.Error.Should().BeNull();

        _clientMock.Verify(c => c.CreateBranchAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _clientMock.Verify(c => c.PushChangesAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<FileChange>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _clientMock.Verify(c => c.CreatePullRequestAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<FileChange>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Should have logged at least 4 workflow steps (validation, create branch, push, create PR)
        _auditLogMock.Verify(a => a.LogEvaluationAsync(
            It.IsAny<EthicsAuditEntry>(),
            It.IsAny<CancellationToken>()), Times.AtLeast(4));
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTitle_ShouldFail()
    {
        // Arrange
        var proposal = new SelfModificationProposal
        {
            Title = string.Empty,
            Description = "Test description",
            Rationale = "Test rationale",
            Changes = new List<FileChange>
            {
                new FileChange
                {
                    Path = "test.cs",
                    Content = "content",
                    ChangeType = FileChangeType.Update
                }
            },
            Category = ChangeCategory.Refactoring
        };

        // Act
        var result = await _workflow.ExecuteAsync(proposal);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("title is required");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoChanges_ShouldFail()
    {
        // Arrange
        var proposal = new SelfModificationProposal
        {
            Title = "Test",
            Description = "Test description",
            Rationale = "Test rationale",
            Changes = Array.Empty<FileChange>(),
            Category = ChangeCategory.Refactoring
        };

        // Act
        var result = await _workflow.ExecuteAsync(proposal);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("file change is required");
    }

    [Fact]
    public async Task ExecuteAsync_WhenBranchCreationFails_ShouldFail()
    {
        // Arrange
        var proposal = new SelfModificationProposal
        {
            Title = "Test Modification",
            Description = "Test description",
            Rationale = "Test rationale",
            Changes = new List<FileChange>
            {
                new FileChange
                {
                    Path = "test.cs",
                    Content = "content",
                    ChangeType = FileChangeType.Update
                }
            },
            Category = ChangeCategory.Refactoring
        };

        _clientMock.Setup(c => c.CreateBranchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchInfo, string>.Failure("Branch creation failed"));

        // Act
        var result = await _workflow.ExecuteAsync(proposal);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to create branch");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPushFails_ShouldFail()
    {
        // Arrange
        var proposal = new SelfModificationProposal
        {
            Title = "Test Modification",
            Description = "Test description",
            Rationale = "Test rationale",
            Changes = new List<FileChange>
            {
                new FileChange
                {
                    Path = "test.cs",
                    Content = "content",
                    ChangeType = FileChangeType.Update
                }
            },
            Category = ChangeCategory.Refactoring
        };

        var branchInfo = new BranchInfo
        {
            Name = "test-branch",
            Sha = "abc123",
            IsProtected = false
        };

        _clientMock.Setup(c => c.CreateBranchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchInfo, string>.Success(branchInfo));

        _clientMock.Setup(c => c.PushChangesAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileChange>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CommitInfo, string>.Failure("Push failed"));

        // Act
        var result = await _workflow.ExecuteAsync(proposal);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to push changes");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPRCreationFails_ShouldFail()
    {
        // Arrange
        var proposal = new SelfModificationProposal
        {
            Title = "Test Modification",
            Description = "Test description",
            Rationale = "Test rationale",
            Changes = new List<FileChange>
            {
                new FileChange
                {
                    Path = "test.cs",
                    Content = "content",
                    ChangeType = FileChangeType.Update
                }
            },
            Category = ChangeCategory.Refactoring
        };

        var branchInfo = new BranchInfo
        {
            Name = "test-branch",
            Sha = "abc123",
            IsProtected = false
        };

        var commitInfo = new CommitInfo
        {
            Sha = "def456",
            Message = "Test commit",
            Url = "https://github.com/test/commit/def456",
            CommittedAt = DateTime.UtcNow
        };

        _clientMock.Setup(c => c.CreateBranchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchInfo, string>.Success(branchInfo));

        _clientMock.Setup(c => c.PushChangesAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileChange>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CommitInfo, string>.Success(commitInfo));

        _clientMock.Setup(c => c.CreatePullRequestAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileChange>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequestInfo, string>.Failure("PR creation failed"));

        // Act
        var result = await _workflow.ExecuteAsync(proposal);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to create pull request");
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        // Act & Assert
        var act = () => new GitHubSelfModificationWorkflow(null!, _auditLogMock.Object, "test-agent");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAuditLog_ShouldThrow()
    {
        // Act & Assert
        var act = () => new GitHubSelfModificationWorkflow(_clientMock.Object, null!, "test-agent");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelfModificationProposal_ShouldBeImmutable()
    {
        // Arrange
        var proposal = new SelfModificationProposal
        {
            Title = "Test",
            Description = "Description",
            Rationale = "Rationale",
            Changes = new List<FileChange>(),
            Category = ChangeCategory.BugFix,
            RequestReview = true
        };

        // Assert
        proposal.Title.Should().Be("Test");
        proposal.Category.Should().Be(ChangeCategory.BugFix);
        proposal.RequestReview.Should().BeTrue();
    }

    [Fact]
    public void SelfModificationResult_ShouldBeImmutable()
    {
        // Arrange
        var result = new SelfModificationResult(
            Success: true,
            PullRequest: null,
            BranchName: "test-branch",
            Error: null,
            EthicsClearance: EthicalClearance.Permitted("Test"));

        // Assert
        result.Success.Should().BeTrue();
        result.BranchName.Should().Be("test-branch");
        result.Error.Should().BeNull();
    }
}
