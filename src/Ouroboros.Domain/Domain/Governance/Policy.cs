// <copyright file="Policy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Governance;

/// <summary>
/// Defines a governance policy with rules, quotas, and thresholds.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
public sealed record Policy
{
    /// <summary>
    /// Gets the unique policy identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the policy name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the policy description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the policy priority (higher values take precedence).
    /// </summary>
    public double Priority { get; init; } = 1.0;

    /// <summary>
    /// Gets a value indicating whether this policy is active.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Gets the policy rules.
    /// </summary>
    public IReadOnlyList<PolicyRule> Rules { get; init; } = Array.Empty<PolicyRule>();

    /// <summary>
    /// Gets the resource quotas defined by this policy.
    /// </summary>
    public IReadOnlyList<ResourceQuota> Quotas { get; init; } = Array.Empty<ResourceQuota>();

    /// <summary>
    /// Gets the thresholds that trigger policy actions.
    /// </summary>
    public IReadOnlyList<Threshold> Thresholds { get; init; } = Array.Empty<Threshold>();

    /// <summary>
    /// Gets the approval gates required by this policy.
    /// </summary>
    public IReadOnlyList<ApprovalGate> ApprovalGates { get; init; } = Array.Empty<ApprovalGate>();

    /// <summary>
    /// Gets the timestamp when the policy was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the timestamp when the policy was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new policy with the specified name and description.
    /// </summary>
    public static Policy Create(string name, string description) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = description,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

/// <summary>
/// Defines a single policy rule.
/// </summary>
public sealed record PolicyRule
{
    /// <summary>
    /// Gets the rule identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the rule name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the condition that triggers this rule.
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// Gets the action to take when the rule is triggered.
    /// </summary>
    public required PolicyAction Action { get; init; }

    /// <summary>
    /// Gets additional metadata for the rule.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Defines actions that can be taken by policies.
/// </summary>
public enum PolicyAction
{
    /// <summary>
    /// Log the policy violation.
    /// </summary>
    Log = 0,

    /// <summary>
    /// Send an alert notification.
    /// </summary>
    Alert = 1,

    /// <summary>
    /// Block the operation.
    /// </summary>
    Block = 2,

    /// <summary>
    /// Require human approval.
    /// </summary>
    RequireApproval = 3,

    /// <summary>
    /// Throttle the operation.
    /// </summary>
    Throttle = 4,

    /// <summary>
    /// Archive old data.
    /// </summary>
    Archive = 5,

    /// <summary>
    /// Compact storage.
    /// </summary>
    Compact = 6,

    /// <summary>
    /// Execute a custom action.
    /// </summary>
    Custom = 99
}

/// <summary>
/// Defines a resource quota limit.
/// </summary>
public sealed record ResourceQuota
{
    /// <summary>
    /// Gets the resource name (e.g., "cpu", "memory", "storage", "requests_per_hour").
    /// </summary>
    public required string ResourceName { get; init; }

    /// <summary>
    /// Gets the maximum allowed value.
    /// </summary>
    public required double MaxValue { get; init; }

    /// <summary>
    /// Gets the current usage value.
    /// </summary>
    public double CurrentValue { get; init; } = 0.0;

    /// <summary>
    /// Gets the unit of measurement.
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Gets the time window for the quota (null means no time window).
    /// </summary>
    public TimeSpan? TimeWindow { get; init; }

    /// <summary>
    /// Gets a value indicating whether the quota is currently exceeded.
    /// </summary>
    public bool IsExceeded => CurrentValue > MaxValue;

    /// <summary>
    /// Gets the utilization percentage.
    /// </summary>
    public double UtilizationPercent => MaxValue > 0 ? (CurrentValue / MaxValue) * 100.0 : 0.0;
}

/// <summary>
/// Defines a threshold that triggers policy actions.
/// </summary>
public sealed record Threshold
{
    /// <summary>
    /// Gets the metric name being monitored.
    /// </summary>
    public required string MetricName { get; init; }

    /// <summary>
    /// Gets the lower bound (violations occur below this value).
    /// </summary>
    public double? LowerBound { get; init; }

    /// <summary>
    /// Gets the upper bound (violations occur above this value).
    /// </summary>
    public double? UpperBound { get; init; }

    /// <summary>
    /// Gets the action to take when threshold is violated.
    /// </summary>
    public required PolicyAction Action { get; init; }

    /// <summary>
    /// Gets the severity level of threshold violations.
    /// </summary>
    public ThresholdSeverity Severity { get; init; } = ThresholdSeverity.Warning;
}

/// <summary>
/// Defines threshold violation severity levels.
/// </summary>
public enum ThresholdSeverity
{
    /// <summary>
    /// Information level - no action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning level - attention recommended.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error level - action required.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical level - immediate action required.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Defines an approval gate that requires human approval.
/// </summary>
public sealed record ApprovalGate
{
    /// <summary>
    /// Gets the gate identifier.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the gate name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the condition that triggers this approval gate.
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// Gets the required approvers (roles or user IDs).
    /// </summary>
    public IReadOnlyList<string> RequiredApprovers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the minimum number of approvals required.
    /// </summary>
    public int MinimumApprovals { get; init; } = 1;

    /// <summary>
    /// Gets the timeout for approval requests.
    /// </summary>
    public TimeSpan ApprovalTimeout { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets the action to take if approval times out.
    /// </summary>
    public ApprovalTimeoutAction TimeoutAction { get; init; } = ApprovalTimeoutAction.Block;
}

/// <summary>
/// Defines actions to take when approval times out.
/// </summary>
public enum ApprovalTimeoutAction
{
    /// <summary>
    /// Block the operation.
    /// </summary>
    Block = 0,

    /// <summary>
    /// Escalate to higher authority.
    /// </summary>
    Escalate = 1,

    /// <summary>
    /// Auto-approve with reduced privileges.
    /// </summary>
    AutoApproveReduced = 2,

    /// <summary>
    /// Auto-reject.
    /// </summary>
    AutoReject = 3
}
