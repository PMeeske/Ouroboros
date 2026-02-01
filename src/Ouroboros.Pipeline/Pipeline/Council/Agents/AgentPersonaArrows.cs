// <copyright file="AgentPersonaArrows.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Council.Agents;

/// <summary>
/// Arrow-based factory functions for agent persona operations.
/// Replaces inheritance-based template methods with composable arrow patterns.
/// </summary>
public static class AgentPersonaArrows
{
    /// <summary>
    /// Creates a proposal generation arrow for an agent persona.
    /// </summary>
    /// <param name="agentName">Name of the agent.</param>
    /// <param name="systemPrompt">System prompt defining agent behavior.</param>
    /// <param name="promptBuilder">Function to build the proposal prompt.</param>
    /// <param name="llm">Language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A step that transforms a topic into an agent contribution.</returns>
    public static Step<CouncilTopic, Result<AgentContribution, string>> CreateProposalArrow(
        string agentName,
        string systemPrompt,
        Func<CouncilTopic, string, string> promptBuilder,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
        => async topic =>
        {
            try
            {
                var prompt = promptBuilder(topic, systemPrompt);
                var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
                return Result<AgentContribution, string>.Success(new AgentContribution(agentName, response));
            }
            catch (Exception ex)
            {
                return Result<AgentContribution, string>.Failure($"[{agentName}] Proposal generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a challenge generation arrow for an agent persona.
    /// </summary>
    /// <param name="agentName">Name of the agent.</param>
    /// <param name="systemPrompt">System prompt defining agent behavior.</param>
    /// <param name="promptBuilder">Function to build the challenge prompt.</param>
    /// <param name="llm">Language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A step that transforms topic and proposals into an agent contribution.</returns>
    public static Step<(CouncilTopic topic, IReadOnlyList<AgentContribution> otherProposals), Result<AgentContribution, string>> CreateChallengeArrow(
        string agentName,
        string systemPrompt,
        Func<CouncilTopic, IReadOnlyList<AgentContribution>, string, string> promptBuilder,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
        => async input =>
        {
            try
            {
                var prompt = promptBuilder(input.topic, input.otherProposals, systemPrompt);
                var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
                return Result<AgentContribution, string>.Success(new AgentContribution(agentName, response));
            }
            catch (Exception ex)
            {
                return Result<AgentContribution, string>.Failure($"[{agentName}] Challenge generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a refinement generation arrow for an agent persona.
    /// </summary>
    /// <param name="agentName">Name of the agent.</param>
    /// <param name="systemPrompt">System prompt defining agent behavior.</param>
    /// <param name="promptBuilder">Function to build the refinement prompt.</param>
    /// <param name="llm">Language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A step that transforms topic, challenges, and own proposal into an agent contribution.</returns>
    public static Step<(CouncilTopic topic, IReadOnlyList<AgentContribution> challenges, AgentContribution ownProposal), Result<AgentContribution, string>> CreateRefinementArrow(
        string agentName,
        string systemPrompt,
        Func<CouncilTopic, IReadOnlyList<AgentContribution>, AgentContribution, string, string> promptBuilder,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
        => async input =>
        {
            try
            {
                var prompt = promptBuilder(input.topic, input.challenges, input.ownProposal, systemPrompt);
                var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
                return Result<AgentContribution, string>.Success(new AgentContribution(agentName, response));
            }
            catch (Exception ex)
            {
                return Result<AgentContribution, string>.Failure($"[{agentName}] Refinement generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a vote generation arrow for an agent persona.
    /// </summary>
    /// <param name="agentName">Name of the agent.</param>
    /// <param name="systemPrompt">System prompt defining agent behavior.</param>
    /// <param name="expertiseWeight">Expertise weight of the agent.</param>
    /// <param name="promptBuilder">Function to build the vote prompt.</param>
    /// <param name="voteParser">Function to parse the vote response.</param>
    /// <param name="llm">Language model to use for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A step that transforms topic and transcript into an agent vote.</returns>
    public static Step<(CouncilTopic topic, IReadOnlyList<DebateRound> transcript), Result<AgentVote, string>> CreateVoteArrow(
        string agentName,
        string systemPrompt,
        double expertiseWeight,
        Func<CouncilTopic, IReadOnlyList<DebateRound>, string, string> promptBuilder,
        Func<string, string, double, AgentVote> voteParser,
        ToolAwareChatModel llm,
        CancellationToken ct = default)
        => async input =>
        {
            try
            {
                var prompt = promptBuilder(input.topic, input.transcript, systemPrompt);
                var (response, _) = await llm.GenerateWithToolsAsync(prompt, ct);
                var vote = voteParser(agentName, response, expertiseWeight);
                return Result<AgentVote, string>.Success(vote);
            }
            catch (Exception ex)
            {
                return Result<AgentVote, string>.Failure($"[{agentName}] Vote generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Default proposal prompt builder.
    /// </summary>
    public static string BuildDefaultProposalPrompt(CouncilTopic topic, string systemPrompt)
    {
        return $"""
            {systemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            **Background**: {topic.Background}

            **Constraints**:
            {string.Join("\n", topic.Constraints.Select(c => $"- {c}"))}

            ## Your Task

            Provide your initial proposal or position on this topic.
            Focus on your unique perspective as described above.
            Be concise but thorough. Structure your response clearly.
            """;
    }

    /// <summary>
    /// Default challenge prompt builder.
    /// </summary>
    public static string BuildDefaultChallengePrompt(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> otherProposals,
        string systemPrompt)
    {
        var proposalsText = string.Join("\n\n", otherProposals.Select(p =>
            $"**{p.AgentName}**:\n{p.Content}"));

        return $"""
            {systemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            ## Other Agents' Proposals

            {proposalsText}

            ## Your Task

            Critically analyze the proposals above from your unique perspective.
            Identify weaknesses, gaps, or concerns. Present counterarguments where appropriate.
            Be constructive but thorough in your critique.
            """;
    }

    /// <summary>
    /// Default refinement prompt builder.
    /// </summary>
    public static string BuildDefaultRefinementPrompt(
        CouncilTopic topic,
        IReadOnlyList<AgentContribution> challenges,
        AgentContribution ownProposal,
        string systemPrompt)
    {
        var challengesText = string.Join("\n\n", challenges.Select(c =>
            $"**{c.AgentName}**:\n{c.Content}"));

        return $"""
            {systemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            ## Your Original Proposal

            {ownProposal.Content}

            ## Challenges Received

            {challengesText}

            ## Your Task

            Revise your position considering the challenges above.
            Address valid concerns while maintaining your core perspective.
            Explain what you're changing and why, or defend your original position if appropriate.
            """;
    }

    /// <summary>
    /// Default vote prompt builder.
    /// </summary>
    public static string BuildDefaultVotePrompt(
        CouncilTopic topic,
        IReadOnlyList<DebateRound> transcript,
        string systemPrompt)
    {
        var transcriptText = string.Join("\n\n", transcript.Select(round =>
            $"## {round.Phase} - Round {round.RoundNumber}\n" +
            string.Join("\n", round.Contributions.Select(c => $"**{c.AgentName}**: {c.Content}"))));

        return $"""
            {systemPrompt}

            ## Council Debate Topic

            **Question**: {topic.Question}

            ## Debate Transcript

            {transcriptText}

            ## Your Task

            Cast your final vote. Respond in the following format:

            POSITION: [APPROVE/REJECT/ABSTAIN]
            RATIONALE: [Your reasoning in 2-3 sentences]
            """;
    }

    /// <summary>
    /// Default vote response parser.
    /// </summary>
    public static AgentVote ParseDefaultVoteResponse(string agentName, string response, double expertiseWeight)
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

        return new AgentVote(agentName, position, expertiseWeight, rationale);
    }
}
