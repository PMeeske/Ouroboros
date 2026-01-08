// <copyright file="LeastLatencyStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.LoadBalancing.Strategies;

/// <summary>
/// Least latency strategy that always selects the provider with the lowest average latency.
/// Optimal for performance-critical applications requiring minimum response time.
/// </summary>
public sealed class LeastLatencyStrategy : IProviderSelectionStrategy
{
    /// <inheritdoc/>
    public string Name => "LeastLatency";

    /// <inheritdoc/>
    public string SelectProvider(List<string> healthyProviders, IReadOnlyDictionary<string, ProviderHealthStatus> healthStatus)
    {
        if (healthyProviders == null || healthyProviders.Count == 0)
            throw new ArgumentException("No healthy providers available", nameof(healthyProviders));

        return healthyProviders
            .OrderBy(id => healthStatus[id].AverageLatencyMs)
            .First();
    }
}
