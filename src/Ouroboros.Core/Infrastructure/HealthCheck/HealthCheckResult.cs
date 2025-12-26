// <copyright file="HealthCheckResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Infrastructure.HealthCheck;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the result of a health check.
/// </summary>
public sealed class HealthCheckResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckResult"/> class.
    /// </summary>
    /// <param name="componentName">Name of the component.</param>
    /// <param name="status">Health status.</param>
    /// <param name="responseTime">Response time in milliseconds.</param>
    /// <param name="details">Additional details about the health status.</param>
    /// <param name="error">Error message if unhealthy.</param>
    public HealthCheckResult(
        string componentName,
        HealthStatus status,
        long responseTime,
        Dictionary<string, object>? details = null,
        string? error = null)
    {
        ComponentName = componentName;
        Status = status;
        ResponseTime = responseTime;
        Details = details ?? new Dictionary<string, object>();
        Error = error;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the name of the component.
    /// </summary>
    public string ComponentName { get; }

    /// <summary>
    /// Gets the health status.
    /// </summary>
    public HealthStatus Status { get; }

    /// <summary>
    /// Gets the response time in milliseconds.
    /// </summary>
    public long ResponseTime { get; }

    /// <summary>
    /// Gets additional details about the health status.
    /// </summary>
    public Dictionary<string, object> Details { get; }

    /// <summary>
    /// Gets the error message if unhealthy.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the timestamp of when the check was performed.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    /// <param name="componentName">Component name.</param>
    /// <param name="responseTime">Response time.</param>
    /// <param name="details">Additional details.</param>
    /// <returns>Healthy result.</returns>
    public static HealthCheckResult Healthy(string componentName, long responseTime, Dictionary<string, object>? details = null)
        => new HealthCheckResult(componentName, HealthStatus.Healthy, responseTime, details);

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    /// <param name="componentName">Component name.</param>
    /// <param name="responseTime">Response time.</param>
    /// <param name="details">Additional details.</param>
    /// <param name="warning">Warning message.</param>
    /// <returns>Degraded result.</returns>
    public static HealthCheckResult Degraded(string componentName, long responseTime, Dictionary<string, object>? details = null, string? warning = null)
        => new HealthCheckResult(componentName, HealthStatus.Degraded, responseTime, details, warning);

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    /// <param name="componentName">Component name.</param>
    /// <param name="responseTime">Response time.</param>
    /// <param name="error">Error message.</param>
    /// <param name="details">Additional details.</param>
    /// <returns>Unhealthy result.</returns>
    public static HealthCheckResult Unhealthy(string componentName, long responseTime, string error, Dictionary<string, object>? details = null)
        => new HealthCheckResult(componentName, HealthStatus.Unhealthy, responseTime, details, error);
}
