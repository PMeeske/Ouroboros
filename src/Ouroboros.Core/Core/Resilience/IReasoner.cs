// <copyright file="IReasoner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Resilience;

using Ouroboros.Core.Monads;

/// <summary>
/// Reasoning modes for hybrid neural-symbolic systems.
/// Duplicated here to avoid circular dependencies between Core and Agent layers.
/// </summary>
public enum ReasoningMode
{
    /// <summary>Try symbolic first, fall back to neural.</summary>
    SymbolicFirst = 0,

    /// <summary>Try neural first, fall back to symbolic.</summary>
    NeuralFirst = 1,

    /// <summary>Run both in parallel, combine results.</summary>
    Parallel = 2,

    /// <summary>Use only symbolic reasoning.</summary>
    SymbolicOnly = 3,

    /// <summary>Use only neural reasoning.</summary>
    NeuralOnly = 4
}

/// <summary>
/// Unified reasoning interface that abstracts over LLM and symbolic reasoning
/// with automatic fallback and circuit breaking.
/// </summary>
public interface IReasoner
{
    /// <summary>
    /// Performs reasoning with automatic fallback based on service health.
    /// </summary>
    /// <param name="query">The reasoning query to process.</param>
    /// <param name="preferredMode">The preferred reasoning mode to use. Default is NeuralFirst.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing either the reasoning response or an error message.</returns>
    Task<Result<string, string>> ReasonAsync(
        string query,
        ReasoningMode preferredMode = ReasoningMode.NeuralFirst,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current health status of the reasoning backends.
    /// </summary>
    /// <returns>Health information including circuit state and failure counts.</returns>
    ReasonerHealth GetHealth();
}

/// <summary>
/// Represents the health status of the reasoning system.
/// </summary>
/// <param name="LlmState">Current state of the LLM circuit breaker.</param>
/// <param name="SymbolicAvailable">Whether symbolic reasoning is available.</param>
/// <param name="ConsecutiveLlmFailures">Number of consecutive LLM failures.</param>
/// <param name="LastLlmSuccess">Timestamp of last successful LLM operation, if any.</param>
public sealed record ReasonerHealth(
    CircuitState LlmState,
    bool SymbolicAvailable,
    int ConsecutiveLlmFailures,
    DateTimeOffset? LastLlmSuccess);
