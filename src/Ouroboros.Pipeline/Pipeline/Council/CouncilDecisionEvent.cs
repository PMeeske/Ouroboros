// <copyright file="CouncilDecisionEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Domain.Events;

namespace LangChainPipeline.Pipeline.Council;

/// <summary>
/// Event representing a council decision in the pipeline execution.
/// Captures the complete debate outcome for event sourcing and replay.
/// </summary>
/// <param name="Id">Unique identifier for this event.</param>
/// <param name="Topic">The topic that was debated.</param>
/// <param name="Decision">The council's decision.</param>
/// <param name="Timestamp">When this decision was made.</param>
public sealed record CouncilDecisionEvent(
    Guid Id,
    CouncilTopic Topic,
    CouncilDecision Decision,
    DateTime Timestamp) : PipelineEvent(Id, "CouncilDecision", Timestamp)
{
    /// <summary>
    /// Creates a new CouncilDecisionEvent with auto-generated ID and current timestamp.
    /// </summary>
    /// <param name="topic">The topic that was debated.</param>
    /// <param name="decision">The council's decision.</param>
    /// <returns>A new CouncilDecisionEvent.</returns>
    public static CouncilDecisionEvent Create(CouncilTopic topic, CouncilDecision decision) =>
        new(Guid.NewGuid(), topic, decision, DateTime.UtcNow);
}
