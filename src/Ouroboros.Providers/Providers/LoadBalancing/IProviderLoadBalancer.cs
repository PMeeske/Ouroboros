// <copyright file="IProviderLoadBalancer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.LoadBalancing;

/// <summary>
/// Strategy for selecting providers from a pool.
/// </summary>
public enum ProviderRotationStrategy
{
    /// <summary>
    /// Distribute requests evenly across all healthy providers in sequence.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Randomly select providers with weights based on historical performance.
    /// </summary>
    WeightedRandom,

    /// <summary>
    /// Always select the provider with the lowest average latency.
    /// </summary>
    LeastLatency,

    /// <summary>
    /// Adaptive selection based on health scores combining latency, success rate, and availability.
    /// </summary>
    AdaptiveHealth
}

/// <summary>
/// Health status of a provider including performance metrics and availability.
/// </summary>
public sealed record ProviderHealthStatus(
    string ProviderId,
    bool IsHealthy,
    double SuccessRate,
    double AverageLatencyMs,
    int ConsecutiveFailures,
    DateTime? LastFailureTime,
    DateTime? CooldownUntil,
    int TotalRequests,
    int SuccessfulRequests,
    DateTime LastChecked)
{
    /// <summary>
    /// Gets a value indicating whether the provider is in cooldown (rate limited).
    /// </summary>
    public bool IsInCooldown => CooldownUntil.HasValue && CooldownUntil.Value > DateTime.UtcNow;

    /// <summary>
    /// Calculates composite health score (0.0 to 1.0).
    /// </summary>
    public double HealthScore
    {
        get
        {
            if (!IsHealthy || IsInCooldown) return 0.0;

            // Weight success rate more heavily, normalize latency
            double latencyScore = Math.Max(0, 1.0 - (AverageLatencyMs / 10000.0));
            return (SuccessRate * 0.7) + (latencyScore * 0.3);
        }
    }
}

/// <summary>
/// Result of provider selection including the selected provider and reasoning.
/// </summary>
public sealed record ProviderSelectionResult<T>(
    T Provider,
    string ProviderId,
    string Strategy,
    string Reason,
    ProviderHealthStatus Health);

/// <summary>
/// Interface for load balancing across multiple provider instances.
/// Implements intelligent routing to prevent rate limiting and maximize availability.
/// </summary>
/// <typeparam name="T">Type of provider being load balanced.</typeparam>
public interface IProviderLoadBalancer<T>
{
    /// <summary>
    /// Gets the currently configured rotation strategy.
    /// </summary>
    ProviderRotationStrategy Strategy { get; }

    /// <summary>
    /// Gets the current health status of all providers.
    /// </summary>
    IReadOnlyDictionary<string, ProviderHealthStatus> GetHealthStatus();

    /// <summary>
    /// Selects the next provider based on the configured strategy and health metrics.
    /// </summary>
    /// <param name="context">Optional context for selection decisions.</param>
    /// <returns>Result containing selected provider or error.</returns>
    Task<Result<ProviderSelectionResult<T>, string>> SelectProviderAsync(
        Dictionary<string, object>? context = null);

    /// <summary>
    /// Records execution metrics for a provider after a request completes.
    /// </summary>
    /// <param name="providerId">Unique identifier of the provider.</param>
    /// <param name="latencyMs">Request latency in milliseconds.</param>
    /// <param name="success">Whether the request succeeded.</param>
    /// <param name="wasRateLimited">Whether the request was rate limited (429).</param>
    void RecordExecution(string providerId, double latencyMs, bool success, bool wasRateLimited = false);

    /// <summary>
    /// Manually marks a provider as unhealthy (circuit breaker).
    /// </summary>
    /// <param name="providerId">Unique identifier of the provider.</param>
    /// <param name="cooldownDuration">Optional cooldown duration.</param>
    void MarkProviderUnhealthy(string providerId, TimeSpan? cooldownDuration = null);

    /// <summary>
    /// Manually marks a provider as healthy.
    /// </summary>
    /// <param name="providerId">Unique identifier of the provider.</param>
    void MarkProviderHealthy(string providerId);

    /// <summary>
    /// Gets the total number of registered providers.
    /// </summary>
    int ProviderCount { get; }

    /// <summary>
    /// Gets the number of currently healthy providers.
    /// </summary>
    int HealthyProviderCount { get; }
}
