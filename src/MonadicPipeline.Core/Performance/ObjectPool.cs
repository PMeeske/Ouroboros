using System.Collections.Concurrent;
using System.Text;

namespace LangChainPipeline.Core.Performance;

/// <summary>
/// Generic object pool for reducing memory allocations.
/// </summary>
/// <typeparam name="T">Type of objects to pool</typeparam>
public class ObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> _objects = new();
    private readonly Func<T> _objectFactory;
    private readonly Action<T>? _resetAction;
    private readonly int _maxPoolSize;
    private int _currentPoolSize;

    /// <summary>
    /// Initializes a new object pool.
    /// </summary>
    /// <param name="objectFactory">Factory function to create new objects</param>
    /// <param name="resetAction">Optional action to reset objects when returned to pool</param>
    /// <param name="maxPoolSize">Maximum number of objects to keep in pool</param>
    public ObjectPool(Func<T> objectFactory, Action<T>? resetAction = null, int maxPoolSize = 100)
    {
        _objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        _resetAction = resetAction;
        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Rents an object from the pool or creates a new one if none available.
    /// </summary>
    public T Rent()
    {
        if (_objects.TryTake(out var obj))
        {
            Interlocked.Decrement(ref _currentPoolSize);
            return obj;
        }

        return _objectFactory();
    }

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// </summary>
    public void Return(T obj)
    {
        if (obj == null)
            return;

        // Don't add to pool if we're at capacity
        if (_currentPoolSize >= _maxPoolSize)
            return;

        // Reset the object if a reset action is provided
        _resetAction?.Invoke(obj);

        _objects.Add(obj);
        Interlocked.Increment(ref _currentPoolSize);
    }

    /// <summary>
    /// Gets the current number of objects in the pool.
    /// </summary>
    public int Count => _currentPoolSize;

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    public void Clear()
    {
        while (_objects.TryTake(out _))
        {
            Interlocked.Decrement(ref _currentPoolSize);
        }
    }
}

/// <summary>
/// Disposable wrapper for pooled objects that automatically returns them to the pool.
/// </summary>
/// <typeparam name="T">Type of pooled object</typeparam>
public struct PooledObject<T> : IDisposable where T : class
{
    private readonly ObjectPool<T> _pool;
    private T? _object;

    internal PooledObject(ObjectPool<T> pool, T obj)
    {
        _pool = pool;
        _object = obj;
    }

    /// <summary>
    /// Gets the pooled object.
    /// </summary>
    public T Object => _object ?? throw new ObjectDisposedException(nameof(PooledObject<T>));

    /// <summary>
    /// Returns the object to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_object != null)
        {
            _pool.Return(_object);
            _object = null;
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
    public static PooledObject<T> RentDisposable<T>(this ObjectPool<T> pool) where T : class
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
    /// Pool for List<string> instances.
    /// </summary>
    public static readonly ObjectPool<List<string>> StringList = new(
        () => new List<string>(),
        list => list.Clear(),
        maxPoolSize: 50);

    /// <summary>
    /// Pool for Dictionary<string, string> instances.
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
    public static string WithStringBuilder(Action<StringBuilder> action)
    {
        using var pooled = CommonPools.StringBuilder.RentDisposable();
        var sb = pooled.Object;
        action(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Executes a function with a pooled List<string> and returns the result.
    /// </summary>
    public static TResult WithStringList<TResult>(Func<List<string>, TResult> func)
    {
        using var pooled = CommonPools.StringList.RentDisposable();
        return func(pooled.Object);
    }

    /// <summary>
    /// Executes a function with a pooled Dictionary<string, string> and returns the result.
    /// </summary>
    public static TResult WithStringDictionary<TResult>(Func<Dictionary<string, string>, TResult> func)
    {
        using var pooled = CommonPools.StringDictionary.RentDisposable();
        return func(pooled.Object);
    }

    /// <summary>
    /// Executes a function with a pooled MemoryStream and returns the result.
    /// </summary>
    public static TResult WithMemoryStream<TResult>(Func<MemoryStream, TResult> func)
    {
        using var pooled = CommonPools.MemoryStream.RentDisposable();
        return func(pooled.Object);
    }
}
