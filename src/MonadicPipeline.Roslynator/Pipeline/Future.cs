using System;
using System.Threading;
using System.Threading.Tasks;

namespace LangChainPipeline.Roslynator.Pipeline;

/// <summary>
/// Lazy Future: wraps a function that returns a Task&lt;T&gt;.
/// Execution only occurs when RunAsync is invoked.
/// Designed for pipelines where steps are Func&lt;T, Task&lt;T&gt;&gt; or Func&lt;T, T&gt;.
/// </summary>
public readonly struct Future<T>
{
    private readonly Func<CancellationToken, Task<T>> _thunk;

    public Future(Func<CancellationToken, Task<T>> thunk)
    {
        _thunk = thunk ?? throw new ArgumentNullException(nameof(thunk));
    }

    /// <summary>
    /// Run the lazy computation.
    /// </summary>
    public Task<T> RunAsync(CancellationToken cancellationToken = default) => _thunk(cancellationToken);

    /// <summary>
    /// Lift a concrete value into a Future.
    /// </summary>
    public static Future<T> FromResult(T value) => new(_ => Task.FromResult(value));

    // Operator overloads for the common FixState pipeline pattern (same input/output type)

    /// <summary>
    /// Compose a Future with an async step (Func<T, Task<T>>).
    /// Builds a new Future that will run left then right when executed.
    /// </summary>
    public static Future<T> operator |(Future<T> left, Func<T, Task<T>> asyncStep)
    {
        if (asyncStep is null) throw new ArgumentNullException(nameof(asyncStep));

        return new Future<T>(async ct =>
        {
            var leftResult = await left.RunAsync(ct).ConfigureAwait(false);
            return await asyncStep(leftResult).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Compose a Future with a pure step (Func<T, T>).
    /// </summary>
    public static Future<T> operator |(Future<T> left, Func<T, T> pureStep)
    {
        if (pureStep is null) throw new ArgumentNullException(nameof(pureStep));

        return new Future<T>(async ct =>
        {
            var leftResult = await left.RunAsync(ct).ConfigureAwait(false);
            return pureStep(leftResult);
        });
    }
}