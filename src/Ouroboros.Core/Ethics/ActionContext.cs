// <copyright file="ActionContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Represents an immutable context for action evaluation.
/// Provides information about the agent, user, and environment.
/// </summary>
public sealed record ActionContext
{
    /// <summary>
    /// Gets the unique identifier of the agent proposing the action.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Gets the unique identifier of the user on whose behalf the action is being performed.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the current environment or deployment context (e.g., "production", "development", "testing").
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>
    /// Gets the current state information relevant to ethical evaluation.
    /// </summary>
    public required IReadOnlyDictionary<string, object> State { get; init; }

    /// <summary>
    /// Gets the recent actions taken by this agent (for context and pattern detection).
    /// </summary>
    public IReadOnlyList<string> RecentActions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the timestamp when this context was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
