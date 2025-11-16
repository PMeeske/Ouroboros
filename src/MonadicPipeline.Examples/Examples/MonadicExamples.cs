// <copyright file="MonadicExamples.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

/// <summary>
/// Demonstration of enhanced monadic operations and functional programming concepts.
/// Shows how to use Option, Result, and Kleisli arrows in practice.
/// </summary>
public static class MonadicExamples
{
    /// <summary>
    /// Demonstrates all enhanced monadic operations including KleisliCompose.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateAll()
    {
        Console.WriteLine("=== Enhanced Monadic Operations Demonstration ===\n");

        DemonstrateOptionMonad();
        DemonstrateResultMonad();
        await DemonstrateKleisliArrows();
        await DemonstrateKleisliResult();
        await DemonstrateKleisliOption();
        await DemonstrateExceptionHandling();
        await DemonstrateMonadicLaws();

        Console.WriteLine("=== All Demonstrations Complete ===");
    }

    /// <summary>
    /// Demonstrates Option monad usage with proper monadic operations.
    /// </summary>
    public static void DemonstrateOptionMonad()
    {
        Console.WriteLine("=== Option Monad Demonstration ===");

        // Creating Options
        Option<int> someValue = Option<int>.Some(42);
        Option<int> noneValue = Option<int>.None();

        Console.WriteLine($"Some(42): {someValue}");
        Console.WriteLine($"None: {noneValue}");

        // Functor map operation
        Option<int> mapped = someValue.Map(x => x * 2);
        Option<int> mappedNone = noneValue.Map(x => x * 2);

        Console.WriteLine($"Some(42).Map(x => x * 2): {mapped}");
        Console.WriteLine($"None.Map(x => x * 2): {mappedNone}");

        // Monadic bind operation
        Option<string> bound = someValue.Bind(x => x > 40 ? Option<string>.Some($"Large: {x}") : Option<string>.None());
        Option<string> boundNone = noneValue.Bind(x => Option<string>.Some($"Value: {x}"));

        Console.WriteLine($"Some(42).Bind(largeCheck): {bound}");
        Console.WriteLine($"None.Bind(largeCheck): {boundNone}");

        // Pattern matching
        string result = someValue.Match(
            value => $"Got value: {value}",
            "No value");
        Console.WriteLine($"Pattern match result: {result}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates Result monad usage for error handling.
    /// </summary>
    public static void DemonstrateResultMonad()
    {
        Console.WriteLine("=== Result Monad Demonstration ===");

        // Creating Results
        Result<int, string> success = Result<int, string>.Success(100);
        Result<int, string> failure = Result<int, string>.Failure("Something went wrong");

        Console.WriteLine($"Success(100): {success}");
        Console.WriteLine($"Failure: {failure}");

        // Functor map operation
        Result<int, string> mappedSuccess = success.Map(x => x / 2);
        Result<int, string> mappedFailure = failure.Map(x => x / 2);

        Console.WriteLine($"Success.Map(x => x / 2): {mappedSuccess}");
        Console.WriteLine($"Failure.Map(x => x / 2): {mappedFailure}");

        // Monadic bind operation
        Result<string, string> boundSuccess = success.Bind(x =>
            x > 50 ? Result<string, string>.Success($"Large number: {x}")
                   : Result<string, string>.Failure("Number too small"));
        Result<string, string> boundFailure = failure.Bind(x => Result<string, string>.Success($"Value: {x}"));

        Console.WriteLine($"Success.Bind(largeCheck): {boundSuccess}");
        Console.WriteLine($"Failure.Bind(largeCheck): {boundFailure}");

        // Error mapping
        Result<int, string> mappedError = failure.MapError(error => $"Error: {error}");
        Console.WriteLine($"Failure.MapError: {mappedError}");

        // Pattern matching
        string result = success.Match(
            value => $"Success with value: {value}",
            error => $"Failed with error: {error}");
        Console.WriteLine($"Pattern match result: {result}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates Kleisli arrows with Task monad.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateKleisliArrows()
    {
        Console.WriteLine("=== Kleisli Arrow Demonstration ===");

        // Define some Kleisli arrows
        Step<string, int> parseNumber = async input =>
        {
            await Task.Delay(10); // Simulate async work
            return int.TryParse(input, out int result) ? result : 0;
        };

        Step<int, string> formatResult = async input =>
        {
            await Task.Delay(10); // Simulate async work
            return $"Result: {input}";
        };

        // Compose arrows using Then
        Step<string, string> pipeline = parseNumber.Then(formatResult);

        // Execute the pipeline
        string result1 = await pipeline("42");
        string result2 = await pipeline("not-a-number");

        Console.WriteLine($"Pipeline('42'): {result1}");
        Console.WriteLine($"Pipeline('not-a-number'): {result2}");

        // Using Map for transformation
        Step<string, int> mappedPipeline = parseNumber.Map(x => x * 10);
        int mappedResult = await mappedPipeline("5");
        Console.WriteLine($"ParseNumber.Map(x => x * 10)('5'): {mappedResult}");

        // Using Tap for side effects
        Step<string, int> tappedPipeline = parseNumber.Tap(x => Console.WriteLine($"Parsed intermediate value: {x}"));
        await tappedPipeline("123");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates KleisliResult for error-aware computations.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateKleisliResult()
    {
        Console.WriteLine("=== KleisliResult Demonstration ===");

        // Define KleisliResult arrows
        KleisliResult<string, int, string> safeParseNumber = async input =>
        {
            await Task.Delay(10);
            return int.TryParse(input, out int result)
                ? Result<int, string>.Success(result)
                : Result<int, string>.Failure($"Cannot parse '{input}' as number");
        };

        KleisliResult<int, string, string> checkPositive = async input =>
        {
            await Task.Delay(10);
            return input > 0
                ? Result<string, string>.Success($"Positive: {input}")
                : Result<string, string>.Failure($"Number {input} is not positive");
        };

        // Compose with error handling
        KleisliResult<string, string, string> safeResultPipeline = safeParseNumber.Then(checkPositive);

        // Test different inputs
        Result<string, string> result1 = await safeResultPipeline("42");
        Result<string, string> result2 = await safeResultPipeline("-5");
        Result<string, string> result3 = await safeResultPipeline("not-a-number");

        Console.WriteLine($"SafePipeline('42'): {result1}");
        Console.WriteLine($"SafePipeline('-5'): {result2}");
        Console.WriteLine($"SafePipeline('not-a-number'): {result3}");

        // Using Map for success transformation
        KleisliResult<string, int, string> mappedSafePipeline = safeParseNumber.Map(x => x * 100);
        Result<int, string> mappedSafeResult = await mappedSafePipeline("7");
        Console.WriteLine($"SafeParseNumber.Map(x => x * 100)('7'): {mappedSafeResult}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates KleisliOption for nullable computations.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateKleisliOption()
    {
        Console.WriteLine("=== KleisliOption Demonstration ===");

        // Define KleisliOption arrows
        KleisliOption<string, int> tryParseNumber = async input =>
        {
            await Task.Delay(10);
            return int.TryParse(input, out int result)
                ? Option<int>.Some(result)
                : Option<int>.None();
        };

        KleisliOption<int, string> formatIfLarge = async input =>
        {
            await Task.Delay(10);
            return input > 100
                ? Option<string>.Some($"Large number: {input}")
                : Option<string>.None();
        };

        // Compose with None handling
        KleisliOption<string, string> optionPipeline = tryParseNumber.Then(formatIfLarge);

        // Test different inputs
        Option<string> result1 = await optionPipeline("200");
        Option<string> result2 = await optionPipeline("50");
        Option<string> result3 = await optionPipeline("not-a-number");

        Console.WriteLine($"OptionPipeline('200'): {result1}");
        Console.WriteLine($"OptionPipeline('50'): {result2}");
        Console.WriteLine($"OptionPipeline('not-a-number'): {result3}");

        // Convert to Result
        KleisliResult<string, int, string> resultPipeline = tryParseNumber.ToResult("Failed to parse number");
        Result<int, string> convertedResult = await resultPipeline("42");
        Console.WriteLine($"TryParseNumber.ToResult('42'): {convertedResult}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates exception handling with Catch.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateExceptionHandling()
    {
        Console.WriteLine("=== Exception Handling Demonstration ===");

        // Define a Kleisli arrow that might throw
        Step<string, int> riskyOperation = async input =>
        {
            await Task.Delay(10);
            if (input == "throw")
            {
                throw new InvalidOperationException("Intentional exception");
            }

            return input.Length;
        };

        // Wrap with exception handling
        KleisliResult<string, int, Exception> safeOperation = riskyOperation.Catch();

        // Test with normal input
        Result<int, Exception> result1 = await safeOperation("hello");
        Console.WriteLine($"SafeOperation('hello'): {result1}");

        // Test with exception-throwing input
        Result<int, Exception> result2 = await safeOperation("throw");
        Console.WriteLine($"SafeOperation('throw'): {result2}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates monadic laws compliance.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static Task DemonstrateMonadicLaws()
    {
        Console.WriteLine("=== Monadic Laws Demonstration ===");

        // Left Identity Law: return(a).bind(f) ≡ f(a)
        int value = 42;
        Func<int, Option<string>> func = (int x) => Option<string>.Some($"Value: {x}");

        Option<string> leftSide = Option<int>.Some(value).Bind(func);
        Option<string> rightSide = func(value);

        Console.WriteLine($"Left Identity Law:");
        Console.WriteLine($"Some({value}).Bind(f): {leftSide}");
        Console.WriteLine($"f({value}): {rightSide}");
        Console.WriteLine($"Equal: {leftSide.Equals(rightSide)}");

        // Right Identity Law: m.bind(return) ≡ m
        Option<int> option = Option<int>.Some(42);
        Option<int> boundWithReturn = option.Bind(x => Option<int>.Some(x));

        Console.WriteLine($"\nRight Identity Law:");
        Console.WriteLine($"Some(42).Bind(Some): {boundWithReturn}");
        Console.WriteLine($"Some(42): {option}");
        Console.WriteLine($"Equal: {boundWithReturn.Equals(option)}");

        // Associativity Law will be complex to demonstrate here, but the structure supports it
        Console.WriteLine();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs all demonstrations.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllDemonstrations()
    {
        Console.WriteLine("=== Enhanced Monadic Operations Demonstration ===\n");

        DemonstrateOptionMonad();
        DemonstrateResultMonad();
        await DemonstrateKleisliArrows();
        await DemonstrateKleisliResult();
        await DemonstrateKleisliOption();
        await DemonstrateExceptionHandling();
        await DemonstrateMonadicLaws();

        Console.WriteLine("=== All Demonstrations Complete ===");
    }
}
