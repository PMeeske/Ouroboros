// <copyright file="StateTransition.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.WorldModel;

using Ouroboros.Domain.Embodied;

/// <summary>
/// Internal representation of state transitions for world model learning.
/// </summary>
/// <param name="FromState">Source sensor state</param>
/// <param name="Action">Action taken</param>
/// <param name="ToState">Resulting sensor state</param>
/// <param name="Reward">Observed reward</param>
/// <param name="Terminal">Whether transition ended episode</param>
/// <param name="ObservedAt">Timestamp of observation</param>
public sealed record StateTransition(
    SensorState FromState,
    EmbodiedAction Action,
    SensorState ToState,
    double Reward,
    bool Terminal,
    DateTime ObservedAt);
