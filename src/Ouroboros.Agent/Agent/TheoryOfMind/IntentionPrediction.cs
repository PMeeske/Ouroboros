// <copyright file="IntentionPrediction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Represents a prediction of another agent's intention or goal.
/// Used to anticipate what the agent is trying to achieve.
/// </summary>
/// <param name="AgentId">The ID of the agent whose intention is predicted</param>
/// <param name="PredictedGoal">The predicted goal or intention</param>
/// <param name="Confidence">Confidence in the prediction (0.0 to 1.0)</param>
/// <param name="SupportingEvidence">Evidence supporting this prediction</param>
/// <param name="AlternativeGoals">Alternative possible goals</param>
public sealed record IntentionPrediction(
    string AgentId,
    string PredictedGoal,
    double Confidence,
    List<string> SupportingEvidence,
    List<string> AlternativeGoals)
{
    /// <summary>
    /// Creates a low-confidence prediction when insufficient data is available.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <returns>Low-confidence intention prediction</returns>
    public static IntentionPrediction Unknown(string agentId) => new(
        agentId,
        "Unknown - insufficient data",
        0.0,
        new List<string> { "Insufficient observation data" },
        new List<string>());

    /// <summary>
    /// Creates a prediction with high confidence.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <param name="goal">The predicted goal</param>
    /// <param name="confidence">Confidence level</param>
    /// <param name="evidence">Supporting evidence</param>
    /// <param name="alternatives">Alternative goals</param>
    /// <returns>Intention prediction</returns>
    public static IntentionPrediction Create(
        string agentId,
        string goal,
        double confidence,
        List<string>? evidence = null,
        List<string>? alternatives = null) => new(
        agentId,
        goal,
        Math.Clamp(confidence, 0.0, 1.0),
        evidence ?? new List<string>(),
        alternatives ?? new List<string>());
}
