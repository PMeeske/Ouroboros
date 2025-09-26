namespace LangChainPipeline.Core.Interop;

/// <summary>
/// Provides static methods for creating pipe operations.
/// </summary>
public static class Pipe
{
    /// <summary>
    /// Creates a new pipe starting with the specified value.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TR">The result type.</typeparam>
    /// <param name="value">The starting value.</param>
    /// <returns>A new pipe with the specified starting value.</returns>
    public static Pipe<T, TR> Start<T, TR>(T value) => new(value);
}

/// <summary>
/// Represents a pipe that can transform values through a series of operations.
/// </summary>
/// <typeparam name="T">The current value type.</typeparam>
/// <typeparam name="TR">The target result type.</typeparam>
/// <param name="value">The current value in the pipe.</param>
public readonly struct Pipe<T, TR>(T value)
{
    /// <summary>
    /// Gets the current value in the pipe.
    /// </summary>
    public readonly T Value = value;

    /// <summary>
    /// Pipes the current value into a pure function.
    /// </summary>
    /// <param name="x">The current pipe.</param>
    /// <param name="f">The transformation function.</param>
    /// <returns>A new pipe with the transformed value.</returns>
    public static Pipe<TR, TR> operator |(Pipe<T, TR> x, Func<T, TR> f)
        => new(f(x.Value));

    /// <summary>
    /// Pipes the current value into a Kleisli arrow (async step).
    /// </summary>
    /// <param name="x">The current pipe.</param>
    /// <param name="f">The async transformation step.</param>
    /// <returns>A task representing the transformed result.</returns>
    public static Task<TR> operator |(Pipe<T, TR> x, Step<T, TR> f)
        => f(x.Value);

    /// <summary>
    /// Implicitly converts the pipe back to its contained value.
    /// </summary>
    /// <param name="x">The pipe to convert.</param>
    /// <returns>The value contained in the pipe.</returns>
    public static implicit operator T(Pipe<T, TR> x) => x.Value;
}
