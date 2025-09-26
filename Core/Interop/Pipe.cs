using LangChainPipeline.Core.Steps;
using LangChainPipeline.Core.Kleisli;
using LangChainPipeline.Core.Monads;

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
    /// Pipes the current value into an async function.
    /// </summary>
    /// <param name="x">The current pipe.</param>
    /// <param name="f">The async transformation function.</param>
    /// <returns>A task representing the transformed result.</returns>
    public static Task<TR> operator |(Pipe<T, TR> x, Func<T, Task<TR>> f)
        => f(x.Value);

    /// <summary>
    /// Implicitly converts the pipe back to its contained value.
    /// </summary>
    /// <param name="x">The pipe to convert.</param>
    /// <returns>The value contained in the pipe.</returns>
    public static implicit operator T(Pipe<T, TR> x) => x.Value;
}

/// <summary>
/// Extension methods for enhanced pipe operations with full monadic support.
/// </summary>
public static class PipeExtensions
{
    /// <summary>
    /// Pipes the value into a KleisliResult arrow using extension method syntax.
    /// Usage: pipe.Into(resultArrow)
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TR">The result type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="pipe">The pipe containing the input value.</param>
    /// <param name="arrow">The KleisliResult arrow to apply.</param>
    /// <returns>A task containing the Result of the transformation.</returns>
    public static Task<Result<TR, TError>> Into<T, TR, TError>(this Pipe<T, TR> pipe, KleisliResult<T, TR, TError> arrow)
        => arrow(pipe.Value);

    /// <summary>
    /// Pipes the value into a KleisliOption arrow using extension method syntax.
    /// Usage: pipe.Into(optionArrow)
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TR">The result type.</typeparam>
    /// <param name="pipe">The pipe containing the input value.</param>
    /// <param name="arrow">The KleisliOption arrow to apply.</param>
    /// <returns>A task containing the Option result of the transformation.</returns>
    public static Task<Option<TR>> Into<T, TR>(this Pipe<T, TR> pipe, KleisliOption<T, TR> arrow)
        => arrow(pipe.Value);

    /// <summary>
    /// Creates a new pipe from any value, providing a fluent starting point.
    /// Usage: value.ToPipe&lt;TResult&gt;()
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TR">The target result type.</typeparam>
    /// <param name="value">The value to wrap in a pipe.</param>
    /// <returns>A new pipe containing the value.</returns>
    public static Pipe<T, TR> ToPipe<T, TR>(this T value)
        => new(value);

    /// <summary>
    /// Chains another transformation to the pipe.
    /// Usage: pipe.Then(func)
    /// </summary>
    /// <typeparam name="T">The current type in the pipe.</typeparam>
    /// <typeparam name="TR">The target result type.</typeparam>
    /// <typeparam name="TNext">The next transformation type.</typeparam>
    /// <param name="pipe">The current pipe.</param>
    /// <param name="func">The transformation function.</param>
    /// <returns>A new pipe with the transformation applied.</returns>
    public static Pipe<TNext, TR> Then<T, TR, TNext>(this Pipe<T, TR> pipe, Func<T, TNext> func)
        => new(func(pipe.Value));

    /// <summary>
    /// Maps a function over the pipe value while maintaining the pipe structure.
    /// Usage: pipe.Map(func)
    /// </summary>
    /// <typeparam name="T">The current type.</typeparam>
    /// <typeparam name="TR">The result type.</typeparam>
    /// <typeparam name="TNext">The mapped type.</typeparam>
    /// <param name="pipe">The current pipe.</param>
    /// <param name="func">The mapping function.</param>
    /// <returns>A new pipe with the mapped value.</returns>
    public static Pipe<TNext, TR> Map<T, TR, TNext>(this Pipe<T, TR> pipe, Func<T, TNext> func)
        => new(func(pipe.Value));
}
