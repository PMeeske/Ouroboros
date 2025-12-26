#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Kleisli <-> LangChain pipe interop
// Enhanced integration with our unified monadic operations
// ==========================================================

namespace Ouroboros.Core.Interop;

using Ouroboros.Core.Steps;

#region Core Integration - Minimal Interop Extensions

/// <summary>
/// Minimal interop-specific extensions for integration with external systems.
/// The main monadic operations are provided by unified KleisliExtensions.
/// </summary>
public static class StepInteropExtensions
{
    /// <summary>
    /// Lifts a pure function into a Step (Kleisli arrow)
    /// </summary>
    public static Step<TA, TB> ToStep<TA, TB>(this Func<TA, TB> func)
        => input => Task.FromResult(func(input));

    /// <summary>
    /// Lifts an async function into a Step
    /// </summary>
    public static Step<TA, TB> ToStep<TA, TB>(this Func<TA, Task<TB>> func)
        => new(func);
}

#endregion

#region Compatible Pipeline System

/// <summary>
/// Compatible node interface for interop with various pipeline systems
/// </summary>
public interface ICompatNode<TIn, TOut>
{
    Task<TOut> InvokeAsync(TIn input, CancellationToken ct = default);
    string Name { get; }
}

/// <summary>
/// Lambda-based compatible node implementation
/// </summary>
public sealed class LambdaNode<TIn, TOut> : ICompatNode<TIn, TOut>
{
    private readonly Func<TIn, CancellationToken, Task<TOut>> _fn;

    /// <inheritdoc/>
    public string Name { get; }

    public LambdaNode(string name, Func<TIn, CancellationToken, Task<TOut>> fn)
    {
        Name = name;
        _fn = fn;
    }

    /// <inheritdoc/>
    public Task<TOut> InvokeAsync(TIn input, CancellationToken ct = default) => _fn(input, ct);

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>
/// Enhanced pipe node wrapper with monadic integration
/// </summary>
public readonly struct PipeNode<TIn, TOut>
{
    public readonly ICompatNode<TIn, TOut> Node;

    public PipeNode(ICompatNode<TIn, TOut> node)
    {
        Node = node;
    }

    /// <summary>
    /// Pipe execution operator: value -> pipeline result
    /// Note: Can't be async, so returns Task directly
    /// </summary>
    public static Task<TOut> operator |(TIn value, PipeNode<TIn, TOut> node)
        => node.Node.InvokeAsync(value);

    /// <summary>
    /// Pipe composition with method syntax for better type safety
    /// </summary>
    public PipeNode<TIn, TNext> Pipe<TNext>(PipeNode<TOut, TNext> next)
    {
        ICompatNode<TIn, TOut> currentNode = Node; // Capture to avoid struct 'this' issues
        return new PipeNode<TIn, TNext>(new LambdaNode<TIn, TNext>(
            $"{currentNode.Name} | {next.Node.Name}",
            async (input, ct) =>
            {
                TOut? mid = await currentNode.InvokeAsync(input, ct).ConfigureAwait(false);
                return await next.Node.InvokeAsync(mid, ct).ConfigureAwait(false);
            }));
    }

    /// <summary>
    /// Pipe composition with Step using method syntax
    /// </summary>
    public PipeNode<TIn, TNext> Pipe<TNext>(Step<TOut, TNext> step, string? stepName = null)
        => Pipe(step.ToCompatNode(stepName));

    /// <summary>
    /// Convert to Kleisli Step for monadic operations
    /// </summary>
    public Step<TIn, TOut> ToStep()
    {
        ICompatNode<TIn, TOut> currentNode = Node; // Capture to avoid struct 'this' issues
        return async input => await currentNode.InvokeAsync(input).ConfigureAwait(false);
    }

    /// <summary>
    /// Convert to KleisliResult with exception handling
    /// </summary>
    public KleisliResult<TIn, TOut, Exception> ToKleisliResult()
    {
        ICompatNode<TIn, TOut> currentNode = Node; // Capture to avoid struct 'this' issues
        return async input =>
        {
            try
            {
                TOut? result = await currentNode.InvokeAsync(input).ConfigureAwait(false);
                return Result<TOut, Exception>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TOut, Exception>.Failure(ex);
            }
        };
    }

    /// <inheritdoc/>
    public override string ToString() => Node?.Name ?? "EmptyPipeNode";
}

#endregion

#region Conversion Extensions

/// <summary>
/// Enhanced compatibility interop with monadic integration
/// </summary>
public static class CompatInterop
{
    /// <summary>
    /// Convert Step to compatible node
    /// </summary>
    public static PipeNode<TIn, TOut> ToCompatNode<TIn, TOut>(this Step<TIn, TOut> step, string? name = null)
        => new(new LambdaNode<TIn, TOut>(
            name ?? $"Step[{typeof(TIn).Name}->{typeof(TOut).Name}]",
            async (input, ct) => await step(input).ConfigureAwait(false)));

    /// <summary>
    /// Convert KleisliResult to compatible node
    /// </summary>
    public static PipeNode<TIn, Result<TOut, TError>> ToCompatNode<TIn, TOut, TError>(
        this KleisliResult<TIn, TOut, TError> kleisliResult,
        string? name = null)
        => new(new LambdaNode<TIn, Result<TOut, TError>>(
            name ?? $"KleisliResult[{typeof(TIn).Name}->{typeof(TOut).Name}]",
            async (input, ct) => await kleisliResult(input).ConfigureAwait(false)));

    /// <summary>
    /// Convert KleisliOption to compatible node
    /// </summary>
    public static PipeNode<TIn, Option<TOut>> ToCompatNode<TIn, TOut>(
        this KleisliOption<TIn, TOut> kleisliOption,
        string? name = null)
        => new(new LambdaNode<TIn, Option<TOut>>(
            name ?? $"KleisliOption[{typeof(TIn).Name}->{typeof(TOut).Name}]",
            async (input, ct) => await kleisliOption(input).ConfigureAwait(false)));

    /// <summary>
    /// Convert pure function to compatible node
    /// </summary>
    public static PipeNode<TIn, TOut> ToCompatNode<TIn, TOut>(this Func<TIn, TOut> f, string? name = null)
        => new(new LambdaNode<TIn, TOut>(
            name ?? $"Func[{typeof(TIn).Name}->{typeof(TOut).Name}]",
            (input, ct) => Task.FromResult(f(input))));

    /// <summary>
    /// Convert async function to compatible node
    /// </summary>
    public static PipeNode<TIn, TOut> ToCompatNode<TIn, TOut>(this Func<TIn, Task<TOut>> f, string? name = null)
        => new(new LambdaNode<TIn, TOut>(
            name ?? $"Async[{typeof(TIn).Name}->{typeof(TOut).Name}]",
            (input, ct) => f(input)));

    /// <summary>
    /// Create a pipeline builder for fluent composition
    /// </summary>
    public static PipelineBuilder<TIn> StartPipeline<TIn>(string name = "Pipeline")
        => new(name);
}

#endregion

#region Fluent Pipeline Builder

/// <summary>
/// Fluent pipeline builder for enhanced composition
/// </summary>
public class PipelineBuilder<TIn>
{
    private readonly string _name;

    public PipelineBuilder(string name)
    {
        _name = name;
    }

    /// <summary>
    /// Add a Step to the pipeline
    /// </summary>
    public PipelineBuilder<TIn, TOut> AddStep<TOut>(Step<TIn, TOut> step, string? stepName = null)
        => new(_name, step.ToCompatNode(stepName));

    /// <summary>
    /// Add a KleisliResult to the pipeline
    /// </summary>
    public PipelineBuilder<TIn, Result<TOut, TError>> AddResultStep<TOut, TError>(
        KleisliResult<TIn, TOut, TError> kleisliResult,
        string? stepName = null)
        => new(_name, kleisliResult.ToCompatNode(stepName));

    /// <summary>
    /// Add a function to the pipeline
    /// </summary>
    public PipelineBuilder<TIn, TOut> AddFunc<TOut>(Func<TIn, TOut> func, string? stepName = null)
        => new(_name, func.ToCompatNode(stepName));
}

/// <summary>
/// Typed pipeline builder for fluent composition
/// </summary>
public class PipelineBuilder<TIn, TCurrent>
{
    private readonly string _name;
    private readonly PipeNode<TIn, TCurrent> _currentPipeline;

    public PipelineBuilder(string name, PipeNode<TIn, TCurrent> pipeline)
    {
        _name = name;
        _currentPipeline = pipeline;
    }

    /// <summary>
    /// Add another step to the pipeline
    /// </summary>
    public PipelineBuilder<TIn, TOut> Then<TOut>(Step<TCurrent, TOut> step, string? stepName = null)
        => new(_name, _currentPipeline.Pipe(step.ToCompatNode(stepName)));

    /// <summary>
    /// Add a function step to the pipeline
    /// </summary>
    public PipelineBuilder<TIn, TOut> Then<TOut>(Func<TCurrent, TOut> func, string? stepName = null)
        => new(_name, _currentPipeline.Pipe(func.ToCompatNode(stepName)));

    /// <summary>
    /// Build the final pipeline
    /// </summary>
    public PipeNode<TIn, TCurrent> Build() => _currentPipeline;

    /// <summary>
    /// Build and execute the pipeline
    /// </summary>
    public async Task<TCurrent> ExecuteAsync(TIn input)
        => await (input | _currentPipeline);
}

#endregion

#region Enhanced Examples with Monadic Operations

/// <summary>
/// Enhanced step examples integrating with our monadic operations
/// </summary>
public static class EnhancedSteps
{
    /// <summary>
    /// Example: Step that uppercases text
    /// </summary>
    public static readonly Step<string, string> Upper = async s =>
    {
        await Task.Yield();
        return s.ToUpperInvariant();
    };

    /// <summary>
    /// Example: Step that gets string length
    /// </summary>
    public static readonly Step<string, int> Length = async s =>
    {
        await Task.Yield();
        return s.Length;
    };

    /// <summary>
    /// Example: Step that formats number
    /// </summary>
    public static readonly Step<int, string> Show = async n =>
    {
        await Task.Yield();
        return $"length={n}";
    };

    /// <summary>
    /// Example: KleisliResult that safely parses integers
    /// </summary>
    public static readonly KleisliResult<string, int, string> SafeParse = async s =>
    {
        await Task.Yield();
        return int.TryParse(s, out int result)
            ? Result<int, string>.Success(result)
            : Result<int, string>.Failure($"Cannot parse '{s}' as integer");
    };

    /// <summary>
    /// Example: KleisliOption that returns value if positive
    /// </summary>
    public static readonly KleisliOption<int, int> OnlyPositive = async n =>
    {
        await Task.Yield();
        return n > 0 ? Option<int>.Some(n) : Option<int>.None();
    };
}

/// <summary>
/// Enhanced demonstration examples
/// </summary>
public static class EnhancedDemo
{
    /// <summary>
    /// Demonstrate Kleisli composition with our enhanced operations
    /// </summary>
    public static async Task RunEnhancedKleisli()
    {
        Console.WriteLine("=== Enhanced Kleisli Composition ===");

        Step<string, string> pipeline = EnhancedSteps.Upper
            .Then(EnhancedSteps.Length)
            .Then(EnhancedSteps.Show);

        string result = await pipeline("hello enhanced kleisli");
        Console.WriteLine($"Result: {result}"); // length=22

        // With error handling
        KleisliResult<string, string, string> safePipeline = EnhancedSteps.SafeParse
            .Then(EnhancedSteps.OnlyPositive.ToResult("Number must be positive"))
            .Map(n => $"Valid positive number: {n}");

        Result<string, string> safeResult1 = await safePipeline("42");
        Result<string, string> safeResult2 = await safePipeline("-5");
        Result<string, string> safeResult3 = await safePipeline("not-a-number");

        Console.WriteLine($"Safe parse '42': {safeResult1}");
        Console.WriteLine($"Safe parse '-5': {safeResult2}");
        Console.WriteLine($"Safe parse 'not-a-number': {safeResult3}");
    }

    /// <summary>
    /// Demonstrate enhanced compatibility pipe with monadic operations
    /// </summary>
    public static async Task RunEnhancedCompatPipe()
    {
        Console.WriteLine("=== Enhanced Compatibility Pipe ===");

        PipeNode<string, string> n1 = EnhancedSteps.Upper.ToCompatNode("Upper");
        PipeNode<string, int> n2 = EnhancedSteps.Length.ToCompatNode("Length");
        PipeNode<int, string> n3 = EnhancedSteps.Show.ToCompatNode("Show");

        // Method-based composition since operator overloading had issues
        PipeNode<string, string> pipeline = n1.Pipe(n2).Pipe(n3);
        string result = await ("enhanced compat pipe" | pipeline);
        Console.WriteLine($"Compat pipe result: {result}");

        // Using fluent pipeline builder
        string fluentResult = await CompatInterop
            .StartPipeline<string>("FluentDemo")
            .AddStep(EnhancedSteps.Upper, "Uppercase")
            .Then(EnhancedSteps.Length, "GetLength")
            .Then(EnhancedSteps.Show, "Format")
            .ExecuteAsync("fluent pipeline demo");

        Console.WriteLine($"Fluent pipeline result: {fluentResult}");

        // With monadic error handling
        PipeNode<string, Result<int, string>> Ouroboros = EnhancedSteps.SafeParse.ToCompatNode("SafeParse");
        Result<int, string> monadicResult = await ("456" | Ouroboros);

        monadicResult.Match(
            success => Console.WriteLine($"Monadic compat success: {success}"),
            error => Console.WriteLine($"Monadic compat error: {error}")
        );
    }

    /// <summary>
    /// Run all enhanced demonstrations
    /// </summary>
    public static async Task RunAllEnhanced()
    {
        await RunEnhancedKleisli();
        await RunEnhancedCompatPipe();

        Console.WriteLine("=== All Enhanced Interop Demonstrations Complete ===");
    }
}

#endregion
