// <copyright file="HealthStatus.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Infrastructure.HealthCheck;

/// <summary>
/// Represents the health status of a component.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Component is healthy and fully operational.
    /// </summary>
    Healthy,

    /// <summary>
    /// Component is operational but experiencing degraded performance.
    /// </summary>
    Degraded,

    /// <summary>
    /// Component is unhealthy or not operational.
    /// </summary>
    Unhealthy,
}
