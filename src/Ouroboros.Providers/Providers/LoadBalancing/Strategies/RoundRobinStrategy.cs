// <copyright file="RoundRobinStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.LoadBalancing.Strategies;

/// <summary>
/// Round-robin strategy that distributes requests evenly across all healthy providers in sequence.
/// Thread-safe implementation using lock-based index increment.
/// </summary>
public sealed class RoundRobinStrategy : IProviderSelectionStrategy
{
    private int _currentIndex = 0;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public string Name => "RoundRobin";

    /// <inheritdoc/>
    public string SelectProvider(List<string> healthyProviders, IReadOnlyDictionary<string, ProviderHealthStatus> healthStatus)
    {
        if (healthyProviders == null || healthyProviders.Count == 0)
            throw new ArgumentException("No healthy providers available", nameof(healthyProviders));

        lock (_lock)
        {
            int index = _currentIndex % healthyProviders.Count;
            _currentIndex++;
            return healthyProviders[index];
        }
    }
}
