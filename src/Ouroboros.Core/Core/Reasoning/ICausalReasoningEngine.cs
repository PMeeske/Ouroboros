// <copyright file="ICausalReasoningEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Reasoning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for causal reasoning engine implementing Pearl's causal inference framework.
/// Supports causal discovery, do-calculus, counterfactual reasoning, and intervention planning.
/// </summary>
public interface ICausalReasoningEngine
{
    /// <summary>
    /// Discovers the causal structure from observational data using the specified algorithm.
    /// Implements constraint-based or score-based causal discovery methods.
    /// </summary>
    /// <param name="data">List of observations to learn from.</param>
    /// <param name="algorithm">The algorithm to use for causal discovery.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the discovered causal graph or an error message.</returns>
    Task<Result<CausalGraph, string>> DiscoverCausalStructureAsync(
        List<Observation> data,
        DiscoveryAlgorithm algorithm,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates the effect of an intervention using do-calculus.
    /// Computes P(outcome | do(intervention)) using the causal model.
    /// </summary>
    /// <param name="intervention">The intervention variable and value.</param>
    /// <param name="outcome">The outcome variable to measure.</param>
    /// <param name="model">The causal graph model.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the estimated intervention effect or an error message.</returns>
    Task<Result<double, string>> EstimateInterventionEffectAsync(
        string intervention,
        string outcome,
        CausalGraph model,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates counterfactual outcomes using the twin network approach.
    /// Computes what would have happened if an intervention was made, given the factual observation.
    /// </summary>
    /// <param name="intervention">The counterfactual intervention to consider.</param>
    /// <param name="outcome">The outcome variable to estimate.</param>
    /// <param name="factual">The actual observed values.</param>
    /// <param name="model">The causal graph model.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the counterfactual distribution or an error message.</returns>
    Task<Result<Distribution, string>> EstimateCounterfactualAsync(
        string intervention,
        string outcome,
        Observation factual,
        CausalGraph model,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a causal explanation for an observed effect.
    /// Identifies the causal paths and computes attribution scores for potential causes.
    /// </summary>
    /// <param name="effect">The effect to explain.</param>
    /// <param name="possibleCauses">List of potential cause variables.</param>
    /// <param name="model">The causal graph model.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the causal explanation or an error message.</returns>
    Task<Result<Explanation, string>> ExplainCausallyAsync(
        string effect,
        List<string> possibleCauses,
        CausalGraph model,
        CancellationToken ct = default);

    /// <summary>
    /// Plans an optimal intervention to achieve a desired outcome.
    /// Considers constraints on controllable variables and side effects.
    /// </summary>
    /// <param name="desiredOutcome">The desired outcome and target value.</param>
    /// <param name="model">The causal graph model.</param>
    /// <param name="controllableVariables">List of variables that can be intervened on.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the planned intervention or an error message.</returns>
    Task<Result<Intervention, string>> PlanInterventionAsync(
        string desiredOutcome,
        CausalGraph model,
        List<string> controllableVariables,
        CancellationToken ct = default);
}
