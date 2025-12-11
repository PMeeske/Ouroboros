// <copyright file="IEnvironmentActor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Core.Monads;

namespace LangChainPipeline.Domain.Environment;

/// <summary>
/// Interface for actors that can interact with environments.
/// Enables embodiment for closed-loop learning.
/// </summary>
public interface IEnvironmentActor
{
    /// <summary>
    /// Gets the current state of the environment.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the current environment state</returns>
    ValueTask<Result<EnvironmentState>> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action in the environment and returns the observation.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the observation from the environment</returns>
    ValueTask<Result<Observation>> ExecuteActionAsync(
        EnvironmentAction action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the environment to its initial state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the initial state</returns>
    ValueTask<Result<EnvironmentState>> ResetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available actions in the current state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result containing the list of available actions</returns>
    ValueTask<Result<IReadOnlyList<EnvironmentAction>>> GetAvailableActionsAsync(
        CancellationToken cancellationToken = default);
}
