// <copyright file="ActionResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Represents the result of executing an embodied action.
/// Contains the resulting state, reward, and episode termination status.
/// </summary>
/// <param name="Success">Whether the action executed successfully</param>
/// <param name="ResultingState">The sensor state after action execution</param>
/// <param name="Reward">Reward signal from the environment</param>
/// <param name="EpisodeTerminated">Whether the episode has ended</param>
/// <param name="FailureReason">Optional failure reason if Success is false</param>
public sealed record ActionResult(
    bool Success,
    SensorState ResultingState,
    double Reward,
    bool EpisodeTerminated,
    string? FailureReason = null);
