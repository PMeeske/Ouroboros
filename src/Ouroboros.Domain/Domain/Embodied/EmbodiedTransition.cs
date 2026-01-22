// <copyright file="EmbodiedTransition.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Represents a state transition in embodied learning.
/// Used for experience replay and reinforcement learning.
/// </summary>
/// <param name="StateBefore">Sensor state before action</param>
/// <param name="Action">Action taken</param>
/// <param name="StateAfter">Sensor state after action</param>
/// <param name="Reward">Reward received</param>
/// <param name="Terminal">Whether this transition ended the episode</param>
public sealed record EmbodiedTransition(
    SensorState StateBefore,
    EmbodiedAction Action,
    SensorState StateAfter,
    double Reward,
    bool Terminal);
