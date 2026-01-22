// <copyright file="GoalExecutedEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Event published when a goal has been executed.
/// Records execution outcome and timing information.
/// </summary>
/// <param name="Id">Unique identifier for the event.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="GoalId">Identifier of the goal that was executed.</param>
/// <param name="GoalDescription">Human-readable description of the goal.</param>
/// <param name="Success">Whether the goal execution succeeded.</param>
/// <param name="ExecutionTime">Time taken to execute the goal.</param>
public sealed record GoalExecutedEvent(
    Guid Id,
    DateTime Timestamp,
    string GoalId,
    string GoalDescription,
    bool Success,
    TimeSpan ExecutionTime);
