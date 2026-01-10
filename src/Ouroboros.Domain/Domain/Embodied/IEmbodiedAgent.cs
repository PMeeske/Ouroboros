// <copyright file="IEmbodiedAgent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Interface for embodied agents that can perceive, act, learn, and plan in simulated environments.
/// Grounds cognition in sensorimotor experience for physical reasoning.
/// </summary>
public interface IEmbodiedAgent
{
    /// <summary>
    /// Initializes the agent in the specified environment.
    /// </summary>
    /// <param name="environment">Environment configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with error message</returns>
    Task<Result<Unit, string>> InitializeInEnvironmentAsync(
        EnvironmentConfig environment,
        CancellationToken ct = default);

    /// <summary>
    /// Perceives the current state from sensors.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the current sensor state or error message</returns>
    Task<Result<SensorState, string>> PerceiveAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Executes an embodied action in the environment.
    /// </summary>
    /// <param name="action">Action to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the action result or error message</returns>
    Task<Result<ActionResult, string>> ActAsync(
        EmbodiedAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Learns from a batch of embodied transitions using reinforcement learning.
    /// </summary>
    /// <param name="transitions">List of state-action-reward transitions</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with error message</returns>
    Task<Result<Unit, string>> LearnFromExperienceAsync(
        IReadOnlyList<EmbodiedTransition> transitions,
        CancellationToken ct = default);

    /// <summary>
    /// Plans a sequence of embodied actions to achieve a goal based on current state.
    /// Integrates with world model for model-based planning.
    /// </summary>
    /// <param name="goal">Natural language goal description</param>
    /// <param name="currentState">Current sensor state</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the plan or error message</returns>
    Task<Result<Plan, string>> PlanEmbodiedAsync(
        string goal,
        SensorState currentState,
        CancellationToken ct = default);
}
