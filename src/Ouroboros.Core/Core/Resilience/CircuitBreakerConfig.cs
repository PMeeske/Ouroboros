// <copyright file="CircuitBreakerConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Resilience;

/// <summary>
/// Configuration for circuit breaker behavior.
/// </summary>
public sealed record CircuitBreakerConfig
{
    /// <summary>
    /// Gets the number of consecutive failures before opening the circuit.
    /// Default is 3.
    /// </summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>
    /// Gets the duration to keep the circuit open before transitioning to half-open.
    /// Default is 2 minutes.
    /// </summary>
    public TimeSpan OpenDuration { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets the timeout for operations in half-open state.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan HalfOpenTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
