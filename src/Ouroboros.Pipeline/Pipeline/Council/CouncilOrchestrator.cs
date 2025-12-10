// <copyright file="CouncilOrchestrator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Pipeline.Council.Agents;

namespace LangChainPipeline.Pipeline.Council;

/// <summary>
/// Orchestrates multi-agent council debates using the Round Table protocol.
/// </summary>
public sealed class CouncilOrchestrator : ICouncilOrchestrator
{
    private readonly List<IAgentPersona> _agents = [];
    private readonly ToolAwareChatModel _llm;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouncilOrchestrator"/> class.
    /// </summary>
    /// <param name="llm">The language model to use for agent interactions.</param>
    public CouncilOrchestrator(ToolAwareChatModel llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    /// <summary>
    /// Initializes a new instance with default agents.
    /// </summary>
    /// <param name="llm">The language model to use.</param>
    /// <returns>A new CouncilOrchestrator with default agents.</returns>
    public static CouncilOrchestrator CreateWithDefaultAgents(ToolAwareChatModel llm)
    {
        var orchestrator = new CouncilOrchestrator(llm);
        orchestrator.AddAgent(new OptimistAgent());
        orchestrator.AddAgent(new SecurityCynicAgent());
        orchestrator.AddAgent(new PragmatistAgent());
        orchestrator.AddAgent(new TheoristAgent());
        orchestrator.AddAgent(new UserAdvocateAgent());
        return orchestrator;
    }

    /// <inheritdoc />
    public IReadOnlyList<IAgentPersona> Agents => _agents.AsReadOnly();

    /// <inheritdoc />
    public void AddAgent(IAgentPersona agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (_agents.Any(a => a.Name == agent.Name))
        {
            throw new InvalidOperationException($"Agent with name '{agent.Name}' already exists in the council.");
        }

        _agents.Add(agent);
    }

    /// <inheritdoc />
    public bool RemoveAgent(string agentName)
    {
        var agent = _agents.FirstOrDefault(a => a.Name == agentName);
        if (agent is null)
        {
            return false;
        }

        _agents.Remove(agent);
        return true;
    }

    /// <inheritdoc />
    public Task<Result<CouncilDecision, string>> ConveneCouncilAsync(
        CouncilTopic topic,
        CancellationToken ct = default)
        => ConveneCouncilAsync(topic, CouncilConfig.Default, ct);

    /// <inheritdoc />
    public async Task<Result<CouncilDecision, string>> ConveneCouncilAsync(
        CouncilTopic topic,
        CouncilConfig config,
        CancellationToken ct = default)
    {
        if (_agents.Count == 0)
        {
            return Result<CouncilDecision, string>.Failure("No agents registered in the council.");
        }

        var transcript = new List<DebateRound>();
        var agentProposals = new Dictionary<string, AgentContribution>();

        try
        {
            // Phase 1: Proposal
            var proposalRound = await ExecuteProposalPhaseAsync(topic, config, ct);
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
            var challengeRound = await ExecuteChallengePhaseAsync(topic, agentProposals, config, ct);
            if (challengeRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(challengeRound.Error);
            }

            transcript.Add(challengeRound.Value);

            // Phase 3: Refinement
            var refinementRound = await ExecuteRefinementPhaseAsync(
                topic, agentProposals, challengeRound.Value.Contributions, config, ct);
            if (refinementRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(refinementRound.Error);
            }

            transcript.Add(refinementRound.Value);

            // Phase 4: Voting
            var votingResult = await ExecuteVotingPhaseAsync(topic, transcript, config, ct);
            if (votingResult.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(votingResult.Error);
            }

            var (votes, votingRound) = votingResult.Value;
            transcript.Add(votingRound);

            // Phase 5: Synthesis
            var decision = await SynthesizeDecisionAsync(topic, transcript, votes, config, ct);
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

    private async Task<Result<DebateRound, string>> ExecuteProposalPhaseAsync(
        CouncilTopic topic,
        CouncilConfig config,
        CancellationToken ct)
    {
        var contributions = new List<AgentContribution>();

        foreach (var agent in _agents)
        {
            ct.ThrowIfCancellationRequested();
            var result = await agent.GenerateProposalAsync(topic, _llm, ct);
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

    private async Task<Result<DebateRound, string>> ExecuteChallengePhaseAsync(
        CouncilTopic topic,
        Dictionary<string, AgentContribution> proposals,
        CouncilConfig config,
        CancellationToken ct)
    {
        var contributions = new List<AgentContribution>();

        foreach (var agent in _agents)
        {
            ct.ThrowIfCancellationRequested();
            var otherProposals = proposals
                .Where(p => p.Key != agent.Name)
                .Select(p => p.Value)
                .ToList();

            var result = await agent.GenerateChallengeAsync(topic, otherProposals, _llm, ct);
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

    private async Task<Result<DebateRound, string>> ExecuteRefinementPhaseAsync(
        CouncilTopic topic,
        Dictionary<string, AgentContribution> proposals,
        IReadOnlyList<AgentContribution> challenges,
        CouncilConfig config,
        CancellationToken ct)
    {
        var contributions = new List<AgentContribution>();

        foreach (var agent in _agents)
        {
            ct.ThrowIfCancellationRequested();
            if (!proposals.TryGetValue(agent.Name, out var ownProposal))
            {
                continue;
            }

            var result = await agent.GenerateRefinementAsync(topic, challenges, ownProposal, _llm, ct);
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

    private async Task<Result<(Dictionary<string, AgentVote> Votes, DebateRound Round), string>> ExecuteVotingPhaseAsync(
        CouncilTopic topic,
        IReadOnlyList<DebateRound> transcript,
        CouncilConfig config,
        CancellationToken ct)
    {
        var votes = new Dictionary<string, AgentVote>();
        var contributions = new List<AgentContribution>();

        foreach (var agent in _agents)
        {
            ct.ThrowIfCancellationRequested();
            var result = await agent.GenerateVoteAsync(topic, transcript, _llm, ct);
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

    private async Task<Result<CouncilDecision, string>> SynthesizeDecisionAsync(
        CouncilTopic topic,
        List<DebateRound> transcript,
        Dictionary<string, AgentVote> votes,
        CouncilConfig config,
        CancellationToken ct)
    {
        // Calculate weighted vote totals
        var voteGroups = votes.Values
            .GroupBy(v => v.Position.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Weight));

        var totalWeight = voteGroups.Values.Sum();
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
        var (conclusion, _) = await _llm.GenerateWithToolsAsync(synthesisPrompt, ct);

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
