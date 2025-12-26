// <copyright file="HealthCheckReport.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Infrastructure.HealthCheck;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents an aggregated health check report.
/// </summary>
public sealed class HealthCheckReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckReport"/> class.
    /// </summary>
    /// <param name="results">Individual health check results.</param>
    /// <param name="totalDuration">Total duration of all checks.</param>
    public HealthCheckReport(IEnumerable<HealthCheckResult> results, long totalDuration)
    {
        Results = results?.ToList() ?? throw new ArgumentNullException(nameof(results));
        TotalDuration = totalDuration;
        Timestamp = DateTime.UtcNow;

        // Calculate overall status
        if (Results.Any(r => r.Status == HealthStatus.Unhealthy))
        {
            OverallStatus = HealthStatus.Unhealthy;
        }
        else if (Results.Any(r => r.Status == HealthStatus.Degraded))
        {
            OverallStatus = HealthStatus.Degraded;
        }
        else
        {
            OverallStatus = HealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the individual health check results.
    /// </summary>
    public List<HealthCheckResult> Results { get; }

    /// <summary>
    /// Gets the overall health status.
    /// </summary>
    public HealthStatus OverallStatus { get; }

    /// <summary>
    /// Gets the total duration of all checks in milliseconds.
    /// </summary>
    public long TotalDuration { get; }

    /// <summary>
    /// Gets the timestamp of the report.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets a value indicating whether the system is healthy.
    /// </summary>
    public bool IsHealthy => OverallStatus == HealthStatus.Healthy;

    /// <summary>
    /// Gets a value indicating whether the system is ready to serve traffic.
    /// </summary>
    public bool IsReady => OverallStatus != HealthStatus.Unhealthy;
}
