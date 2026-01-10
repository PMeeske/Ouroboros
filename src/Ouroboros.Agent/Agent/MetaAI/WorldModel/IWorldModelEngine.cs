// <copyright file="IWorldModelEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

using Ouroboros.Core.Monads;

/// <summary>
/// Main interface for world model learning and imagination-based planning.
/// Enables model-based reinforcement learning through learned environment models.
/// All operations return Result types for robust error handling.
/// </summary>
public interface IWorldModelEngine
{
    /// <summary>
    /// Learns a world model from experience data (transitions).
    /// </summary>
    /// <param name="transitions">List of observed transitions from the environment.</param>
    /// <param name="architecture">The model architecture to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the learned world model or error message.</returns>
    Task<Result<WorldModel, string>> LearnModelAsync(
        List<Transition> transitions,
        ModelArchitecture architecture,
        CancellationToken ct = default);

    /// <summary>
    /// Predicts the next state given current state and action using the learned model.
    /// </summary>
    /// <param name="currentState">The current state.</param>
    /// <param name="action">The action to simulate.</param>
    /// <param name="model">The world model to use for prediction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the predicted next state or error message.</returns>
    Task<Result<State, string>> PredictNextStateAsync(
        State currentState,
        Action action,
        WorldModel model,
        CancellationToken ct = default);

    /// <summary>
    /// Plans a sequence of actions using imagination (model-based planning).
    /// Uses techniques like Monte Carlo Tree Search with the world model.
    /// </summary>
    /// <param name="initialState">The starting state for planning.</param>
    /// <param name="goal">Natural language description of the goal.</param>
    /// <param name="model">The world model to use for imagination.</param>
    /// <param name="lookaheadDepth">How many steps to look ahead.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the planned action sequence or error message.</returns>
    Task<Result<Plan, string>> PlanInImaginationAsync(
        State initialState,
        string goal,
        WorldModel model,
        int lookaheadDepth,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates the quality of a world model against a test set.
    /// </summary>
    /// <param name="model">The world model to evaluate.</param>
    /// <param name="testSet">Test transitions for evaluation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing quality metrics or error message.</returns>
    Task<Result<ModelQuality, string>> EvaluateModelAsync(
        WorldModel model,
        List<Transition> testSet,
        CancellationToken ct = default);

    /// <summary>
    /// Generates synthetic experience by rolling out trajectories in the learned model.
    /// Useful for data augmentation in model-based RL.
    /// </summary>
    /// <param name="model">The world model to use.</param>
    /// <param name="startState">The starting state for trajectory generation.</param>
    /// <param name="trajectoryLength">Number of steps to generate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing synthetic transitions or error message.</returns>
    Task<Result<List<Transition>, string>> GenerateSyntheticExperienceAsync(
        WorldModel model,
        State startState,
        int trajectoryLength,
        CancellationToken ct = default);
}
