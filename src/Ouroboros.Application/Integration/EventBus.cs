// <copyright file="EventBus.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

/// <summary>
/// Implementation of the event bus for cross-feature communication.
/// Thread-safe implementation using reactive subjects for each event type.
/// </summary>
public sealed class EventBus : IEventBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, object> _subjects = new();
    private bool _disposed;

    /// <inheritdoc/>
    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        ThrowIfDisposed();

        var subject = GetOrCreateSubject<TEvent>();
        subject.OnNext(@event);
    }

    /// <inheritdoc/>
    public IObservable<TEvent> Subscribe<TEvent>() where TEvent : class
    {
        ThrowIfDisposed();
        return GetOrCreateSubject<TEvent>().AsObservable();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ThrowIfDisposed();

        foreach (var kvp in _subjects)
        {
            if (kvp.Value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _subjects.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
    }

    private Subject<TEvent> GetOrCreateSubject<TEvent>() where TEvent : class
    {
        return (Subject<TEvent>)_subjects.GetOrAdd(
            typeof(TEvent),
            _ => new Subject<TEvent>());
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EventBus));
        }
    }
}
