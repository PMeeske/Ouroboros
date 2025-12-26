// <copyright file="PolicyEvaluation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Governance;

/// <summary>
/// Represents the result of a policy evaluation.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
public sealed record PolicyEvaluationResult
{
    /// <summary>
    /// Gets the policy that was evaluated.
    /// </summary>
    public required Policy Policy { get; init; }

    /// <summary>
    /// Gets a value indicating whether the policy was compliant.
    /// </summary>
    public required bool IsCompliant { get; init; }

    /// <summary>
    /// Gets the violations found during evaluation.
    /// </summary>
    public IReadOnlyList<PolicyViolation> Violations { get; init; } = Array.Empty<PolicyViolation>();

    /// <summary>
    /// Gets the timestamp when the evaluation was performed.
    /// </summary>
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets additional context about the evaluation.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents a policy violation.
/// </summary>
public sealed record PolicyViolation
{
    /// <summary>
    /// Gets the violation identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the policy rule that was violated.
    /// </summary>
    public required PolicyRule Rule { get; init; }

    /// <summary>
    /// Gets the severity of the violation.
    /// </summary>
    public ViolationSeverity Severity { get; init; }

    /// <summary>
    /// Gets the violation message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the actual value that violated the policy.
    /// </summary>
    public object? ActualValue { get; init; }

    /// <summary>
    /// Gets the expected value or range.
    /// </summary>
    public object? ExpectedValue { get; init; }

    /// <summary>
    /// Gets the recommended action to resolve the violation.
    /// </summary>
    public required PolicyAction RecommendedAction { get; init; }

    /// <summary>
    /// Gets the timestamp when the violation was detected.
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Defines policy violation severity levels.
/// </summary>
public enum ViolationSeverity
{
    /// <summary>
    /// Low severity - informational only.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium severity - should be addressed.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High severity - requires attention.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical severity - immediate action required.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Represents an approval request.
/// </summary>
public sealed record ApprovalRequest
{
    /// <summary>
    /// Gets the request identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the approval gate that triggered this request.
    /// </summary>
    public required ApprovalGate Gate { get; init; }

    /// <summary>
    /// Gets the operation description requiring approval.
    /// </summary>
    public required string OperationDescription { get; init; }

    /// <summary>
    /// Gets the context information for the approval decision.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the status of the approval request.
    /// </summary>
    public ApprovalStatus Status { get; init; } = ApprovalStatus.Pending;

    /// <summary>
    /// Gets the approvals received.
    /// </summary>
    public IReadOnlyList<Approval> Approvals { get; init; } = Array.Empty<Approval>();

    /// <summary>
    /// Gets the timestamp when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the deadline for approval.
    /// </summary>
    public DateTime Deadline { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request has met approval requirements.
    /// </summary>
    public bool IsApproved => Status == ApprovalStatus.Approved &&
                              Approvals.Count(a => a.Decision == ApprovalDecision.Approve) >= Gate.MinimumApprovals;

    /// <summary>
    /// Gets a value indicating whether the request is expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > Deadline;
}

/// <summary>
/// Defines approval request statuses.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Waiting for approval.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Approved.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Rejected.
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Expired/timed out.
    /// </summary>
    Expired = 3,

    /// <summary>
    /// Cancelled.
    /// </summary>
    Cancelled = 4
}

/// <summary>
/// Represents a single approval from an approver.
/// </summary>
public sealed record Approval
{
    /// <summary>
    /// Gets the approver identifier (user ID or role).
    /// </summary>
    public required string ApproverId { get; init; }

    /// <summary>
    /// Gets the approval decision.
    /// </summary>
    public required ApprovalDecision Decision { get; init; }

    /// <summary>
    /// Gets optional comments from the approver.
    /// </summary>
    public string? Comments { get; init; }

    /// <summary>
    /// Gets the timestamp when the approval was given.
    /// </summary>
    public DateTime ApprovedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Defines approval decisions.
/// </summary>
public enum ApprovalDecision
{
    /// <summary>
    /// Approve the request.
    /// </summary>
    Approve = 0,

    /// <summary>
    /// Reject the request.
    /// </summary>
    Reject = 1,

    /// <summary>
    /// Request more information.
    /// </summary>
    RequestInfo = 2
}

/// <summary>
/// Represents an audit trail entry for policy enforcement.
/// </summary>
public sealed record PolicyAuditEntry
{
    /// <summary>
    /// Gets the entry identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the policy that was evaluated or enforced.
    /// </summary>
    public required Policy Policy { get; init; }

    /// <summary>
    /// Gets the action that was taken.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets the actor who triggered the audit entry (user, system, etc.).
    /// </summary>
    public required string Actor { get; init; }

    /// <summary>
    /// Gets the evaluation result if applicable.
    /// </summary>
    public PolicyEvaluationResult? EvaluationResult { get; init; }

    /// <summary>
    /// Gets the approval request if applicable.
    /// </summary>
    public ApprovalRequest? ApprovalRequest { get; init; }

    /// <summary>
    /// Gets the timestamp of the audit entry.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets additional metadata for the audit entry.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}
