using LangChainPipeline.Core.Interop;
using LangChainPipeline.Core.Kleisli;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Core.Steps;

namespace LangChainPipeline.Examples;

/// <summary>
/// Demonstrates the new first-class monadic pipe operators.
/// Shows how values can be piped through monadic computations using fluent syntax.
/// </summary>
public static class MonadicPipeExamples
{
    /// <summary>
    /// Runs all monadic pipe operator demonstrations.
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("=== MONADIC PIPE OPERATORS - FIRST CLASS CITIZENS ===");
        Console.WriteLine();

        await DemonstratePipeExtensionsAsync();
        await DemonstrateStaticPipeAsync();
        await DemonstrateExistingPipeStructAsync();
        await DemonstratePipeCompositionAsync();
        await DemonstrateAdvancedPipingAsync();

        Console.WriteLine("=== All Monadic Pipe Demonstrations Complete ===");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates the new Pipe extension methods for fluent piping.
    /// </summary>
    private static async Task DemonstratePipeExtensionsAsync()
    {
        Console.WriteLine("--- Fluent Pipe Extension Methods ---");

        // Simple value piping through pure functions
        var result1 = await "42".Pipe((string s) => int.Parse(s));
        Console.WriteLine($"String '42' piped to int.Parse: {result1}");

        // Piping through async functions
        var result2 = await "hello world".Pipe(async (string s) => 
        {
            await Task.Delay(10);
            return s.ToUpper();
        });
        Console.WriteLine($"String piped through async ToUpper: {result2}");

        // Piping through Step (Kleisli arrow) - use explicit delegate assignment
        Func<string, Task<int>> parseDelegate = input => Task.FromResult(int.Parse(input));
        var result3 = await "123".Pipe(parseDelegate);
        Console.WriteLine($"String '123' piped through Step: {result3}");

        // Piping through KleisliResult
        KleisliResult<string, int, string> safeParseResult = async input =>
        {
            await Task.Delay(1); // Add actual async work
            try
            {
                var value = int.Parse(input);
                return value > 0 
                    ? Result<int, string>.Success(value)
                    : Result<int, string>.Failure($"Value {value} is not positive");
            }
            catch (Exception ex)
            {
                return Result<int, string>.Failure(ex.Message);
            }
        };

        var result4 = await "456".Pipe(safeParseResult);
        Console.WriteLine($"String '456' piped through KleisliResult: {result4}");

        var result5 = await "-123".Pipe(safeParseResult);
        Console.WriteLine($"String '-123' piped through KleisliResult: {result5}");

        // Piping through KleisliOption
        KleisliOption<string, int> tryParseOption = async input =>
        {
            await Task.Delay(1); // Add actual async work
            try
            {
                var value = int.Parse(input);
                return value > 100 ? Option<int>.Some(value) : Option<int>.None();
            }
            catch
            {
                return Option<int>.None();
            }
        };

        var result6 = await "200".Pipe(tryParseOption);
        Console.WriteLine($"String '200' piped through KleisliOption: {result6}");

        var result7 = await "50".Pipe(tryParseOption);
        Console.WriteLine($"String '50' piped through KleisliOption: {result7}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates the static MonadicPipe helper class.
    /// </summary>
    private static async Task DemonstrateStaticPipeAsync()
    {
        Console.WriteLine("--- Static MonadicPipe Helper Class ---");

        // Using MonadicPipe.Apply for explicit pipe operations
        var result1 = await MonadicPipe.Apply(42, (Func<int, int>)((int x) => x * 2));
        Console.WriteLine($"MonadicPipe.Apply(42, x => x * 2): {result1}");

        // Async function application
        var result2 = await MonadicPipe.Apply("test", async (string s) =>
        {
            await Task.Delay(10);
            return $"Processed: {s}";
        });
        Console.WriteLine($"MonadicPipe.Apply with async function: {result2}");

        // KleisliResult application
        KleisliResult<int, string, string> formatNumber = async x =>
        {
            await Task.Delay(1); // Add actual async work
            if (x < 0)
                return Result<string, string>.Failure("Negative numbers not allowed");
            
            return Result<string, string>.Success($"Number: {x}");
        };

        var result3 = await MonadicPipe.Apply(42, formatNumber);
        Console.WriteLine($"MonadicPipe.Apply with KleisliResult(42): {result3}");

        var result4 = await MonadicPipe.Apply(-10, formatNumber);
        Console.WriteLine($"MonadicPipe.Apply with KleisliResult(-10): {result4}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates the enhanced existing Pipe&lt;T, TR&gt; struct with new operators.
    /// </summary>
    private static async Task DemonstrateExistingPipeStructAsync()
    {
        Console.WriteLine("--- Enhanced Pipe<T, TR> Struct with Operators ---");

        // Local helper function for this demonstration
        KleisliResult<int, string, string> localFormatNumber = async x =>
        {
            await Task.Delay(1);
            if (x < 0)
                return Result<string, string>.Failure("Negative numbers not supported");
            
            return Result<string, string>.Success($"Formatted: {x:N0}");
        };

        // Create a pipe and use pure function transformation
        var pipe1 = Pipe.Start<string, int>("42");
        var result1 = pipe1 | ((string s) => int.Parse(s));
        Console.WriteLine($"Pipe('42') | int.Parse: {result1.Value}");

        // Use extension method for async function - use explicit delegate
        Func<int, Task<string>> formatDelegate = x => Task.FromResult($"Value: {x}");
        var result2 = await result1.Value.Pipe(formatDelegate);
        Console.WriteLine($"Pipe(42).Pipe(formatStep): {result2}");

        // Use extension method for async function
        var result3 = await result1.Value.Pipe(async (int x) =>
        {
            await Task.Delay(10);
            return $"Async processed: {x}";
        });
        Console.WriteLine($"Pipe(42).Pipe(async function): {result3}");

        // Use extension methods for additional functionality  
        var result4 = await result1.Value.Pipe(localFormatNumber);
        Console.WriteLine($"Pipe(42).Pipe(kleisliResult): {result4}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates pipe composition with monadic arrows.
    /// </summary>
    private static async Task DemonstratePipeCompositionAsync()
    {
        Console.WriteLine("--- Pipe Composition with Monadic Arrows ---");

        // Create some reusable Steps as delegates
        Func<string, Task<int>> parseStep = s => Task.FromResult(int.Parse(s));
        Func<int, Task<int>> doubleStep = x => Task.FromResult(x * 2);
        Func<int, Task<string>> formatStep = x => Task.FromResult($"Result: {x}");

        // Compose Steps using fluent pipe syntax - use delegates directly
        var result1 = await "21".Pipe(parseStep).ContinueWith(t => t.Result.Pipe(doubleStep)).Unwrap().ContinueWith(t => t.Result.Pipe(formatStep)).Unwrap();
        Console.WriteLine($"Composed pipe '21' -> parse -> double -> format: {result1}");

        // Simpler approach using the MonadicPipe
        var parseResult = await "15".Pipe(parseStep);
        var doubleResult = await parseResult.Pipe(doubleStep);
        Console.WriteLine($"Simple pipe composition '15' -> parse -> double: {doubleResult}");

        // KleisliResult composition
        KleisliResult<string, int, string> safeParse = async s =>
        {
            await Task.Delay(1); // Add actual async work
            try { return Result<int, string>.Success(int.Parse(s)); }
            catch (Exception ex) { return Result<int, string>.Failure(ex.Message); }
        };

        KleisliResult<int, int, string> validatePositive = async x =>
        {
            await Task.Delay(1); // Add actual async work
            return x > 0 
                ? Result<int, string>.Success(x)
                : Result<int, string>.Failure("Must be positive");
        };

        var composedResult = safeParse.Pipe(validatePositive);
        var result3 = await "100".Pipe(composedResult);
        Console.WriteLine($"KleisliResult composition '100': {result3}");

        var result4 = await "-50".Pipe(composedResult);
        Console.WriteLine($"KleisliResult composition '-50': {result4}");

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates advanced piping scenarios with mixed monadic types.
    /// </summary>
    private static async Task DemonstrateAdvancedPipingAsync()
    {
        Console.WriteLine("--- Advanced Piping with Mixed Monadic Types ---");

        // Simplified pipeline without complex Task unwrapping
        var value42 = 42;
        var result1 = await value42.Pipe(safeDouble);
        Console.WriteLine($"Simple pipeline 42 -> safeDouble: {result1}");

        // Using the Pipe class static helper instead of problematic extension
        var result2 = await 50.Pipe(safeDouble);
        Console.WriteLine($"Enhanced piping 50 -> safeDouble: {result2}");

        // Demonstrating the PipeTo extension from KleisliExtensions
        var result3 = await 25.PipeTo(safeDouble);
        Console.WriteLine($"Direct value.PipeTo(kleisliResult): {result3}");

        var result4 = await "test".PipeTo((string s) => s.Length);
        Console.WriteLine($"String.PipeTo(length function): {result4}");

        Console.WriteLine();
    }

    /// <summary>
    /// Helper KleisliResult for demonstrations.
    /// </summary>
    private static readonly KleisliResult<int, int, string> safeDouble = async x =>
    {
        await Task.Delay(1); // Add actual async work
        if (x > 1000)
            return Result<int, string>.Failure("Value too large to double");
        
        return Result<int, string>.Success(x * 2);
    };

    /// <summary>
    /// Helper KleisliResult for formatting numbers.
    /// </summary>
    private static readonly KleisliResult<int, string, string> formatNumber = async x =>
    {
        await Task.Delay(1); // Add actual async work
        if (x < 0)
            return Result<string, string>.Failure("Negative numbers not supported");
        
        return Result<string, string>.Success($"Formatted: {x:N0}");
    };
}