// <copyright file="ToolApprovalQueueTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for ToolApprovalQueue.
/// </summary>
[Trait("Category", "Unit")]
public class ToolApprovalQueueTests
{
    [Fact]
    public void Enqueue_AddsItemToQueue()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        // Act
        var queueId = queue.Enqueue(toolCall, decision);

        // Assert
        queueId.Should().NotBeNullOrEmpty();
        queue.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPending_ReturnsAllPendingApprovals()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall1 = new ToolCall("tool1", "args1");
        var toolCall2 = new ToolCall("tool2", "args2");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        queue.Enqueue(toolCall1, decision);
        queue.Enqueue(toolCall2, decision);

        // Act
        var pending = await queue.GetPending();

        // Assert
        pending.Should().HaveCount(2);
    }

    [Fact]
    public void GetPending_ById_ReturnsSpecificApproval()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        var queueId = queue.Enqueue(toolCall, decision);

        // Act
        var pending = queue.GetPending(queueId);

        // Assert
        pending.Should().NotBeNull();
        pending!.QueueId.Should().Be(queueId);
        pending.Call.Should().Be(toolCall);
    }

    [Fact]
    public async Task Resolve_WithApproval_ReturnsApprovedDecision()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        var queueId = queue.Enqueue(toolCall, decision);

        // Act
        var resolved = await queue.Resolve(queueId, approved: true, "Looks good");

        // Assert
        resolved.Certainty.IsMark().Should().BeTrue();
        resolved.Result.IsSuccess.Should().BeTrue();
        resolved.EvidenceTrail.Should().Contain(e => e.CriterionName == "human_review" && e.Evaluation.IsMark());
        queue.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_WithRejection_ReturnsRejectedDecision()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        var queueId = queue.Enqueue(toolCall, decision);

        // Act
        var resolved = await queue.Resolve(queueId, approved: false, "Not safe");

        // Assert
        resolved.Certainty.IsVoid().Should().BeTrue();
        resolved.Result.IsFailure.Should().BeTrue();
        resolved.Reasoning.Should().Contain("Not safe");
        queue.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task Resolve_InvalidQueueId_ThrowsException()
    {
        // Arrange
        var queue = new ToolApprovalQueue();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await queue.Resolve("invalid_id", approved: true, "notes"));
    }

    [Fact]
    public async Task EnqueueAndWait_WaitsForResolution()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        // Act - Start waiting task
        var waitTask = queue.EnqueueAndWait(toolCall, decision);

        // Give it a moment to enqueue
        await Task.Delay(10);

        // Get the queue ID from pending
        var pending = await queue.GetPending();
        var queueId = pending[0].QueueId;

        // Resolve it
        await queue.Resolve(queueId, approved: true, "approved");

        // Wait for the task to complete
        var result = await waitTask;

        // Assert
        result.Certainty.IsMark().Should().BeTrue();
    }

    [Fact]
    public async Task EnqueueAndWait_WithTimeout_TimesOut()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        // Act - Wait with short timeout
        var result = await queue.EnqueueAndWait(toolCall, decision, TimeSpan.FromMilliseconds(100));

        // Assert
        result.Certainty.IsImaginary().Should().BeTrue();
        result.Result.Error.Should().Contain("timed out");
        queue.PendingCount.Should().Be(0); // Should be removed after timeout
    }

    [Fact]
    public void Cancel_RemovesPendingApproval()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        var queueId = queue.Enqueue(toolCall, decision);

        // Act
        var cancelled = queue.Cancel(queueId);

        // Assert
        cancelled.Should().BeTrue();
        queue.PendingCount.Should().Be(0);
        queue.GetPending(queueId).Should().BeNull();
    }

    [Fact]
    public void Cancel_InvalidQueueId_ReturnsFalse()
    {
        // Arrange
        var queue = new ToolApprovalQueue();

        // Act
        var cancelled = queue.Cancel("invalid_id");

        // Assert
        cancelled.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_CompletesWaitingTask()
    {
        // Arrange
        var queue = new ToolApprovalQueue();
        var toolCall = new ToolCall("test_tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("test", "test");

        // Act - Start waiting
        var waitTask = queue.EnqueueAndWait(toolCall, decision);

        // Give it a moment
        await Task.Delay(10);

        // Get queue ID and cancel
        var pending = await queue.GetPending();
        queue.Cancel(pending[0].QueueId);

        // Wait for completion
        var result = await waitTask;

        // Assert
        result.Certainty.IsVoid().Should().BeTrue();
        result.Result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public void PendingApproval_StoresAllProperties()
    {
        // Arrange
        var queueId = "test_queue";
        var toolCall = new ToolCall("tool", "args");
        var decision = AuditableDecision<ToolResult>.Uncertain("err", "reason");
        var timestamp = DateTime.UtcNow;

        // Act
        var pending = new PendingApproval(queueId, toolCall, decision, timestamp);

        // Assert
        pending.QueueId.Should().Be(queueId);
        pending.Call.Should().Be(toolCall);
        pending.OriginalDecision.Should().Be(decision);
        pending.QueuedAt.Should().Be(timestamp);
    }
}
