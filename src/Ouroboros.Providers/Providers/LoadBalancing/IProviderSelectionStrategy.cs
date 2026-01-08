// <copyright file="IProviderSelectionStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.LoadBalancing;

/// <summary>
/// Strategy interface for selecting a provider from a pool of healthy providers.
/// Implements the Strategy design pattern for pluggable provider selection algorithms.
/// </summary>
public interface IProviderSelectionStrategy
{
    /// <summary>
    /// Gets the name of this strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Selects a provider from the list of healthy providers based on this strategy's algorithm.
    /// </summary>
    /// <param name="healthyProviders">List of provider IDs that are healthy and available.</param>
    /// <param name="healthStatus">Dictionary of health status for all providers.</param>
    /// <returns>The ID of the selected provider.</returns>
    string SelectProvider(List<string> healthyProviders, IReadOnlyDictionary<string, ProviderHealthStatus> healthStatus);
}
