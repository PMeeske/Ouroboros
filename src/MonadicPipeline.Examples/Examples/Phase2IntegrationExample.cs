// ==========================================================
// Complete Phase 2 Integration Example
// End-to-end demonstration of metacognitive agent
// ==========================================================

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

namespace LangChainPipeline.Examples;

/// <summary>
/// Complete integration example showing Phase 2 metacognitive agent in action.
/// Demonstrates the full lifecycle: task assessment, goal decomposition, 
/// execution, learning, and self-improvement.
/// </summary>
public static class Phase2IntegrationExample
{
    /// <summary>
    /// Runs a complete task lifecycle with metacognitive monitoring.
    /// </summary>
    public static async Task RunCompleteTaskLifecycle()
    {
        Console.WriteLine("=== Complete Phase 2 Integration Example ===\n");
        Console.WriteLine("Scenario: Building a comprehensive research assistant");
        Console.WriteLine("This example demonstrates the complete metacognitive lifecycle.\n");

        // === Setup ===
        Console.WriteLine("1. SETUP: Initializing Phase 2 components...\n");
        
        var provider = new OllamaProvider();
        var llm = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

        var (orchestrator, capabilities, goals, evaluator) = 
            Phase2OrchestratorBuilder.CreateDefault(llm);

        Console.WriteLine("✓ Orchestrator with Phase 2 capabilities initialized\n");

        // Register initial capabilities based on available tools
        await RegisterInitialCapabilities(capabilities);

        // === Step 1: Task Assessment ===
        Console.WriteLine("\n2. TASK ASSESSMENT: Evaluating agent capabilities...\n");
        
        var task = "Research and summarize recent advances in AI safety";
        Console.WriteLine($"Incoming task: \"{task}\"\n");

        // Check if agent can handle the task
        var canHandle = await capabilities.CanHandleAsync(task);
        Console.WriteLine($"Initial assessment: {(canHandle ? "✓ CAN HANDLE" : "✗ CANNOT HANDLE")}\n");

        if (!canHandle)
        {
            var gaps = await capabilities.IdentifyCapabilityGapsAsync(task);
            Console.WriteLine("Capability gaps identified:");
            foreach (var gap in gaps)
            {
                Console.WriteLine($"  ⚠ {gap}");
            }
            Console.WriteLine();

            var alternatives = await capabilities.SuggestAlternativesAsync(task);
            Console.WriteLine("Alternative approaches:");
            foreach (var alt in alternatives.Take(3))
            {
                Console.WriteLine($"  • {alt}");
            }
            Console.WriteLine();
        }

        // === Step 2: Goal Decomposition ===
        Console.WriteLine("\n3. GOAL DECOMPOSITION: Creating hierarchical plan...\n");

        var mainGoal = new Goal(task, GoalType.Primary, 1.0);

        // Check value alignment before proceeding
        var alignmentCheck = await goals.CheckValueAlignmentAsync(mainGoal);
        if (!alignmentCheck.IsSuccess)
        {
            Console.WriteLine($"✗ Goal rejected due to value misalignment: {alignmentCheck.Error}");
            return;
        }
        Console.WriteLine("✓ Goal passes value alignment check\n");

        // Decompose goal into subgoals
        Console.WriteLine("Decomposing goal into actionable subgoals...\n");
        var decomposedResult = await goals.DecomposeGoalAsync(mainGoal, maxDepth: 2);

        if (decomposedResult.IsSuccess)
        {
            var decomposed = decomposedResult.Value;
            Console.WriteLine($"✓ Created hierarchical plan with {decomposed.Subgoals.Count} subgoals:\n");

            for (int i = 0; i < decomposed.Subgoals.Count; i++)
            {
                var subgoal = decomposed.Subgoals[i];
                Console.WriteLine($"  {i + 1}. {subgoal.Description}");
                Console.WriteLine($"     Priority: {subgoal.Priority:F2}, Type: {subgoal.Type}");
                
                if (subgoal.Subgoals.Any())
                {
                    Console.WriteLine($"     Breakdown:");
                    foreach (var sub in subgoal.Subgoals.Take(2))
                    {
                        Console.WriteLine($"       • {sub.Description}");
                    }
                }
                Console.WriteLine();
            }

            goals.AddGoal(decomposed);
        }

        // Detect any conflicts with existing goals
        var conflicts = await goals.DetectConflictsAsync();
        if (conflicts.Any())
        {
            Console.WriteLine($"⚠ Detected {conflicts.Count} goal conflicts:");
            foreach (var conflict in conflicts.Take(2))
            {
                Console.WriteLine($"  - {conflict.Description}");
                Console.WriteLine($"    Suggested: {conflict.SuggestedResolutions.FirstOrDefault()}");
            }
            Console.WriteLine();
        }

        // Prioritize all active goals
        var prioritized = await goals.PrioritizeGoalsAsync();
        Console.WriteLine("Goal execution order (prioritized):");
        foreach (var goal in prioritized.Take(5))
        {
            var index = prioritized.IndexOf(goal) + 1;
            var desc = goal.Description.Length > 50 
                ? goal.Description.Substring(0, 50) + "..." 
                : goal.Description;
            Console.WriteLine($"  {index}. [{goal.Type}] {desc}");
        }
        Console.WriteLine();

        // === Step 3: Execution & Learning ===
        Console.WriteLine("\n4. EXECUTION & LEARNING: Performing tasks and extracting skills...\n");

        // Simulate execution of first subgoal
        if (decomposedResult.IsSuccess && decomposedResult.Value.Subgoals.Any())
        {
            var firstSubgoal = decomposedResult.Value.Subgoals.First();
            Console.WriteLine($"Executing: {firstSubgoal.Description}\n");

            // Plan and execute
            var planResult = await orchestrator.PlanAsync(firstSubgoal.Description);
            
            if (planResult.IsSuccess)
            {
                var plan = planResult.Value;
                Console.WriteLine($"✓ Created plan with {plan.Steps.Count} steps");
                Console.WriteLine($"  Confidence: {plan.ConfidenceScores.GetValueOrDefault("overall", 0.5):P0}\n");

                var execResult = await orchestrator.ExecuteAsync(plan);
                
                if (execResult.IsSuccess)
                {
                    var execution = execResult.Value;
                    Console.WriteLine($"✓ Execution completed: {(execution.Success ? "SUCCESS" : "FAILED")}");
                    Console.WriteLine($"  Duration: {execution.Duration.TotalSeconds:F2}s");
                    Console.WriteLine($"  Steps completed: {execution.StepResults.Count(r => r.Success)}/{execution.StepResults.Count}\n");

                    // Verify results
                    var verifyResult = await orchestrator.VerifyAsync(execution);
                    
                    if (verifyResult.IsSuccess)
                    {
                        var verification = verifyResult.Value;
                        Console.WriteLine($"✓ Verification: {(verification.Verified ? "PASSED" : "FAILED")}");
                        Console.WriteLine($"  Quality Score: {verification.QualityScore:P0}\n");

                        // Learn from execution
                        orchestrator.LearnFromExecution(verification);
                        Console.WriteLine("✓ Learning completed (experience stored, skill extraction triggered if quality > 80%)\n");

                        // Mark subgoal as complete if successful
                        if (verification.Verified)
                        {
                            goals.CompleteGoal(firstSubgoal.Id, $"Successfully completed with quality {verification.QualityScore:P0}");
                            Console.WriteLine($"✓ Marked subgoal as complete\n");
                        }
                    }
                }
            }
        }

        // === Step 4: Self-Evaluation ===
        Console.WriteLine("\n5. SELF-EVALUATION: Analyzing performance...\n");

        var assessmentResult = await evaluator.EvaluatePerformanceAsync();
        
        if (assessmentResult.IsSuccess)
        {
            var assessment = assessmentResult.Value;
            
            Console.WriteLine("=== PERFORMANCE SELF-ASSESSMENT ===\n");
            Console.WriteLine($"Overall Performance:      {assessment.OverallPerformance:P0}");
            Console.WriteLine($"Confidence Calibration:   {assessment.ConfidenceCalibration:P0}");
            Console.WriteLine($"Skill Acquisition Rate:   {assessment.SkillAcquisitionRate:F2} skills/day\n");

            if (assessment.Strengths.Any())
            {
                Console.WriteLine("Key Strengths:");
                foreach (var strength in assessment.Strengths.Take(3))
                {
                    Console.WriteLine($"  ✓ {strength}");
                }
                Console.WriteLine();
            }

            if (assessment.Weaknesses.Any())
            {
                Console.WriteLine("Areas for Improvement:");
                foreach (var weakness in assessment.Weaknesses.Take(3))
                {
                    Console.WriteLine($"  ⚠ {weakness}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"Assessment Summary:\n  {assessment.Summary}\n");
        }

        // Generate insights
        Console.WriteLine("\n6. INSIGHT GENERATION: Identifying patterns...\n");
        
        var insights = await evaluator.GenerateInsightsAsync();
        
        if (insights.Any())
        {
            Console.WriteLine($"Generated {insights.Count} insights:\n");
            foreach (var insight in insights.Take(3))
            {
                Console.WriteLine($"[{insight.Category}] Confidence: {insight.Confidence:P0}");
                Console.WriteLine($"  {insight.Description}");
                if (insight.SupportingEvidence.Any())
                {
                    Console.WriteLine($"  Evidence: {string.Join(", ", insight.SupportingEvidence.Take(2))}");
                }
                Console.WriteLine();
            }
        }

        // === Step 5: Improvement Planning ===
        Console.WriteLine("\n7. IMPROVEMENT PLANNING: Creating action plan...\n");

        var improvementResult = await evaluator.SuggestImprovementsAsync();
        
        if (improvementResult.IsSuccess)
        {
            var plan = improvementResult.Value;
            
            Console.WriteLine("=== SELF-IMPROVEMENT PLAN ===\n");
            Console.WriteLine($"Goal: {plan.Goal}");
            Console.WriteLine($"Priority: {plan.Priority:F2}");
            Console.WriteLine($"Duration: {plan.EstimatedDuration.TotalDays:F0} days\n");

            Console.WriteLine("Proposed Actions:");
            for (int i = 0; i < plan.Actions.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {plan.Actions[i]}");
            }
            Console.WriteLine();

            if (plan.ExpectedImprovements.Any())
            {
                Console.WriteLine("Expected Outcomes:");
                foreach (var improvement in plan.ExpectedImprovements)
                {
                    Console.WriteLine($"  • {improvement.Key}: +{improvement.Value:P0}");
                }
                Console.WriteLine();
            }
        }

        // === Summary ===
        Console.WriteLine("\n8. SUMMARY: Phase 2 Metacognitive Capabilities Demonstrated\n");
        
        var allCapabilities = await capabilities.GetCapabilitiesAsync();
        var activeGoals = goals.GetActiveGoals();
        var metrics = orchestrator.GetMetrics();

        Console.WriteLine("Current Agent State:");
        Console.WriteLine($"  • Registered Capabilities: {allCapabilities.Count}");
        Console.WriteLine($"  • Active Goals: {activeGoals.Count}");
        Console.WriteLine($"  • Performance Metrics Tracked: {metrics.Count}");
        Console.WriteLine($"  • Calibration Score: {await evaluator.GetConfidenceCalibrationAsync():P0}");
        Console.WriteLine();

        Console.WriteLine("Phase 2 Features Demonstrated:");
        Console.WriteLine("  ✓ Self-Model (Capability Registry)");
        Console.WriteLine("  ✓ Goal Hierarchy & Decomposition");
        Console.WriteLine("  ✓ Value Alignment Checking");
        Console.WriteLine("  ✓ Conflict Detection");
        Console.WriteLine("  ✓ Performance Self-Evaluation");
        Console.WriteLine("  ✓ Insight Generation");
        Console.WriteLine("  ✓ Autonomous Improvement Planning");
        Console.WriteLine();

        Console.WriteLine("=== Phase 2 Integration Example Complete ===");
    }

    private static async Task RegisterInitialCapabilities(ICapabilityRegistry registry)
    {
        var capabilities = new[]
        {
            new AgentCapability(
                "information_search",
                "Search and retrieve information from various sources",
                new List<string> { "web_search", "vector_search" },
                SuccessRate: 0.82,
                AverageLatency: 200.0,
                new List<string> { "Limited to indexed content", "May miss recent updates" },
                UsageCount: 150,
                DateTime.UtcNow.AddDays(-45),
                DateTime.UtcNow,
                new Dictionary<string, object>()),

            new AgentCapability(
                "text_summarization",
                "Summarize long-form text into concise summaries",
                new List<string> { "llm" },
                SuccessRate: 0.88,
                AverageLatency: 150.0,
                new List<string> { "May lose nuance", "Limited to text length" },
                UsageCount: 200,
                DateTime.UtcNow.AddDays(-50),
                DateTime.UtcNow,
                new Dictionary<string, object>()),

            new AgentCapability(
                "knowledge_synthesis",
                "Synthesize information from multiple sources",
                new List<string> { "llm", "reasoning" },
                SuccessRate: 0.75,
                AverageLatency: 300.0,
                new List<string> { "Requires high-quality sources", "May introduce bias" },
                UsageCount: 80,
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                new Dictionary<string, object>())
        };

        foreach (var cap in capabilities)
        {
            registry.RegisterCapability(cap);
        }

        await Task.CompletedTask;
    }
}
