// <copyright file="FullSystemIntegrationExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Application.Integration;
using Ouroboros.Core.Monads;

/// <summary>
/// Example demonstrating the full Ouroboros system integration with all features.
/// </summary>
public static class FullSystemIntegrationExample
{
    /// <summary>
    /// Demonstrates building and using the full Ouroboros system.
    /// </summary>
    public static async Task RunFullSystemExampleAsync()
    {
        Console.WriteLine("=== Ouroboros Full System Integration Example ===\n");

        // Phase 1: Setup with Dependency Injection
        Console.WriteLine("Phase 1: Building Ouroboros with all features...");
        var services = new ServiceCollection();

        // Use simple AddOuroboros() without configuration for basic example
        services.AddOuroboros();
        Console.WriteLine("  - All basic services registered");

        var serviceProvider = services.BuildServiceProvider();
        var ouroboros = serviceProvider.GetRequiredService<IOuroborosCore>();
        Console.WriteLine("✓ Ouroboros system built successfully\n");

        // Phase 2: Execute a Goal
        Console.WriteLine("Phase 2: Executing a goal with full cognitive pipeline...");
        var executionConfig = new ExecutionConfig(
            UseEpisodicMemory: true,
            UseCausalReasoning: true,
            UseHierarchicalPlanning: true,
            MaxPlanningDepth: 5);

        var goal = "Analyze the performance trends and suggest improvements";
        Console.WriteLine($"Goal: {goal}");

        var executionResult = await ouroboros.ExecuteGoalAsync(goal, executionConfig);
        
        executionResult.Match(
            success =>
            {
                Console.WriteLine($"✓ Execution successful:");
                Console.WriteLine($"  - Output: {success.Output}");
                Console.WriteLine($"  - Duration: {success.Duration.TotalMilliseconds}ms");
                Console.WriteLine($"  - Episodes Generated: {success.GeneratedEpisodes.Count}");
                Console.WriteLine($"  - Plan Executed: {(success.ExecutedPlan != null ? "Yes" : "No")}");
            },
            error => Console.WriteLine($"✗ Execution failed: {error}"));

        Console.WriteLine();

        // Phase 3: Reasoning with Multiple Engines
        Console.WriteLine("Phase 3: Performing unified reasoning...");
        var reasoningConfig = new ReasoningConfig(
            UseSymbolicReasoning: true,
            UseCausalInference: true,
            UseAbduction: true);

        var query = "What are the root causes of performance degradation?";
        Console.WriteLine($"Query: {query}");

        var reasoningResult = await ouroboros.ReasonAboutAsync(query, reasoningConfig);
        
        reasoningResult.Match(
            success =>
            {
                Console.WriteLine($"✓ Reasoning completed:");
                Console.WriteLine($"  - Answer: {success.Answer}");
                Console.WriteLine($"  - Certainty: {success.Certainty}");
                Console.WriteLine($"  - Supporting Facts: {success.SupportingFacts.Count}");
                Console.WriteLine($"  - Proof Available: {(success.Proof != null ? "Yes" : "No")}");
            },
            error => Console.WriteLine($"✗ Reasoning failed: {error}"));

        Console.WriteLine();

        // Phase 4: Consciousness Integration
        Console.WriteLine("Phase 4: Interacting with consciousness scaffold...");
        var consciousnessResult = await ouroboros.Consciousness.BroadcastToConsciousnessAsync(
            "Performance analysis completed",
            "ExecutionEngine");

        consciousnessResult.Match(
            item => Console.WriteLine($"✓ Broadcast to workspace: {item.Content}"),
            error => Console.WriteLine($"✗ Broadcast failed: {error}"));

        var attentionResult = await ouroboros.Consciousness.GetAttentionalFocusAsync(topK: 5);

        attentionResult.Match(
            items =>
            {
                Console.WriteLine($"✓ Attended items: {items.Count}");
                foreach (var item in items.Take(3))
                {
                    Console.WriteLine($"  - [{item.Priority}] {item.Content.Substring(0, Math.Min(50, item.Content.Length))}...");
                }
            },
            error => Console.WriteLine($"✗ Failed to get attended items: {error}"));

        Console.WriteLine();

        // Phase 5: System State Inspection
        Console.WriteLine("Phase 5: Inspecting system state...");
        Console.WriteLine($"Episodic Memory: {ouroboros.EpisodicMemory.GetType().Name}");
        Console.WriteLine($"MeTTa Reasoning: {ouroboros.MeTTaReasoning.GetType().Name}");
        Console.WriteLine($"Hierarchical Planner: {ouroboros.HierarchicalPlanner.GetType().Name}");
        Console.WriteLine($"Causal Reasoning: {ouroboros.CausalReasoning.GetType().Name}");
        Console.WriteLine($"Program Synthesis: {ouroboros.ProgramSynthesis.GetType().Name}");
        Console.WriteLine($"World Model: {ouroboros.WorldModel.GetType().Name}");
        Console.WriteLine($"Multi-Agent: {ouroboros.MultiAgent.GetType().Name}");
        Console.WriteLine($"Meta-Learning: {ouroboros.MetaLearning.GetType().Name}");
        Console.WriteLine($"Embodied Agent: {ouroboros.EmbodiedAgent.GetType().Name}");
        Console.WriteLine($"Consciousness: {ouroboros.Consciousness.GetType().Name}");
        Console.WriteLine($"Benchmarks: {ouroboros.Benchmarks.GetType().Name}");

        Console.WriteLine("\n=== Example Complete ===");
    }

    /// <summary>
    /// Demonstrates the simplified full-system setup.
    /// </summary>
    public static async Task RunSimplifiedExampleAsync()
    {
        Console.WriteLine("=== Simplified Full System Example ===\n");

        var services = new ServiceCollection();
        services.AddOuroborosFull(); // One-liner setup with all features

        var serviceProvider = services.BuildServiceProvider();
        var ouroboros = serviceProvider.GetRequiredService<IOuroborosCore>();

        Console.WriteLine("✓ Full system configured with defaults");

        // Quick execution
        var result = await ouroboros.ExecuteGoalAsync(
            "Test system integration",
            ExecutionConfig.Default);

        result.Match(
            success => Console.WriteLine($"✓ System integration test: {(success.Success ? "PASSED" : "FAILED")}"),
            error => Console.WriteLine($"✗ System integration test failed: {error}"));

        Console.WriteLine("\n=== Simplified Example Complete ===");
    }
}
