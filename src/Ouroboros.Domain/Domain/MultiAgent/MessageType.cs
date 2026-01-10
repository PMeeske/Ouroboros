// <copyright file="MessageType.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Defines types of messages that can be exchanged between agents.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// A query message requesting information.
    /// </summary>
    Query,

    /// <summary>
    /// An answer message providing information.
    /// </summary>
    Answer,

    /// <summary>
    /// A proposal message suggesting an action or decision.
    /// </summary>
    Proposal,

    /// <summary>
    /// A vote message in a consensus protocol.
    /// </summary>
    Vote,

    /// <summary>
    /// A knowledge message sharing information.
    /// </summary>
    Knowledge,

    /// <summary>
    /// A request message asking for action.
    /// </summary>
    Request,

    /// <summary>
    /// A notification message informing about events.
    /// </summary>
    Notification,

    /// <summary>
    /// A heartbeat message indicating agent is alive.
    /// </summary>
    Heartbeat,
}
