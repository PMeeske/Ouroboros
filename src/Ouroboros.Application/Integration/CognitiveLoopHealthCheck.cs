// <copyright file="CognitiveLoopHealthCheck.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for cognitive loop subsystem.
/// </summary>
public sealed class CognitiveLoopHealthCheck : IHealthCheck
{
    private readonly ICognitiveLoop? _loop;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveLoopHealthCheck"/> class.
    /// </summary>
    /// <param name="loop">Optional cognitive loop instance.</param>
    public CognitiveLoopHealthCheck(ICognitiveLoop? loop = null)
    {
        _loop = loop;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_loop == null)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Cognitive loop not initialized"));
            }

            if (!_loop.IsRunning)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Cognitive loop not running"));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy("Cognitive loop operational"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Cognitive loop error", ex));
        }
    }
}
