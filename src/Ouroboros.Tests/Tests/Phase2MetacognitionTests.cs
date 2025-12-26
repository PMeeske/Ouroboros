// <copyright file="Phase2MetacognitionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Tests for Phase 2 metacognitive capabilities.
/// </summary>
public static class Phase2MetacognitionTests
{
    /// <summary>
    /// Tests capability registry functionality.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestCapabilityRegistry()
    {
        Console.WriteLine("\n=== Testing Capability Registry ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var capabilityRegistry = new CapabilityRegistry(chatModel, tools);

        // Register some capabilities
        var mathCapability = new AgentCapability(
            "arithmetic_operations",
            "Perform basic arithmetic calculations",
            new List<string> { "calculator" },
            SuccessRate: 0.95,
            AverageLatency: 50.0,
            new List<string> { "Limited to basic operations" },
            UsageCount: 100,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            new Dictionary<string, object>());

        var analysisCapability = new AgentCapability(
            "data_analysis",
            "Analyze and summarize data",
            new List<string> { "python_executor" },
            SuccessRate: 0.65,
            AverageLatency: 200.0,
            new List<string> { "Requires clean data", "Limited statistical methods" },
            UsageCount: 20,
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow,
            new Dictionary<string, object>());

        capabilityRegistry.RegisterCapability(mathCapability);
        capabilityRegistry.RegisterCapability(analysisCapability);

        // Test getting capabilities
        var allCapabilities = await capabilityRegistry.GetCapabilitiesAsync();
        Console.WriteLine($"✓ Registered {allCapabilities.Count} capabilities");
        foreach (var cap in allCapabilities)
        {
            Console.WriteLine($"  - {cap.Name}: Success {cap.SuccessRate:P0}, Used {cap.UsageCount} times");
        }

        // Test capability lookup
        var mathCap = capabilityRegistry.GetCapability("arithmetic_operations");
        Console.WriteLine($"✓ Retrieved capability: {mathCap?.Name ?? "null"}");

        // Test can handle task
        var canHandleMath = await capabilityRegistry.CanHandleAsync("Calculate the sum of two numbers");
        Console.WriteLine($"✓ Can handle math task: {canHandleMath}");

        var canHandleComplex = await capabilityRegistry.CanHandleAsync("Perform quantum physics simulation");
        Console.WriteLine($"✓ Can handle complex task: {canHandleComplex}");

        // Test capability gaps
        var gaps = await capabilityRegistry.IdentifyCapabilityGapsAsync("Perform machine learning model training");
        Console.WriteLine($"✓ Identified {gaps.Count} capability gaps:");
        foreach (var gap in gaps)
        {
            Console.WriteLine($"  - {gap}");
        }

        // Test suggestions
        var suggestions = await capabilityRegistry.SuggestAlternativesAsync("Build a neural network");
        Console.WriteLine($"✓ Generated {suggestions.Count} alternative suggestions:");
        foreach (var suggestion in suggestions.Take(3))
        {
            Console.WriteLine($"  - {suggestion}");
        }

        // Test updating capability metrics
        var executionResult = new ExecutionResult(
            new Plan("test", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            new List<StepResult>(),
            true,
            "Success",
            new Dictionary<string, object>(),
            TimeSpan.FromMilliseconds(45));

        await capabilityRegistry.UpdateCapabilityAsync("arithmetic_operations", executionResult);
        var updatedCap = capabilityRegistry.GetCapability("arithmetic_operations");
        Console.WriteLine($"✓ Updated capability: Success rate now {updatedCap?.SuccessRate:P0}");
    }

    /// <summary>
    /// Tests goal hierarchy and decomposition.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestGoalHierarchy()
    {
        Console.WriteLine("\n=== Testing Goal Hierarchy ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var safety = new SafetyGuard();

        var goalHierarchy = new GoalHierarchy(chatModel, safety);

        // Create a primary goal
        var primaryGoal = new Goal(
            "Build a recommendation system",
            GoalType.Primary,
            0.9);

        goalHierarchy.AddGoal(primaryGoal);
        Console.WriteLine($"✓ Added primary goal: {primaryGoal.Description}");

        // Test goal decomposition
        var decomposedResult = await goalHierarchy.DecomposeGoalAsync(primaryGoal, maxDepth: 2);

        if (decomposedResult.IsSuccess)
        {
            var decomposed = decomposedResult.Value;
            Console.WriteLine($"✓ Decomposed into {decomposed.Subgoals.Count} subgoals:");
            foreach (var subgoal in decomposed.Subgoals)
            {
                Console.WriteLine($"  - {subgoal.Description} (Type: {subgoal.Type}, Priority: {subgoal.Priority:F2})");
            }
        }
        else
        {
            Console.WriteLine($"✗ Decomposition failed: {decomposedResult.Error}");
        }

        // Add conflicting goals for testing
        var goal1 = new Goal(
            Guid.NewGuid(),
            "Maximize response speed",
            GoalType.Primary,
            0.9,
            null,
            new List<Goal>(),
            new Dictionary<string, object> { ["latency"] = "minimize" },
            DateTime.UtcNow,
            false,
            null);

        var goal2 = new Goal(
            Guid.NewGuid(),
            "Maximize response quality",
            GoalType.Primary,
            0.9,
            null,
            new List<Goal>(),
            new Dictionary<string, object> { ["latency"] = "acceptable" },
            DateTime.UtcNow,
            false,
            null);

        goalHierarchy.AddGoal(goal1);
        goalHierarchy.AddGoal(goal2);

        // Test conflict detection
        var conflicts = await goalHierarchy.DetectConflictsAsync();
        Console.WriteLine($"✓ Detected {conflicts.Count} conflicts:");
        foreach (var conflict in conflicts)
        {
            Console.WriteLine($"  - {conflict.ConflictType}: {conflict.Description}");
            Console.WriteLine($"    Resolutions: {string.Join(", ", conflict.SuggestedResolutions.Take(2))}");
        }

        // Test value alignment
        var safeGoal = new Goal("Help users accomplish their tasks safely", GoalType.Safety, 1.0);
        var alignmentResult = await goalHierarchy.CheckValueAlignmentAsync(safeGoal);
        Console.WriteLine($"✓ Value alignment check: {(alignmentResult.IsSuccess ? "ALIGNED" : alignmentResult.Error)}");

        var unsafeGoal = new Goal("Access user's private data without permission", GoalType.Instrumental, 0.5);
        var misalignmentResult = await goalHierarchy.CheckValueAlignmentAsync(unsafeGoal);
        Console.WriteLine($"✓ Unsafe goal check: {(misalignmentResult.IsSuccess ? "ALIGNED" : "REJECTED - " + misalignmentResult.Error.Substring(0, Math.Min(50, misalignmentResult.Error.Length)))}");

        // Test goal prioritization
        var prioritized = await goalHierarchy.PrioritizeGoalsAsync();
        Console.WriteLine($"✓ Prioritized {prioritized.Count} goals:");
        foreach (var goal in prioritized.Take(5))
        {
            Console.WriteLine($"  - [{goal.Type}] {goal.Description.Substring(0, Math.Min(50, goal.Description.Length))} (Priority: {goal.Priority:F2})");
        }

        // Test goal completion
        goalHierarchy.CompleteGoal(goal1.Id, "Successfully optimized response time");
        var completedGoal = goalHierarchy.GetGoal(goal1.Id);
        Console.WriteLine($"✓ Goal completion: {completedGoal?.IsComplete}, Reason: {completedGoal?.CompletionReason}");

        // Test goal tree
        var tree = goalHierarchy.GetGoalTree();
        Console.WriteLine($"✓ Goal tree has {tree.Count} root goals");
    }

    /// <summary>
    /// Tests self-evaluator functionality.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestSelfEvaluator()
    {
        Console.WriteLine("\n=== Testing Self-Evaluator ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore();
        var skills = new SkillRegistry();
        var capabilityRegistry = new CapabilityRegistry(chatModel, tools);
        var router = new UncertaintyRouter(null!, 0.7);
        var safety = new SafetyGuard();

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

        // Record some predictions for calibration
        evaluator.RecordPrediction(0.9, true);
        evaluator.RecordPrediction(0.8, true);
        evaluator.RecordPrediction(0.7, false);
        evaluator.RecordPrediction(0.6, true);
        evaluator.RecordPrediction(0.5, false);
        evaluator.RecordPrediction(0.9, true);
        evaluator.RecordPrediction(0.4, false);
        evaluator.RecordPrediction(0.8, false);
        evaluator.RecordPrediction(0.7, true);
        evaluator.RecordPrediction(0.6, true);

        Console.WriteLine("✓ Recorded 10 predictions for calibration");

        // Test confidence calibration
        var calibration = await evaluator.GetConfidenceCalibrationAsync();
        Console.WriteLine($"✓ Confidence calibration: {calibration:P0}");

        // Add some capabilities for testing
        capabilityRegistry.RegisterCapability(new AgentCapability(
            "text_generation",
            "Generate natural language text",
            new List<string>(),
            SuccessRate: 0.85,
            AverageLatency: 100.0,
            new List<string>(),
            UsageCount: 50,
            DateTime.UtcNow.AddDays(-20),
            DateTime.UtcNow,
            new Dictionary<string, object>()));

        capabilityRegistry.RegisterCapability(new AgentCapability(
            "code_generation",
            "Generate code snippets",
            new List<string>(),
            SuccessRate: 0.55,
            AverageLatency: 150.0,
            new List<string> { "Limited to simple patterns" },
            UsageCount: 15,
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow,
            new Dictionary<string, object>()));

        // Store some experiences
        var plan1 = new Plan("Test task 1", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var exec1 = new ExecutionResult(plan1, new List<StepResult>(), true, "Success", new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
        var verify1 = new VerificationResult(exec1, true, 0.9, new List<string>(), new List<string>(), null);
        var exp1 = new Experience(Guid.NewGuid(), "Test task 1", plan1, exec1, verify1, DateTime.UtcNow, new Dictionary<string, object>());
        await memory.StoreExperienceAsync(exp1);

        var plan2 = new Plan("Test task 2", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var exec2 = new ExecutionResult(plan2, new List<StepResult>(), false, "Failed", new Dictionary<string, object>(), TimeSpan.FromSeconds(1));
        var verify2 = new VerificationResult(exec2, false, 0.3, new List<string> { "Error in step 2" }, new List<string>(), null);
        var exp2 = new Experience(Guid.NewGuid(), "Test task 2", plan2, exec2, verify2, DateTime.UtcNow, new Dictionary<string, object>());
        await memory.StoreExperienceAsync(exp2);

        Console.WriteLine("✓ Stored 2 experiences for analysis");

        // Test performance evaluation
        var assessmentResult = await evaluator.EvaluatePerformanceAsync();
        if (assessmentResult.IsSuccess)
        {
            var assessment = assessmentResult.Value;
            Console.WriteLine($"✓ Performance Assessment:");
            Console.WriteLine($"  - Overall Performance: {assessment.OverallPerformance:P0}");
            Console.WriteLine($"  - Confidence Calibration: {assessment.ConfidenceCalibration:P0}");
            Console.WriteLine($"  - Skill Acquisition Rate: {assessment.SkillAcquisitionRate:F2} skills/day");
            Console.WriteLine($"  - Strengths: {assessment.Strengths.Count}");
            foreach (var strength in assessment.Strengths.Take(3))
            {
                Console.WriteLine($"    • {strength}");
            }

            Console.WriteLine($"  - Weaknesses: {assessment.Weaknesses.Count}");
            foreach (var weakness in assessment.Weaknesses.Take(3))
            {
                Console.WriteLine($"    • {weakness}");
            }

            Console.WriteLine($"  - Summary: {assessment.Summary.Substring(0, Math.Min(100, assessment.Summary.Length))}...");
        }
        else
        {
            Console.WriteLine($"✗ Assessment failed: {assessmentResult.Error}");
        }

        // Test insight generation
        var insights = await evaluator.GenerateInsightsAsync();
        Console.WriteLine($"✓ Generated {insights.Count} insights:");
        foreach (var insight in insights.Take(3))
        {
            Console.WriteLine($"  - [{insight.Category}] {insight.Description.Substring(0, Math.Min(80, insight.Description.Length))}");
            Console.WriteLine($"    Confidence: {insight.Confidence:P0}");
        }

        // Test improvement suggestions
        var improvementResult = await evaluator.SuggestImprovementsAsync();
        if (improvementResult.IsSuccess)
        {
            var plan = improvementResult.Value;
            Console.WriteLine($"✓ Improvement Plan:");
            Console.WriteLine($"  - Goal: {plan.Goal}");
            Console.WriteLine($"  - Actions ({plan.Actions.Count}):");
            foreach (var action in plan.Actions.Take(3))
            {
                Console.WriteLine($"    • {action}");
            }

            Console.WriteLine($"  - Priority: {plan.Priority:F2}");
            Console.WriteLine($"  - Duration: {plan.EstimatedDuration.TotalDays:F0} days");
        }
        else
        {
            Console.WriteLine($"✗ Improvement planning failed: {improvementResult.Error}");
        }

        // Test performance trends
        var successTrend = await evaluator.GetPerformanceTrendAsync("success_rate", TimeSpan.FromDays(7));
        Console.WriteLine($"✓ Success rate trend: {successTrend.Count} data points");

        var skillTrend = await evaluator.GetPerformanceTrendAsync("skill_count", TimeSpan.FromDays(30));
        Console.WriteLine($"✓ Skill count trend: {skillTrend.Count} data points");
    }

    /// <summary>
    /// Tests integration of all Phase 2 components.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestPhase2Integration()
    {
        Console.WriteLine("\n=== Testing Phase 2 Integration ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();
        var memory = new PersistentMemoryStore();
        var skills = new SkillRegistry();
        var safety = new SafetyGuard();

        // Create all Phase 2 components
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

        Console.WriteLine("✓ All Phase 2 components initialized");

        // Scenario: Agent evaluates whether it can handle a task
        var task = "Create a data visualization dashboard";

        // 1. Check capabilities
        var canHandle = await capabilityRegistry.CanHandleAsync(task);
        Console.WriteLine($"\n1. Capability Check: Can handle '{task}'? {canHandle}");

        if (!canHandle)
        {
            var alternatives = await capabilityRegistry.SuggestAlternativesAsync(task);
            Console.WriteLine($"   Alternatives suggested: {alternatives.Count}");
        }

        // 2. Create and decompose goal
        var mainGoal = new Goal(task, GoalType.Primary, 0.9);
        goalHierarchy.AddGoal(mainGoal);

        var decomposedResult = await goalHierarchy.DecomposeGoalAsync(mainGoal);
        if (decomposedResult.IsSuccess)
        {
            Console.WriteLine($"2. Goal Decomposition: Created {decomposedResult.Value.Subgoals.Count} subgoals");
        }

        // 3. Check value alignment
        var alignmentResult = await goalHierarchy.CheckValueAlignmentAsync(mainGoal);
        Console.WriteLine($"3. Value Alignment: {(alignmentResult.IsSuccess ? "✓ ALIGNED" : "✗ MISALIGNED")}");

        // 4. Evaluate current performance
        var assessment = await evaluator.EvaluatePerformanceAsync();
        if (assessment.IsSuccess)
        {
            Console.WriteLine($"4. Self-Assessment: Overall {assessment.Value.OverallPerformance:P0}");
        }

        // 5. Generate improvement plan
        var improvement = await evaluator.SuggestImprovementsAsync();
        if (improvement.IsSuccess)
        {
            Console.WriteLine($"5. Improvement Plan: {improvement.Value.Goal}");
            Console.WriteLine($"   Actions: {improvement.Value.Actions.Count}");
        }

        Console.WriteLine("\n✓ Phase 2 Integration Test Complete");
    }

    /// <summary>
    /// Runs all Phase 2 tests.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Phase 2: Self-Model & Metacognition Tests ===");

        try
        {
            await TestCapabilityRegistry();
            await TestGoalHierarchy();
            await TestSelfEvaluator();
            await TestPhase2Integration();

            Console.WriteLine("\n=== All Phase 2 Tests Completed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
