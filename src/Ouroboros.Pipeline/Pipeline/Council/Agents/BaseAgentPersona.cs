// <copyright file="BaseAgentPersona.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Council.Agents;

/// <summary>
/// Base implementation for agent personas with common functionality.
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

    /// <inheritdoc />
    public virtual async Task<Result<AgentContribution, string>> GenerateProposalAsync(
        CouncilTopic topic,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildProposalPrompt(topic);
            var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
            return Result<AgentContribution, string>.Success(new AgentContribution(Name, response));
        }
        catch (Exception ex)
        {
            return Result<AgentContribution, string>.Failure($"[{Name}] Proposal generation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<AgentContribution, string>> GenerateChallengeAsync(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> otherProposals,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildChallengePrompt(topic, otherProposals);
            var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
            return Result<AgentContribution, string>.Success(new AgentContribution(Name, response));
        }
        catch (Exception ex)
        {
            return Result<AgentContribution, string>.Failure($"[{Name}] Challenge generation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<AgentContribution, string>> GenerateRefinementAsync(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> challenges,
        AgentContribution ownProposal,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildRefinementPrompt(topic, challenges, ownProposal);
            var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
            return Result<AgentContribution, string>.Success(new AgentContribution(Name, response));
        }
        catch (Exception ex)
        {
            return Result<AgentContribution, string>.Failure($"[{Name}] Refinement generation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public virtual async Task<Result<AgentVote, string>> GenerateVoteAsync(
        CouncilTopic topic,
        IReadOnlyList<DebateRound> transcript,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = BuildVotePrompt(topic, transcript);
            var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
            var vote = ParseVoteResponse(response);
            return Result<AgentVote, string>.Success(vote);
        }
        catch (Exception ex)
        {
            return Result<AgentVote, string>.Failure($"[{Name}] Vote generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the prompt for generating a proposal.
    /// </summary>
    protected virtual string BuildProposalPrompt(CouncilTopic topic)
    {
        return $"""
            {SystemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            **Background**: {topic.Background}

            **Constraints**:
            {string.Join("\n", topic.Constraints.Select(c => $"- {c}"))}

            ## Your Task

            As {Name}, provide your initial proposal or position on this topic.
            Focus on your unique perspective as described above.
            Be concise but thorough. Structure your response clearly.
            """;
    }

    /// <summary>
    /// Builds the prompt for generating a challenge.
    /// </summary>
    protected virtual string BuildChallengePrompt(CouncilTopic topic, IReadOnlyList<AgentContribution> otherProposals)
    {
        var proposalsText = string.Join("\n\n", otherProposals.Select(p =>
            $"**{p.AgentName}**:\n{p.Content}"));

        return $"""
            {SystemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            ## Other Agents' Proposals

            {proposalsText}

            ## Your Task

            As {Name}, critically analyze the proposals above from your unique perspective.
            Identify weaknesses, gaps, or concerns. Present counterarguments where appropriate.
            Be constructive but thorough in your critique.
            """;
    }

    /// <summary>
    /// Builds the prompt for generating a refinement.
    /// </summary>
    protected virtual string BuildRefinementPrompt(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> challenges,
        AgentContribution ownProposal)
    {
        var challengesText = string.Join("\n\n", challenges.Select(c =>
            $"**{c.AgentName}**:\n{c.Content}"));

        return $"""
            {SystemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            ## Your Original Proposal

            {ownProposal.Content}

            ## Challenges Received

            {challengesText}

            ## Your Task

            As {Name}, revise your position considering the challenges above.
            Address valid concerns while maintaining your core perspective.
            Explain what you're changing and why, or defend your original position if appropriate.
            """;
    }

    /// <summary>
    /// Builds the prompt for generating a vote.
    /// </summary>
    protected virtual string BuildVotePrompt(CouncilTopic topic, IReadOnlyList<DebateRound> transcript)
    {
        var transcriptText = string.Join("\n\n", transcript.Select(round =>
            $"## {round.Phase} - Round {round.RoundNumber}\n" +
            string.Join("\n", round.Contributions.Select(c => $"**{c.AgentName}**: {c.Content}"))));

        return $"""
            {SystemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            ## Debate Transcript

            {transcriptText}

            ## Your Task

            As {Name}, cast your final vote. Respond in the following format:

            POSITION: [APPROVE/REJECT/ABSTAIN]
            RATIONALE: [Your reasoning in 2-3 sentences]
            """;
    }

    /// <summary>
    /// Parses a vote response from the LLM output.
    /// </summary>
    protected virtual AgentVote ParseVoteResponse(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var position = "ABSTAIN";
        var rationale = response;

        foreach (var line in lines)
        {
            if (line.StartsWith("POSITION:", StringComparison.OrdinalIgnoreCase))
            {
                position = line.Replace("POSITION:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            }
            else if (line.StartsWith("RATIONALE:", StringComparison.OrdinalIgnoreCase))
            {
                rationale = line.Replace("RATIONALE:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        return new AgentVote(Name, position, ExpertiseWeight, rationale);
    }
}
