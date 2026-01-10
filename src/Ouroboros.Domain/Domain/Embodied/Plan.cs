// <copyright file="Plan.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Represents a plan of embodied actions to achieve a goal.
/// Used by embodied planning algorithms.
/// </summary>
/// <param name="Goal">The goal to achieve</param>
/// <param name="Actions">Sequence of actions to execute</param>
/// <param name="ExpectedStates">Expected sensor states after each action</param>
/// <param name="Confidence">Confidence score for the plan (0-1)</param>
/// <param name="EstimatedReward">Estimated cumulative reward</param>
public sealed record Plan(
    string Goal,
    IReadOnlyList<EmbodiedAction> Actions,
    IReadOnlyList<SensorState> ExpectedStates,
    double Confidence,
    double EstimatedReward);
