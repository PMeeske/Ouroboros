// <copyright file="AgentObservation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Represents an observation of another agent's behavior.
/// Used to build and update Theory of Mind models.
/// </summary>
/// <param name="AgentId">The ID of the observed agent</param>
/// <param name="ObservationType">Type of observation ("action", "statement", "state_change")</param>
/// <param name="Content">The content of the observation</param>
/// <param name="Context">Additional contextual information</param>
/// <param name="ObservedAt">When the observation was made</param>
public sealed record AgentObservation(
    string AgentId,
    string ObservationType,
    string Content,
    Dictionary<string, object> Context,
    DateTime ObservedAt)
{
    /// <summary>
    /// Creates an action observation.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <param name="actionDescription">Description of the action</param>
    /// <param name="context">Optional context</param>
    /// <returns>Action observation</returns>
    public static AgentObservation Action(
        string agentId,
        string actionDescription,
        Dictionary<string, object>? context = null) => new(
        agentId,
        "action",
        actionDescription,
        context ?? new Dictionary<string, object>(),
        DateTime.UtcNow);

    /// <summary>
    /// Creates a statement observation.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <param name="statement">The statement made by the agent</param>
    /// <param name="context">Optional context</param>
    /// <returns>Statement observation</returns>
    public static AgentObservation Statement(
        string agentId,
        string statement,
        Dictionary<string, object>? context = null) => new(
        agentId,
        "statement",
        statement,
        context ?? new Dictionary<string, object>(),
        DateTime.UtcNow);

    /// <summary>
    /// Creates a state change observation.
    /// </summary>
    /// <param name="agentId">The agent ID</param>
    /// <param name="stateDescription">Description of the state change</param>
    /// <param name="context">Optional context</param>
    /// <returns>State change observation</returns>
    public static AgentObservation StateChange(
        string agentId,
        string stateDescription,
        Dictionary<string, object>? context = null) => new(
        agentId,
        "state_change",
        stateDescription,
        context ?? new Dictionary<string, object>(),
        DateTime.UtcNow);
}
