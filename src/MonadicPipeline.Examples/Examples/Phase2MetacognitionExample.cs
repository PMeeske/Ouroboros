// <copyright file="Phase2MetacognitionExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Example demonstrating Phase 2 metacognitive capabilities.
/// Shows how the agent understands its own capabilities, manages goals,
/// and performs self-evaluation.
/// </summary>
public static class Phase2MetacognitionExample
{
    /// <summary>
    /// Demonstrates complete Phase 2 workflow: capability assessment,
    /// goal decomposition, and self-evaluation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunCompleteWorkflow()
    {
        Console.WriteLine("=== Phase 2 Metacognition Example ===\n");
        Console.WriteLine("This example shows how the agent:");
        Console.WriteLine("1. Understands its own capabilities (self-model)");
        Console.WriteLine("2. Decomposes and manages hierarchical goals");
        Console.WriteLine("3. Evaluates its own performance and suggests improvements\n");

        // Setup
        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();
        var memory = new PersistentMemoryStore();
        var skills = new SkillRegistry();
        var safety = new SafetyGuard();

        // Initialize Phase 2 components
        var capabilityRegistry = new CapabilityRegistry(chatModel, tools);
        var goalHierarchy = new GoalHierarchy(chatModel, safety);
        var router = new UncertaintyRouter(null!, 0.7);

        var orchestrator = new MetaAIPlannerOrchestrator(
            chatModel,
            tools,
            memory,
            skills,
            router,
            safety);

        var evaluator = new SelfEvaluator(
            chatModel,
            capabilityRegistry,
            skills,
            memory,
            orchestrator);

        Console.WriteLine("✓ Phase 2 components initialized\n");

        // === Phase 1: Capability Self-Model ===
        Console.WriteLine("=== Phase 1: Capability Self-Model ===\n");

        // Register agent capabilities
        RegisterInitialCapabilities(capabilityRegistry);

        // Display what the agent knows it can do
        var capabilities = await capabilityRegistry.GetCapabilitiesAsync();
        Console.WriteLine($"Agent self-model: {capabilities.Count} capabilities registered\n");
        foreach (var cap in capabilities.Take(5))
        {
            Console.WriteLine($"✓ {cap.Name}");
            Console.WriteLine($"  Description: {cap.Description}");
            Console.WriteLine($"  Success Rate: {cap.SuccessRate:P0}");
            Console.WriteLine($"  Average Latency: {cap.AverageLatency:F0}ms");
            Console.WriteLine($"  Limitations: {string.Join(", ", cap.KnownLimitations)}");
            Console.WriteLine();
        }

        // Test capability assessment
        await TestCapabilityAssessment(capabilityRegistry);

        // === Phase 2: Goal Hierarchy & Decomposition ===
        Console.WriteLine("\n=== Phase 2: Goal Hierarchy & Decomposition ===\n");

        var complexGoal = new Goal(
            "Build an intelligent research assistant",
            GoalType.Primary,
            1.0);

        Console.WriteLine($"Complex Goal: {complexGoal.Description}\n");

        // Check value alignment
        var alignmentResult = await goalHierarchy.CheckValueAlignmentAsync(complexGoal);
        if (alignmentResult.IsSuccess)
        {
            Console.WriteLine("✓ Goal is value-aligned with safety constraints\n");
        }
        else
        {
            Console.WriteLine($"✗ Goal rejected: {alignmentResult.Error}\n");
            return;
        }

        // Decompose goal hierarchically
        Console.WriteLine("Decomposing goal into subgoals...\n");
        var decomposedResult = await goalHierarchy.DecomposeGoalAsync(complexGoal, maxDepth: 2);

        if (decomposedResult.IsSuccess)
        {
            var decomposed = decomposedResult.Value;
            Console.WriteLine($"✓ Decomposed into {decomposed.Subgoals.Count} subgoals:\n");

            int subgoalNum = 1;
            foreach (var subgoal in decomposed.Subgoals)
            {
                Console.WriteLine($"{subgoalNum}. {subgoal.Description}");
                Console.WriteLine($"   Type: {subgoal.Type}, Priority: {subgoal.Priority:F2}");

                if (subgoal.Subgoals.Any())
                {
                    Console.WriteLine($"   Sub-subgoals:");
                    foreach (var subsubgoal in subgoal.Subgoals.Take(3))
                    {
                        Console.WriteLine($"   • {subsubgoal.Description}");
                    }
                }

                Console.WriteLine();
                subgoalNum++;
            }

            // Add to hierarchy
            goalHierarchy.AddGoal(decomposed);
        }

        // Test conflict detection
        await TestGoalConflicts(goalHierarchy);

        // Prioritize goals
        var prioritized = await goalHierarchy.PrioritizeGoalsAsync();
        Console.WriteLine($"\nGoal Execution Order (prioritized):");
        foreach (var goal in prioritized.Take(5))
        {
            Console.WriteLine($"  {prioritized.IndexOf(goal) + 1}. [{goal.Type}] {goal.Description.Substring(0, Math.Min(60, goal.Description.Length))}...");
        }

        // === Phase 3: Self-Evaluation & Improvement ===
        Console.WriteLine("\n\n=== Phase 3: Self-Evaluation & Improvement ===\n");

        // Simulate some execution history for evaluation
        await SimulateExecutionHistory(memory, evaluator);

        // Perform self-assessment
        Console.WriteLine("Performing self-assessment...\n");
        var assessmentResult = await evaluator.EvaluatePerformanceAsync();

        if (assessmentResult.IsSuccess)
        {
            var assessment = assessmentResult.Value;

            Console.WriteLine("=== SELF-ASSESSMENT REPORT ===\n");
            Console.WriteLine($"Overall Performance: {assessment.OverallPerformance:P0}");
            Console.WriteLine($"Confidence Calibration: {assessment.ConfidenceCalibration:P0}");
            Console.WriteLine($"Skill Acquisition Rate: {assessment.SkillAcquisitionRate:F2} skills/day\n");

            Console.WriteLine($"Strengths ({assessment.Strengths.Count}):");
            foreach (var strength in assessment.Strengths.Take(5))
            {
                Console.WriteLine($"  ✓ {strength}");
            }

            Console.WriteLine();

            Console.WriteLine($"Weaknesses ({assessment.Weaknesses.Count}):");
            foreach (var weakness in assessment.Weaknesses.Take(5))
            {
                Console.WriteLine($"  ⚠ {weakness}");
            }

            Console.WriteLine();

            Console.WriteLine($"Summary: {assessment.Summary}\n");
        }

        // Generate insights
        Console.WriteLine("Generating insights from recent experiences...\n");
        var insights = await evaluator.GenerateInsightsAsync();

        Console.WriteLine($"=== INSIGHTS ({insights.Count}) ===\n");
        foreach (var insight in insights.Take(5))
        {
            Console.WriteLine($"[{insight.Category}] (Confidence: {insight.Confidence:P0})");
            Console.WriteLine($"  {insight.Description}");
            if (insight.SupportingEvidence.Any())
            {
                Console.WriteLine($"  Evidence: {string.Join(", ", insight.SupportingEvidence.Take(2))}");
            }

            Console.WriteLine();
        }

        // Create improvement plan
        Console.WriteLine("Creating self-improvement plan...\n");
        var improvementResult = await evaluator.SuggestImprovementsAsync();

        if (improvementResult.IsSuccess)
        {
            var plan = improvementResult.Value;

            Console.WriteLine("=== IMPROVEMENT PLAN ===\n");
            Console.WriteLine($"Goal: {plan.Goal}");
            Console.WriteLine($"Priority: {plan.Priority:F2}");
            Console.WriteLine($"Estimated Duration: {plan.EstimatedDuration.TotalDays:F0} days\n");

            Console.WriteLine("Actions:");
            for (int i = 0; i < plan.Actions.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {plan.Actions[i]}");
            }

            Console.WriteLine();

            if (plan.ExpectedImprovements.Any())
            {
                Console.WriteLine("Expected Improvements:");
                foreach (var improvement in plan.ExpectedImprovements)
                {
                    Console.WriteLine($"  • {improvement.Key}: +{improvement.Value:P0}");
                }
            }
        }

        // Show performance trends
        Console.WriteLine("\n=== PERFORMANCE TRENDS ===\n");
        var successTrend = await evaluator.GetPerformanceTrendAsync("success_rate", TimeSpan.FromDays(30));
        if (successTrend.Any())
        {
            Console.WriteLine("Success Rate (last 30 days):");
            foreach (var point in successTrend.TakeLast(7))
            {
                Console.WriteLine($"  {point.Time:yyyy-MM-dd}: {point.Value:P0}");
            }
        }

        Console.WriteLine("\n=== Phase 2 Example Complete ===");
        Console.WriteLine("\nThe agent has demonstrated:");
        Console.WriteLine("✓ Understanding of its own capabilities (self-model)");
        Console.WriteLine("✓ Hierarchical goal decomposition and management");
        Console.WriteLine("✓ Value alignment checking");
        Console.WriteLine("✓ Performance self-evaluation");
        Console.WriteLine("✓ Autonomous improvement planning");
    }

    private static void RegisterInitialCapabilities(CapabilityRegistry registry)
    {
        var capabilities = new List<AgentCapability>
        {
            new AgentCapability(
                "text_generation",
                "Generate natural language text responses",
                new List<string> { "llm" },
                SuccessRate: 0.92,
                AverageLatency: 150.0,
                new List<string> { "May hallucinate facts", "Context window limited" },
                UsageCount: 250,
                DateTime.UtcNow.AddDays(-60),
                DateTime.UtcNow,
                new Dictionary<string, object>()),

            new AgentCapability(
                "data_analysis",
                "Analyze and summarize structured data",
                new List<string> { "python_executor", "data_loader" },
                SuccessRate: 0.78,
                AverageLatency: 300.0,
                new List<string> { "Requires clean data", "Limited to CSV/JSON formats" },
                UsageCount: 85,
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                new Dictionary<string, object>()),

            new AgentCapability(
                "code_generation",
                "Generate code snippets in multiple languages",
                new List<string> { "llm" },
                SuccessRate: 0.68,
                AverageLatency: 200.0,
                new List<string> { "Best for simple patterns", "May miss edge cases" },
                UsageCount: 120,
                DateTime.UtcNow.AddDays(-45),
                DateTime.UtcNow,
                new Dictionary<string, object>()),

            new AgentCapability(
                "information_retrieval",
                "Search and retrieve relevant information",
                new List<string> { "vector_search", "web_search" },
                SuccessRate: 0.85,
                AverageLatency: 250.0,
                new List<string> { "Depends on data quality", "May miss recent updates" },
                UsageCount: 180,
                DateTime.UtcNow.AddDays(-40),
                DateTime.UtcNow,
                new Dictionary<string, object>()),

            new AgentCapability(
                "mathematical_reasoning",
                "Solve mathematical problems and perform calculations",
                new List<string> { "calculator", "symbolic_math" },
                SuccessRate: 0.94,
                AverageLatency: 100.0,
                new List<string> { "Limited to standard math", "No proof generation" },
                UsageCount: 200,
                DateTime.UtcNow.AddDays(-50),
                DateTime.UtcNow,
                new Dictionary<string, object>()),
        };

        foreach (var cap in capabilities)
        {
            registry.RegisterCapability(cap);
        }
    }

    private static async Task TestCapabilityAssessment(CapabilityRegistry registry)
    {
        Console.WriteLine("Testing capability assessment:\n");

        var testTasks = new[]
        {
            "Write a poem about artificial intelligence",
            "Analyze sales data and identify trends",
            "Build a quantum computer",
            "Calculate the derivative of x^2 + 3x + 5",
        };

        foreach (var task in testTasks)
        {
            var canHandle = await registry.CanHandleAsync(task);
            Console.WriteLine($"Task: {task}");
            Console.WriteLine($"  Can Handle: {(canHandle ? "✓ YES" : "✗ NO")}");

            if (!canHandle)
            {
                var alternatives = await registry.SuggestAlternativesAsync(task);
                if (alternatives.Any())
                {
                    Console.WriteLine($"  Alternatives:");
                    foreach (var alt in alternatives.Take(2))
                    {
                        Console.WriteLine($"    • {alt}");
                    }
                }
            }

            Console.WriteLine();
        }
    }

    private static async Task TestGoalConflicts(GoalHierarchy hierarchy)
    {
        Console.WriteLine("\nTesting goal conflict detection:\n");

        // Add potentially conflicting goals
        var speedGoal = new Goal(
            "Minimize response latency to under 100ms",
            GoalType.Secondary,
            0.8);

        var qualityGoal = new Goal(
            "Maximize response accuracy and detail",
            GoalType.Secondary,
            0.85);

        hierarchy.AddGoal(speedGoal);
        hierarchy.AddGoal(qualityGoal);

        var conflicts = await hierarchy.DetectConflictsAsync();

        if (conflicts.Any())
        {
            Console.WriteLine($"Detected {conflicts.Count} goal conflicts:\n");
            foreach (var conflict in conflicts)
            {
                Console.WriteLine($"Conflict: {conflict.ConflictType}");
                Console.WriteLine($"  Between: {conflict.Goal1.Description}");
                Console.WriteLine($"  And: {conflict.Goal2.Description}");
                Console.WriteLine($"  Suggested Resolutions:");
                foreach (var resolution in conflict.SuggestedResolutions.Take(2))
                {
                    Console.WriteLine($"    • {resolution}");
                }

                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("✓ No conflicts detected\n");
        }
    }

    private static async Task SimulateExecutionHistory(IMemoryStore memory, ISelfEvaluator evaluator)
    {
        // Simulate some successful and failed executions
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < 20; i++)
        {
            var success = random.NextDouble() > 0.3; // 70% success rate
            var quality = success ? 0.7 + (random.NextDouble() * 0.3) : random.NextDouble() * 0.5;

            var plan = new Plan(
                $"Simulated task {i + 1}",
                new List<PlanStep>(),
                new Dictionary<string, double>(),
                DateTime.UtcNow.AddDays(-random.Next(1, 30)));

            var exec = new ExecutionResult(
                plan,
                new List<StepResult>(),
                success,
                success ? "Success" : "Failed",
                new Dictionary<string, object>(),
                TimeSpan.FromMilliseconds(random.Next(50, 500)));

            var verify = new VerificationResult(
                exec,
                success,
                quality,
                success ? new List<string>() : new List<string> { "Error occurred" },
                new List<string>(),
                null);

            var experience = new Experience(
                Guid.NewGuid(),
                plan.Goal,
                plan,
                exec,
                verify,
                plan.CreatedAt,
                new Dictionary<string, object>());

            await memory.StoreExperienceAsync(experience);

            // Record for calibration
            var predictedConfidence = random.NextDouble();
            evaluator.RecordPrediction(predictedConfidence, success);
        }

        Console.WriteLine("✓ Simulated 20 execution experiences\n");
    }
}
