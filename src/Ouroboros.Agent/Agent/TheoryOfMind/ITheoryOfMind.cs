// <copyright file="ITheoryOfMind.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;
using Unit = Ouroboros.Core.Learning.Unit;

namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Interface for Theory of Mind capabilities.
/// Enables agents to model other agents' beliefs, desires, and intentions.
/// </summary>
public interface ITheoryOfMind
{
    /// <summary>
    /// Infers another agent's beliefs from observations.
    /// </summary>
    /// <param name="agentId">The ID of the agent to model</param>
    /// <param name="observations">Recent observations of the agent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the inferred belief state or error message</returns>
    Task<Result<BeliefState, string>> InferBeliefsAsync(
        string agentId,
        IReadOnlyList<AgentObservation> observations,
        CancellationToken ct = default);

    /// <summary>
    /// Predicts another agent's likely intention or goal.
    /// </summary>
    /// <param name="agentId">The ID of the agent to model</param>
    /// <param name="beliefs">The agent's current belief state</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the intention prediction or error message</returns>
    Task<Result<IntentionPrediction, string>> PredictIntentionAsync(
        string agentId,
        BeliefState beliefs,
        CancellationToken ct = default);

    /// <summary>
    /// Predicts what action another agent will take next.
    /// </summary>
    /// <param name="agentId">The ID of the agent to model</param>
    /// <param name="beliefs">The agent's current belief state</param>
    /// <param name="availableActions">Actions available to the agent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the action prediction or error message</returns>
    Task<Result<ActionPrediction, string>> PredictNextActionAsync(
        string agentId,
        BeliefState beliefs,
        IReadOnlyList<EmbodiedAction> availableActions,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the model of a specific agent based on new observation.
    /// </summary>
    /// <param name="agentId">The ID of the agent to update</param>
    /// <param name="observation">The new observation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing Unit on success or error message on failure</returns>
    Task<Result<Unit, string>> UpdateAgentModelAsync(
        string agentId,
        AgentObservation observation,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current model of a specific agent.
    /// </summary>
    /// <param name="agentId">The ID of the agent to retrieve</param>
    /// <returns>The agent model if it exists, null otherwise</returns>
    AgentModel? GetAgentModel(string agentId);

    /// <summary>
    /// Evaluates how well the agent understands another agent.
    /// Returns a confidence score based on observation history and model quality.
    /// </summary>
    /// <param name="agentId">The ID of the agent to evaluate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Confidence score (0.0 to 1.0)</returns>
    Task<double> GetModelConfidenceAsync(string agentId, CancellationToken ct = default);
}
