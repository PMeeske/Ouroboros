// <copyright file="EthicsEnforcedGitHubMcpClientTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Application.GitHub;
using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Xunit;

namespace Ouroboros.Tests.Tests.GitHub;

/// <summary>
/// Tests for EthicsEnforcedGitHubMcpClient wrapper.
/// </summary>
public sealed class EthicsEnforcedGitHubMcpClientTests
{
    private readonly Mock<IGitHubMcpClient> _innerClientMock;
    private readonly Mock<IEthicsFramework> _ethicsMock;
    private readonly Mock<IEthicsAuditLog> _auditLogMock;
    private readonly EthicsEnforcedGitHubMcpClient _client;

    public EthicsEnforcedGitHubMcpClientTests()
    {
        _innerClientMock = new Mock<IGitHubMcpClient>();
        _ethicsMock = new Mock<IEthicsFramework>();
        _auditLogMock = new Mock<IEthicsAuditLog>();
        _client = new EthicsEnforcedGitHubMcpClient(
            _innerClientMock.Object,
            _ethicsMock.Object,
            _auditLogMock.Object,
            "test-agent");
    }

    [Fact]
    public async Task CreatePullRequestAsync_WithEthicsApproval_ShouldSucceed()
    {
        // Arrange
        var clearance = EthicalClearance.Permitted("Approved for testing");
        _ethicsMock.Setup(e => e.EvaluateSelfModificationAsync(
                It.IsAny<SelfModificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(clearance));

        var prInfo = new PullRequestInfo
        {
            Number = 1,
            Url = "https://github.com/test/test/pull/1",
            Title = "Test PR",
            State = "open",
            HeadBranch = "feature",
            BaseBranch = "main",
            CreatedAt = DateTime.UtcNow
        };

        _innerClientMock.Setup(c => c.CreatePullRequestAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<FileChange>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PullRequestInfo, string>.Success(prInfo));

        // Act
        var result = await _client.CreatePullRequestAsync("Test", "Description", "feature");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Number.Should().Be(1);

        _ethicsMock.Verify(e => e.EvaluateSelfModificationAsync(
            It.IsAny<SelfModificationRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _auditLogMock.Verify(a => a.LogEvaluationAsync(
            It.IsAny<EthicsAuditEntry>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePullRequestAsync_WithEthicsDenial_ShouldFail()
    {
        // Arrange
        var violation = new EthicalViolation
        {
            ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
            Description = "Violates safety principle",
            Severity = ViolationSeverity.High,
            Evidence = "Test evidence",
            AffectedParties = new[] { "System" }
        };

        var clearance = EthicalClearance.Denied(
            "Denied for testing",
            new[] { violation });

        _ethicsMock.Setup(e => e.EvaluateSelfModificationAsync(
                It.IsAny<SelfModificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(clearance));

        // Act
        var result = await _client.CreatePullRequestAsync("Test", "Description", "feature");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Ethics denial");
        result.Error.Should().Contain("Violates safety principle");

        _ethicsMock.Verify(e => e.EvaluateSelfModificationAsync(
            It.IsAny<SelfModificationRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _innerClientMock.Verify(c => c.CreatePullRequestAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<FileChange>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PushChangesAsync_WithInvalidFileExtension_ShouldFail()
    {
        // Arrange
        var clearance = EthicalClearance.Permitted("Approved for testing");
        _ethicsMock.Setup(e => e.EvaluateSelfModificationAsync(
                It.IsAny<SelfModificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(clearance));

        var changes = new List<FileChange>
        {
            new FileChange
            {
                Path = "test.exe", // Invalid extension
                Content = "binary",
                ChangeType = FileChangeType.Create
            }
        };

        // Act
        var result = await _client.PushChangesAsync("branch", changes, "commit");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid file extensions");

        _innerClientMock.Verify(c => c.PushChangesAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<FileChange>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReadFileAsync_ShouldNotRequireEthicsCheck()
    {
        // Arrange
        var fileContent = new FileContent
        {
            Path = "test.cs",
            Content = "content",
            Size = 7,
            Sha = "abc123"
        };

        _innerClientMock.Setup(c => c.ReadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FileContent, string>.Success(fileContent));

        // Act
        var result = await _client.ReadFileAsync("test.cs");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Path.Should().Be("test.cs");

        _ethicsMock.Verify(e => e.EvaluateSelfModificationAsync(
            It.IsAny<SelfModificationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _auditLogMock.Verify(a => a.LogEvaluationAsync(
            It.IsAny<EthicsAuditEntry>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBranchAsync_WithEthicsApproval_ShouldSucceed()
    {
        // Arrange
        var clearance = EthicalClearance.Permitted("Approved for testing");
        _ethicsMock.Setup(e => e.EvaluateSelfModificationAsync(
                It.IsAny<SelfModificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(clearance));

        var branchInfo = new BranchInfo
        {
            Name = "new-branch",
            Sha = "abc123",
            IsProtected = false
        };

        _innerClientMock.Setup(c => c.CreateBranchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchInfo, string>.Success(branchInfo));

        // Act
        var result = await _client.CreateBranchAsync("new-branch");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("new-branch");

        _ethicsMock.Verify(e => e.EvaluateSelfModificationAsync(
            It.IsAny<SelfModificationRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListFilesAsync_ShouldNotRequireEthicsCheck()
    {
        // Arrange
        var files = new List<GitHubFileInfo>
        {
            new GitHubFileInfo
            {
                Name = "test.cs",
                Path = "src/test.cs",
                Type = "file",
                Size = 100
            }
        };

        _innerClientMock.Setup(c => c.ListFilesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<GitHubFileInfo>, string>.Success(files));

        // Act
        var result = await _client.ListFilesAsync("src");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        _ethicsMock.Verify(e => e.EvaluateSelfModificationAsync(
            It.IsAny<SelfModificationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullInnerClient_ShouldThrow()
    {
        // Act & Assert
        var act = () => new EthicsEnforcedGitHubMcpClient(
            null!,
            _ethicsMock.Object,
            _auditLogMock.Object,
            "test-agent");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullEthicsFramework_ShouldThrow()
    {
        // Act & Assert
        var act = () => new EthicsEnforcedGitHubMcpClient(
            _innerClientMock.Object,
            null!,
            _auditLogMock.Object,
            "test-agent");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAuditLog_ShouldThrow()
    {
        // Act & Assert
        var act = () => new EthicsEnforcedGitHubMcpClient(
            _innerClientMock.Object,
            _ethicsMock.Object,
            null!,
            "test-agent");

        act.Should().Throw<ArgumentNullException>();
    }
}
