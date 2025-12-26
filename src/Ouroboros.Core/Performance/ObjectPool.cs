// <copyright file="ObjectPool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Performance;

using System.Collections.Concurrent;
using System.Text;

/// <summary>
/// Generic object pool for reducing memory allocations.
/// </summary>
/// <typeparam name="T">Type of objects to pool.</typeparam>
public class ObjectPool<T>
    where T : class
{
    private readonly ConcurrentBag<T> objects = new();
    private readonly Func<T> objectFactory;
    private readonly Action<T>? resetAction;
    private readonly int maxPoolSize;
    private int currentPoolSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
    /// Initializes a new object pool.
    /// </summary>
    /// <param name="objectFactory">Factory function to create new objects.</param>
    /// <param name="resetAction">Optional action to reset objects when returned to pool.</param>
    /// <param name="maxPoolSize">Maximum number of objects to keep in pool.</param>
    public ObjectPool(Func<T> objectFactory, Action<T>? resetAction = null, int maxPoolSize = 100)
    {
        this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        this.resetAction = resetAction;
        this.maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Rents an object from the pool or creates a new one if none available.
    /// </summary>
    /// <returns></returns>
    public T Rent()
    {
        if (this.objects.TryTake(out T? obj))
        {
            Interlocked.Decrement(ref this.currentPoolSize);
            return obj;
        }

        return this.objectFactory();
    }

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// </summary>
    public void Return(T obj)
    {
        if (obj == null)
        {
            return;
        }

        // Don't add to pool if we're at capacity
        if (this.currentPoolSize >= this.maxPoolSize)
        {
            return;
        }

        // Reset the object if a reset action is provided
        this.resetAction?.Invoke(obj);

        this.objects.Add(obj);
        Interlocked.Increment(ref this.currentPoolSize);
    }

    /// <summary>
    /// Gets the current number of objects in the pool.
    /// </summary>
    public int Count => this.currentPoolSize;

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    public void Clear()
    {
        while (this.objects.TryTake(out _))
        {
            Interlocked.Decrement(ref this.currentPoolSize);
        }
    }
}

/// <summary>
/// Disposable wrapper for pooled objects that automatically returns them to the pool.
/// </summary>
/// <typeparam name="T">Type of pooled object.</typeparam>
public struct PooledObject<T> : IDisposable
    where T : class
{
    private readonly ObjectPool<T> pool;
    private T? @object;

    internal PooledObject(ObjectPool<T> pool, T obj)
    {
        this.pool = pool;
        this.@object = obj;
    }

    /// <summary>
    /// Gets the pooled object.
    /// </summary>
    public T Object => this.@object ?? throw new ObjectDisposedException(nameof(PooledObject<T>));

    /// <summary>
    /// Returns the object to the pool.
    /// </summary>
    public void Dispose()
    {
        if (this.@object != null)
        {
            this.pool.Return(this.@object);
            this.@object = null;
        }
    }
}

/// <summary>
/// Extension methods for object pools.
/// </summary>
public static class ObjectPoolExtensions
{
    /// <summary>
    /// Rents an object from the pool and wraps it in a disposable wrapper.
    /// </summary>
    /// <returns></returns>
    public static PooledObject<T> RentDisposable<T>(this ObjectPool<T> pool)
        where T : class
    {
        return new PooledObject<T>(pool, pool.Rent());
    }
}

/// <summary>
/// Pre-configured object pools for common types.
/// </summary>
public static class CommonPools
{
    /// <summary>
    /// Pool for StringBuilder instances.
    /// </summary>
    public static readonly ObjectPool<StringBuilder> StringBuilder = new(
        () => new StringBuilder(),
        sb => sb.Clear(),
        maxPoolSize: 100);

    /// <summary>
    /// Pool for List{string} instances.
    /// </summary>
    public static readonly ObjectPool<List<string>> StringList = new(
        () => new List<string>(),
        list => list.Clear(),
        maxPoolSize: 50);

    /// <summary>
    /// Pool for Dictionary{string, string} instances.
    /// </summary>
    public static readonly ObjectPool<Dictionary<string, string>> StringDictionary = new(
        () => new Dictionary<string, string>(),
        dict => dict.Clear(),
        maxPoolSize: 50);

    /// <summary>
    /// Pool for MemoryStream instances.
    /// </summary>
    public static readonly ObjectPool<MemoryStream> MemoryStream = new(
        () => new MemoryStream(),
        ms =>
        {
            ms.SetLength(0);
            ms.Position = 0;
        },
        maxPoolSize: 20);
}

/// <summary>
/// Helper methods for working with pooled objects.
/// </summary>
public static class PooledHelpers
{
    /// <summary>
    /// Executes a function with a pooled StringBuilder and returns the result.
    /// </summary>
    /// <returns></returns>
    public static string WithStringBuilder(Action<StringBuilder> action)
    {
        using PooledObject<StringBuilder> pooled = CommonPools.StringBuilder.RentDisposable();
        StringBuilder sb = pooled.Object;
        action(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Executes a function with a pooled List{string} and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>The result of the function.</returns>
    public static TResult WithStringList<TResult>(Func<List<string>, TResult> func)
    {
        using PooledObject<List<string>> pooled = CommonPools.StringList.RentDisposable();
        return func(pooled.Object);
    }

    /// <summary>
    /// Executes a function with a pooled Dictionary{string, string} and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>The result of the function.</returns>
    public static TResult WithStringDictionary<TResult>(Func<Dictionary<string, string>, TResult> func)
    {
        using PooledObject<Dictionary<string, string>> pooled = CommonPools.StringDictionary.RentDisposable();
        return func(pooled.Object);
    }

    /// <summary>
    /// Executes a function with a pooled MemoryStream and returns the result.
    /// </summary>
    /// <returns></returns>
    public static TResult WithMemoryStream<TResult>(Func<MemoryStream, TResult> func)
    {
        using PooledObject<MemoryStream> pooled = CommonPools.MemoryStream.RentDisposable();
        return func(pooled.Object);
    }
}
