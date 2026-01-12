// <copyright file="IDistinctionLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for the Distinction Learning system.
/// Orchestrates the learning process based on observations and dream stages.
/// </summary>
public interface IDistinctionLearner
{
    /// <summary>
    /// Updates the distinction state based on a new observation.
    /// Trains PEFT weights and updates the state accordingly.
    /// </summary>
    /// <param name="currentState">The current distinction state.</param>
    /// <param name="observation">The new observation to learn from.</param>
    /// <param name="stage">The current dream stage.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated state or error message.</returns>
    Task<Result<DistinctionState, string>> UpdateFromDistinctionAsync(
        DistinctionState currentState,
        Observation observation,
        DreamStage stage,
        CancellationToken ct = default);

    /// <summary>
    /// Performs recognition merge - subject becomes the distinction (i = ⌐).
    /// This is the key operation in the Recognition stage.
    /// </summary>
    /// <param name="state">The current distinction state.</param>
    /// <param name="circumstance">The circumstance triggering recognition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated state or error message.</returns>
    Task<Result<DistinctionState, string>> RecognizeAsync(
        DistinctionState state,
        string circumstance,
        CancellationToken ct = default);

    /// <summary>
    /// Performs dissolution - removes low-fitness distinctions.
    /// Used during the Dissolution stage.
    /// </summary>
    /// <param name="state">The current distinction state.</param>
    /// <param name="fitnessThreshold">Fitness threshold for dissolution. Default: 0.3.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated state or error message.</returns>
    Task<Result<DistinctionState, string>> DissolveAsync(
        DistinctionState state,
        double fitnessThreshold = 0.3,
        CancellationToken ct = default);
}
