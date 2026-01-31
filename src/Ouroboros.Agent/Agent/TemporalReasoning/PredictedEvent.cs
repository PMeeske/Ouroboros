// <copyright file="PredictedEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Represents a predicted future event based on temporal patterns.
/// </summary>
public sealed record PredictedEvent(
    string EventType,
    string Description,
    DateTime PredictedTime,
    double Confidence,
    List<TemporalEvent> BasedOnEvents,
    string ReasoningExplanation);
