// <copyright file="IEthicsAuditLog.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Represents an immutable audit entry for ethical evaluations.
/// </summary>
public sealed record EthicsAuditEntry
{
    /// <summary>
    /// Gets the unique identifier for this audit entry.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the timestamp when this evaluation occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the agent ID that requested the evaluation.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Gets the user ID associated with this request (if applicable).
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the type of evaluation performed (e.g., "Action", "Plan", "Goal", "SelfModification").
    /// </summary>
    public required string EvaluationType { get; init; }

    /// <summary>
    /// Gets a brief description of what was evaluated.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the clearance decision that was made.
    /// </summary>
    public required EthicalClearance Clearance { get; init; }

    /// <summary>
    /// Gets additional context data for this audit entry.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } = 
        new Dictionary<string, object>();
}

/// <summary>
/// Interface for logging and querying ethical evaluation audit trails.
/// All ethical evaluations MUST be logged for accountability and review.
/// </summary>
public interface IEthicsAuditLog
{
    /// <summary>
    /// Logs an ethical evaluation for audit purposes.
    /// </summary>
    /// <param name="entry">The audit entry to log.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogEvaluationAsync(EthicsAuditEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Logs an attempted violation that was blocked.
    /// </summary>
    /// <param name="agentId">The agent that attempted the violation.</param>
    /// <param name="userId">The user ID (if applicable).</param>
    /// <param name="description">Description of the attempted action.</param>
    /// <param name="violations">The violations that caused blocking.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogViolationAttemptAsync(
        string agentId,
        string? userId,
        string description,
        IReadOnlyList<EthicalViolation> violations,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves audit history for a specific agent.
    /// </summary>
    /// <param name="agentId">The agent ID to query.</param>
    /// <param name="startTime">Optional start time for the query range.</param>
    /// <param name="endTime">Optional end time for the query range.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of audit entries for the specified agent.</returns>
    Task<IReadOnlyList<EthicsAuditEntry>> GetAuditHistoryAsync(
        string agentId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default);
}
