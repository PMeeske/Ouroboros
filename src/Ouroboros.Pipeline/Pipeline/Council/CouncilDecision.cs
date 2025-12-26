// <copyright file="CouncilDecision.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Represents the outcome of a council debate including votes, transcript, and minority opinions.
/// </summary>
/// <param name="Conclusion">The final synthesized conclusion from the debate.</param>
/// <param name="Votes">Dictionary mapping agent names to their votes.</param>
/// <param name="Transcript">List of debate rounds capturing the full discussion.</param>
/// <param name="Confidence">Confidence score for the decision (0.0 to 1.0).</param>
/// <param name="MinorityOpinions">List of dissenting opinions that were not adopted.</param>
public sealed record CouncilDecision(
    string Conclusion,
    IReadOnlyDictionary<string, AgentVote> Votes,
    IReadOnlyList<DebateRound> Transcript,
    double Confidence,
    IReadOnlyList<MinorityOpinion> MinorityOpinions)
{
    /// <summary>
    /// Gets whether the council reached consensus (all votes agree).
    /// </summary>
    public bool IsConsensus => Votes.Values.All(v => v.Position == Votes.Values.First().Position);

    /// <summary>
    /// Gets the majority position if one exists.
    /// </summary>
    public string? MajorityPosition
    {
        get
        {
            var grouped = Votes.Values.GroupBy(v => v.Position)
                .OrderByDescending(g => g.Sum(v => v.Weight))
                .FirstOrDefault();
            return grouped?.Key;
        }
    }

    /// <summary>
    /// Creates an empty decision representing a failed deliberation.
    /// </summary>
    /// <param name="reason">The reason for the failed deliberation.</param>
    /// <returns>A CouncilDecision indicating failure.</returns>
    public static CouncilDecision Failed(string reason) =>
        new(
            Conclusion: $"Deliberation failed: {reason}",
            Votes: new Dictionary<string, AgentVote>(),
            Transcript: [],
            Confidence: 0.0,
            MinorityOpinions: []);
}

/// <summary>
/// Represents a single agent's vote with position, weight, and rationale.
/// </summary>
/// <param name="AgentName">Name of the voting agent.</param>
/// <param name="Position">The agent's position (Approve, Reject, Abstain, etc.).</param>
/// <param name="Weight">The weight of this vote based on expertise (0.0 to 1.0).</param>
/// <param name="Rationale">Explanation for the vote.</param>
public sealed record AgentVote(
    string AgentName,
    string Position,
    double Weight,
    string Rationale);

/// <summary>
/// Represents a single round in the council debate.
/// </summary>
/// <param name="Phase">The debate phase (Proposal, Challenge, Refinement, Voting, Synthesis).</param>
/// <param name="RoundNumber">The round number within the phase.</param>
/// <param name="Contributions">List of agent contributions in this round.</param>
/// <param name="Timestamp">When this round occurred.</param>
public sealed record DebateRound(
    DebatePhase Phase,
    int RoundNumber,
    IReadOnlyList<AgentContribution> Contributions,
    DateTime Timestamp);

/// <summary>
/// Represents a contribution from an agent during a debate round.
/// </summary>
/// <param name="AgentName">Name of the contributing agent.</param>
/// <param name="Content">The content of the contribution.</param>
/// <param name="TargetAgent">Optional target agent (for direct responses).</param>
/// <param name="ReferencedContributions">IDs of contributions being referenced.</param>
public sealed record AgentContribution(
    string AgentName,
    string Content,
    string? TargetAgent = null,
    IReadOnlyList<Guid>? ReferencedContributions = null)
{
    /// <summary>
    /// Gets a unique identifier for this contribution.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}

/// <summary>
/// Represents a minority opinion that was not adopted but is recorded for transparency.
/// </summary>
/// <param name="AgentName">Name of the dissenting agent.</param>
/// <param name="Position">The dissenting position.</param>
/// <param name="Rationale">Explanation for the dissent.</param>
/// <param name="Concerns">Specific concerns that remain unaddressed.</param>
public sealed record MinorityOpinion(
    string AgentName,
    string Position,
    string Rationale,
    IReadOnlyList<string> Concerns);

/// <summary>
/// Enumeration of debate phases in the council protocol.
/// </summary>
public enum DebatePhase
{
    /// <summary>Each agent presents their initial position.</summary>
    Proposal,

    /// <summary>Agents critique positions and present counterarguments.</summary>
    Challenge,

    /// <summary>Agents revise positions based on feedback.</summary>
    Refinement,

    /// <summary>Weighted voting mechanism.</summary>
    Voting,

    /// <summary>Orchestrator synthesizes consensus or flags conflicts.</summary>
    Synthesis
}
