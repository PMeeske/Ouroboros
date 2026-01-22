// <copyright file="DisposableResource.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Core;

/// <summary>
/// Base class for implementing the disposable pattern with monadic resource management.
/// Provides a consistent way to manage resources throughout the pipeline.
/// </summary>
public abstract class DisposableResource : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether this instance has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <summary>
    /// Throws if this instance has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if disposed.</exception>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    protected virtual void ReleaseManagedResources()
    {
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// </summary>
    protected virtual void ReleaseUnmanagedResources()
    {
    }

    /// <summary>
    /// Asynchronously releases managed resources.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    protected virtual ValueTask ReleaseManagedResourcesAsync() => default;

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            ReleaseManagedResources();
        }

        ReleaseUnmanagedResources();
        _disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await ReleaseManagedResourcesAsync().ConfigureAwait(false);
        ReleaseManagedResources();
        ReleaseUnmanagedResources();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~DisposableResource()
    {
        Dispose(false);
    }
}

/// <summary>
/// Extension methods for working with disposable resources in pipelines.
/// </summary>
public static class DisposableExtensions
{
    /// <summary>
    /// Executes an action within a using scope, ensuring the resource is disposed.
    /// Returns a Result monad for error handling.
    /// </summary>
    /// <typeparam name="TResource">The disposable resource type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="resource">The resource to use.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A Result containing the action result or an error.</returns>
    public static Result<TResult, Exception> Use<TResource, TResult>(
        this TResource resource,
        Func<TResource, TResult> action)
        where TResource : IDisposable
    {
        try
        {
            using (resource)
            {
                return Result<TResult, Exception>.Success(action(resource));
            }
        }
        catch (Exception ex)
        {
            return Result<TResult, Exception>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async action within a using scope, ensuring the resource is disposed.
    /// Returns a Result monad for error handling.
    /// </summary>
    /// <typeparam name="TResource">The async disposable resource type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="resource">The resource to use.</param>
    /// <param name="action">The async action to execute.</param>
    /// <returns>A task with a Result containing the action result or an error.</returns>
    public static async Task<Result<TResult, Exception>> UseAsync<TResource, TResult>(
        this TResource resource,
        Func<TResource, Task<TResult>> action)
        where TResource : IAsyncDisposable
    {
        try
        {
            await using (resource.ConfigureAwait(false))
            {
                return Result<TResult, Exception>.Success(await action(resource).ConfigureAwait(false));
            }
        }
        catch (Exception ex)
        {
            return Result<TResult, Exception>.Failure(ex);
        }
    }
}
