// <copyright file="ConsensusProtocol.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System;
using System.Collections.Immutable;
using Ouroboros.Core.Monads;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a vote cast by an agent in a consensus decision.
/// </summary>
/// <param name="AgentId">The unique identifier of the voting agent.</param>
/// <param name="Option">The option being voted for.</param>
/// <param name="Confidence">The confidence level of the vote (0.0 to 1.0).</param>
/// <param name="Reasoning">Optional reasoning for the vote.</param>
/// <param name="Timestamp">When the vote was cast.</param>
public sealed record AgentVote(
    Guid AgentId,
    string Option,
    double Confidence,
    string? Reasoning,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a new agent vote with the current timestamp.
    /// </summary>
    /// <param name="agentId">The unique identifier of the voting agent.</param>
    /// <param name="option">The option being voted for.</param>
    /// <param name="confidence">The confidence level of the vote (0.0 to 1.0).</param>
    /// <param name="reasoning">Optional reasoning for the vote.</param>
    /// <returns>A new <see cref="AgentVote"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when option is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when confidence is not between 0.0 and 1.0.</exception>
    public static AgentVote Create(Guid agentId, string option, double confidence, string? reasoning = null)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (confidence < 0.0 || confidence > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0.0 and 1.0.");
        }

        return new AgentVote(agentId, option, confidence, reasoning, DateTime.UtcNow);
    }
}

/// <summary>
/// Represents the result of a consensus evaluation.
/// </summary>
/// <param name="WinningOption">The option that won the consensus (empty if no consensus).</param>
/// <param name="AggregateConfidence">The aggregate confidence in the winning option.</param>
/// <param name="AllVotes">All votes cast in the evaluation.</param>
/// <param name="VoteCounts">Count of votes per option.</param>
/// <param name="ConfidenceByOption">Aggregate confidence per option.</param>
/// <param name="HasConsensus">Whether consensus was reached.</param>
/// <param name="Protocol">The protocol used to evaluate consensus.</param>
public sealed record ConsensusResult(
    string WinningOption,
    double AggregateConfidence,
    IReadOnlyList<AgentVote> AllVotes,
    IReadOnlyDictionary<string, int> VoteCounts,
    IReadOnlyDictionary<string, double> ConfidenceByOption,
    bool HasConsensus,
    string Protocol)
{
    /// <summary>
    /// Gets the total number of votes cast.
    /// </summary>
    public int TotalVotes => AllVotes.Count;

    /// <summary>
    /// Calculates the participation rate as a percentage.
    /// </summary>
    /// <param name="totalAgents">The total number of agents eligible to vote.</param>
    /// <returns>The participation rate between 0.0 and 1.0.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when totalAgents is less than or equal to zero.</exception>
    public double ParticipationRate(int totalAgents)
    {
        if (totalAgents <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAgents), totalAgents, "Total agents must be greater than zero.");
        }

        return (double)TotalVotes / totalAgents;
    }

    /// <summary>
    /// Creates a result indicating no consensus was reached.
    /// </summary>
    /// <param name="votes">The votes that were cast.</param>
    /// <param name="protocol">The protocol that was used.</param>
    /// <returns>A <see cref="ConsensusResult"/> indicating no consensus.</returns>
    public static ConsensusResult NoConsensus(IReadOnlyList<AgentVote> votes, string protocol)
    {
        ArgumentNullException.ThrowIfNull(votes);
        ArgumentNullException.ThrowIfNull(protocol);

        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        return new ConsensusResult(
            WinningOption: string.Empty,
            AggregateConfidence: 0.0,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: false,
            Protocol: protocol);
    }

    /// <summary>
    /// Calculates vote counts per option.
    /// </summary>
    private static Dictionary<string, int> CalculateVoteCounts(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (AgentVote vote in votes)
        {
            if (counts.TryGetValue(vote.Option, out int count))
            {
                counts[vote.Option] = count + 1;
            }
            else
            {
                counts[vote.Option] = 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Calculates aggregate confidence per option.
    /// </summary>
    private static Dictionary<string, double> CalculateConfidenceByOption(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, double> confidence = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (AgentVote vote in votes)
        {
            if (confidence.TryGetValue(vote.Option, out double existing))
            {
                confidence[vote.Option] = existing + vote.Confidence;
            }
            else
            {
                confidence[vote.Option] = vote.Confidence;
            }
        }

        return confidence;
    }
}

/// <summary>
/// Defines the available consensus strategies.
/// </summary>
public enum ConsensusStrategy
{
    /// <summary>
    /// Simple majority wins (greater than 50%).
    /// </summary>
    Majority,

    /// <summary>
    /// Super majority required (greater than 66%).
    /// </summary>
    SuperMajority,

    /// <summary>
    /// Unanimous agreement required (100%).
    /// </summary>
    Unanimous,

    /// <summary>
    /// Votes weighted by confidence level.
    /// </summary>
    WeightedByConfidence,

    /// <summary>
    /// Single highest confidence vote wins.
    /// </summary>
    HighestConfidence,

    /// <summary>
    /// Ranked choice voting with elimination rounds.
    /// </summary>
    RankedChoice,
}

/// <summary>
/// Defines the contract for consensus protocol implementations.
/// </summary>
public interface IConsensusProtocol
{
    /// <summary>
    /// Gets the consensus strategy used by this protocol.
    /// </summary>
    ConsensusStrategy Strategy { get; }

    /// <summary>
    /// Evaluates the given votes and determines if consensus is reached.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <returns>The consensus result.</returns>
    ConsensusResult Evaluate(IReadOnlyList<AgentVote> votes);

    /// <summary>
    /// Determines if the votes meet a specified threshold.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <param name="threshold">The threshold to meet (0.0 to 1.0).</param>
    /// <returns>True if threshold is met; otherwise, false.</returns>
    bool MeetsThreshold(IReadOnlyList<AgentVote> votes, double threshold);
}

/// <summary>
/// Implements consensus mechanisms for multi-agent decision making.
/// </summary>
public sealed class ConsensusProtocol : IConsensusProtocol
{
    private readonly ConsensusStrategy strategy;
    private readonly double customThreshold;

    private const double MajorityThreshold = 0.5;
    private const double SuperMajorityThreshold = 0.666666666666667;
    private const double UnanimousThreshold = 1.0;

    /// <summary>
    /// Gets a protocol using simple majority voting.
    /// </summary>
    public static ConsensusProtocol Majority { get; } = new ConsensusProtocol(ConsensusStrategy.Majority, MajorityThreshold);

    /// <summary>
    /// Gets a protocol using super majority voting (66%+).
    /// </summary>
    public static ConsensusProtocol SuperMajority { get; } = new ConsensusProtocol(ConsensusStrategy.SuperMajority, SuperMajorityThreshold);

    /// <summary>
    /// Gets a protocol requiring unanimous agreement.
    /// </summary>
    public static ConsensusProtocol Unanimous { get; } = new ConsensusProtocol(ConsensusStrategy.Unanimous, UnanimousThreshold);

    /// <summary>
    /// Gets a protocol using confidence-weighted voting.
    /// </summary>
    public static ConsensusProtocol WeightedByConfidence { get; } = new ConsensusProtocol(ConsensusStrategy.WeightedByConfidence, MajorityThreshold);

    /// <summary>
    /// Gets a protocol selecting the highest confidence vote.
    /// </summary>
    public static ConsensusProtocol HighestConfidence { get; } = new ConsensusProtocol(ConsensusStrategy.HighestConfidence, 0.0);

    /// <summary>
    /// Gets the consensus strategy used by this protocol.
    /// </summary>
    public ConsensusStrategy Strategy => strategy;

    /// <summary>
    /// Gets the threshold used for consensus determination.
    /// </summary>
    public double Threshold => customThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsensusProtocol"/> class.
    /// </summary>
    /// <param name="strategy">The consensus strategy to use.</param>
    /// <param name="threshold">The threshold for consensus (0.0 to 1.0).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when threshold is invalid.</exception>
    public ConsensusProtocol(ConsensusStrategy strategy, double threshold = 0.5)
    {
        if (threshold < 0.0 || threshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold must be between 0.0 and 1.0.");
        }

        this.strategy = strategy;
        this.customThreshold = threshold;
    }

    /// <summary>
    /// Evaluates the given votes and determines if consensus is reached.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <returns>The consensus result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when votes is null.</exception>
    public ConsensusResult Evaluate(IReadOnlyList<AgentVote> votes)
    {
        ArgumentNullException.ThrowIfNull(votes);

        if (votes.Count == 0)
        {
            return ConsensusResult.NoConsensus(votes, strategy.ToString());
        }

        return strategy switch
        {
            ConsensusStrategy.Majority => EvaluateMajority(votes),
            ConsensusStrategy.SuperMajority => EvaluateSuperMajority(votes),
            ConsensusStrategy.Unanimous => EvaluateUnanimous(votes),
            ConsensusStrategy.WeightedByConfidence => EvaluateWeightedByConfidence(votes),
            ConsensusStrategy.HighestConfidence => EvaluateHighestConfidence(votes),
            ConsensusStrategy.RankedChoice => EvaluateRankedChoice(votes),
            _ => ConsensusResult.NoConsensus(votes, strategy.ToString()),
        };
    }

    /// <summary>
    /// Determines if the votes meet a specified threshold.
    /// </summary>
    /// <param name="votes">The votes to evaluate.</param>
    /// <param name="threshold">The threshold to meet (0.0 to 1.0).</param>
    /// <returns>True if threshold is met; otherwise, false.</returns>
    public bool MeetsThreshold(IReadOnlyList<AgentVote> votes, double threshold)
    {
        ArgumentNullException.ThrowIfNull(votes);

        if (votes.Count == 0)
        {
            return false;
        }

        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        int maxVotes = voteCounts.Values.Max();
        double ratio = (double)maxVotes / votes.Count;

        return ratio > threshold;
    }

    /// <summary>
    /// Evaluates votes using simple majority (greater than 50%).
    /// </summary>
    private ConsensusResult EvaluateMajority(IReadOnlyList<AgentVote> votes)
    {
        return EvaluateThresholdBased(votes, MajorityThreshold, nameof(ConsensusStrategy.Majority));
    }

    /// <summary>
    /// Evaluates votes using super majority (greater than 66%).
    /// </summary>
    private ConsensusResult EvaluateSuperMajority(IReadOnlyList<AgentVote> votes)
    {
        return EvaluateThresholdBased(votes, SuperMajorityThreshold, nameof(ConsensusStrategy.SuperMajority));
    }

    /// <summary>
    /// Evaluates votes requiring unanimous agreement.
    /// </summary>
    private ConsensusResult EvaluateUnanimous(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        // Unanimous requires all votes for the same option
        if (voteCounts.Count == 1)
        {
            KeyValuePair<string, int> winner = voteCounts.First();
            double avgConfidence = confidenceByOption[winner.Key] / winner.Value;

            return new ConsensusResult(
                WinningOption: winner.Key,
                AggregateConfidence: avgConfidence,
                AllVotes: votes,
                VoteCounts: voteCounts,
                ConfidenceByOption: confidenceByOption,
                HasConsensus: true,
                Protocol: nameof(ConsensusStrategy.Unanimous));
        }

        return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.Unanimous));
    }

    /// <summary>
    /// Evaluates votes using confidence-weighted voting.
    /// </summary>
    private ConsensusResult EvaluateWeightedByConfidence(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        double totalConfidence = votes.Sum(v => v.Confidence);

        if (totalConfidence <= 0.0)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.WeightedByConfidence));
        }

        // Find the option with the highest weighted confidence
        string? winningOption = null;
        double maxWeightedConfidence = 0.0;

        foreach (KeyValuePair<string, double> kvp in confidenceByOption)
        {
            double weightedRatio = kvp.Value / totalConfidence;
            if (weightedRatio > maxWeightedConfidence)
            {
                maxWeightedConfidence = weightedRatio;
                winningOption = kvp.Key;
            }
        }

        if (winningOption == null || maxWeightedConfidence <= customThreshold)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.WeightedByConfidence));
        }

        return new ConsensusResult(
            WinningOption: winningOption,
            AggregateConfidence: maxWeightedConfidence,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: true,
            Protocol: nameof(ConsensusStrategy.WeightedByConfidence));
    }

    /// <summary>
    /// Evaluates votes selecting the single highest confidence vote.
    /// </summary>
    private ConsensusResult EvaluateHighestConfidence(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        // Find the vote with the highest individual confidence
        AgentVote? highestConfidenceVote = null;
        double maxConfidence = -1.0;

        foreach (AgentVote vote in votes)
        {
            if (vote.Confidence > maxConfidence)
            {
                maxConfidence = vote.Confidence;
                highestConfidenceVote = vote;
            }
        }

        if (highestConfidenceVote == null)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.HighestConfidence));
        }

        return new ConsensusResult(
            WinningOption: highestConfidenceVote.Option,
            AggregateConfidence: highestConfidenceVote.Confidence,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: true,
            Protocol: nameof(ConsensusStrategy.HighestConfidence));
    }

    /// <summary>
    /// Evaluates votes using ranked choice voting with elimination.
    /// </summary>
    private ConsensusResult EvaluateRankedChoice(IReadOnlyList<AgentVote> votes)
    {
        // For ranked choice, we simulate elimination rounds
        // Since we have single votes per agent, we use confidence as tiebreaker
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        if (voteCounts.Count == 0)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.RankedChoice));
        }

        // Check if any option has majority
        int totalVotes = votes.Count;
        foreach (KeyValuePair<string, int> kvp in voteCounts)
        {
            double ratio = (double)kvp.Value / totalVotes;
            if (ratio > MajorityThreshold)
            {
                double avgConfidence = confidenceByOption[kvp.Key] / kvp.Value;
                return new ConsensusResult(
                    WinningOption: kvp.Key,
                    AggregateConfidence: avgConfidence,
                    AllVotes: votes,
                    VoteCounts: voteCounts,
                    ConfidenceByOption: confidenceByOption,
                    HasConsensus: true,
                    Protocol: nameof(ConsensusStrategy.RankedChoice));
            }
        }

        // If no majority, select option with highest confidence-weighted votes
        string? bestOption = null;
        double bestScore = -1.0;

        foreach (KeyValuePair<string, double> kvp in confidenceByOption)
        {
            if (kvp.Value > bestScore)
            {
                bestScore = kvp.Value;
                bestOption = kvp.Key;
            }
        }

        if (bestOption == null)
        {
            return ConsensusResult.NoConsensus(votes, nameof(ConsensusStrategy.RankedChoice));
        }

        int winnerVoteCount = voteCounts[bestOption];
        double avgWinnerConfidence = confidenceByOption[bestOption] / winnerVoteCount;

        return new ConsensusResult(
            WinningOption: bestOption,
            AggregateConfidence: avgWinnerConfidence,
            AllVotes: votes,
            VoteCounts: voteCounts,
            ConfidenceByOption: confidenceByOption,
            HasConsensus: true,
            Protocol: nameof(ConsensusStrategy.RankedChoice));
    }

    /// <summary>
    /// Evaluates votes using a threshold-based approach.
    /// </summary>
    private ConsensusResult EvaluateThresholdBased(IReadOnlyList<AgentVote> votes, double threshold, string protocolName)
    {
        Dictionary<string, int> voteCounts = CalculateVoteCounts(votes);
        Dictionary<string, double> confidenceByOption = CalculateConfidenceByOption(votes);

        int totalVotes = votes.Count;
        string? winningOption = null;
        int maxVotes = 0;

        foreach (KeyValuePair<string, int> kvp in voteCounts)
        {
            if (kvp.Value > maxVotes)
            {
                maxVotes = kvp.Value;
                winningOption = kvp.Key;
            }
        }

        if (winningOption == null)
        {
            return ConsensusResult.NoConsensus(votes, protocolName);
        }

        double ratio = (double)maxVotes / totalVotes;

        if (ratio > threshold)
        {
            double avgConfidence = confidenceByOption[winningOption] / maxVotes;

            return new ConsensusResult(
                WinningOption: winningOption,
                AggregateConfidence: avgConfidence,
                AllVotes: votes,
                VoteCounts: voteCounts,
                ConfidenceByOption: confidenceByOption,
                HasConsensus: true,
                Protocol: protocolName);
        }

        return ConsensusResult.NoConsensus(votes, protocolName);
    }

    /// <summary>
    /// Calculates vote counts per option.
    /// </summary>
    private static Dictionary<string, int> CalculateVoteCounts(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (AgentVote vote in votes)
        {
            if (counts.TryGetValue(vote.Option, out int count))
            {
                counts[vote.Option] = count + 1;
            }
            else
            {
                counts[vote.Option] = 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Calculates aggregate confidence per option.
    /// </summary>
    private static Dictionary<string, double> CalculateConfidenceByOption(IReadOnlyList<AgentVote> votes)
    {
        Dictionary<string, double> confidence = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (AgentVote vote in votes)
        {
            if (confidence.TryGetValue(vote.Option, out double existing))
            {
                confidence[vote.Option] = existing + vote.Confidence;
            }
            else
            {
                confidence[vote.Option] = vote.Confidence;
            }
        }

        return confidence;
    }
}

/// <summary>
/// Manages a voting session for multi-agent consensus decisions.
/// </summary>
public sealed class VotingSession
{
    private readonly Guid sessionId;
    private readonly string topic;
    private readonly IReadOnlyList<string> options;
    private readonly IConsensusProtocol protocol;
    private readonly List<AgentVote> votes;
    private readonly HashSet<Guid> votedAgents;
    private readonly object syncLock = new object();

    /// <summary>
    /// Gets the unique identifier for this voting session.
    /// </summary>
    public Guid SessionId => sessionId;

    /// <summary>
    /// Gets the topic being voted on.
    /// </summary>
    public string Topic => topic;

    /// <summary>
    /// Gets the available options for voting.
    /// </summary>
    public IReadOnlyList<string> Options => options;

    /// <summary>
    /// Gets the number of votes cast so far.
    /// </summary>
    public int VoteCount
    {
        get
        {
            lock (syncLock)
            {
                return votes.Count;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VotingSession"/> class.
    /// </summary>
    /// <param name="sessionId">The unique session identifier.</param>
    /// <param name="topic">The topic being voted on.</param>
    /// <param name="options">The available voting options.</param>
    /// <param name="protocol">The consensus protocol to use.</param>
    private VotingSession(Guid sessionId, string topic, IReadOnlyList<string> options, IConsensusProtocol protocol)
    {
        this.sessionId = sessionId;
        this.topic = topic;
        this.options = options;
        this.protocol = protocol;
        this.votes = new List<AgentVote>();
        this.votedAgents = new HashSet<Guid>();
    }

    /// <summary>
    /// Creates a new voting session.
    /// </summary>
    /// <param name="topic">The topic being voted on.</param>
    /// <param name="options">The available voting options.</param>
    /// <param name="protocol">The consensus protocol to use.</param>
    /// <returns>A new <see cref="VotingSession"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when options is empty.</exception>
    public static VotingSession Create(string topic, IReadOnlyList<string> options, IConsensusProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(protocol);

        if (options.Count == 0)
        {
            throw new ArgumentException("At least one option must be provided.", nameof(options));
        }

        return new VotingSession(Guid.NewGuid(), topic, options.ToImmutableList(), protocol);
    }

    /// <summary>
    /// Casts a vote in this session.
    /// </summary>
    /// <param name="vote">The vote to cast.</param>
    /// <exception cref="ArgumentNullException">Thrown when vote is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when agent has already voted or option is invalid.</exception>
    public void CastVote(AgentVote vote)
    {
        ArgumentNullException.ThrowIfNull(vote);

        lock (syncLock)
        {
            if (votedAgents.Contains(vote.AgentId))
            {
                throw new InvalidOperationException($"Agent {vote.AgentId} has already voted in this session.");
            }

            bool isValidOption = false;
            foreach (string option in options)
            {
                if (string.Equals(option, vote.Option, StringComparison.Ordinal))
                {
                    isValidOption = true;
                    break;
                }
            }

            if (!isValidOption)
            {
                throw new InvalidOperationException($"Option '{vote.Option}' is not a valid option for this session.");
            }

            votes.Add(vote);
            votedAgents.Add(vote.AgentId);
        }
    }

    /// <summary>
    /// Checks if an agent has already voted.
    /// </summary>
    /// <param name="agentId">The agent identifier to check.</param>
    /// <returns>True if the agent has voted; otherwise, false.</returns>
    public bool HasVoted(Guid agentId)
    {
        lock (syncLock)
        {
            return votedAgents.Contains(agentId);
        }
    }

    /// <summary>
    /// Attempts to get the consensus result if available.
    /// </summary>
    /// <returns>Some with the result if consensus is reached; otherwise, None.</returns>
    public Option<ConsensusResult> TryGetResult()
    {
        ConsensusResult result = GetResult();

        if (result.HasConsensus)
        {
            return Option<ConsensusResult>.Some(result);
        }

        return Option<ConsensusResult>.None();
    }

    /// <summary>
    /// Gets the current consensus result.
    /// </summary>
    /// <returns>The current consensus result.</returns>
    public ConsensusResult GetResult()
    {
        IReadOnlyList<AgentVote> currentVotes;

        lock (syncLock)
        {
            currentVotes = votes.ToImmutableList();
        }

        return protocol.Evaluate(currentVotes);
    }

    /// <summary>
    /// Gets a snapshot of all votes cast in this session.
    /// </summary>
    /// <returns>An immutable list of all votes.</returns>
    public IReadOnlyList<AgentVote> GetVotes()
    {
        lock (syncLock)
        {
            return votes.ToImmutableList();
        }
    }
}
