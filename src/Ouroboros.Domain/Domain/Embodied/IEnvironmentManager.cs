// <copyright file="IEnvironmentManager.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Interface for managing embodied simulation environments.
/// Handles environment lifecycle (creation, reset, destruction) and discovery.
/// </summary>
public interface IEnvironmentManager
{
    /// <summary>
    /// Creates a new environment instance based on the provided configuration.
    /// </summary>
    /// <param name="config">Environment configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the environment handle or error message</returns>
    Task<Result<EnvironmentHandle, string>> CreateEnvironmentAsync(
        EnvironmentConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Resets an environment to its initial state.
    /// </summary>
    /// <param name="handle">Handle to the environment to reset</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with error message</returns>
    Task<Result<Unit, string>> ResetEnvironmentAsync(
        EnvironmentHandle handle,
        CancellationToken ct = default);

    /// <summary>
    /// Destroys an environment instance and releases resources.
    /// </summary>
    /// <param name="handle">Handle to the environment to destroy</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with error message</returns>
    Task<Result<Unit, string>> DestroyEnvironmentAsync(
        EnvironmentHandle handle,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all available environments that can be created.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing list of available environments or error message</returns>
    Task<Result<IReadOnlyList<EnvironmentInfo>, string>> ListAvailableEnvironmentsAsync(
        CancellationToken ct = default);
}
