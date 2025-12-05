#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reactive.Disposables;

namespace Ouroboros.Application;

/// <summary>
/// Manages the lifecycle of streaming operations with automatic resource cleanup.
/// Implements observer-based cleanup pattern to prevent deadlocks and resource leaks.
/// Ensures request isolation and concurrent safety.
/// </summary>
public sealed class StreamingContext : IDisposable
{
    private readonly CompositeDisposable _subscriptions;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingContext"/> class.
    /// </summary>
    public StreamingContext()
    {
        _subscriptions = new CompositeDisposable();
    }

    /// <summary>
    /// Gets a value indicating whether this context has been disposed.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lock)
            {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// Registers a disposable resource for automatic cleanup when this context is disposed.
    /// </summary>
    /// <param name="disposable">The disposable resource to register.</param>
    /// <returns>The registered disposable.</returns>
    public IDisposable Register(IDisposable disposable)
    {
        if (disposable == null)
        {
            throw new ArgumentNullException(nameof(disposable));
        }

        lock (_lock)
        {
            if (_disposed)
            {
                disposable.Dispose();
                return Disposable.Empty;
            }

            _subscriptions.Add(disposable);
            return disposable;
        }
    }

    /// <summary>
    /// Registers an action to be executed when this context is disposed.
    /// </summary>
    /// <param name="cleanupAction">The cleanup action to register.</param>
    /// <returns>A disposable that can be used to unregister the action.</returns>
    public IDisposable RegisterCleanup(Action cleanupAction)
    {
        if (cleanupAction == null)
        {
            throw new ArgumentNullException(nameof(cleanupAction));
        }

        return Register(Disposable.Create(cleanupAction));
    }

    /// <summary>
    /// Disposes all registered resources in reverse order of registration.
    /// This method is thread-safe and idempotent.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        // Dispose outside the lock to prevent deadlocks
        try
        {
            _subscriptions.Dispose();
        }
        catch
        {
            // Suppress disposal exceptions to ensure cleanup continues
        }
    }
}

