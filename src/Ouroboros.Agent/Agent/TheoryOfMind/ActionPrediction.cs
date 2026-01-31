// <copyright file="ActionPrediction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Domain.Embodied;

namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Represents a prediction of another agent's next action.
/// Used to anticipate agent behavior in multi-agent scenarios.
/// </summary>
/// <param name="AgentId">The ID of the agent whose action is predicted</param>
/// <param name="PredictedAction">The predicted action</param>
/// <param name="Confidence">Confidence in the prediction (0.0 to 1.0)</param>
/// <param name="Reasoning">Explanation of why this action is predicted</param>
public sealed record ActionPrediction(
    string AgentId,
    EmbodiedAction PredictedAction,
    double Confidence,
    string Reasoning)
{
    /// <summary>
    /// Creates a no-op prediction when uncertain.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <param name="reason">Reason for uncertainty</param>
    /// <returns>No-op action prediction</returns>
    public static ActionPrediction NoOp(string agentId, string reason = "Insufficient data") => new(
        agentId,
        EmbodiedAction.NoOp(),
        0.0,
        reason);

    /// <summary>
    /// Creates an action prediction.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <param name="action">The predicted action</param>
    /// <param name="confidence">Confidence level</param>
    /// <param name="reasoning">Reasoning behind the prediction</param>
    /// <returns>Action prediction</returns>
    public static ActionPrediction Create(
        string agentId,
        EmbodiedAction action,
        double confidence,
        string reasoning) => new(
        agentId,
        action,
        Math.Clamp(confidence, 0.0, 1.0),
        reasoning);
}
