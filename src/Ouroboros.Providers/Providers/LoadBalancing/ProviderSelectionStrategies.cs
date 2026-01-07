// <copyright file="ProviderSelectionStrategies.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Providers.LoadBalancing.Strategies;

namespace Ouroboros.Providers.LoadBalancing;

/// <summary>
/// Factory class providing convenient access to built-in provider selection strategies.
/// </summary>
public static class ProviderSelectionStrategies
{
    /// <summary>
    /// Gets a new instance of the Round Robin strategy.
    /// Distributes requests evenly across all healthy providers in sequence.
    /// </summary>
    public static IProviderSelectionStrategy RoundRobin => new RoundRobinStrategy();

    /// <summary>
    /// Gets a new instance of the Weighted Random strategy.
    /// Probabilistically selects providers based on their health scores.
    /// </summary>
    public static IProviderSelectionStrategy WeightedRandom => new WeightedRandomStrategy();

    /// <summary>
    /// Gets a new instance of the Least Latency strategy.
    /// Always selects the provider with the lowest average latency.
    /// </summary>
    public static IProviderSelectionStrategy LeastLatency => new LeastLatencyStrategy();

    /// <summary>
    /// Gets a new instance of the Adaptive Health strategy.
    /// Selects providers based on composite health scores (70% success rate + 30% latency).
    /// Recommended for general-purpose load balancing.
    /// </summary>
    public static IProviderSelectionStrategy AdaptiveHealth => new AdaptiveHealthStrategy();
}
