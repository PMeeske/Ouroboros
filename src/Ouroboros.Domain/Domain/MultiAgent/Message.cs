// <copyright file="Message.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MultiAgent;

/// <summary>
/// Represents a message passed between agents in the multi-agent system.
/// </summary>
/// <param name="Sender">The agent sending the message.</param>
/// <param name="Recipient">The intended recipient agent, or null for broadcast.</param>
/// <param name="Type">The type of message being sent.</param>
/// <param name="Payload">The message content.</param>
/// <param name="Timestamp">When the message was created.</param>
/// <param name="ConversationId">Identifier grouping related messages.</param>
public sealed record Message(
    AgentId Sender,
    AgentId? Recipient,
    MessageType Type,
    object Payload,
    DateTime Timestamp,
    Guid ConversationId);
