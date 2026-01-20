// <copyright file="IDistinctionLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for distinction learning from consciousness dream cycles.
/// </summary>
public interface IDistinctionLearner
{
    /// <summary>
    /// Updates state from a new distinction observation.
    /// </summary>
    Task<Result<DistinctionState, string>> UpdateFromDistinctionAsync(
        DistinctionState currentState,
        Observation observation,
        string stage,
        CancellationToken ct = default);

    /// <summary>
    /// Applies recognition to boost certainty of related distinctions.
    /// </summary>
    Task<Result<DistinctionState, string>> RecognizeAsync(
        DistinctionState currentState,
        string circumstance,
        CancellationToken ct = default);

    /// <summary>
    /// Dissolves distinctions based on strategy.
    /// </summary>
    Task<Result<Unit, string>> DissolveAsync(
        DistinctionState currentState,
        DissolutionStrategy strategy,
        CancellationToken ct = default);
}
