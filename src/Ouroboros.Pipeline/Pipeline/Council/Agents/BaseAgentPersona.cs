// <copyright file="BaseAgentPersona.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Council.Agents;

/// <summary>
/// Composable agent persona using arrow-based operations.
/// Provides default implementations using arrow composition instead of template methods.
/// </summary>
public abstract class BaseAgentPersona : IAgentPersona
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public virtual double ExpertiseWeight => 1.0;

    /// <inheritdoc />
    public abstract string SystemPrompt { get; }

    /// <summary>
    /// Gets the prompt builder for proposal generation.
    /// Override to customize prompt building logic.
    /// </summary>
    protected virtual Func<CouncilTopic, string, string> ProposalPromptBuilder =>
        AgentPersonaArrows.BuildDefaultProposalPrompt;

    /// <summary>
    /// Gets the prompt builder for challenge generation.
    /// Override to customize prompt building logic.
    /// </summary>
    protected virtual Func<CouncilTopic, IReadOnlyList<AgentContribution>, string, string> ChallengePromptBuilder =>
        AgentPersonaArrows.BuildDefaultChallengePrompt;

    /// <summary>
    /// Gets the prompt builder for refinement generation.
    /// Override to customize prompt building logic.
    /// </summary>
    protected virtual Func<CouncilTopic, IReadOnlyList<AgentContribution>, AgentContribution, string, string> RefinementPromptBuilder =>
        AgentPersonaArrows.BuildDefaultRefinementPrompt;

    /// <summary>
    /// Gets the prompt builder for vote generation.
    /// Override to customize prompt building logic.
    /// </summary>
    protected virtual Func<CouncilTopic, IReadOnlyList<DebateRound>, string, string> VotePromptBuilder =>
        AgentPersonaArrows.BuildDefaultVotePrompt;

    /// <summary>
    /// Gets the vote parser.
    /// Override to customize vote parsing logic.
    /// </summary>
    protected virtual Func<string, string, double, AgentVote> VoteParser =>
        AgentPersonaArrows.ParseDefaultVoteResponse;

    /// <inheritdoc />
    public virtual Task<Result<AgentContribution, string>> GenerateProposalAsync(
        CouncilTopic topic,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        var arrow = AgentPersonaArrows.CreateProposalArrow(Name, SystemPrompt, ProposalPromptBuilder, llm, ct);
        return arrow(topic);
    }

    /// <inheritdoc />
    public virtual Task<Result<AgentContribution, string>> GenerateChallengeAsync(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> otherProposals,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        var arrow = AgentPersonaArrows.CreateChallengeArrow(Name, SystemPrompt, ChallengePromptBuilder, llm, ct);
        return arrow((topic, otherProposals));
    }

    /// <inheritdoc />
    public virtual Task<Result<AgentContribution, string>> GenerateRefinementAsync(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> challenges,
        AgentContribution ownProposal,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        var arrow = AgentPersonaArrows.CreateRefinementArrow(Name, SystemPrompt, RefinementPromptBuilder, llm, ct);
        return arrow((topic, challenges, ownProposal));
    }

    /// <inheritdoc />
    public virtual Task<Result<AgentVote, string>> GenerateVoteAsync(
        CouncilTopic topic,
        IReadOnlyList<DebateRound> transcript,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        var arrow = AgentPersonaArrows.CreateVoteArrow(Name, SystemPrompt, ExpertiseWeight, VotePromptBuilder, VoteParser, llm, ct);
        return arrow((topic, transcript));
    }
}
