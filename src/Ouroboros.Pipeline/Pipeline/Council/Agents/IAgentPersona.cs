// <copyright file="IAgentPersona.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Council.Agents;

/// <summary>
/// Interface for council agent personas that participate in debates.
/// Each persona has a distinct perspective and expertise area.
/// </summary>
public interface IAgentPersona
{
    /// <summary>
    /// Gets the unique name of this agent persona.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of this agent's perspective and focus areas.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the expertise weight of this agent (0.0 to 1.0).
    /// Higher values indicate more expertise in the subject area.
    /// </summary>
    double ExpertiseWeight { get; }

    /// <summary>
    /// Gets the system prompt that defines this agent's behavior.
    /// </summary>
    string SystemPrompt { get; }

    /// <summary>
    /// Generates the agent's initial proposal for a topic.
    /// </summary>
    /// <param name="topic">The topic to propose on.</param>
    /// <param name="llm">The language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's initial proposal contribution.</returns>
    Task<Result<AgentContribution, string>> GenerateProposalAsync(
        CouncilTopic topic,
        ToolAwareChatModel llm,
        CancellationToken ct = default);

    /// <summary>
    /// Generates the agent's challenge to other agents' proposals.
    /// </summary>
    /// <param name="topic">The topic being debated.</param>
    /// <param name="otherProposals">Proposals from other agents to challenge.</param>
    /// <param name="llm">The language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's challenge contribution.</returns>
    Task<Result<AgentContribution, string>> GenerateChallengeAsync(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> otherProposals,
        ToolAwareChatModel llm,
        CancellationToken ct = default);

    /// <summary>
    /// Generates the agent's refined position after considering challenges.
    /// </summary>
    /// <param name="topic">The topic being debated.</param>
    /// <param name="challenges">Challenges received from other agents.</param>
    /// <param name="ownProposal">The agent's original proposal.</param>
    /// <param name="llm">The language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's refined position.</returns>
    Task<Result<AgentContribution, string>> GenerateRefinementAsync(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> challenges,
        AgentContribution ownProposal,
        ToolAwareChatModel llm,
        CancellationToken ct = default);

    /// <summary>
    /// Generates the agent's final vote on the synthesized positions.
    /// </summary>
    /// <param name="topic">The topic being debated.</param>
    /// <param name="transcript">The full debate transcript.</param>
    /// <param name="llm">The language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent's vote.</returns>
    Task<Result<AgentVote, string>> GenerateVoteAsync(
        CouncilTopic topic,
        IReadOnlyList<DebateRound> transcript,
        ToolAwareChatModel llm,
        CancellationToken ct = default);
}
