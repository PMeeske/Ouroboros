// <copyright file="BenchmarkSuiteExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Benchmarks;

namespace Ouroboros.Examples;

/// <summary>
/// Example demonstrating the Benchmark Suite functionality for evaluating
/// Ouroboros capabilities across multiple dimensions and standard AI benchmarks.
/// </summary>
public static class BenchmarkSuiteExample
{
    /// <summary>
    /// Demonstrates running individual benchmarks.
    /// </summary>
    public static async Task RunIndividualBenchmarksAsync()
    {
        Console.WriteLine("=== Individual Benchmark Examples ===\n");

        var suite = new BenchmarkSuite();

        // Example 1: Run ARC-AGI-2 benchmark
        Console.WriteLine("Running ARC-AGI-2 Benchmark...");
        var arcResult = await suite.RunARCBenchmarkAsync(taskCount: 20);
        arcResult.Match(
            onSuccess: report =>
            {
                Console.WriteLine($"✓ {report.BenchmarkName} completed");
                Console.WriteLine($"  Overall Score: {report.OverallScore:P1}");
                Console.WriteLine($"  Duration: {report.TotalDuration}");
                Console.WriteLine($"  Tasks Completed: {report.DetailedResults.Count}");
                Console.WriteLine($"  Sub-scores:");
                foreach (var (category, score) in report.SubScores)
                {
                    Console.WriteLine($"    - {category}: {score:P1}");
                }
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ ARC benchmark failed: {error}");
            });

        Console.WriteLine();

        // Example 2: Run MMLU benchmark
        Console.WriteLine("Running MMLU Benchmark...");
        var subjects = new List<string> { "mathematics", "physics", "computer_science", "history" };
        var mmluResult = await suite.RunMMLUBenchmarkAsync(subjects);
        mmluResult.Match(
            onSuccess: report =>
            {
                Console.WriteLine($"✓ {report.BenchmarkName} completed");
                Console.WriteLine($"  Overall Score: {report.OverallScore:P1}");
                Console.WriteLine($"  Subjects tested: {report.SubScores.Count}");
                foreach (var (subject, score) in report.SubScores)
                {
                    Console.WriteLine($"    - {subject}: {score:P1}");
                }
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ MMLU benchmark failed: {error}");
            });

        Console.WriteLine();

        // Example 3: Run Cognitive Dimension benchmark
        Console.WriteLine("Running Cognitive Dimension Benchmark (Reasoning)...");
        var cognitiveResult = await suite.RunCognitiveBenchmarkAsync(CognitiveDimension.Reasoning);
        cognitiveResult.Match(
            onSuccess: report =>
            {
                Console.WriteLine($"✓ {report.BenchmarkName} completed");
                Console.WriteLine($"  Overall Score: {report.OverallScore:P1}");
                Console.WriteLine($"  Tasks Completed: {report.DetailedResults.Count}");
                var successRate = report.DetailedResults.Count(r => r.Success) / (double)report.DetailedResults.Count;
                Console.WriteLine($"  Success Rate: {successRate:P1}");
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ Cognitive benchmark failed: {error}");
            });

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates running continual learning benchmark.
    /// </summary>
    public static async Task RunContinualLearningBenchmarkAsync()
    {
        Console.WriteLine("=== Continual Learning Benchmark Example ===\n");

        var suite = new BenchmarkSuite();

        // Create sample task sequences
        var sequences = new List<TaskSequence>
        {
            new TaskSequence(
                Name: "Sequential Learning",
                Tasks: new List<LearningTask>
                {
                    new LearningTask(
                        Name: "Task A",
                        TrainingData: new List<TrainingExample>
                        {
                            new TrainingExample("Input A1", "Output A1"),
                            new TrainingExample("Input A2", "Output A2"),
                        },
                        TestData: new List<TestExample>
                        {
                            new TestExample("Test A1", "Expected A1", (a, e) => a == e),
                        }),
                    new LearningTask(
                        Name: "Task B",
                        TrainingData: new List<TrainingExample>
                        {
                            new TrainingExample("Input B1", "Output B1"),
                        },
                        TestData: new List<TestExample>
                        {
                            new TestExample("Test B1", "Expected B1", (a, e) => a == e),
                        }),
                },
                MeasureRetention: true),
        };

        Console.WriteLine("Running Continual Learning Benchmark...");
        var result = await suite.RunContinualLearningBenchmarkAsync(sequences);
        result.Match(
            onSuccess: report =>
            {
                Console.WriteLine($"✓ {report.BenchmarkName} completed");
                Console.WriteLine($"  Overall Retention Score: {report.OverallScore:P1}");
                Console.WriteLine($"  Duration: {report.TotalDuration}");
                Console.WriteLine("\nDetailed Results:");
                foreach (var taskResult in report.DetailedResults)
                {
                    Console.WriteLine($"\n  {taskResult.TaskName}:");
                    Console.WriteLine($"    - Success: {taskResult.Success}");
                    Console.WriteLine($"    - Retention Score: {taskResult.Score:P1}");
                    Console.WriteLine($"    - Initial Accuracy: {taskResult.Metadata["initial_accuracy"]:P1}");
                    Console.WriteLine($"    - Final Accuracy: {taskResult.Metadata["final_accuracy"]:P1}");
                }
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ Continual learning benchmark failed: {error}");
            });

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates running a comprehensive evaluation.
    /// </summary>
    public static async Task RunComprehensiveEvaluationAsync()
    {
        Console.WriteLine("=== Comprehensive Evaluation Example ===\n");

        var suite = new BenchmarkSuite();

        Console.WriteLine("Running comprehensive evaluation across all benchmarks...");
        Console.WriteLine("This may take a while...\n");

        var result = await suite.RunFullEvaluationAsync();
        result.Match(
            onSuccess: report =>
            {
                Console.WriteLine("✓ Comprehensive evaluation completed!\n");
                Console.WriteLine($"Overall Score: {report.OverallScore:P1}");
                Console.WriteLine($"Generated at: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");

                Console.WriteLine("\n=== Benchmark Results ===");
                foreach (var (name, benchmarkReport) in report.BenchmarkResults)
                {
                    Console.WriteLine($"\n{name}:");
                    Console.WriteLine($"  Score: {benchmarkReport.OverallScore:P1}");
                    Console.WriteLine($"  Duration: {benchmarkReport.TotalDuration}");
                    Console.WriteLine($"  Tasks: {benchmarkReport.DetailedResults.Count}");
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
            },
            onFailure: error =>
            {
                Console.WriteLine($"✗ Comprehensive evaluation failed: {error}");
            });

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates running all cognitive dimension benchmarks.
    /// </summary>
    public static async Task RunAllCognitiveDimensionsAsync()
    {
        Console.WriteLine("=== All Cognitive Dimensions Example ===\n");

        var suite = new BenchmarkSuite();
        var dimensions = Enum.GetValues<CognitiveDimension>();

        Console.WriteLine($"Testing {dimensions.Length} cognitive dimensions...\n");

        foreach (var dimension in dimensions)
        {
            Console.WriteLine($"Testing {dimension}...");
            var result = await suite.RunCognitiveBenchmarkAsync(dimension);
            result.Match(
                onSuccess: report =>
                {
                    Console.WriteLine($"  ✓ Score: {report.OverallScore:P1}");
                },
                onFailure: error =>
                {
                    Console.WriteLine($"  ✗ Failed: {error}");
                });
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates error handling with invalid inputs.
    /// </summary>
    public static async Task DemonstrateErrorHandlingAsync()
    {
        Console.WriteLine("=== Error Handling Examples ===\n");

        var suite = new BenchmarkSuite();

        // Example 1: Invalid task count
        Console.WriteLine("Example 1: Invalid task count");
        var result1 = await suite.RunARCBenchmarkAsync(taskCount: -5);
        result1.Match(
            onSuccess: _ => Console.WriteLine("  Unexpected success"),
            onFailure: error => Console.WriteLine($"  ✓ Handled error: {error}"));

        Console.WriteLine();

        // Example 2: Empty subject list
        Console.WriteLine("Example 2: Empty subject list");
        var result2 = await suite.RunMMLUBenchmarkAsync(new List<string>());
        result2.Match(
            onSuccess: _ => Console.WriteLine("  Unexpected success"),
            onFailure: error => Console.WriteLine($"  ✓ Handled error: {error}"));

        Console.WriteLine();

        // Example 3: Empty task sequences
        Console.WriteLine("Example 3: Empty task sequences");
        var result3 = await suite.RunContinualLearningBenchmarkAsync(new List<TaskSequence>());
        result3.Match(
            onSuccess: _ => Console.WriteLine("  Unexpected success"),
            onFailure: error => Console.WriteLine($"  ✓ Handled error: {error}"));

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates using cancellation tokens.
    /// </summary>
    public static async Task DemonstrateCancellationAsync()
    {
        Console.WriteLine("=== Cancellation Token Example ===\n");

        var suite = new BenchmarkSuite();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        Console.WriteLine("Starting benchmark with short cancellation timeout...");
        var result = await suite.RunFullEvaluationAsync(cts.Token);
        result.Match(
            onSuccess: _ => Console.WriteLine("  Benchmark completed (unexpected)"),
            onFailure: error => Console.WriteLine($"  ✓ Benchmark cancelled: {error}"));

        Console.WriteLine();
    }

    /// <summary>
    /// Main entry point for running all examples.
    /// </summary>
    public static async Task RunAllExamplesAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        Ouroboros Benchmark Suite Examples                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        await RunIndividualBenchmarksAsync();
        await RunContinualLearningBenchmarkAsync();
        await RunAllCognitiveDimensionsAsync();
        await DemonstrateErrorHandlingAsync();
        await DemonstrateCancellationAsync();
        await RunComprehensiveEvaluationAsync();

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              All Examples Completed                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }
}
