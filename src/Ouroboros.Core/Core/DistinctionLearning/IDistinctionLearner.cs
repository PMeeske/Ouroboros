// <copyright file="IDistinctionLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// Interface for distinction learning - learning from the consciousness dream cycle.
/// </summary>
public interface IDistinctionLearner
{
    /// <summary>
    /// Updates distinction state from a new observation at a specific dream stage.
    /// </summary>
    /// <param name="currentState">Current distinction state.</param>
    /// <param name="observation">The observation to learn from.</param>
    /// <param name="stage">The dream stage where learning occurs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated distinction state or error.</returns>
    Task<Result<DistinctionState, string>> UpdateFromDistinctionAsync(
        DistinctionState currentState,
        Observation observation,
        string stage,
        CancellationToken ct = default);

    /// <summary>
    /// Applies self-insight at the Recognition stage.
    /// </summary>
    /// <param name="currentState">Current distinction state.</param>
    /// <param name="circumstance">The circumstance being recognized.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated distinction state or error.</returns>
    Task<Result<DistinctionState, string>> RecognizeAsync(
        DistinctionState currentState,
        string circumstance,
        CancellationToken ct = default);

    /// <summary>
    /// Dissolves low-fitness distinctions using the specified strategy.
    /// </summary>
    /// <param name="currentState">Current distinction state.</param>
    /// <param name="strategy">Dissolution strategy to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<Unit, string>> DissolveAsync(
        DistinctionState currentState,
        DissolutionStrategy strategy,
        CancellationToken ct = default);
}
