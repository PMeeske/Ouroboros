// <copyright file="EpicBranchOrchestratorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Agent;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the Epic Branch Orchestration system.
/// </summary>
public class EpicBranchOrchestratorTests
{
    private IEpicBranchOrchestrator CreateTestOrchestrator(EpicBranchConfig? config = null)
    {
        var safetyGuard = new SafetyGuard(PermissionLevel.Isolated);
        var distributor = new DistributedOrchestrator(safetyGuard);
        return new EpicBranchOrchestrator(distributor, config);
    }

    [Fact]
    public async Task RegisterEpic_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        var subIssues = new List<int> { 1, 2, 3 };

        // Act
        var result = await orchestrator.RegisterEpicAsync(
            1,
            "Test Epic",
            "Test Description",
            subIssues);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.EpicNumber);
        Assert.Equal("Test Epic", result.Value.Title);
        Assert.Equal(3, result.Value.SubIssueNumbers.Count);
    }

    [Fact]
    public async Task RegisterEpic_WithEmptyTitle_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();

        // Act
        var result = await orchestrator.RegisterEpicAsync(
            1,
            string.Empty,
            "Description",
            new List<int> { 1 });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("title", result.Error.ToLower());
    }

    [Fact]
    public async Task RegisterEpic_WithNoSubIssues_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();

        // Act
        var result = await orchestrator.RegisterEpicAsync(
            1,
            "Test Epic",
            "Description",
            new List<int>());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("sub-issue", result.Error.ToLower());
    }

    [Fact]
    public async Task RegisterEpic_WithAutoAssign_CreatesAssignments()
    {
        // Arrange
        var config = new EpicBranchConfig(AutoAssignAgents: true, AutoCreateBranches: true);
        var orchestrator = this.CreateTestOrchestrator(config);
        var subIssues = new List<int> { 1, 2, 3 };

        // Act
        var result = await orchestrator.RegisterEpicAsync(
            1,
            "Test Epic",
            "Description",
            subIssues);

        // Assert
        Assert.True(result.IsSuccess);
        var assignments = orchestrator.GetSubIssueAssignments(1);
        Assert.Equal(3, assignments.Count);

        foreach (var assignment in assignments)
        {
            Assert.NotNull(assignment.AssignedAgentId);
            Assert.NotNull(assignment.BranchName);
            Assert.Equal(SubIssueStatus.BranchCreated, assignment.Status);
        }
    }

    [Fact]
    public async Task AssignSubIssue_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var config = new EpicBranchConfig(AutoAssignAgents: false);
        var orchestrator = this.CreateTestOrchestrator(config);
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = await orchestrator.AssignSubIssueAsync(1, 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.IssueNumber);
        Assert.NotNull(result.Value.AssignedAgentId);
        Assert.NotNull(result.Value.BranchName);
    }

    [Fact]
    public async Task AssignSubIssue_WithNonexistentEpic_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();

        // Act
        var result = await orchestrator.AssignSubIssueAsync(999, 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error.ToLower());
    }

    [Fact]
    public async Task AssignSubIssue_WithNonexistentSubIssue_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = await orchestrator.AssignSubIssueAsync(1, 999);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not part of", result.Error.ToLower());
    }

    [Fact]
    public async Task AssignSubIssue_WithPreferredAgent_UsesPreferredAgent()
    {
        // Arrange
        var config = new EpicBranchConfig(AutoAssignAgents: false);
        var orchestrator = this.CreateTestOrchestrator(config);
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = await orchestrator.AssignSubIssueAsync(1, 1, "custom-agent-id");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("custom-agent-id", result.Value.AssignedAgentId);
    }

    [Fact]
    public async Task GetSubIssueAssignments_ReturnsAllAssignments()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1, 2, 3 });

        // Act
        var assignments = orchestrator.GetSubIssueAssignments(1);

        // Assert
        Assert.Equal(3, assignments.Count);
        Assert.All(assignments, a => Assert.NotNull(a.AssignedAgentId));
    }

    [Fact]
    public void GetSubIssueAssignments_ForNonexistentEpic_ReturnsEmpty()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();

        // Act
        var assignments = orchestrator.GetSubIssueAssignments(999);

        // Assert
        Assert.Empty(assignments);
    }

    [Fact]
    public async Task GetSubIssueAssignment_ReturnsSpecificAssignment()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1, 2, 3 });

        // Act
        var assignment = orchestrator.GetSubIssueAssignment(1, 2);

        // Assert
        Assert.NotNull(assignment);
        Assert.Equal(2, assignment.IssueNumber);
    }

    [Fact]
    public void GetSubIssueAssignment_ForNonexistent_ReturnsNull()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();

        // Act
        var assignment = orchestrator.GetSubIssueAssignment(999, 1);

        // Assert
        Assert.Null(assignment);
    }

    [Fact]
    public async Task UpdateSubIssueStatus_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = orchestrator.UpdateSubIssueStatus(1, 1, SubIssueStatus.InProgress);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubIssueStatus.InProgress, result.Value.Status);
    }

    [Fact]
    public async Task UpdateSubIssueStatus_ToCompleted_SetsCompletedAt()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = orchestrator.UpdateSubIssueStatus(1, 1, SubIssueStatus.Completed);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CompletedAt);
    }

    [Fact]
    public async Task UpdateSubIssueStatus_WithError_SetsErrorMessage()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = orchestrator.UpdateSubIssueStatus(1, 1, SubIssueStatus.Failed, "Test error");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubIssueStatus.Failed, result.Value.Status);
        Assert.Equal("Test error", result.Value.ErrorMessage);
    }

    [Fact]
    public void UpdateSubIssueStatus_ForNonexistent_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();

        // Act
        var result = orchestrator.UpdateSubIssueStatus(999, 1, SubIssueStatus.Completed);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error.ToLower());
    }

    [Fact]
    public async Task ExecuteSubIssue_WithValidWork_ReturnsSuccess()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = await orchestrator.ExecuteSubIssueAsync(
            1,
            1,
            async assignment =>
            {
                await Task.Delay(10);
                return Result<SubIssueAssignment, string>.Success(assignment);
            });

        // Assert
        Assert.True(result.IsSuccess);

        var updatedAssignment = orchestrator.GetSubIssueAssignment(1, 1);
        Assert.NotNull(updatedAssignment);
        Assert.Equal(SubIssueStatus.Completed, updatedAssignment.Status);
    }

    [Fact]
    public async Task ExecuteSubIssue_WithFailingWork_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = await orchestrator.ExecuteSubIssueAsync(
            1,
            1,
            async assignment =>
            {
                await Task.Delay(10);
                return Result<SubIssueAssignment, string>.Failure("Work failed");
            });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Work failed", result.Error);

        var updatedAssignment = orchestrator.GetSubIssueAssignment(1, 1);
        Assert.NotNull(updatedAssignment);
        Assert.Equal(SubIssueStatus.Failed, updatedAssignment.Status);
    }

    [Fact]
    public async Task ExecuteSubIssue_WithException_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });

        // Act
        var result = await orchestrator.ExecuteSubIssueAsync(
            1,
            1,
            async assignment =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Test exception");
            });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Test exception", result.Error);

        var updatedAssignment = orchestrator.GetSubIssueAssignment(1, 1);
        Assert.NotNull(updatedAssignment);
        Assert.Equal(SubIssueStatus.Failed, updatedAssignment.Status);
    }

    [Fact]
    public async Task ExecuteSubIssue_WhenAlreadyInProgress_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });
        orchestrator.UpdateSubIssueStatus(1, 1, SubIssueStatus.InProgress);

        // Act
        var result = await orchestrator.ExecuteSubIssueAsync(
            1,
            1,
            async assignment =>
            {
                await Task.CompletedTask;
                return Result<SubIssueAssignment, string>.Success(assignment);
            });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("already in progress", result.Error.ToLower());
    }

    [Fact]
    public async Task ExecuteSubIssue_ForNonexistent_ReturnsFailure()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();

        // Act
        var result = await orchestrator.ExecuteSubIssueAsync(
            999,
            1,
            async assignment =>
            {
                await Task.CompletedTask;
                return Result<SubIssueAssignment, string>.Success(assignment);
            });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error.ToLower());
    }

    [Fact]
    public async Task BranchNaming_FollowsConvention()
    {
        // Arrange
        var config = new EpicBranchConfig(BranchPrefix: "test-epic");
        var orchestrator = this.CreateTestOrchestrator(config);
        await orchestrator.RegisterEpicAsync(42, "Test Epic", "Description", new List<int> { 123 });

        // Act
        var assignment = orchestrator.GetSubIssueAssignment(42, 123);

        // Assert
        Assert.NotNull(assignment);
        Assert.Equal("test-epic-42/sub-issue-123", assignment.BranchName);
    }

    [Fact]
    public async Task AgentNaming_FollowsConvention()
    {
        // Arrange
        var config = new EpicBranchConfig(AgentPoolPrefix: "test-agent");
        var orchestrator = this.CreateTestOrchestrator(config);
        await orchestrator.RegisterEpicAsync(42, "Test Epic", "Description", new List<int> { 123 });

        // Act
        var assignment = orchestrator.GetSubIssueAssignment(42, 123);

        // Assert
        Assert.NotNull(assignment);
        Assert.Equal("test-agent-42-123", assignment.AssignedAgentId);
    }

    [Fact]
    public async Task ParallelExecution_HandlesMultipleSubIssues()
    {
        // Arrange
        var orchestrator = this.CreateTestOrchestrator();
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1, 2, 3, 4, 5 });

        // Act
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            return await orchestrator.ExecuteSubIssueAsync(
                1,
                i,
                async assignment =>
                {
                    await Task.Delay(50);
                    return Result<SubIssueAssignment, string>.Success(assignment);
                });
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, results.Length);
        Assert.All(results, r => Assert.True(r.IsSuccess));

        var assignments = orchestrator.GetSubIssueAssignments(1);
        Assert.All(assignments, a => Assert.Equal(SubIssueStatus.Completed, a.Status));
    }

    [Fact]
    public async Task Config_DisableAutoCreateBranches_DoesNotCreateBranches()
    {
        // Arrange
        var config = new EpicBranchConfig(AutoCreateBranches: false);
        var orchestrator = this.CreateTestOrchestrator(config);

        // Act
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });
        var assignment = orchestrator.GetSubIssueAssignment(1, 1);

        // Assert
        Assert.NotNull(assignment);
        Assert.Null(assignment.Branch);
        Assert.Equal(SubIssueStatus.Pending, assignment.Status);
    }

    [Fact]
    public async Task Config_DisableAutoAssignAgents_DoesNotAutoAssign()
    {
        // Arrange
        var config = new EpicBranchConfig(AutoAssignAgents: false);
        var orchestrator = this.CreateTestOrchestrator(config);

        // Act
        await orchestrator.RegisterEpicAsync(1, "Test Epic", "Description", new List<int> { 1 });
        var assignments = orchestrator.GetSubIssueAssignments(1);

        // Assert
        Assert.Empty(assignments);
    }
}
