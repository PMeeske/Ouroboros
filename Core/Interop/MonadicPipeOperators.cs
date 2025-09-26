// ==========================================================
// Monadic Pipe Operators - First Class Citizens
// Provides enhanced pipe operator syntax for monadic operations
// ==========================================================

using LangChainPipeline.Core.Steps;
using LangChainPipeline.Core.Kleisli; 
using LangChainPipeline.Core.Monads;

namespace LangChainPipeline.Core.Interop;

/// <summary>
/// Static helper class providing first-class pipe operations for monadic computations.
/// Since C# doesn't allow generic operators directly, we provide extension methods
/// and static helpers that mimic pipe operator behavior.
/// </summary>
public static class MonadicPipe
{
    /// <summary>
    /// Applies a KleisliResult arrow to a value.
    /// Usage: MonadicPipe.Apply(value, resultArrow)
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="arrow">The KleisliResult arrow to apply.</param>
    /// <returns>A Task containing the Result.</returns>
    public static Task<Result<TOut, TError>> Apply<TIn, TOut, TError>(TIn input, KleisliResult<TIn, TOut, TError> arrow)
        => arrow(input);

    /// <summary>
    /// Applies a KleisliOption arrow to a value.
    /// Usage: MonadicPipe.Apply(value, optionArrow)
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="arrow">The KleisliOption arrow to apply.</param>
    /// <returns>A Task containing the Option result.</returns>
    public static Task<Option<TOut>> Apply<TIn, TOut>(TIn input, KleisliOption<TIn, TOut> arrow)
        => arrow(input);

    /// <summary>
    /// Applies a pure function to a value and wraps the result in a Task.
    /// Usage: MonadicPipe.Apply(value, func)
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="func">The function to apply.</param>
    /// <returns>A Task containing the result.</returns>
    public static Task<TOut> Apply<TIn, TOut>(TIn input, Func<TIn, TOut> func)
        => Task.FromResult(func(input));

    /// <summary>
    /// Applies an async function or Step to a value.
    /// Usage: MonadicPipe.Apply(value, asyncFunc)
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="func">The async function to apply.</param>
    /// <returns>A Task containing the result.</returns>
    public static Task<TOut> Apply<TIn, TOut>(TIn input, Func<TIn, Task<TOut>> func)
        => func(input);

    /// <summary>
    /// Composes two Steps using pipe syntax.
    /// Usage: MonadicPipe.Compose(step1, step2)
    /// </summary>
    /// <typeparam name="TA">Input type of first step.</typeparam>
    /// <typeparam name="TB">Output type of first step, input type of second step.</typeparam>
    /// <typeparam name="TC">Output type of second step.</typeparam>
    /// <param name="first">The first step.</param>
    /// <param name="second">The second step.</param>
    /// <returns>A composed step.</returns>
    public static Step<TA, TC> Compose<TA, TB, TC>(Step<TA, TB> first, Step<TB, TC> second)
        => first.Then(second);

    /// <summary>
    /// Composes two KleisliResult arrows.
    /// Usage: MonadicPipe.Compose(resultArrow1, resultArrow2)
    /// </summary>
    /// <typeparam name="TA">Input type.</typeparam>
    /// <typeparam name="TB">Intermediate type.</typeparam>
    /// <typeparam name="TC">Output type.</typeparam>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <param name="first">The first result arrow.</param>
    /// <param name="second">The second result arrow.</param>
    /// <returns>A composed result arrow.</returns>
    public static KleisliResult<TA, TC, TError> Compose<TA, TB, TC, TError>(
        KleisliResult<TA, TB, TError> first, 
        KleisliResult<TB, TC, TError> second)
        => first.Then(second);

    /// <summary>
    /// Composes two KleisliOption arrows.
    /// Usage: MonadicPipe.Compose(optionArrow1, optionArrow2)
    /// </summary>
    /// <typeparam name="TA">Input type.</typeparam>
    /// <typeparam name="TB">Intermediate type.</typeparam>
    /// <typeparam name="TC">Output type.</typeparam>
    /// <param name="first">The first option arrow.</param>
    /// <param name="second">The second option arrow.</param>
    /// <returns>A composed option arrow.</returns>
    public static KleisliOption<TA, TC> Compose<TA, TB, TC>(
        KleisliOption<TA, TB> first, 
        KleisliOption<TB, TC> second)
        => first.Then(second);
}

/// <summary>
/// Extension methods to provide fluent pipe-like syntax.
/// These work as first-class pipe operations through method chaining.
/// </summary>
public static class MonadicPipeExtensions
{
    /// <summary>
    /// Fluent pipe operation: applies a KleisliResult arrow to the value.
    /// Usage: value.Pipe(resultArrow)
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <typeparam name="TError">The error type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="arrow">The KleisliResult arrow to apply.</param>
    /// <returns>A Task containing the Result.</returns>
    public static Task<Result<TOut, TError>> Pipe<TIn, TOut, TError>(this TIn input, KleisliResult<TIn, TOut, TError> arrow)
        => arrow(input);

    /// <summary>
    /// Fluent pipe operation: applies a KleisliOption arrow to the value.
    /// Usage: value.Pipe(optionArrow)
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="arrow">The KleisliOption arrow to apply.</param>
    /// <returns>A Task containing the Option result.</returns>
    public static Task<Option<TOut>> Pipe<TIn, TOut>(this TIn input, KleisliOption<TIn, TOut> arrow)
        => arrow(input);

    /// <summary>
    /// Fluent pipe operation: applies a pure function to the value.
    /// Usage: value.Pipe(func)
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="func">The function to apply.</param>
    /// <returns>A Task containing the result.</returns>
    public static Task<TOut> Pipe<TIn, TOut>(this TIn input, Func<TIn, TOut> func)
        => Task.FromResult(func(input));

    /// <summary>
    /// Fluent pipe operation: applies an async function or Step to the value.
    /// Usage: value.Pipe(asyncFunc) - works with both Step and Func<TIn, Task<TOut>>
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="input">The input value.</param>
    /// <param name="func">The async function to apply.</param>
    /// <returns>A Task containing the result.</returns>
    public static Task<TOut> Pipe<TIn, TOut>(this TIn input, Func<TIn, Task<TOut>> func)
        => func(input);

    /// <summary>
    /// Fluent pipe operation for Step composition.
    /// Usage: step1.Pipe(step2)
    /// </summary>
    /// <typeparam name="TA">Input type of first step.</typeparam>
    /// <typeparam name="TB">Output type of first step, input type of second step.</typeparam>
    /// <typeparam name="TC">Output type of second step.</typeparam>
    /// <param name="first">The first step.</param>
    /// <param name="second">The second step.</param>
    /// <returns>A composed step.</returns>
    public static Step<TA, TC> Pipe<TA, TB, TC>(this Step<TA, TB> first, Step<TB, TC> second)
        => first.Then(second);

    /// <summary>
    /// Fluent pipe operation for KleisliResult composition.
    /// Usage: resultArrow1.Pipe(resultArrow2)
    /// </summary>
    /// <typeparam name="TA">Input type.</typeparam>
    /// <typeparam name="TB">Intermediate type.</typeparam>
    /// <typeparam name="TC">Output type.</typeparam>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <param name="first">The first result arrow.</param>
    /// <param name="second">The second result arrow.</param>
    /// <returns>A composed result arrow.</returns>
    public static KleisliResult<TA, TC, TError> Pipe<TA, TB, TC, TError>(
        this KleisliResult<TA, TB, TError> first, 
        KleisliResult<TB, TC, TError> second)
        => first.Then(second);

    /// <summary>
    /// Fluent pipe operation for KleisliOption composition.
    /// Usage: optionArrow1.Pipe(optionArrow2)
    /// </summary>
    /// <typeparam name="TA">Input type.</typeparam>
    /// <typeparam name="TB">Intermediate type.</typeparam>
    /// <typeparam name="TC">Output type.</typeparam>
    /// <param name="first">The first option arrow.</param>
    /// <param name="second">The second option arrow.</param>
    /// <returns>A composed option arrow.</returns>
    public static KleisliOption<TA, TC> Pipe<TA, TB, TC>(
        this KleisliOption<TA, TB> first, 
        KleisliOption<TB, TC> second)
        => first.Then(second);
}