// <copyright file="CircuitBreaker.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Resilience;

/// <summary>
/// Implements the circuit breaker pattern for external service resilience.
/// States: Closed (normal) → Open (failing, reject calls) → HalfOpen (testing recovery).
/// </summary>
public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly object _lock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTimeOffset _lastStateChange;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreaker"/> class.
    /// </summary>
    /// <param name="failureThreshold">Number of failures before opening the circuit. Default is 3.</param>
    /// <param name="openDuration">How long to stay open before transitioning to half-open. Default is 2 minutes.</param>
    public CircuitBreaker(int failureThreshold = 3, TimeSpan? openDuration = null)
    {
        if (failureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be greater than zero");
        }

        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromMinutes(2);
        _lastStateChange = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the circuit is open (rejecting calls).
    /// </summary>
    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return _state == CircuitState.Open;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the circuit is closed (normal operation).
    /// </summary>
    public bool IsClosed
    {
        get
        {
            lock (_lock)
            {
                return _state == CircuitState.Closed;
            }
        }
    }

    /// <summary>
    /// Records a successful operation.
    /// Resets failure count and transitions from HalfOpen to Closed if applicable.
    /// </summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;

            if (_state == CircuitState.HalfOpen)
            {
                TransitionTo(CircuitState.Closed);
            }
        }
    }

    /// <summary>
    /// Records a failed operation.
    /// Increments failure count and transitions to Open if threshold is reached.
    /// If already in HalfOpen state, transitions back to Open.
    /// </summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;

            if (_state == CircuitState.HalfOpen)
            {
                // Failed during recovery attempt - back to Open
                TransitionTo(CircuitState.Open);
            }
            else if (_state == CircuitState.Closed && _failureCount >= _failureThreshold)
            {
                // Threshold reached - open the circuit
                TransitionTo(CircuitState.Open);
            }
        }
    }

    /// <summary>
    /// Checks if a call should be attempted.
    /// Returns true for Closed and HalfOpen states.
    /// For Open state, checks if openDuration has elapsed and transitions to HalfOpen if so.
    /// </summary>
    /// <returns>True if the call should be attempted; false otherwise.</returns>
    public bool ShouldAttempt()
    {
        lock (_lock)
        {
            if (_state == CircuitState.Closed || _state == CircuitState.HalfOpen)
            {
                return true;
            }

            // State is Open - check if we should transition to HalfOpen
            var elapsed = DateTimeOffset.UtcNow - _lastStateChange;
            if (elapsed >= _openDuration)
            {
                TransitionTo(CircuitState.HalfOpen);
                return true;
            }

            return false;
        }
    }

    private void TransitionTo(CircuitState newState)
    {
        _state = newState;
        _lastStateChange = DateTimeOffset.UtcNow;

        if (newState == CircuitState.Closed)
        {
            _failureCount = 0;
        }
    }
}

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Normal operation - calls are allowed.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - calls are rejected to prevent cascading failures.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is testing recovery - limited calls are allowed.
    /// </summary>
    HalfOpen
}
