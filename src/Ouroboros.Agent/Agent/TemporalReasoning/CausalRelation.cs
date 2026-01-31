// <copyright file="CausalRelation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Represents a causal relationship between two temporal events.
/// </summary>
public sealed record CausalRelation(
    TemporalEvent Cause,
    TemporalEvent Effect,
    double CausalStrength,
    string Mechanism,
    IReadOnlyList<string> ConfoundingFactors);
