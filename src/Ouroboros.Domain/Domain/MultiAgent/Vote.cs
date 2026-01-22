// <copyright file="Vote.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Represents a single vote in a consensus protocol.
/// </summary>
/// <param name="Voter">The agent casting the vote.</param>
/// <param name="InFavor">Whether the vote is in favor of the proposal.</param>
/// <param name="Confidence">Confidence level of the vote (0.0 to 1.0).</param>
/// <param name="Reasoning">Optional explanation for the vote.</param>
public sealed record Vote(
    AgentId Voter,
    bool InFavor,
    double Confidence,
    string? Reasoning);
