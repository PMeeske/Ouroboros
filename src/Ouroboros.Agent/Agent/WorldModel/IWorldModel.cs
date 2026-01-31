// <copyright file="IWorldModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.WorldModel;

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// Interface for predictive world models enabling model-based planning.
/// Allows agents to simulate future states and plan action sequences.
/// </summary>
public interface IWorldModel
{
    /// <summary>
    /// Predicts next state given current state and action.
    /// </summary>
    /// <param name="currentState">Current sensor state</param>
    /// <param name="action">Action to simulate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing predicted state or error</returns>
    Task<Result<PredictedState, string>> PredictAsync(
        SensorState currentState,
        EmbodiedAction action,
        CancellationToken ct = default);

    /// <summary>
    /// Updates world model from observed transitions.
    /// </summary>
    /// <param name="transitions">List of observed state transitions</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or error</returns>
    Task<Result<Unit, string>> UpdateFromExperienceAsync(
        IReadOnlyList<EmbodiedTransition> transitions,
        CancellationToken ct = default);

    /// <summary>
    /// Plans action sequence using model-based search (e.g., MCTS, beam search).
    /// </summary>
    /// <param name="current">Current sensor state</param>
    /// <param name="goal">Goal description</param>
    /// <param name="horizon">Planning horizon (number of steps)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing planned action sequence or error</returns>
    Task<Result<List<EmbodiedAction>, string>> PlanWithModelAsync(
        SensorState current,
        string goal,
        int horizon = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Returns model uncertainty/epistemic confidence for given state-action.
    /// </summary>
    /// <param name="state">Current state</param>
    /// <param name="action">Action to evaluate</param>
    /// <returns>Uncertainty value (0-1, where 1 is high uncertainty)</returns>
    Task<double> GetUncertaintyAsync(SensorState state, EmbodiedAction action);

    /// <summary>
    /// Simulates a full trajectory given initial state and action sequence.
    /// </summary>
    /// <param name="initialState">Initial sensor state</param>
    /// <param name="actions">Sequence of actions to simulate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing predicted states or error</returns>
    Task<Result<List<PredictedState>, string>> SimulateTrajectoryAsync(
        SensorState initialState,
        IReadOnlyList<EmbodiedAction> actions,
        CancellationToken ct = default);
}
