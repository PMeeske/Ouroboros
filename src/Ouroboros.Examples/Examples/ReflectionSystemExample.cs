// <copyright file="ReflectionSystemExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Application.Services.Reflection;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Persistence;
using Ouroboros.Domain.Reflection;

/// <summary>
/// Demonstrates the Self-Diagnostic &amp; Reflection System (F1.5).
/// Shows meta-cognitive reflection, performance analysis, and self-improvement capabilities.
/// </summary>
public static class ReflectionSystemExample
{
    /// <summary>
    /// Main example demonstrating all reflection capabilities.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunExampleAsync()
    {
        Console.WriteLine("=== Self-Diagnostic & Reflection System Demo ===\n");

        var engine = new ReflectionEngine();

        // 1. Performance Analysis
        await DemonstratePerformanceAnalysisAsync(engine);

        Console.WriteLine("\n" + new string('-', 60) + "\n");

        // 2. Error Pattern Detection
        await DemonstrateErrorPatternDetectionAsync(engine);

        Console.WriteLine("\n" + new string('-', 60) + "\n");

        // 3. Capability Assessment
        await DemonstrateCapabilityAssessmentAsync(engine);

        Console.WriteLine("\n" + new string('-', 60) + "\n");

        // 4. Improvement Suggestions
        await DemonstrateImprovementSuggestionsAsync(engine);

        Console.WriteLine("\n" + new string('-', 60) + "\n");

        // 5. Certainty Assessment with Laws of Form
        await DemonstrateCertaintyAssessmentAsync(engine);
    }

    private static async Task DemonstratePerformanceAnalysisAsync(ReflectionEngine engine)
    {
        Console.WriteLine("1. PERFORMANCE ANALYSIS");
        Console.WriteLine("========================\n");

        // Simulate some episodes with varying success rates
        var episodes = new List<Episode>();

        // Task A: High success rate
        for (int i = 0; i < 10; i++)
        {
            episodes.Add(new Episode(
                Guid.NewGuid(),
                "DataProcessing",
                new List<EnvironmentStep>(),
                10.0,
                DateTime.UtcNow.AddHours(-i),
                DateTime.UtcNow.AddHours(-i).AddMinutes(2),
                true));
        }

        // Task B: Lower success rate
        for (int i = 0; i < 10; i++)
        {
            episodes.Add(new Episode(
                Guid.NewGuid(),
                "ComplexReasoning",
                new List<EnvironmentStep>(),
                5.0,
                DateTime.UtcNow.AddHours(-i),
                DateTime.UtcNow.AddHours(-i).AddMinutes(8),
                i % 2 == 0)); // 50% success rate
        }

        var result = await engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromDays(1));

        if (result.IsSuccess)
        {
            var report = result.Value;
            Console.WriteLine($"Overall Success Rate: {report.AverageSuccessRate:P1}");
            Console.WriteLine($"Average Execution Time: {report.AverageExecutionTime.TotalMinutes:F1} minutes");
            Console.WriteLine($"\nPerformance by Task Type:");

            foreach (var kvp in report.ByTaskType.OrderByDescending(x => x.Value.SuccessRate))
            {
                var perf = kvp.Value;
                Console.WriteLine($"  {kvp.Key}:");
                Console.WriteLine($"    Success Rate: {perf.SuccessRate:P1} ({perf.Successes}/{perf.TotalAttempts})");
                Console.WriteLine($"    Avg Time: {perf.AverageTime:F1}s");
            }

            Console.WriteLine($"\nInsights Discovered: {report.Insights.Count}");
            foreach (var insight in report.Insights)
            {
                Console.WriteLine($"  [{insight.Type}] {insight.Description} (confidence: {insight.Confidence:P0})");
            }
        }
    }

    private static async Task DemonstrateErrorPatternDetectionAsync(ReflectionEngine engine)
    {
        Console.WriteLine("2. ERROR PATTERN DETECTION");
        Console.WriteLine("===========================\n");

        // Simulate failed episodes with patterns
        var failures = new List<FailedEpisode>();

        // Pattern 1: Timeout errors (5 occurrences)
        for (int i = 0; i < 5; i++)
        {
            failures.Add(new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow.AddHours(-i),
                "Process large dataset",
                $"Timeout error occurred while processing dataset {i}",
                new object(),
                new Dictionary<string, object>
                {
                    ["dataset_size"] = 1000000 + (i * 100000),
                    ["error_code"] = "E_TIMEOUT"
                }));
        }

        // Pattern 2: Memory errors (3 occurrences)
        for (int i = 0; i < 3; i++)
        {
            failures.Add(new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow.AddHours(-i),
                "Load ML model",
                "Out of memory error during model initialization",
                new object(),
                new Dictionary<string, object>
                {
                    ["model_size"] = "2GB",
                    ["error_code"] = "E_OOM"
                }));
        }

        var result = await engine.DetectErrorPatternsAsync(failures);

        if (result.IsSuccess)
        {
            var patterns = result.Value;
            Console.WriteLine($"Detected {patterns.Count} error pattern(s):\n");

            foreach (var pattern in patterns)
            {
                Console.WriteLine($"Pattern: {pattern.Description}");
                Console.WriteLine($"  Frequency: {pattern.Frequency} occurrence(s)");
                Console.WriteLine($"  Severity Score: {pattern.SeverityScore:F2}");
                if (pattern.SuggestedFix != null)
                {
                    Console.WriteLine($"  Suggested Fix: {pattern.SuggestedFix}");
                }

                Console.WriteLine($"  Examples:");
                foreach (var example in pattern.Examples.Take(2))
                {
                    Console.WriteLine($"    - {example.FailureReason}");
                }

                Console.WriteLine();
            }
        }
    }

    private static async Task DemonstrateCapabilityAssessmentAsync(ReflectionEngine engine)
    {
        Console.WriteLine("3. CAPABILITY ASSESSMENT");
        Console.WriteLine("=========================\n");

        // Create benchmark tasks for different cognitive dimensions
        var tasks = new List<BenchmarkTask>
        {
            // Reasoning tasks
            new BenchmarkTask(
                "Logic Puzzle",
                CognitiveDimension.Reasoning,
                () => Task.FromResult(true),
                TimeSpan.FromSeconds(5)),
            new BenchmarkTask(
                "Mathematical Problem",
                CognitiveDimension.Reasoning,
                () => Task.FromResult(true),
                TimeSpan.FromSeconds(5)),

            // Planning tasks
            new BenchmarkTask(
                "Route Planning",
                CognitiveDimension.Planning,
                () => Task.FromResult(false),
                TimeSpan.FromSeconds(5)),
            new BenchmarkTask(
                "Resource Allocation",
                CognitiveDimension.Planning,
                () => Task.FromResult(true),
                TimeSpan.FromSeconds(5)),

            // Learning tasks
            new BenchmarkTask(
                "Pattern Recognition",
                CognitiveDimension.Learning,
                () => Task.FromResult(true),
                TimeSpan.FromSeconds(5)),

            // Memory tasks
            new BenchmarkTask(
                "Information Recall",
                CognitiveDimension.Memory,
                () => Task.FromResult(true),
                TimeSpan.FromSeconds(5)),

            // Creativity tasks
            new BenchmarkTask(
                "Novel Solution Generation",
                CognitiveDimension.Creativity,
                () => Task.FromResult(false),
                TimeSpan.FromSeconds(5))
        };

        var result = await engine.AssessCapabilitiesAsync(tasks);

        if (result.IsSuccess)
        {
            var map = result.Value;
            Console.WriteLine($"Overall Capability Score: {map.OverallScore:P1}\n");

            Console.WriteLine("Scores by Cognitive Dimension:");
            foreach (var kvp in map.Scores.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value:P1}");
            }

            if (map.StrongestDimension.HasValue)
            {
                Console.WriteLine($"\nStrongest Dimension: {map.StrongestDimension}");
            }

            if (map.WeakestDimension.HasValue)
            {
                Console.WriteLine($"Weakest Dimension: {map.WeakestDimension}");
            }

            if (map.Strengths.Count > 0)
            {
                Console.WriteLine("\nIdentified Strengths:");
                foreach (var strength in map.Strengths)
                {
                    Console.WriteLine($"  ✓ {strength}");
                }
            }

            if (map.Weaknesses.Count > 0)
            {
                Console.WriteLine("\nIdentified Weaknesses:");
                foreach (var weakness in map.Weaknesses)
                {
                    Console.WriteLine($"  ✗ {weakness}");
                }
            }
        }
    }

    private static async Task DemonstrateImprovementSuggestionsAsync(ReflectionEngine engine)
    {
        Console.WriteLine("4. IMPROVEMENT SUGGESTIONS");
        Console.WriteLine("===========================\n");

        // Create a performance report with some issues
        var report = new PerformanceReport(
            0.45, // Low success rate
            TimeSpan.FromMinutes(12), // High execution time
            new Dictionary<string, TaskPerformance>
            {
                ["TaskA"] = new TaskPerformance("TaskA", 20, 15, 300, Array.Empty<string>()),
                ["TaskB"] = new TaskPerformance("TaskB", 20, 4, 600, new[] { "Timeout", "Network error" })
            },
            new List<Insight>
            {
                new Insight(
                    InsightType.Weakness,
                    "Frequent failures in network-dependent operations",
                    0.85,
                    Array.Empty<Episode>())
            },
            DateTime.UtcNow);

        var result = await engine.SuggestImprovementsAsync(report);

        if (result.IsSuccess)
        {
            var suggestions = result.Value;
            Console.WriteLine($"Generated {suggestions.Count} improvement suggestion(s):\n");

            foreach (var suggestion in suggestions)
            {
                Console.WriteLine($"Area: {suggestion.Area}");
                Console.WriteLine($"Priority: {suggestion.Priority}");
                Console.WriteLine($"Expected Impact: {suggestion.ExpectedImpact:P0}");
                Console.WriteLine($"Suggestion: {suggestion.Suggestion}");
                Console.WriteLine($"Implementation: {suggestion.Implementation}");
                Console.WriteLine();
            }
        }
    }

    private static async Task DemonstrateCertaintyAssessmentAsync(ReflectionEngine engine)
    {
        Console.WriteLine("5. CERTAINTY ASSESSMENT (Laws of Form)");
        Console.WriteLine("========================================\n");

        // Test case 1: High certainty (strong supporting evidence)
        Console.WriteLine("Test 1: Strong Supporting Evidence");
        var claim1 = "system is performing optimally";
        var evidence1 = new List<Fact>
        {
            new Fact(Guid.NewGuid(), "System performance metrics show 99.9% uptime", "Monitor", 0.95, DateTime.UtcNow),
            new Fact(Guid.NewGuid(), "All performance tests passing with optimal results", "TestSuite", 0.90, DateTime.UtcNow),
            new Fact(Guid.NewGuid(), "System health checks indicate optimal performance", "HealthCheck", 0.88, DateTime.UtcNow)
        };

        var result1 = await engine.AssessCertaintyAsync(claim1, evidence1);
        Console.WriteLine($"Claim: \"{claim1}\"");
        Console.WriteLine($"Evidence Count: {evidence1.Count}");
        Console.WriteLine($"Certainty Assessment: {FormatForm(result1.Value)}");
        Console.WriteLine($"Interpretation: {InterpretForm(result1.Value)}\n");

        // Test case 2: Uncertainty (mixed evidence)
        Console.WriteLine("Test 2: Mixed Evidence");
        var claim2 = "system performance acceptable";
        var evidence2 = new List<Fact>
        {
            new Fact(Guid.NewGuid(), "System performance varies significantly", "Monitor", 0.6, DateTime.UtcNow),
            new Fact(Guid.NewGuid(), "Database queries unrelated metrics", "Database", 0.5, DateTime.UtcNow)
        };

        var result2 = await engine.AssessCertaintyAsync(claim2, evidence2);
        Console.WriteLine($"Claim: \"{claim2}\"");
        Console.WriteLine($"Evidence Count: {evidence2.Count}");
        Console.WriteLine($"Certainty Assessment: {FormatForm(result2.Value)}");
        Console.WriteLine($"Interpretation: {InterpretForm(result2.Value)}\n");

        // Test case 3: No evidence
        Console.WriteLine("Test 3: No Evidence");
        var claim3 = "new feature will improve performance";
        var evidence3 = new List<Fact>();

        var result3 = await engine.AssessCertaintyAsync(claim3, evidence3);
        Console.WriteLine($"Claim: \"{claim3}\"");
        Console.WriteLine($"Evidence Count: {evidence3.Count}");
        Console.WriteLine($"Certainty Assessment: {FormatForm(result3.Value)}");
        Console.WriteLine($"Interpretation: {InterpretForm(result3.Value)}");
    }

    private static string FormatForm(Form form)
    {
        return form.Match(
            onMark: () => "⌐ (Mark/Cross)",
            onVoid: () => "∅ (Void)",
            onImaginary: () => "i (Imaginary)");
    }

    private static string InterpretForm(Form form)
    {
        return form.Match(
            onMark: () => "Claim is CERTAIN and TRUE based on evidence",
            onVoid: () => "Claim is CERTAIN but FALSE/CONTRADICTED by evidence",
            onImaginary: () => "Claim has UNCERTAIN/PARADOXICAL status - insufficient or contradictory evidence");
    }
}
