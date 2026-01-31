// <copyright file="InMemoryEthicsAuditLog.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Simple in-memory implementation of the ethics audit log.
/// For production use, this should be replaced with a persistent storage implementation.
/// </summary>
public sealed class InMemoryEthicsAuditLog : IEthicsAuditLog
{
    private readonly ConcurrentBag<EthicsAuditEntry> _entries = new();

    /// <inheritdoc/>
    public Task LogEvaluationAsync(EthicsAuditEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task LogViolationAttemptAsync(
        string agentId,
        string? userId,
        string description,
        IReadOnlyList<EthicalViolation> violations,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(violations);

        var clearance = EthicalClearance.Denied(
            $"Violation attempt blocked: {description}",
            violations,
            violations.Select(v => v.ViolatedPrinciple).Distinct().ToList());

        var entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = agentId,
            UserId = userId,
            EvaluationType = "ViolationAttempt",
            Description = description,
            Clearance = clearance,
            Context = new Dictionary<string, object>
            {
                ["ViolationCount"] = violations.Count,
                ["Severity"] = violations.Max(v => v.Severity).ToString()
            }
        };

        _entries.Add(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<EthicsAuditEntry>> GetAuditHistoryAsync(
        string agentId,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(agentId);

        var query = _entries.Where(e => e.AgentId == agentId);

        if (startTime.HasValue)
            query = query.Where(e => e.Timestamp >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(e => e.Timestamp <= endTime.Value);

        var result = query
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        return Task.FromResult<IReadOnlyList<EthicsAuditEntry>>(result);
    }

    /// <summary>
    /// Gets all audit entries (for testing and diagnostics).
    /// </summary>
    /// <returns>All audit entries.</returns>
    public IReadOnlyList<EthicsAuditEntry> GetAllEntries()
    {
        return _entries.OrderByDescending(e => e.Timestamp).ToList();
    }
}
