// <copyright file="IDistinctionLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Interface for distinction-based learning following Laws of Form.
/// Learning = Making distinctions (∅ → ⌐)
/// Understanding = Recognition (i = ⌐, the subject IS the distinction)
/// Unlearning = Dissolution (principled forgetting, ⌐ → ∅)
/// Uncertainty = Imaginary state (Form.Imaginary for epistemic uncertainty)
/// </summary>
public interface IDistinctionLearner
{
    /// <summary>
    /// Updates the learning state from a new observation at a given dream stage.
    /// Advances through the consciousness cycle, making and refining distinctions.
    /// </summary>
    /// <param name="currentState">Current distinction learning state.</param>
    /// <param name="observation">New observation to learn from.</param>
    /// <param name="stage">Dream stage to process at.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated distinction state or error message.</returns>
    Task<Result<DistinctionState>> UpdateFromDistinctionAsync(
        DistinctionState currentState,
        Observation observation,
        DreamStage stage,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates how well a distinction fits the observed data.
    /// Higher fitness means the distinction successfully predicts/explains observations.
    /// </summary>
    /// <param name="distinction">The distinction to evaluate.</param>
    /// <param name="observations">Historical observations to evaluate against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Fitness score (0.0 to 1.0) or error message.</returns>
    Task<Result<double>> EvaluateDistinctionFitnessAsync(
        string distinction,
        List<Observation> observations,
        CancellationToken ct = default);

    /// <summary>
    /// Dissolves (forgets) distinctions according to a strategy.
    /// Principled forgetting: returning distinctions to void (⌐ → ∅).
    /// Prevents catastrophic forgetting by selective dissolution.
    /// </summary>
    /// <param name="state">Current distinction state.</param>
    /// <param name="strategy">Dissolution strategy to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>State with dissolved distinctions or error message.</returns>
    Task<Result<DistinctionState>> DissolveAsync(
        DistinctionState state,
        DissolutionStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Recognition stage: realizes "I am the distinction" (i = ⌐).
    /// Transforms understanding by merging self with observation.
    /// This is the moment of insight where the subject recognizes itself as the process.
    /// </summary>
    /// <param name="state">Current distinction state.</param>
    /// <param name="circumstance">The circumstance being recognized.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>State after recognition or error message.</returns>
    Task<Result<DistinctionState>> RecognizeAsync(
        DistinctionState state,
        string circumstance,
        CancellationToken ct = default);
}
