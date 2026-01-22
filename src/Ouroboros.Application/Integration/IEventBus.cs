// <copyright file="IEventBus.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Interface for event bus supporting publish-subscribe pattern.
/// Enables cross-cutting communication between components.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="event">The event instance to publish.</param>
    void Publish<TEvent>(TEvent @event) where TEvent : class;

    /// <summary>
    /// Subscribes to events of the specified type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <returns>An observable stream of events.</returns>
    IObservable<TEvent> Subscribe<TEvent>() where TEvent : class;
}
