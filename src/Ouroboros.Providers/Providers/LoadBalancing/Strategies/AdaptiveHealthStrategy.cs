// <copyright file="AdaptiveHealthStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.LoadBalancing.Strategies;

/// <summary>
/// Adaptive health strategy that selects providers based on composite health scores.
/// Combines success rate (70%) and latency (30%) for balanced selection.
/// Recommended for general-purpose load balancing.
/// </summary>
public sealed class AdaptiveHealthStrategy : IProviderSelectionStrategy
{
    /// <inheritdoc/>
    public string Name => "AdaptiveHealth";

    /// <inheritdoc/>
    public string SelectProvider(List<string> healthyProviders, IReadOnlyDictionary<string, ProviderHealthStatus> healthStatus)
    {
        if (healthyProviders == null || healthyProviders.Count == 0)
            throw new ArgumentException("No healthy providers available", nameof(healthyProviders));

        return healthyProviders
            .OrderByDescending(id => healthStatus[id].HealthScore)
            .First();
    }
}
