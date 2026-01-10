// <copyright file="Decision.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Represents the outcome of a consensus protocol.
/// </summary>
/// <param name="Proposal">The proposal that was voted on.</param>
/// <param name="Accepted">Whether the proposal was accepted.</param>
/// <param name="Votes">The votes cast by each agent.</param>
/// <param name="ConsensusScore">Overall consensus score (0.0 to 1.0).</param>
public sealed record Decision(
    string Proposal,
    bool Accepted,
    Dictionary<AgentId, Vote> Votes,
    double ConsensusScore);
