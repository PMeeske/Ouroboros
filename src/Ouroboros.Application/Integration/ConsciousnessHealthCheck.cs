// <copyright file="ConsciousnessHealthCheck.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Health check for consciousness scaffold subsystem.
/// </summary>
public sealed class ConsciousnessHealthCheck : IHealthCheck
{
    private readonly IConsciousnessScaffold? _scaffold;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsciousnessHealthCheck"/> class.
    /// </summary>
    /// <param name="scaffold">Optional consciousness scaffold instance.</param>
    public ConsciousnessHealthCheck(IConsciousnessScaffold? scaffold = null)
    {
        _scaffold = scaffold;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_scaffold == null)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Consciousness scaffold not initialized"));
            }

            if (!_scaffold.IsRunning)
            {
                return Task.FromResult(
                    HealthCheckResult.Degraded("Consciousness scaffold not running"));
            }

            return Task.FromResult(
                HealthCheckResult.Healthy("Consciousness scaffold operational"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Consciousness scaffold error", ex));
        }
    }
}
