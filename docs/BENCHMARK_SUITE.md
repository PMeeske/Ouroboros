# Benchmark Suite Documentation

## Overview

The Ouroboros Benchmark Suite provides a comprehensive evaluation framework for assessing AI capabilities across multiple dimensions and standard benchmarks. It implements functional programming patterns with Result monads, immutable data structures, and full cancellation support.

## Features

- **ARC-AGI-2 Benchmark**: Abstract reasoning and pattern recognition evaluation
- **MMLU Benchmark**: Massive Multitask Language Understanding across multiple subjects
- **Continual Learning**: Measures catastrophic forgetting and retention
- **Cognitive Dimensions**: Tests reasoning, planning, learning, memory, generalization, creativity, and social intelligence
- **Comprehensive Evaluation**: Aggregates all benchmarks with strengths/weaknesses analysis

## Architecture

### Core Types

All types are immutable records following functional programming principles:

```csharp
public sealed record BenchmarkReport(
    string BenchmarkName,
    double OverallScore,
    Dictionary<string, double> SubScores,
    List<TaskResult> DetailedResults,
    TimeSpan TotalDuration,
    DateTime CompletedAt);

public sealed record ComprehensiveReport(
    Dictionary<string, BenchmarkReport> BenchmarkResults,
    double OverallScore,
    List<string> Strengths,
    List<string> Weaknesses,
    List<string> Recommendations,
    DateTime GeneratedAt);
```

### Error Handling

All operations use `Result<T, E>` monads - no exceptions are thrown:

```csharp
Task<Result<BenchmarkReport, string>> RunARCBenchmarkAsync(
    int taskCount = 100,
    CancellationToken ct = default);
```

## Usage

### Programmatic API

```csharp
using Ouroboros.Domain.Benchmarks;

var suite = new BenchmarkSuite();

// Run individual benchmarks
var arcResult = await suite.RunARCBenchmarkAsync(taskCount: 100);
arcResult.Match(
    onSuccess: report => Console.WriteLine($"Score: {report.OverallScore:P1}"),
    onFailure: error => Console.WriteLine($"Failed: {error}"));

// Run comprehensive evaluation
var fullResult = await suite.RunFullEvaluationAsync();
fullResult.Match(
    onSuccess: report =>
    {
        Console.WriteLine($"Overall Score: {report.OverallScore:P1}");
        Console.WriteLine("Strengths:");
        report.Strengths.ForEach(s => Console.WriteLine($"  - {s}"));
    },
    onFailure: error => Console.WriteLine($"Failed: {error}"));
```

### CLI Interface

```bash
# Run individual benchmarks
ouroboros benchmark --arc
ouroboros benchmark --mmlu --subjects "mathematics,physics,history"
ouroboros benchmark --cognitive --dimension Reasoning
ouroboros benchmark --continual

# Run comprehensive evaluation
ouroboros benchmark --full

# Run all benchmarks sequentially
ouroboros benchmark --all

# Save results to JSON file
ouroboros benchmark --arc --output results.json

# Customize task counts
ouroboros benchmark --arc --task-count 200
```

### Example Output

```
╔════════════════════════════════════════════════════════════╗
║  ARC-AGI-2                                                 ║
╚════════════════════════════════════════════════════════════╝

✓ Overall Score:  45.2%
  Duration:       00:00:05.3421
  Tasks:          100
  Successful:     45
  Failed:         55
  Completed at:   2026-01-10 08:00:00 UTC

Sub-Scores:
  rotation                       65.3%
  scaling                        48.1%
  color_mapping                  42.7%
  shape_transformation           25.8%
```

## Target Benchmarks

### ARC-AGI-2
- **Target**: 15%+ (baseline: 0-4%)
- **Tasks**: Abstract reasoning challenges
- **Metrics**: Pattern recognition, spatial reasoning

### MMLU
- **Target**: 70%+
- **Subjects**: Mathematics, Physics, Computer Science, History, and more
- **Metrics**: Multi-domain knowledge understanding

### Continual Learning
- **Target**: 80%+ retention after 10 tasks
- **Metrics**: Initial accuracy, final accuracy, retention rate
- **Focus**: Measuring catastrophic forgetting

### Cognitive Dimensions
- **Reasoning**: 65%+ (deductive, inductive)
- **Planning**: 70%+ (short-term, long-term)
- **Learning**: 75%+ (few-shot adaptation)
- **Memory**: 80%+ (episodic, semantic)
- **Generalization**: 60%+
- **Creativity**: 55%+
- **Social Intelligence**: 50%+

## Implementation Details

### Benchmark Execution Flow

1. **Initialization**: Create `BenchmarkSuite` instance
2. **Task Generation**: Generate benchmark-specific tasks
3. **Execution**: Run tasks with proper cancellation support
4. **Aggregation**: Collect results and calculate scores
5. **Reporting**: Generate comprehensive reports with insights

### Cancellation Support

All benchmarks support cancellation tokens:

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromMinutes(5));

var result = await suite.RunFullEvaluationAsync(cts.Token);
```

### Result Persistence

Reports can be serialized to JSON for historical tracking:

```csharp
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true
});
File.WriteAllText("benchmark-results.json", json);
```

## Testing

The benchmark suite includes comprehensive unit tests:

```bash
dotnet test --filter "FullyQualifiedName~BenchmarkSuiteTests"
```

**Test Coverage**: 30 unit tests covering all major functionality

## Future Enhancements

### Phase 4: Persistence and Historical Comparison
- [ ] Database backend for result storage
- [ ] Historical trend analysis
- [ ] Regression detection
- [ ] Comparative visualizations
- [ ] Benchmark result versioning

### Additional Benchmarks
- [ ] Program synthesis (50%+ on simple DSL tasks)
- [ ] Few-shot adaptation (3-5 examples)
- [ ] Planning horizon (10+ steps, 80%+ success)
- [ ] Episodic memory (<100ms retrieval, 90% relevance)
- [ ] Causal reasoning (70%+ correct cause identification)

## Contributing

When adding new benchmarks:

1. Follow the existing `Result<T, E>` monad pattern
2. Use immutable records for all data types
3. Add comprehensive unit tests
4. Include XML documentation
5. Update CLI commands
6. Add examples to the Examples project

## License

Part of the Ouroboros project. See LICENSE file for details.
