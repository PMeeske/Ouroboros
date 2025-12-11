// <copyright file="IPolicy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Core.Monads;
using LangChainPipeline.Domain.Environment;

namespace LangChainPipeline.Domain.Reinforcement;

/// <summary>
/// Interface for reinforcement learning policies.
/// Selects actions based on environment state.
/// </summary>
public interface IPolicy
{
    /// <summary>
    /// Selects an action based on the current state and available actions.
    /// </summary>
    /// <param name="state">The current environment state</param>
    /// <param name="availableActions">List of available actions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the selected action</returns>
    ValueTask<Result<EnvironmentAction>> SelectActionAsync(
        EnvironmentState state,
        IReadOnlyList<EnvironmentAction> availableActions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the policy based on observed outcomes.
    /// </summary>
    /// <param name="state">The state where the action was taken</param>
    /// <param name="action">The action that was taken</param>
    /// <param name="observation">The observation received</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    ValueTask<Result<Unit>> UpdateAsync(
        EnvironmentState state,
        EnvironmentAction action,
        Observation observation,
        CancellationToken cancellationToken = default);
}
