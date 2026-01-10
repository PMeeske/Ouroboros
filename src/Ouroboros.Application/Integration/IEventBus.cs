// <copyright file="IEventBus.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.Reactive.Subjects;

/// <summary>
/// Interface for the event bus enabling cross-feature communication.
/// Uses reactive extensions (IObservable) for event streaming.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">The event to publish.</param>
    void Publish<TEvent>(TEvent @event) where TEvent : class;

    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <returns>An observable stream of events.</returns>
    IObservable<TEvent> Subscribe<TEvent>() where TEvent : class;

    /// <summary>
    /// Clears all subscriptions and resets the event bus.
    /// </summary>
    void Clear();
}

/// <summary>
/// Base event type for system-wide events.
/// </summary>
public abstract record SystemEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source);

/// <summary>
/// Event fired when a goal is executed.
/// </summary>
public sealed record GoalExecutedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    string Goal,
    bool Success,
    TimeSpan Duration) : SystemEvent(EventId, Timestamp, Source);

/// <summary>
/// Event fired when learning occurs.
/// </summary>
public sealed record LearningCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    int EpisodesProcessed,
    int RulesLearned) : SystemEvent(EventId, Timestamp, Source);

/// <summary>
/// Event fired when reasoning completes.
/// </summary>
public sealed record ReasoningCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    string Query,
    string Answer,
    double Confidence) : SystemEvent(EventId, Timestamp, Source);

/// <summary>
/// Event fired when consciousness state changes.
/// </summary>
public sealed record ConsciousnessStateChangedEvent(
    Guid EventId,
    DateTime Timestamp,
    string Source,
    string NewState,
    List<string> ActiveItems) : SystemEvent(EventId, Timestamp, Source);
