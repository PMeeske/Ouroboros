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

/// <summary>
/// Resilient reasoner that wraps neural-symbolic reasoning with circuit breaker pattern.
/// Automatically falls back to symbolic-only mode when LLM is unavailable.
/// </summary>
public sealed class ResilientReasoner : IReasoner
{
    private readonly INeuralSymbolicBridge _bridge;
    private readonly IChatCompletionModel? _llm;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly CircuitBreakerConfig _config;
    private readonly ILogger<ResilientReasoner>? _logger;
    
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
        
        _circuitBreaker = new CircuitBreaker(
            _config.FailureThreshold,
            _config.OpenDuration);
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
                _circuitBreaker.State,
                effectiveMode,
                preferredMode);
        }

        // Perform reasoning with the effective mode
        try
        {
            var result = await ReasonWithMode(query, effectiveMode, ct);
            
            if (result.IsSuccess)
            {
                if (UsesNeuralReasoning(effectiveMode))
                {
                    // Successful neural reasoning - record success
                    RecordNeuralSuccess();
                }
                return Result<string, string>.Success(result.Value.Answer);
            }
            else
            {
                // Result failed - handle based on mode
                if (UsesNeuralReasoning(effectiveMode))
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
        catch (Exception ex)
        {
            if (UsesNeuralReasoning(effectiveMode))
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
        return new ReasonerHealth(
            _circuitBreaker.State,
            SymbolicAvailable: true, // Symbolic reasoning is always available
            _consecutiveLlmFailures,
            _lastLlmSuccess);
    }

    private Core.Resilience.ReasoningMode DetermineEffectiveMode(Core.Resilience.ReasoningMode preferredMode)
    {
        // Check if circuit breaker allows neural reasoning
        if (!_circuitBreaker.ShouldAttempt())
        {
            // Circuit is open - force symbolic-only mode
            return Core.Resilience.ReasoningMode.SymbolicOnly;
        }

        // Circuit is closed or half-open - use preferred mode
        // But if circuit is half-open, we should prefer modes with fallback
        if (_circuitBreaker.State == CircuitState.HalfOpen)
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

    private async Task<Result<ReasoningResult, string>> ReasonWithMode(
        string query,
        Core.Resilience.ReasoningMode mode,
        CancellationToken ct)
    {
        // Convert Core.Resilience.ReasoningMode to NeuralSymbolic.ReasoningMode
        var bridgeMode = (NeuralSymbolic.ReasoningMode)(int)mode;
        
        // Add timeout for half-open state
        if (_circuitBreaker.State == CircuitState.HalfOpen && UsesNeuralReasoning(mode))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_config.HalfOpenTimeout);
            return await _bridge.HybridReasonAsync(query, bridgeMode, cts.Token);
        }

        return await _bridge.HybridReasonAsync(query, bridgeMode, ct);
    }

    private void RecordNeuralSuccess()
    {
        _circuitBreaker.RecordSuccess();
        _consecutiveLlmFailures = 0;
        _lastLlmSuccess = DateTimeOffset.UtcNow;
        
        _logger?.LogInformation(
            "Neural reasoning succeeded. Circuit state: {State}",
            _circuitBreaker.State);
    }

    private void RecordNeuralFailure()
    {
        _circuitBreaker.RecordFailure();
        _consecutiveLlmFailures++;
        
        _logger?.LogWarning(
            "Neural reasoning failed. Consecutive failures: {Failures}, Circuit state: {State}",
            _consecutiveLlmFailures,
            _circuitBreaker.State);
    }

    private static bool UsesNeuralReasoning(Core.Resilience.ReasoningMode mode)
    {
        return mode switch
        {
            Core.Resilience.ReasoningMode.NeuralOnly => true,
            Core.Resilience.ReasoningMode.NeuralFirst => true,
            Core.Resilience.ReasoningMode.Parallel => true,
            Core.Resilience.ReasoningMode.SymbolicFirst => true, // Can fall back to neural
            Core.Resilience.ReasoningMode.SymbolicOnly => false,
            _ => false
        };
    }
}
