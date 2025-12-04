// <copyright file="HealthCheckAggregator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Infrastructure.HealthCheck;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Aggregates health checks from multiple providers.
/// </summary>
public sealed class HealthCheckAggregator
{
    private readonly List<IHealthCheckProvider> providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckAggregator"/> class.
    /// </summary>
    /// <param name="providers">Health check providers.</param>
    public HealthCheckAggregator(IEnumerable<IHealthCheckProvider> providers)
    {
        this.providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>
    /// Checks health of all registered providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated health check report.</returns>
    public async Task<HealthCheckReport> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<HealthCheckResult> results = new List<HealthCheckResult>();

        foreach (IHealthCheckProvider provider in this.providers)
        {
            try
            {
                HealthCheckResult result = await provider.CheckHealthAsync(cancellationToken);
                results.Add(result);
            }
            catch (Exception ex)
            {
                results.Add(HealthCheckResult.Unhealthy(
                    provider.ComponentName,
                    0,
                    $"Health check failed: {ex.Message}"));
            }
        }

        sw.Stop();

        return new HealthCheckReport(results, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Registers a health check provider.
    /// </summary>
    /// <param name="provider">Provider to register.</param>
    /// <returns>This aggregator for fluent configuration.</returns>
    public HealthCheckAggregator RegisterProvider(IHealthCheckProvider provider)
    {
        this.providers.Add(provider ?? throw new ArgumentNullException(nameof(provider)));
        return this;
    }
}
