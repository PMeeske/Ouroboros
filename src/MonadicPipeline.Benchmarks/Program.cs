using BenchmarkDotNet.Running;
using MonadicPipeline.Benchmarks;

var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// To run specific benchmarks:
// BenchmarkRunner.Run<ToolExecutionBenchmarks>();
// BenchmarkRunner.Run<MonadicOperationsBenchmarks>();
// BenchmarkRunner.Run<PipelineOperationsBenchmarks>();
