using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LangChainPipeline.Tools;
using LangChainPipeline.Domain;
using LangChainPipeline.Core.Monads;

namespace MonadicPipeline.Benchmarks;

/// <summary>
/// Benchmarks for tool execution performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ToolExecutionBenchmarks
{
    private ITool _mathTool = null!;
    private ITool _cachedTool = null!;
    private ITool _timeoutTool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _mathTool = new MathTool();
        _cachedTool = _mathTool.WithCaching(TimeSpan.FromMinutes(1));
        _timeoutTool = _mathTool.WithTimeout(TimeSpan.FromSeconds(5));
    }

    [Benchmark(Baseline = true)]
    public async Task<Result<string, string>> BasicToolExecution()
    {
        return await _mathTool.InvokeAsync("2 + 2", CancellationToken.None);
    }

    [Benchmark]
    public async Task<Result<string, string>> CachedToolExecution()
    {
        return await _cachedTool.InvokeAsync("2 + 2", CancellationToken.None);
    }

    [Benchmark]
    public async Task<Result<string, string>> ToolWithTimeout()
    {
        return await _timeoutTool.InvokeAsync("2 + 2", CancellationToken.None);
    }

    [Benchmark]
    public async Task<Result<string, string>> ToolWithRetry()
    {
        var tool = _mathTool.WithRetry(maxRetries: 3);
        return await tool.InvokeAsync("2 + 2", CancellationToken.None);
    }

    [Benchmark]
    public async Task<Result<string, string>> ToolWithPerformanceTracking()
    {
        bool callbackInvoked = false;
        var tool = _mathTool.WithPerformanceTracking((name, duration, success) =>
        {
            callbackInvoked = true;
        });
        return await tool.InvokeAsync("2 + 2", CancellationToken.None);
    }
}

/// <summary>
/// Benchmarks for monadic operations (Result<T> and Option<T>).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class MonadicOperationsBenchmarks
{
    [Benchmark(Baseline = true)]
    public Result<int, string> CreateSuccessResult()
    {
        return Result<int, string>.Success(42);
    }

    [Benchmark]
    public Result<int, string> CreateFailureResult()
    {
        return Result<int, string>.Failure("Error");
    }

    [Benchmark]
    public Result<int, string> MapSuccessResult()
    {
        var result = Result<int, string>.Success(21);
        return result.Map(x => x * 2);
    }

    [Benchmark]
    public Result<int, string> BindSuccessResult()
    {
        var result = Result<int, string>.Success(21);
        return result.Bind(x => Result<int, string>.Success(x * 2));
    }

    [Benchmark]
    public Result<int, string> ChainedOperations()
    {
        return Result<int, string>.Success(10)
            .Map(x => x * 2)
            .Bind(x => Result<int, string>.Success(x + 5))
            .Map(x => x / 5);
    }

    [Benchmark]
    public int MatchSuccessResult()
    {
        var result = Result<int, string>.Success(42);
        return result.Match(
            success => success,
            failure => 0);
    }

    [Benchmark]
    public Option<int> CreateSomeOption()
    {
        return Option<int>.Some(42);
    }

    [Benchmark]
    public Option<int> CreateNoneOption()
    {
        return Option<int>.None();
    }

    [Benchmark]
    public Option<int> MapSomeOption()
    {
        var option = Option<int>.Some(21);
        return option.Map(x => x * 2);
    }

    [Benchmark]
    public Option<int> BindSomeOption()
    {
        var option = Option<int>.Some(21);
        return option.Bind(x => Option<int>.Some(x * 2));
    }

    [Benchmark]
    public int GetValueOrDefaultSomeOption()
    {
        var option = Option<int>.Some(42);
        return option.GetValueOrDefault(0);
    }
}

/// <summary>
/// Benchmarks for common pipeline operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class PipelineOperationsBenchmarks
{
    private const int IterationCount = 100;

    [Benchmark(Baseline = true)]
    public Result<int, string> SimplePipeline()
    {
        var result = Result<int, string>.Success(0);
        for (int i = 0; i < IterationCount; i++)
        {
            result = result.Map(x => x + 1);
        }
        return result;
    }

    [Benchmark]
    public Result<int, string> PipelineWithBind()
    {
        var result = Result<int, string>.Success(0);
        for (int i = 0; i < IterationCount; i++)
        {
            result = result.Bind(x => Result<int, string>.Success(x + 1));
        }
        return result;
    }

    [Benchmark]
    public Result<int, string> PipelineWithMatch()
    {
        var result = Result<int, string>.Success(0);
        for (int i = 0; i < IterationCount; i++)
        {
            result = result.Match(
                success => Result<int, string>.Success(success + 1),
                failure => Result<int, string>.Failure(failure));
        }
        return result;
    }

    [Benchmark]
    public async Task<Result<int, string>> AsyncPipeline()
    {
        var result = Result<int, string>.Success(0);
        for (int i = 0; i < IterationCount; i++)
        {
            result = await Task.FromResult(result.Map(x => x + 1));
        }
        return result;
    }
}
