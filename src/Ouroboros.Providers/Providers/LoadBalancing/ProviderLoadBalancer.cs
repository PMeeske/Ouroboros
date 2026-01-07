// <copyright file="ProviderLoadBalancer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ouroboros.Providers.LoadBalancing;

/// <summary>
/// Load balancer that distributes requests across multiple provider instances
/// using configurable strategies to prevent rate limiting and optimize performance.
/// Implements functional programming patterns with monadic error handling.
/// Uses the Strategy design pattern for pluggable provider selection algorithms.
/// </summary>
/// <typeparam name="T">Type of provider being load balanced.</typeparam>
public sealed class ProviderLoadBalancer<T> : IProviderLoadBalancer<T>
{
    private const int MaxConsecutiveFailures = 3;
    private static readonly TimeSpan DefaultCooldownDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, T> _providers = new();
    private readonly ConcurrentDictionary<string, ProviderHealthStatus> _healthStatus = new();
    private readonly IProviderSelectionStrategy _strategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderLoadBalancer{T}"/> class.
    /// </summary>
    /// <param name="strategy">The selection strategy to use. If null, defaults to AdaptiveHealth.</param>
    public ProviderLoadBalancer(IProviderSelectionStrategy? strategy = null)
    {
        _strategy = strategy ?? ProviderSelectionStrategies.AdaptiveHealth;
    }

    /// <inheritdoc/>
    public IProviderSelectionStrategy Strategy => _strategy;

    /// <inheritdoc/>
    public int ProviderCount => _providers.Count;

    /// <inheritdoc/>
    public int HealthyProviderCount => _healthStatus.Values.Count(h => h.IsHealthy && !h.IsInCooldown);

    /// <summary>
    /// Registers a provider with the load balancer.
    /// </summary>
    /// <param name="providerId">Unique identifier for the provider.</param>
    /// <param name="provider">The provider instance.</param>
    public void RegisterProvider(string providerId, T provider)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be empty", nameof(providerId));
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        _providers[providerId] = provider;
        _healthStatus[providerId] = new ProviderHealthStatus(
            ProviderId: providerId,
            IsHealthy: true,
            SuccessRate: 1.0,
            AverageLatencyMs: 0,
            ConsecutiveFailures: 0,
            LastFailureTime: null,
            CooldownUntil: null,
            TotalRequests: 0,
            SuccessfulRequests: 0,
            LastChecked: DateTime.UtcNow);
    }

    /// <summary>
    /// Removes a provider from the load balancer.
    /// </summary>
    /// <param name="providerId">Unique identifier of the provider to remove.</param>
    /// <returns>True if the provider was removed, false if not found.</returns>
    public bool UnregisterProvider(string providerId)
    {
        bool removed = _providers.TryRemove(providerId, out _);
        _healthStatus.TryRemove(providerId, out _);
        return removed;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ProviderHealthStatus> GetHealthStatus()
    {
        // Update cooldown status before returning
        foreach (var kvp in _healthStatus)
        {
            if (kvp.Value.IsInCooldown && kvp.Value.CooldownUntil <= DateTime.UtcNow)
            {
                // Cooldown expired, restore health if no other issues
                if (kvp.Value.ConsecutiveFailures == 0)
                {
                    _healthStatus[kvp.Key] = kvp.Value with
                    {
                        IsHealthy = true,
                        CooldownUntil = null
                    };
                }
            }
        }

        return _healthStatus;
    }

    /// <inheritdoc/>
    public async Task<Result<ProviderSelectionResult<T>, string>> SelectProviderAsync(
        Dictionary<string, object>? context = null)
    {
        if (_providers.IsEmpty)
        {
            return Result<ProviderSelectionResult<T>, string>.Failure(
                "No providers registered with load balancer");
        }

        // Get healthy providers
        var healthyProviders = _healthStatus
            .Where(kvp => kvp.Value.IsHealthy && !kvp.Value.IsInCooldown)
            .Select(kvp => kvp.Key)
            .ToList();

        if (healthyProviders.Count == 0)
        {
            // Try to recover providers that have been in cooldown
            var recoverablePlaceholders = _healthStatus
                .Where(kvp => kvp.Value.IsInCooldown && kvp.Value.CooldownUntil <= DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            if (recoverablePlaceholders.Count > 0)
            {
                // Mark first recoverable as healthy and try it
                string recoverId = recoverablePlaceholders.First();
                MarkProviderHealthy(recoverId);
                healthyProviders.Add(recoverId);
            }
            else
            {
                return Result<ProviderSelectionResult<T>, string>.Failure(
                    "No healthy providers available. All providers are unhealthy or in cooldown.");
            }
        }

        // Select provider using the configured strategy
        string selectedId = _strategy.SelectProvider(healthyProviders, _healthStatus);

        if (!_providers.TryGetValue(selectedId, out T? provider))
        {
            return Result<ProviderSelectionResult<T>, string>.Failure(
                $"Selected provider '{selectedId}' not found in registry");
        }

        ProviderHealthStatus health = _healthStatus[selectedId];
        string reason = GenerateSelectionReason(selectedId, health);

        ProviderSelectionResult<T> result = new(
            Provider: provider,
            ProviderId: selectedId,
            Strategy: _strategy.Name,
            Reason: reason,
            Health: health);

        return await Task.FromResult(
            Result<ProviderSelectionResult<T>, string>.Success(result));
    }

    /// <inheritdoc/>
    public void RecordExecution(string providerId, double latencyMs, bool success, bool wasRateLimited = false)
    {
        if (!_healthStatus.TryGetValue(providerId, out ProviderHealthStatus? currentHealth))
            return;

        int newTotalRequests = currentHealth.TotalRequests + 1;
        int newSuccessfulRequests = currentHealth.SuccessfulRequests + (success ? 1 : 0);
        double newSuccessRate = newSuccessfulRequests / (double)newTotalRequests;

        // Update average latency with exponential moving average
        double alpha = 0.3; // Weight for new measurement
        double newAvgLatency = currentHealth.TotalRequests == 0
            ? latencyMs
            : (alpha * latencyMs) + ((1 - alpha) * currentHealth.AverageLatencyMs);

        int newConsecutiveFailures = success ? 0 : currentHealth.ConsecutiveFailures + 1;
        DateTime? newLastFailureTime = success ? currentHealth.LastFailureTime : DateTime.UtcNow;

        // Handle rate limiting
        DateTime? newCooldownUntil = currentHealth.CooldownUntil;
        if (wasRateLimited)
        {
            // Apply exponential backoff for repeated rate limits
            TimeSpan cooldown = CalculateCooldownDuration(currentHealth);
            newCooldownUntil = DateTime.UtcNow.Add(cooldown);
            Console.WriteLine($"[ProviderLoadBalancer] Provider '{providerId}' rate limited. Cooldown until {newCooldownUntil:HH:mm:ss}");
        }

        // Circuit breaker: mark unhealthy after consecutive failures
        bool newIsHealthy = currentHealth.IsHealthy;
        if (newConsecutiveFailures >= MaxConsecutiveFailures)
        {
            newIsHealthy = false;
            newCooldownUntil = DateTime.UtcNow.Add(DefaultCooldownDuration);
            Console.WriteLine($"[ProviderLoadBalancer] Provider '{providerId}' marked unhealthy after {newConsecutiveFailures} failures");
        }

        ProviderHealthStatus updatedHealth = new(
            ProviderId: providerId,
            IsHealthy: newIsHealthy,
            SuccessRate: newSuccessRate,
            AverageLatencyMs: newAvgLatency,
            ConsecutiveFailures: newConsecutiveFailures,
            LastFailureTime: newLastFailureTime,
            CooldownUntil: newCooldownUntil,
            TotalRequests: newTotalRequests,
            SuccessfulRequests: newSuccessfulRequests,
            LastChecked: DateTime.UtcNow);

        _healthStatus[providerId] = updatedHealth;
    }

    /// <inheritdoc/>
    public void MarkProviderUnhealthy(string providerId, TimeSpan? cooldownDuration = null)
    {
        if (!_healthStatus.TryGetValue(providerId, out ProviderHealthStatus? currentHealth))
            return;

        TimeSpan cooldown = cooldownDuration ?? DefaultCooldownDuration;
        ProviderHealthStatus updatedHealth = currentHealth with
        {
            IsHealthy = false,
            CooldownUntil = DateTime.UtcNow.Add(cooldown),
            LastChecked = DateTime.UtcNow
        };

        _healthStatus[providerId] = updatedHealth;
        Console.WriteLine($"[ProviderLoadBalancer] Provider '{providerId}' manually marked unhealthy");
    }

    /// <inheritdoc/>
    public void MarkProviderHealthy(string providerId)
    {
        if (!_healthStatus.TryGetValue(providerId, out ProviderHealthStatus? currentHealth))
            return;

        ProviderHealthStatus updatedHealth = currentHealth with
        {
            IsHealthy = true,
            ConsecutiveFailures = 0,
            CooldownUntil = null,
            LastChecked = DateTime.UtcNow
        };

        _healthStatus[providerId] = updatedHealth;
        Console.WriteLine($"[ProviderLoadBalancer] Provider '{providerId}' marked healthy");
    }

    private TimeSpan CalculateCooldownDuration(ProviderHealthStatus health)
    {
        // Exponential backoff based on how recently it was rate limited
        if (health.CooldownUntil.HasValue && health.CooldownUntil > DateTime.UtcNow)
        {
            // Already in cooldown, double the duration
            return TimeSpan.FromSeconds(120);
        }

        return DefaultCooldownDuration;
    }

    private string GenerateSelectionReason(string providerId, ProviderHealthStatus health)
    {
        return $"Strategy: {_strategy.Name}, Health Score: {health.HealthScore:F2}, " +
               $"Success Rate: {health.SuccessRate:P0}, Avg Latency: {health.AverageLatencyMs:F0}ms";
    }
}
