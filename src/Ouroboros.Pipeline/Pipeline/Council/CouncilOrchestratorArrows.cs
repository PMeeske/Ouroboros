// <copyright file="CouncilOrchestratorArrows.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Provides arrow factory methods for council orchestration with explicit dependency parameterization.
/// This transforms the traditional constructor DI pattern to functional arrow composition.
/// </summary>
public static class CouncilOrchestratorArrows
{
    /// <summary>
    /// Creates a council debate arrow that convenes a council with explicit dependencies.
    /// </summary>
    /// <param name="llm">The language model to use for agent interactions.</param>
    /// <param name="agents">The list of agent personas to participate in the council.</param>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A step that transforms a pipeline branch by adding a council decision event.</returns>
    public static Step<PipelineBranch, PipelineBranch> ConveneCouncilArrow(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        CouncilConfig? config = null)
        => async branch =>
        {
            config ??= CouncilConfig.Default;

            if (agents.Count == 0)
            {
                return branch.WithEvent(CouncilDecisionEvent.Create(
                    topic,
                    CouncilDecision.Failed("No agents registered in the council.")));
            }

            var result = await ExecuteCouncilDebateAsync(llm, agents, topic, config);

            return result.Match(
                decision => branch.WithEvent(CouncilDecisionEvent.Create(topic, decision)),
                error => branch.WithEvent(CouncilDecisionEvent.Create(
                    topic,
                    CouncilDecision.Failed(error))));
        };

    /// <summary>
    /// Creates a council debate arrow with default agents.
    /// </summary>
    /// <param name="llm">The language model to use for agent interactions.</param>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A step that transforms a pipeline branch by adding a council decision event.</returns>
    public static Step<PipelineBranch, PipelineBranch> ConveneCouncilWithDefaultAgentsArrow(
        ToolAwareChatModel llm,
        CouncilTopic topic,
        CouncilConfig? config = null)
    {
        var defaultAgents = new List<IAgentPersona>
        {
            new OptimistAgent(),
            new SecurityCynicAgent(),
            new PragmatistAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        return ConveneCouncilArrow(llm, defaultAgents, topic, config);
    }

    /// <summary>
    /// Creates a Result-safe council debate arrow with comprehensive error handling.
    /// </summary>
    /// <param name="llm">The language model to use for agent interactions.</param>
    /// <param name="agents">The list of agent personas to participate in the council.</param>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A Kleisli arrow that returns a Result with the updated branch or error.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeConveneCouncilArrow(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        CouncilConfig? config = null)
        => async branch =>
        {
            try
            {
                config ??= CouncilConfig.Default;

                if (agents.Count == 0)
                {
                    return Result<PipelineBranch, string>.Failure("No agents registered in the council.");
                }

                var result = await ExecuteCouncilDebateAsync(llm, agents, topic, config);

                return result.Match(
                    decision => Result<PipelineBranch, string>.Success(
                        branch.WithEvent(CouncilDecisionEvent.Create(topic, decision))),
                    error => Result<PipelineBranch, string>.Failure(error));
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Council debate exception: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a dynamic council debate arrow that builds the topic from the current pipeline state.
    /// </summary>
    /// <param name="llm">The language model to use for agent interactions.</param>
    /// <param name="agents">The list of agent personas to participate in the council.</param>
    /// <param name="topicBuilder">Function to build the question from the branch.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A step that transforms a pipeline branch by adding a council decision event.</returns>
    public static Step<PipelineBranch, PipelineBranch> DynamicConveneCouncilArrow(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        Func<PipelineBranch, CouncilTopic> topicBuilder,
        CouncilConfig? config = null)
        => async branch =>
        {
            var topic = topicBuilder(branch);
            return await ConveneCouncilArrow(llm, agents, topic, config)(branch);
        };

    /// <summary>
    /// Creates a pre-configured council arrow with common agent configuration.
    /// </summary>
    /// <param name="llm">The language model to use.</param>
    /// <param name="config">Optional council configuration.</param>
    /// <returns>A function that creates council arrows for any topic.</returns>
    public static Func<CouncilTopic, Step<PipelineBranch, PipelineBranch>> CreateConfiguredCouncil(
        ToolAwareChatModel llm,
        CouncilConfig? config = null)
    {
        var defaultAgents = new List<IAgentPersona>
        {
            new OptimistAgent(),
            new SecurityCynicAgent(),
            new PragmatistAgent(),
            new TheoristAgent(),
            new UserAdvocateAgent()
        };

        return topic => ConveneCouncilArrow(llm, defaultAgents, topic, config);
    }

    /// <summary>
    /// Executes the full council debate process with explicit dependencies.
    /// </summary>
    private static async Task<Result<CouncilDecision, string>> ExecuteCouncilDebateAsync(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        CouncilConfig config,
        CancellationToken ct = default)
    {
        var transcript = new List<DebateRound>();
        var agentProposals = new Dictionary<string, AgentContribution>();

        try
        {
            // Phase 1: Proposal
            var proposalRound = await ExecuteProposalPhaseAsync(llm, agents, topic, config, ct);
            if (proposalRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(proposalRound.Error);
            }

            transcript.Add(proposalRound.Value);
            foreach (var contribution in proposalRound.Value.Contributions)
            {
                agentProposals[contribution.AgentName] = contribution;
            }

            // Phase 2: Challenge
            var challengeRound = await ExecuteChallengePhaseAsync(llm, agents, topic, agentProposals, config, ct);
            if (challengeRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(challengeRound.Error);
            }

            transcript.Add(challengeRound.Value);

            // Phase 3: Refinement
            var refinementRound = await ExecuteRefinementPhaseAsync(
                llm, agents, topic, agentProposals, challengeRound.Value.Contributions, config, ct);
            if (refinementRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(refinementRound.Error);
            }

            transcript.Add(refinementRound.Value);

            // Phase 4: Voting
            var votingResult = await ExecuteVotingPhaseAsync(llm, agents, topic, transcript, config, ct);
            if (votingResult.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(votingResult.Error);
            }

            var (votes, votingRound) = votingResult.Value;
            transcript.Add(votingRound);

            // Phase 5: Synthesis
            var decision = await SynthesizeDecisionAsync(llm, topic, transcript, votes, config, ct);
            return decision;
        }
        catch (OperationCanceledException)
        {
            return Result<CouncilDecision, string>.Failure("Council debate was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<CouncilDecision, string>.Failure($"Council debate failed: {ex.Message}");
        }
    }

    private static async Task<Result<DebateRound, string>> ExecuteProposalPhaseAsync(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        CouncilConfig config,
        CancellationToken ct)
    {
        var contributions = new List<AgentContribution>();

        foreach (var agent in agents)
        {
            ct.ThrowIfCancellationRequested();
            var result = await agent.GenerateProposalAsync(topic, llm, ct);
            if (result.IsFailure)
            {
                // Log but continue - graceful degradation
                continue;
            }

            contributions.Add(result.Value);
        }

        if (contributions.Count == 0)
        {
            return Result<DebateRound, string>.Failure("No agents were able to generate proposals.");
        }

        return Result<DebateRound, string>.Success(new DebateRound(
            Phase: DebatePhase.Proposal,
            RoundNumber: 1,
            Contributions: contributions,
            Timestamp: DateTime.UtcNow));
    }

    private static async Task<Result<DebateRound, string>> ExecuteChallengePhaseAsync(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        Dictionary<string, AgentContribution> proposals,
        CouncilConfig config,
        CancellationToken ct)
    {
        var contributions = new List<AgentContribution>();

        foreach (var agent in agents)
        {
            ct.ThrowIfCancellationRequested();
            var otherProposals = proposals
                .Where(p => p.Key != agent.Name)
                .Select(p => p.Value)
                .ToList();

            var result = await agent.GenerateChallengeAsync(topic, otherProposals, llm, ct);
            if (result.IsFailure)
            {
                continue;
            }

            contributions.Add(result.Value);
        }

        return Result<DebateRound, string>.Success(new DebateRound(
            Phase: DebatePhase.Challenge,
            RoundNumber: 1,
            Contributions: contributions,
            Timestamp: DateTime.UtcNow));
    }

    private static async Task<Result<DebateRound, string>> ExecuteRefinementPhaseAsync(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        Dictionary<string, AgentContribution> proposals,
        IReadOnlyList<AgentContribution> challenges,
        CouncilConfig config,
        CancellationToken ct)
    {
        var contributions = new List<AgentContribution>();

        foreach (var agent in agents)
        {
            ct.ThrowIfCancellationRequested();
            if (!proposals.TryGetValue(agent.Name, out var ownProposal))
            {
                continue;
            }

            var result = await agent.GenerateRefinementAsync(topic, challenges, ownProposal, llm, ct);
            if (result.IsFailure)
            {
                continue;
            }

            contributions.Add(result.Value);
        }

        return Result<DebateRound, string>.Success(new DebateRound(
            Phase: DebatePhase.Refinement,
            RoundNumber: 1,
            Contributions: contributions,
            Timestamp: DateTime.UtcNow));
    }

    private static async Task<Result<(Dictionary<string, AgentVote> Votes, DebateRound Round), string>> ExecuteVotingPhaseAsync(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        IReadOnlyList<DebateRound> transcript,
        CouncilConfig config,
        CancellationToken ct)
    {
        var votes = new Dictionary<string, AgentVote>();
        var contributions = new List<AgentContribution>();

        foreach (var agent in agents)
        {
            ct.ThrowIfCancellationRequested();
            var result = await agent.GenerateVoteAsync(topic, transcript, llm, ct);
            if (result.IsFailure)
            {
                continue;
            }

            votes[agent.Name] = result.Value;
            contributions.Add(new AgentContribution(
                agent.Name,
                $"VOTE: {result.Value.Position} - {result.Value.Rationale}"));
        }

        var round = new DebateRound(
            Phase: DebatePhase.Voting,
            RoundNumber: 1,
            Contributions: contributions,
            Timestamp: DateTime.UtcNow);

        return Result<(Dictionary<string, AgentVote>, DebateRound), string>.Success((votes, round));
    }

    private static async Task<Result<CouncilDecision, string>> SynthesizeDecisionAsync(
        ToolAwareChatModel llm,
        CouncilTopic topic,
        List<DebateRound> transcript,
        Dictionary<string, AgentVote> votes,
        CouncilConfig config,
        CancellationToken ct)
    {
        // If no votes were produced, we cannot synthesize a meaningful decision.
        if (votes.Count == 0)
        {
            return Result<CouncilDecision, string>.Failure(
                "No votes were produced by any agents; unable to synthesize a council decision.");
        }

        // Calculate weighted vote totals
        var voteGroups = votes.Values
            .GroupBy(v => v.Position.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Weight));

        var totalWeight = voteGroups.Values.Sum();
        if (totalWeight <= 0)
        {
            return Result<CouncilDecision, string>.Failure(
                "All agent votes have zero weight; unable to synthesize a council decision.");
        }

        var majorityPosition = voteGroups.OrderByDescending(g => g.Value).FirstOrDefault();

        // Check for consensus
        var consensusReached = majorityPosition.Value >= totalWeight * config.ConsensusThreshold;

        // Identify minority opinions
        var minorityOpinions = new List<MinorityOpinion>();
        if (config.EnableMinorityReport)
        {
            var minorityVotes = votes.Values
                .Where(v => !v.Position.Equals(majorityPosition.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var vote in minorityVotes)
            {
                minorityOpinions.Add(new MinorityOpinion(
                    vote.AgentName,
                    vote.Position,
                    vote.Rationale,
                    []));
            }
        }

        // Generate synthesis using LLM
        var synthesisPrompt = BuildSynthesisPrompt(topic, transcript, votes, majorityPosition.Key, consensusReached);
        var (conclusion, _) = await llm.GenerateWithToolsAsync(synthesisPrompt, ct);

        var confidence = consensusReached ? majorityPosition.Value / totalWeight : 0.5;

        var synthesisRound = new DebateRound(
            Phase: DebatePhase.Synthesis,
            RoundNumber: 1,
            Contributions: [new AgentContribution("Orchestrator", conclusion)],
            Timestamp: DateTime.UtcNow);

        transcript.Add(synthesisRound);

        var decision = new CouncilDecision(
            Conclusion: conclusion,
            Votes: votes,
            Transcript: transcript,
            Confidence: confidence,
            MinorityOpinions: minorityOpinions);

        return Result<CouncilDecision, string>.Success(decision);
    }

    private static string BuildSynthesisPrompt(
        CouncilTopic topic,
        List<DebateRound> transcript,
        Dictionary<string, AgentVote> votes,
        string majorityPosition,
        bool consensusReached)
    {
        var transcriptSummary = string.Join("\n", transcript.Select(r =>
            $"## {r.Phase}\n" + string.Join("\n", r.Contributions.Select(c =>
                $"- **{c.AgentName}**: {c.Content[..Math.Min(200, c.Content.Length)]}..."))));

        var votesSummary = string.Join("\n", votes.Select(v =>
            $"- {v.Key}: {v.Value.Position} (weight: {v.Value.Weight:F2})"));

        return $"""
            You are the Council Orchestrator synthesizing the results of a multi-agent debate.

            ## Topic
            {topic.Question}

            ## Debate Summary
            {transcriptSummary}

            ## Votes
            {votesSummary}

            ## Status
            - Majority Position: {majorityPosition}
            - Consensus Reached: {consensusReached}

            ## Your Task
            Synthesize the debate into a clear, actionable conclusion. Include:
            1. The final decision and recommendation
            2. Key points of agreement
            3. Remaining concerns to address
            4. Next steps or action items
            """;
    }
}
