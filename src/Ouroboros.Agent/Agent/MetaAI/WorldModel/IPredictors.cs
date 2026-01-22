// <copyright file="IPredictors.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Interface for predicting next states given current state and action.
/// Follows functional programming principles with async operations.
/// </summary>
public interface IStatePredictor
{
    /// <summary>
    /// Predicts the next state given current state and action.
    /// </summary>
    /// <param name="current">The current state.</param>
    /// <param name="action">The action to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The predicted next state.</returns>
    Task<State> PredictAsync(State current, Action action, CancellationToken ct = default);
}

/// <summary>
/// Interface for predicting rewards for state-action-next state transitions.
/// Follows functional programming principles with async operations.
/// </summary>
public interface IRewardPredictor
{
    /// <summary>
    /// Predicts the reward for a transition.
    /// </summary>
    /// <param name="current">The current state.</param>
    /// <param name="action">The action taken.</param>
    /// <param name="next">The resulting next state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The predicted reward value.</returns>
    Task<double> PredictAsync(State current, Action action, State next, CancellationToken ct = default);
}

/// <summary>
/// Interface for predicting whether a state is terminal (episode ending).
/// Follows functional programming principles with async operations.
/// </summary>
public interface ITerminalPredictor
{
    /// <summary>
    /// Predicts whether the given state is terminal.
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the state is terminal, false otherwise.</returns>
    Task<bool> PredictAsync(State state, CancellationToken ct = default);
}
