// <copyright file="ToolApprovalQueue.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

using System.Collections.Concurrent;

/// <summary>
/// Manages a queue of tool calls that require human approval.
/// Thread-safe queue for human-in-the-loop workflows.
/// </summary>
public sealed class ToolApprovalQueue
{
    private readonly ConcurrentDictionary<string, PendingApproval> pendingApprovals;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AuditableDecision<ToolResult>>> completionSources;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolApprovalQueue"/> class.
    /// </summary>
    public ToolApprovalQueue()
    {
        this.pendingApprovals = new ConcurrentDictionary<string, PendingApproval>();
        this.completionSources = new ConcurrentDictionary<string, TaskCompletionSource<AuditableDecision<ToolResult>>>();
    }

    /// <summary>
    /// Enqueues a tool call for human review.
    /// </summary>
    /// <param name="call">The tool call to review.</param>
    /// <param name="decision">The original uncertain decision.</param>
    /// <returns>A queue ID for tracking this approval request.</returns>
    public string Enqueue(ToolCall call, AuditableDecision<ToolResult> decision)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(decision);

        var queueId = Guid.NewGuid().ToString();
        var pending = new PendingApproval(queueId, call, decision, DateTime.UtcNow);

        this.pendingApprovals[queueId] = pending;
        this.completionSources[queueId] = new TaskCompletionSource<AuditableDecision<ToolResult>>();

        return queueId;
    }

    /// <summary>
    /// Enqueues a tool call and waits asynchronously for human approval.
    /// </summary>
    /// <param name="call">The tool call to review.</param>
    /// <param name="decision">The original uncertain decision.</param>
    /// <returns>A task that completes when a human reviews the request.</returns>
    public async Task<AuditableDecision<ToolResult>> EnqueueAndWait(
        ToolCall call,
        AuditableDecision<ToolResult> decision)
    {
        var queueId = this.Enqueue(call, decision);
        var tcs = this.completionSources[queueId];
        return await tcs.Task;
    }

    /// <summary>
    /// Enqueues a tool call and waits with a timeout.
    /// </summary>
    /// <param name="call">The tool call to review.</param>
    /// <param name="decision">The original uncertain decision.</param>
    /// <param name="timeout">Maximum time to wait for approval.</param>
    /// <returns>A task that completes when reviewed or times out.</returns>
    public async Task<AuditableDecision<ToolResult>> EnqueueAndWait(
        ToolCall call,
        AuditableDecision<ToolResult> decision,
        TimeSpan timeout)
    {
        var queueId = this.Enqueue(call, decision);
        var tcs = this.completionSources[queueId];

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

        if (completedTask == tcs.Task)
        {
            return await tcs.Task;
        }
        else
        {
            // Timeout - remove from queue and return timeout decision
            this.pendingApprovals.TryRemove(queueId, out _);
            this.completionSources.TryRemove(queueId, out _);

            return AuditableDecision<ToolResult>.Uncertain(
                $"Approval request timed out after {timeout.TotalSeconds} seconds",
                "Human review timeout",
                decision.EvidenceTrail.ToArray());
        }
    }

    /// <summary>
    /// Resolves a pending approval with a human decision.
    /// </summary>
    /// <param name="queueId">The queue ID to resolve.</param>
    /// <param name="approved">True to approve, false to reject.</param>
    /// <param name="reviewerNotes">Notes from the human reviewer.</param>
    /// <returns>The final auditable decision.</returns>
    public Task<AuditableDecision<ToolResult>> Resolve(
        string queueId,
        bool approved,
        string reviewerNotes)
    {
        ArgumentNullException.ThrowIfNull(queueId);

        if (!this.pendingApprovals.TryRemove(queueId, out var pending))
        {
            throw new InvalidOperationException($"No pending approval found for queue ID: {queueId}");
        }

        if (!this.completionSources.TryRemove(queueId, out var tcs))
        {
            throw new InvalidOperationException($"No completion source found for queue ID: {queueId}");
        }

        // Build new evidence list with reviewer decision
        var evidence = new List<Evidence>(pending.OriginalDecision.EvidenceTrail)
        {
            new Evidence(
                "human_review",
                approved ? Form.Mark : Form.Void,
                $"Human review: {reviewerNotes}")
        };

        AuditableDecision<ToolResult> finalDecision;

        if (approved)
        {
            // Create a success result (actual execution would happen here in a real system)
            var toolResult = ToolResult.Success(
                "Approved by human review",
                pending.Call,
                TimeSpan.Zero);

            finalDecision = AuditableDecision<ToolResult>.Approve(
                toolResult,
                $"Approved by human reviewer: {reviewerNotes}",
                evidence.ToArray());
        }
        else
        {
            finalDecision = AuditableDecision<ToolResult>.Reject(
                "Rejected by human reviewer",
                reviewerNotes,
                evidence.ToArray());
        }

        tcs.SetResult(finalDecision);
        return Task.FromResult(finalDecision);
    }

    /// <summary>
    /// Gets all pending approvals.
    /// </summary>
    /// <returns>A read-only list of pending approvals.</returns>
    public Task<IReadOnlyList<PendingApproval>> GetPending()
    {
        var pending = this.pendingApprovals.Values.OrderBy(p => p.QueuedAt).ToList();
        return Task.FromResult<IReadOnlyList<PendingApproval>>(pending);
    }

    /// <summary>
    /// Gets a specific pending approval by queue ID.
    /// </summary>
    /// <param name="queueId">The queue ID to look up.</param>
    /// <returns>The pending approval if found, null otherwise.</returns>
    public PendingApproval? GetPending(string queueId)
    {
        this.pendingApprovals.TryGetValue(queueId, out var pending);
        return pending;
    }

    /// <summary>
    /// Cancels a pending approval request.
    /// </summary>
    /// <param name="queueId">The queue ID to cancel.</param>
    /// <returns>True if cancelled, false if not found.</returns>
    public bool Cancel(string queueId)
    {
        var removed = this.pendingApprovals.TryRemove(queueId, out var pending);

        if (removed && this.completionSources.TryRemove(queueId, out var tcs))
        {
            var evidence = new List<Evidence>(pending!.OriginalDecision.EvidenceTrail)
            {
                new Evidence("cancellation", Form.Void, "Approval request cancelled")
            };

            var cancelDecision = AuditableDecision<ToolResult>.Reject(
                "Approval request cancelled",
                "Request was cancelled before review",
                evidence.ToArray());

            tcs.SetResult(cancelDecision);
        }

        return removed;
    }

    /// <summary>
    /// Gets the count of pending approvals.
    /// </summary>
    public int PendingCount => this.pendingApprovals.Count;
}

/// <summary>
/// Represents a tool call pending human approval.
/// </summary>
public sealed record PendingApproval
{
    /// <summary>
    /// Gets the queue ID for this pending approval.
    /// </summary>
    public string QueueId { get; init; }

    /// <summary>
    /// Gets the tool call awaiting approval.
    /// </summary>
    public ToolCall Call { get; init; }

    /// <summary>
    /// Gets the original uncertain decision.
    /// </summary>
    public AuditableDecision<ToolResult> OriginalDecision { get; init; }

    /// <summary>
    /// Gets the timestamp when this was queued.
    /// </summary>
    public DateTime QueuedAt { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingApproval"/> class.
    /// </summary>
    /// <param name="queueId">The queue ID.</param>
    /// <param name="call">The tool call.</param>
    /// <param name="originalDecision">The original decision.</param>
    /// <param name="queuedAt">The queued timestamp.</param>
    public PendingApproval(
        string queueId,
        ToolCall call,
        AuditableDecision<ToolResult> originalDecision,
        DateTime queuedAt)
    {
        this.QueueId = queueId;
        this.Call = call;
        this.OriginalDecision = originalDecision;
        this.QueuedAt = queuedAt;
    }
}
