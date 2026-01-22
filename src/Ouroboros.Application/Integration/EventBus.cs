// <copyright file="EventBus.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.Reactive.Linq;
using System.Reactive.Subjects;

/// <summary>
/// Simple in-memory event bus implementation.
/// Provides publish-subscribe pattern for cross-component communication.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, object> _subjects = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);

        var subject = GetOrCreateSubject<TEvent>();
        subject.OnNext(@event);
    }

    /// <inheritdoc/>
    public IObservable<TEvent> Subscribe<TEvent>() where TEvent : class
    {
        return GetOrCreateSubject<TEvent>().AsObservable();
    }

    private ISubject<TEvent> GetOrCreateSubject<TEvent>() where TEvent : class
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_subjects.TryGetValue(eventType, out var subject))
            {
                subject = new Subject<TEvent>();
                _subjects[eventType] = subject;
            }

            return (ISubject<TEvent>)subject;
        }
    }
}
