// <copyright file="ExecutionContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Represents the execution context for tool invocation.
/// Contains authorization, rate limiting, and other contextual information.
/// </summary>
public sealed record ExecutionContext
{
    /// <summary>
    /// Gets the user information for authorization checks.
    /// </summary>
    public UserInfo User { get; init; }

    /// <summary>
    /// Gets the rate limiter for controlling execution frequency.
    /// </summary>
    public IRateLimiter RateLimiter { get; init; }

    /// <summary>
    /// Gets the content filter for safety checks.
    /// </summary>
    public IContentFilter ContentFilter { get; init; }

    /// <summary>
    /// Gets optional session information.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets additional context metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionContext"/> class.
    /// </summary>
    /// <param name="user">The user information.</param>
    /// <param name="rateLimiter">The rate limiter.</param>
    /// <param name="contentFilter">The content filter.</param>
    /// <param name="sessionId">Optional session ID.</param>
    /// <param name="metadata">Optional metadata.</param>
    public ExecutionContext(
        UserInfo user,
        IRateLimiter rateLimiter,
        IContentFilter contentFilter,
        string? sessionId = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        this.User = user;
        this.RateLimiter = rateLimiter;
        this.ContentFilter = contentFilter;
        this.SessionId = sessionId;
        this.Metadata = metadata ?? new Dictionary<string, object>();
    }
}

/// <summary>
/// Represents user information for authorization.
/// </summary>
public sealed record UserInfo
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets the user's permissions/roles.
    /// </summary>
    public IReadOnlySet<string> Permissions { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfo"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="permissions">The user's permissions.</param>
    public UserInfo(string userId, IReadOnlySet<string> permissions)
    {
        this.UserId = userId;
        this.Permissions = permissions;
    }

    /// <summary>
    /// Checks if the user has a specific permission.
    /// </summary>
    /// <param name="permission">The permission to check.</param>
    /// <returns>True if the user has the permission.</returns>
    public bool HasPermission(string permission)
    {
        return this.Permissions.Contains(permission);
    }
}

/// <summary>
/// Interface for rate limiting tool executions.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if a tool call is allowed under current rate limits.
    /// </summary>
    /// <param name="toolCall">The tool call to check.</param>
    /// <returns>True if allowed, false if rate limit exceeded.</returns>
    bool IsAllowed(ToolCall toolCall);

    /// <summary>
    /// Records a tool execution for rate limiting.
    /// </summary>
    /// <param name="toolCall">The tool call that was executed.</param>
    void Record(ToolCall toolCall);
}

/// <summary>
/// Interface for content filtering and safety checks.
/// </summary>
public interface IContentFilter
{
    /// <summary>
    /// Analyzes content for safety violations.
    /// </summary>
    /// <param name="content">The content to analyze.</param>
    /// <returns>The safety level of the content.</returns>
    SafetyLevel Analyze(string content);
}

/// <summary>
/// Represents the safety level of content.
/// </summary>
public enum SafetyLevel
{
    /// <summary>
    /// Content is safe to proceed.
    /// </summary>
    Safe = 0,

    /// <summary>
    /// Content safety is uncertain, requires review.
    /// </summary>
    Uncertain = 1,

    /// <summary>
    /// Content is unsafe and should be blocked.
    /// </summary>
    Unsafe = 2
}
