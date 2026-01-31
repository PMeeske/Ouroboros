// <copyright file="ITemporalReasoner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Interface for temporal reasoning capabilities.
/// Enables reasoning about time, sequences, causality, and temporal relationships between events.
/// </summary>
public interface ITemporalReasoner
{
    /// <summary>
    /// Determines temporal relationship between two events.
    /// </summary>
    /// <param name="event1">First temporal event.</param>
    /// <param name="event2">Second temporal event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The temporal relation between the events or an error message.</returns>
    Task<Result<TemporalRelation, string>> GetRelationAsync(
        TemporalEvent event1,
        TemporalEvent event2,
        CancellationToken ct = default);

    /// <summary>
    /// Queries events matching temporal constraints.
    /// </summary>
    /// <param name="query">Query parameters for filtering events.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching temporal events or an error message.</returns>
    Task<Result<List<TemporalEvent>, string>> QueryEventsAsync(
        TemporalQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Infers causal relationships from temporal patterns.
    /// </summary>
    /// <param name="events">Collection of temporal events to analyze.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of inferred causal relations or an error message.</returns>
    Task<Result<List<CausalRelation>, string>> InferCausalityAsync(
        IReadOnlyList<TemporalEvent> events,
        CancellationToken ct = default);

    /// <summary>
    /// Predicts future events based on temporal patterns.
    /// </summary>
    /// <param name="history">Historical temporal events to base predictions on.</param>
    /// <param name="horizon">Time horizon for predictions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of predicted events or an error message.</returns>
    Task<Result<List<PredictedEvent>, string>> PredictFutureEventsAsync(
        IReadOnlyList<TemporalEvent> history,
        TimeSpan horizon,
        CancellationToken ct = default);

    /// <summary>
    /// Constructs a timeline from a set of events.
    /// </summary>
    /// <param name="events">Collection of temporal events to organize into a timeline.</param>
    /// <returns>A constructed timeline or an error message.</returns>
    Result<Timeline, string> ConstructTimeline(IReadOnlyList<TemporalEvent> events);

    /// <summary>
    /// Checks if a temporal constraint is satisfiable.
    /// </summary>
    /// <param name="constraints">List of temporal constraints to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if constraints are satisfiable, false otherwise, or an error message.</returns>
    Task<Result<bool, string>> CheckConstraintSatisfiabilityAsync(
        List<TemporalConstraint> constraints,
        CancellationToken ct = default);
}
