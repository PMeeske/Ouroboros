// <copyright file="GroupType.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Defines how messages are distributed within an agent group.
/// </summary>
public enum GroupType
{
    /// <summary>
    /// All members in the group receive the message.
    /// </summary>
    Broadcast,

    /// <summary>
    /// One member receives in rotation (round-robin).
    /// </summary>
    RoundRobin,

    /// <summary>
    /// The least loaded member receives the message.
    /// </summary>
    LoadBalanced,
}
