// <copyright file="WorldModelTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Represents a transition in an environment - a complete experience tuple.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="PreviousState">The state before the action was taken.</param>
/// <param name="ActionTaken">The action that was executed.</param>
/// <param name="NextState">The resulting state after the action.</param>
/// <param name="Reward">The reward received for this transition.</param>
/// <param name="Terminal">Whether the next state is terminal.</param>
public sealed record Transition(
    State PreviousState,
    Action ActionTaken,
    State NextState,
    double Reward,
    bool Terminal);

/// <summary>
/// Represents a state in the environment with features and embedding.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Features">Dictionary of named features for the state.</param>
/// <param name="Embedding">Vector embedding representation of the state.</param>
public sealed record State(
    Dictionary<string, object> Features,
    float[] Embedding);

/// <summary>
/// Represents an action that can be taken in the environment.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Name">The name/type of the action.</param>
/// <param name="Parameters">Dictionary of parameters for the action.</param>
public sealed record Action(
    string Name,
    Dictionary<string, object> Parameters);

/// <summary>
/// Represents a learned world model for an environment domain.
/// Contains transition, reward, and terminal predictors.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Id">Unique identifier for this model.</param>
/// <param name="Domain">The domain/environment this model represents.</param>
/// <param name="TransitionModel">Predictor for next state transitions.</param>
/// <param name="RewardModel">Predictor for rewards.</param>
/// <param name="TerminalModel">Predictor for terminal states.</param>
/// <param name="Hyperparameters">Model hyperparameters and configuration.</param>
public sealed record WorldModel(
    Guid Id,
    string Domain,
    IStatePredictor TransitionModel,
    IRewardPredictor RewardModel,
    ITerminalPredictor TerminalModel,
    Dictionary<string, object> Hyperparameters);

/// <summary>
/// Represents quality metrics for a world model.
/// Used to evaluate model accuracy and calibration.
/// </summary>
/// <param name="PredictionAccuracy">Accuracy of state predictions (0-1).</param>
/// <param name="RewardCorrelation">Correlation of predicted vs actual rewards (0-1).</param>
/// <param name="TerminalAccuracy">Accuracy of terminal state predictions (0-1).</param>
/// <param name="CalibrationError">Mean calibration error for uncertainty estimates.</param>
/// <param name="TestSamples">Number of samples used in evaluation.</param>
public sealed record ModelQuality(
    double PredictionAccuracy,
    double RewardCorrelation,
    double TerminalAccuracy,
    double CalibrationError,
    int TestSamples);

/// <summary>
/// Represents a plan generated through imagination-based planning.
/// Integrates with existing Plan type in Ouroboros.
/// </summary>
/// <param name="Description">Description of the plan.</param>
/// <param name="Actions">Sequence of actions to execute.</param>
/// <param name="ExpectedReward">Expected cumulative reward.</param>
/// <param name="Confidence">Confidence in the plan (0-1).</param>
public sealed record Plan(
    string Description,
    List<Action> Actions,
    double ExpectedReward,
    double Confidence);

/// <summary>
/// Supported model architectures for world model learning.
/// </summary>
public enum ModelArchitecture
{
    /// <summary>
    /// Multi-layer perceptron (simple feed-forward network).
    /// </summary>
    MLP,

    /// <summary>
    /// Transformer-based architecture with attention mechanisms.
    /// </summary>
    Transformer,

    /// <summary>
    /// Graph neural network for structured state spaces.
    /// </summary>
    GNN,

    /// <summary>
    /// Hybrid architecture combining multiple approaches.
    /// </summary>
    Hybrid,
}
