using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Domain.Benchmarks;
using Ouroboros.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// CLI commands for running benchmark suite evaluations.
/// </summary>
public static class BenchmarkCommands
{
    /// <summary>
    /// Runs benchmarks based on the provided options.
    /// </summary>
    /// <param name="o">Benchmark options.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunBenchmarksAsync(BenchmarkOptions o)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        Ouroboros Benchmark Suite                          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var suite = new BenchmarkSuite();
        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nCancelling benchmark execution...");
            cts.Cancel();
        };

        try
        {
            if (o.Full || o.All)
            {
                await RunFullEvaluationAsync(suite, o, cts.Token);
            }
            else if (o.ARC || o.All)
            {
                await RunARCBenchmarkAsync(suite, o, cts.Token);
            }
            else if (o.MMLU || o.All)
            {
                await RunMMLUBenchmarkAsync(suite, o, cts.Token);
            }
            else if (o.Continual || o.All)
            {
                await RunContinualLearningBenchmarkAsync(suite, o, cts.Token);
            }
            else if (o.Cognitive || o.All)
            {
                await RunCognitiveBenchmarkAsync(suite, o, cts.Token);
            }
            else
            {
                // Default: show help
                Console.WriteLine("No benchmark specified. Use --help to see available options.");
                Console.WriteLine("\nQuick start examples:");
                Console.WriteLine("  benchmark --arc              # Run ARC-AGI-2 benchmark");
                Console.WriteLine("  benchmark --mmlu             # Run MMLU benchmark");
                Console.WriteLine("  benchmark --cognitive        # Run cognitive benchmark");
                Console.WriteLine("  benchmark --full             # Run comprehensive evaluation");
                Console.WriteLine("  benchmark --all              # Run all benchmarks");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n✗ Benchmark execution cancelled by user");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n✗ Benchmark execution failed: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    private static async Task RunARCBenchmarkAsync(BenchmarkSuite suite, BenchmarkOptions o, CancellationToken ct)
    {
        Console.WriteLine($"Running ARC-AGI-2 Benchmark ({o.TaskCount} tasks)...\n");

        var result = await suite.RunARCBenchmarkAsync(o.TaskCount, ct);
        result.Match(
            onSuccess: report =>
            {
                PrintBenchmarkReport(report);
                SaveReportIfRequested(report, o.OutputFile);
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ ARC benchmark failed: {error}");
            });
    }

    private static async Task RunMMLUBenchmarkAsync(BenchmarkSuite suite, BenchmarkOptions o, CancellationToken ct)
    {
        var subjects = o.Subjects.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        Console.WriteLine($"Running MMLU Benchmark ({subjects.Count} subjects)...");
        Console.WriteLine($"Subjects: {string.Join(", ", subjects)}\n");

        var result = await suite.RunMMLUBenchmarkAsync(subjects, ct);
        result.Match(
            onSuccess: report =>
            {
                PrintBenchmarkReport(report);
                SaveReportIfRequested(report, o.OutputFile);
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ MMLU benchmark failed: {error}");
            });
    }

    private static async Task RunContinualLearningBenchmarkAsync(BenchmarkSuite suite, BenchmarkOptions o, CancellationToken ct)
    {
        Console.WriteLine("Running Continual Learning Benchmark...\n");

        // Create sample task sequences
        var sequences = new[]
        {
            new TaskSequence(
                Name: "Sequential Task Learning",
                Tasks: new[]
                {
                    new LearningTask("Task 1", new[] { new TrainingExample("Input1", "Output1") }.ToList(), new[] { new TestExample("Test1", "Expected1", (a, e) => a == e) }.ToList()),
                    new LearningTask("Task 2", new[] { new TrainingExample("Input2", "Output2") }.ToList(), new[] { new TestExample("Test2", "Expected2", (a, e) => a == e) }.ToList()),
                }.ToList(),
                MeasureRetention: true),
        }.ToList();

        var result = await suite.RunContinualLearningBenchmarkAsync(sequences, ct);
        result.Match(
            onSuccess: report =>
            {
                PrintBenchmarkReport(report);
                SaveReportIfRequested(report, o.OutputFile);
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ Continual learning benchmark failed: {error}");
            });
    }

    private static async Task RunCognitiveBenchmarkAsync(BenchmarkSuite suite, BenchmarkOptions o, CancellationToken ct)
    {
        if (!Enum.TryParse<CognitiveDimension>(o.Dimension, ignoreCase: true, out var dimension))
        {
            Console.WriteLine($"✗ Invalid cognitive dimension: {o.Dimension}");
            Console.WriteLine("Valid dimensions: Reasoning, Planning, Learning, Memory, Generalization, Creativity, SocialIntelligence");
            return;
        }

        Console.WriteLine($"Running Cognitive Benchmark ({dimension})...\n");

        var result = await suite.RunCognitiveBenchmarkAsync(dimension, ct);
        result.Match(
            onSuccess: report =>
            {
                PrintBenchmarkReport(report);
                SaveReportIfRequested(report, o.OutputFile);
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ Cognitive benchmark failed: {error}");
            });
    }

    private static async Task RunFullEvaluationAsync(BenchmarkSuite suite, BenchmarkOptions o, CancellationToken ct)
    {
        Console.WriteLine("Running Comprehensive Evaluation...");
        Console.WriteLine("This will run all benchmarks and may take several minutes.\n");

        var result = await suite.RunFullEvaluationAsync(ct);
        result.Match(
            onSuccess: report =>
            {
                PrintComprehensiveReport(report);
                SaveComprehensiveReportIfRequested(report, o.OutputFile);
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ Comprehensive evaluation failed: {error}");
            });
    }

    private static void PrintBenchmarkReport(BenchmarkReport report)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  {report.BenchmarkName,-56} ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"✓ Overall Score:  {report.OverallScore:P1}");
        Console.WriteLine($"  Duration:       {report.TotalDuration}");
        Console.WriteLine($"  Tasks:          {report.DetailedResults.Count}");
        Console.WriteLine($"  Successful:     {report.DetailedResults.Count(r => r.Success)}");
        Console.WriteLine($"  Failed:         {report.DetailedResults.Count(r => !r.Success)}");
        Console.WriteLine($"  Completed at:   {report.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");

        if (report.SubScores.Any())
        {
            Console.WriteLine("\nSub-Scores:");
            foreach (var (category, score) in report.SubScores.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {category,-30} {score:P1}");
            }
        }

        Console.WriteLine();
    }

    private static void PrintComprehensiveReport(ComprehensiveReport report)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║            Comprehensive Evaluation Report                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"✓ Overall Score: {report.OverallScore:P1}");
        Console.WriteLine($"  Generated at:  {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"  Benchmarks:    {report.BenchmarkResults.Count}");
        Console.WriteLine();

        Console.WriteLine("=== Benchmark Results ===");
        foreach (var (name, benchmarkReport) in report.BenchmarkResults.OrderByDescending(x => x.Value.OverallScore))
        {
            Console.WriteLine($"\n{name}:");
            Console.WriteLine($"  Score:    {benchmarkReport.OverallScore:P1}");
            Console.WriteLine($"  Duration: {benchmarkReport.TotalDuration}");
            Console.WriteLine($"  Tasks:    {benchmarkReport.DetailedResults.Count}");
        }

        Console.WriteLine("\n=== Strengths ===");
        foreach (var strength in report.Strengths)
        {
            Console.WriteLine($"  ✓ {strength}");
        }

        Console.WriteLine("\n=== Areas for Improvement ===");
        foreach (var weakness in report.Weaknesses)
        {
            Console.WriteLine($"  ⚠ {weakness}");
        }

        Console.WriteLine("\n=== Recommendations ===");
        foreach (var recommendation in report.Recommendations)
        {
            Console.WriteLine($"  → {recommendation}");
        }

        Console.WriteLine();
    }

    private static void SaveReportIfRequested(BenchmarkReport report, string? outputFile)
    {
        if (string.IsNullOrWhiteSpace(outputFile))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(outputFile, json);
            Console.WriteLine($"\n✓ Report saved to: {outputFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Failed to save report: {ex.Message}");
        }
    }

    private static void SaveComprehensiveReportIfRequested(ComprehensiveReport report, string? outputFile)
    {
        if (string.IsNullOrWhiteSpace(outputFile))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(outputFile, json);
            Console.WriteLine($"\n✓ Report saved to: {outputFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Failed to save report: {ex.Message}");
        }
    }
}
