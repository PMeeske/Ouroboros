// <copyright file="ConsensusProtocol.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Protocol for reaching consensus among agents.
/// </summary>
public enum ConsensusProtocol
{
    /// <summary>
    /// Simple majority voting (>50% approval).
    /// </summary>
    Majority,

    /// <summary>
    /// All agents must agree (100% approval).
    /// </summary>
    Unanimous,

    /// <summary>
    /// Weighted voting based on agent confidence or reputation.
    /// </summary>
    Weighted,

    /// <summary>
    /// Raft consensus algorithm for distributed systems.
    /// </summary>
    Raft,
}
