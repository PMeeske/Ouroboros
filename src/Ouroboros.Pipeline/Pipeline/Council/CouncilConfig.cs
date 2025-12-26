// <copyright file="CouncilConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Configuration settings for council debate sessions.
/// </summary>
/// <param name="MaxRoundsPerPhase">Maximum number of rounds per debate phase.</param>
/// <param name="ConsensusThreshold">Threshold for consensus (0.0 to 1.0).</param>
/// <param name="TimeoutPerAgent">Maximum time allowed per agent response.</param>
/// <param name="RequireUnanimity">Whether unanimous agreement is required.</param>
/// <param name="EnableMinorityReport">Whether to record minority opinions.</param>
public sealed record CouncilConfig(
    int MaxRoundsPerPhase = 3,
    double ConsensusThreshold = 0.7,
    TimeSpan? TimeoutPerAgent = null,
    bool RequireUnanimity = false,
    bool EnableMinorityReport = true)
{
    /// <summary>
    /// Gets the default configuration for council debates.
    /// </summary>
    public static CouncilConfig Default => new();

    /// <summary>
    /// Gets a strict configuration requiring unanimous agreement.
    /// </summary>
    public static CouncilConfig Strict => new(
        MaxRoundsPerPhase: 5,
        ConsensusThreshold: 1.0,
        RequireUnanimity: true,
        EnableMinorityReport: true);

    /// <summary>
    /// Gets a fast configuration with fewer rounds.
    /// </summary>
    public static CouncilConfig Fast => new(
        MaxRoundsPerPhase: 1,
        ConsensusThreshold: 0.5,
        RequireUnanimity: false,
        EnableMinorityReport: false);
}
