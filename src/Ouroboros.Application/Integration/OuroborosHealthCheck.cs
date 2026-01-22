// <copyright file="OuroborosHealthCheck.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for overall Ouroboros system.
/// </summary>
public sealed class OuroborosHealthCheck : IHealthCheck
{
    private readonly IOuroborosCore? _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosHealthCheck"/> class.
    /// </summary>
    /// <param name="core">Optional Ouroboros core instance.</param>
    public OuroborosHealthCheck(IOuroborosCore? core = null)
    {
        _core = core;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_core == null)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Ouroboros core not initialized"));
            }

            if (!_core.IsRunning)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Ouroboros core not running"));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy("Ouroboros system operational"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Ouroboros system error", ex));
        }
    }
}
