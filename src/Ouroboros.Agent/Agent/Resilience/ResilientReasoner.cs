// <copyright file="ResilientReasoner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.Agent.Resilience;

using Microsoft.Extensions.Logging;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Resilience;
using Ouroboros.Providers;
using Polly;
using Polly.CircuitBreaker;

/// <summary>
/// Resilient reasoner that wraps neural-symbolic reasoning with Polly circuit breaker pattern.
/// Automatically falls back to symbolic-only mode when LLM is unavailable.
/// </summary>
public sealed class ResilientReasoner : IReasoner
{
    private readonly INeuralSymbolicBridge _bridge;
    private readonly IChatCompletionModel? _llm;
    private readonly AsyncCircuitBreakerPolicy<Result<ReasoningResult, string>> _circuitBreakerPolicy;
    private readonly CircuitBreakerConfig _config;
    private readonly ILogger<ResilientReasoner>? _logger;
    private readonly object _statsLock = new();
    
    private int _consecutiveLlmFailures;
    private DateTimeOffset? _lastLlmSuccess;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientReasoner"/> class.
    /// </summary>
    /// <param name="bridge">The neural-symbolic bridge for reasoning operations.</param>
    /// <param name="llm">Optional LLM for direct reasoning (used for health monitoring).</param>
    /// <param name="config">Optional circuit breaker configuration.</param>
    /// <param name="logger">Optional logger for monitoring state changes.</param>
    public ResilientReasoner(
        INeuralSymbolicBridge bridge,
        IChatCompletionModel? llm = null,
        CircuitBreakerConfig? config = null,
        ILogger<ResilientReasoner>? logger = null)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _llm = llm;
        _config = config ?? new CircuitBreakerConfig();
        _logger = logger;
        
        // Create Polly circuit breaker policy
        _circuitBreakerPolicy = Policy
            .HandleResult<Result<ReasoningResult, string>>(r => r.IsFailure)
            .Or<Exception>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: _config.FailureThreshold,
                durationOfBreak: _config.OpenDuration,
                onBreak: (outcome, breakDelay) =>
                {
                    lock (_statsLock)
                    {
                        _consecutiveLlmFailures++;
                    }
                    _logger?.LogWarning(
                        "Circuit breaker OPENED after {Failures} failures. Will retry after {Delay}s. Switching to symbolic-only mode.",
                        _config.FailureThreshold,
                        breakDelay.TotalSeconds);
                },
                onReset: () =>
                {
                    lock (_statsLock)
                    {
                        _consecutiveLlmFailures = 0;
                        _lastLlmSuccess = DateTimeOffset.UtcNow;
                    }
                    _logger?.LogInformation("Circuit breaker CLOSED - neural reasoning restored");
                },
                onHalfOpen: () =>
                {
                    _logger?.LogInformation("Circuit breaker HALF-OPEN - testing neural reasoning recovery");
                });
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> ReasonAsync(
        string query,
        Core.Resilience.ReasoningMode preferredMode = Core.Resilience.ReasoningMode.NeuralFirst,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<string, string>.Failure("Query cannot be empty");
        }

        // Determine the actual mode to use based on circuit breaker state
        var effectiveMode = DetermineEffectiveMode(preferredMode);
        
        if (effectiveMode != preferredMode)
        {
            _logger?.LogWarning(
                "Circuit breaker is {State}, forcing {EffectiveMode} instead of {PreferredMode}",
                GetCircuitState(),
                effectiveMode,
                preferredMode);
        }

        // Perform reasoning with the effective mode
        try
        {
            var result = await ExecuteWithCircuitBreaker(query, effectiveMode, ct);
            
            if (result.IsSuccess)
            {
                // Check if neural reasoning was actually used based on the result
                if (result.Value.NeuralSucceeded)
                {
                    RecordNeuralSuccess();
                }
                return Result<string, string>.Success(result.Value.Answer);
            }
            else
            {
                // Result failed - handle based on what actually happened
                // For modes that attempted neural reasoning, record failure
                if (effectiveMode == Core.Resilience.ReasoningMode.NeuralOnly || 
                    effectiveMode == Core.Resilience.ReasoningMode.NeuralFirst ||
                    effectiveMode == Core.Resilience.ReasoningMode.Parallel)
                {
                    RecordNeuralFailure();
                }
                
                // If neural failed and we haven't tried symbolic yet, fall back
                if (effectiveMode != Core.Resilience.ReasoningMode.SymbolicOnly && 
                    (effectiveMode == Core.Resilience.ReasoningMode.NeuralOnly || effectiveMode == Core.Resilience.ReasoningMode.NeuralFirst))
                {
                    _logger?.LogWarning("Neural reasoning failed with error: {Error}, falling back to symbolic-only mode", result.Error);
                    
                    var fallbackResult = await ReasonWithMode(query, Core.Resilience.ReasoningMode.SymbolicOnly, ct);
                    if (fallbackResult.IsSuccess)
                    {
                        return Result<string, string>.Success(fallbackResult.Value.Answer);
                    }
                    else
                    {
                        return Result<string, string>.Failure($"Both neural and symbolic reasoning failed. Neural error: {result.Error}, Symbolic error: {fallbackResult.Error}");
                    }
                }
                
                return Result<string, string>.Failure(result.Error);
            }
        }
        catch (BrokenCircuitException)
        {
            // Circuit is open - fall back to symbolic only
            _logger?.LogWarning("Circuit is open, using symbolic-only mode");
            
            try
            {
                var fallbackResult = await ReasonWithMode(query, Core.Resilience.ReasoningMode.SymbolicOnly, ct);
                return fallbackResult.IsSuccess
                    ? Result<string, string>.Success(fallbackResult.Value.Answer)
                    : Result<string, string>.Failure($"Neural reasoning unavailable (circuit open), symbolic fallback failed: {fallbackResult.Error}");
            }
            catch (Exception fallbackEx)
            {
                return Result<string, string>.Failure($"Neural reasoning unavailable (circuit open), symbolic fallback exception: {fallbackEx.Message}");
            }
        }
        catch (Exception ex)
        {
            // Exception thrown - record failure for neural modes
            if (effectiveMode == Core.Resilience.ReasoningMode.NeuralOnly || 
                effectiveMode == Core.Resilience.ReasoningMode.NeuralFirst ||
                effectiveMode == Core.Resilience.ReasoningMode.Parallel)
            {
                RecordNeuralFailure();
            }
            
            // If neural failed and we haven't tried symbolic yet, fall back
            if (effectiveMode != Core.Resilience.ReasoningMode.SymbolicOnly && 
                (effectiveMode == Core.Resilience.ReasoningMode.NeuralOnly || effectiveMode == Core.Resilience.ReasoningMode.NeuralFirst))
            {
                _logger?.LogWarning(ex, "Neural reasoning threw exception, falling back to symbolic-only mode");
                
                try
                {
                    var fallbackResult = await ReasonWithMode(query, Core.Resilience.ReasoningMode.SymbolicOnly, ct);
                    return fallbackResult.IsSuccess
                        ? Result<string, string>.Success(fallbackResult.Value.Answer)
                        : Result<string, string>.Failure($"Both neural and symbolic reasoning failed. Neural error: {ex.Message}, Symbolic error: {fallbackResult.Error}");
                }
                catch (Exception fallbackEx)
                {
                    return Result<string, string>.Failure($"Both neural and symbolic reasoning failed. Neural error: {ex.Message}, Symbolic error: {fallbackEx.Message}");
                }
            }
            
            return Result<string, string>.Failure($"Reasoning failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public ReasonerHealth GetHealth()
    {
        lock (_statsLock)
        {
            return new ReasonerHealth(
                GetCircuitState(),
                SymbolicAvailable: true, // Symbolic reasoning is always available
                _consecutiveLlmFailures,
                _lastLlmSuccess);
        }
    }

    private Core.Resilience.ReasoningMode DetermineEffectiveMode(Core.Resilience.ReasoningMode preferredMode)
    {
        // Check if circuit breaker allows neural reasoning
        var circuitState = GetCircuitState();
        if (circuitState == "Open")
        {
            // Circuit is open - force symbolic-only mode
            return Core.Resilience.ReasoningMode.SymbolicOnly;
        }

        // Circuit is closed or half-open - use preferred mode
        // But if circuit is half-open, we should prefer modes with fallback
        if (circuitState == "HalfOpen")
        {
            // In half-open state, prefer modes with fallback
            return preferredMode switch
            {
                Core.Resilience.ReasoningMode.NeuralOnly => Core.Resilience.ReasoningMode.NeuralFirst,
                _ => preferredMode
            };
        }

        return preferredMode;
    }

    private async Task<Result<ReasoningResult, string>> ExecuteWithCircuitBreaker(
        string query,
        Core.Resilience.ReasoningMode mode,
        CancellationToken ct)
    {
        // Only use circuit breaker for modes that involve neural reasoning
        if (mode == Core.Resilience.ReasoningMode.SymbolicOnly)
        {
            return await ReasonWithMode(query, mode, ct);
        }

        // Execute with circuit breaker for neural modes
        return await _circuitBreakerPolicy.ExecuteAsync(async () =>
        {
            var result = await ReasonWithMode(query, mode, ct);
            
            // Add timeout for half-open state
            if (GetCircuitState() == "HalfOpen" && UsesNeuralReasoning(mode))
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_config.HalfOpenTimeout);
                return await ReasonWithMode(query, mode, cts.Token);
            }
            
            return result;
        });
    }

    private async Task<Result<ReasoningResult, string>> ReasonWithMode(
        string query,
        Core.Resilience.ReasoningMode mode,
        CancellationToken ct)
    {
        // SAFETY: This cast relies on Core.Resilience.ReasoningMode and NeuralSymbolic.ReasoningMode
        // having identical integer values. See documentation in IReasoner.cs for sync requirements.
        // A unit test in ResilientReasonerTests verifies enum alignment.
        var bridgeMode = (NeuralSymbolic.ReasoningMode)(int)mode;
        
        return await _bridge.HybridReasonAsync(query, bridgeMode, ct);
    }

    private string GetCircuitState()
    {
        try
        {
            var state = _circuitBreakerPolicy.CircuitState;
            return state switch
            {
                Polly.CircuitBreaker.CircuitState.Closed => "Closed",
                Polly.CircuitBreaker.CircuitState.Open => "Open",
                Polly.CircuitBreaker.CircuitState.HalfOpen => "HalfOpen",
                Polly.CircuitBreaker.CircuitState.Isolated => "Isolated",
                _ => "Unknown"
            };
        }
        catch
        {
            return "Unknown";
        }
    }

    private void RecordNeuralSuccess()
    {
        lock (_statsLock)
        {
            _consecutiveLlmFailures = 0;
            _lastLlmSuccess = DateTimeOffset.UtcNow;
        }
        
        _logger?.LogInformation(
            "Neural reasoning succeeded. Circuit state: {State}",
            GetCircuitState());
    }

    private void RecordNeuralFailure()
    {
        int failures;
        lock (_statsLock)
        {
            _consecutiveLlmFailures++;
            failures = _consecutiveLlmFailures;
        }
        
        _logger?.LogWarning(
            "Neural reasoning failed. Consecutive failures: {Failures}, Circuit state: {State}",
            failures,
            GetCircuitState());
    }

    /// <summary>
    /// Determines if a reasoning mode may involve neural (LLM) reasoning.
    /// Used to decide whether to apply half-open state timeout.
    /// Note: For success/failure tracking, check ReasoningResult.NeuralSucceeded instead.
    /// </summary>
    private static bool UsesNeuralReasoning(Core.Resilience.ReasoningMode mode)
    {
        return mode switch
        {
            Core.Resilience.ReasoningMode.NeuralOnly => true,
            Core.Resilience.ReasoningMode.NeuralFirst => true,
            Core.Resilience.ReasoningMode.Parallel => true,
            Core.Resilience.ReasoningMode.SymbolicFirst => true, // May use neural as fallback
            Core.Resilience.ReasoningMode.SymbolicOnly => false,
            _ => false
        };
    }
}
