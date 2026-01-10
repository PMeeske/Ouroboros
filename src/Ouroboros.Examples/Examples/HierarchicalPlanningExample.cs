// <copyright file="HierarchicalPlanningExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Monads;

/// <summary>
/// Demonstrates Hierarchical Planning System (F1.4) with HTN decomposition,
/// temporal constraint satisfaction, plan repair, and explanation capabilities.
/// </summary>
public static class HierarchicalPlanningExample
{
    /// <summary>
    /// Demonstrates HTN (Hierarchical Task Network) planning with task decomposition.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunHTNPlanningExample()
    {
        Console.WriteLine("=== HTN Planning Example ===\n");

        // Setup LLM and tools
        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        // Build Meta-AI orchestrator
        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithEmbedding(embedModel)
            .Build();

        var planner = new HierarchicalPlanner(orchestrator, chatModel);

        // Define HTN task network for building a software project
        var taskNetwork = new Dictionary<string, TaskDecomposition>
        {
            ["BuildSoftwareProject"] = new TaskDecomposition(
                "BuildSoftwareProject",
                new List<string> { "Design", "Implementation", "Testing" },
                new List<string> { "Design->Implementation", "Implementation->Testing" }),
            
            ["Design"] = new TaskDecomposition(
                "Design",
                new List<string> { "CreateArchitecture", "DesignAPI", "DocumentDesign" },
                new List<string> { "CreateArchitecture->DesignAPI", "DesignAPI->DocumentDesign" }),
            
            ["Implementation"] = new TaskDecomposition(
                "Implementation",
                new List<string> { "WriteCode", "CodeReview", "Refactor" },
                new List<string> { "WriteCode->CodeReview", "CodeReview->Refactor" }),
            
            ["Testing"] = new TaskDecomposition(
                "Testing",
                new List<string> { "UnitTests", "IntegrationTests", "UserAcceptanceTesting" },
                new List<string> { "UnitTests->IntegrationTests", "IntegrationTests->UserAcceptanceTesting" })
        };

        Console.WriteLine("GOAL: Build a complete software project\n");
        Console.WriteLine("=== HTN DECOMPOSITION ===");

        Result<HtnHierarchicalPlan, string> htnResult = await planner.PlanHierarchicalAsync(
            "BuildSoftwareProject",
            taskNetwork);

        htnResult.Match(
            plan =>
            {
                Console.WriteLine($"✓ HTN Plan created with {plan.AbstractTasks.Count} abstract tasks:");
                foreach (var task in plan.AbstractTasks)
                {
                    Console.WriteLine($"\n  Abstract Task: {task.Name}");
                    Console.WriteLine($"    Decompositions: {task.PossibleDecompositions.Count}");
                    foreach (var decomp in task.PossibleDecompositions)
                    {
                        Console.WriteLine($"      → SubTasks: {string.Join(", ", decomp.SubTasks)}");
                    }
                }

                Console.WriteLine($"\n✓ Generated {plan.Refinements.Count} concrete refinements:");
                foreach (var refinement in plan.Refinements)
                {
                    Console.WriteLine($"\n  Refinement for: {refinement.AbstractTaskName}");
                    Console.WriteLine($"    Steps: {string.Join(" → ", refinement.ConcreteSteps)}");
                }
            },
            error =>
            {
                Console.WriteLine($"✗ HTN Planning failed: {error}");
            });

        Console.WriteLine("\n" + new string('=', 60) + "\n");
    }

    /// <summary>
    /// Demonstrates temporal planning with constraint satisfaction.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunTemporalPlanningExample()
    {
        Console.WriteLine("=== Temporal Planning Example ===\n");

        // Setup components
        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithEmbedding(embedModel)
            .Build();

        var planner = new HierarchicalPlanner(orchestrator, chatModel);

        // Define temporal constraints for a research project
        var constraints = new List<TemporalConstraint>
        {
            // Literature review must happen before data collection
            new TemporalConstraint("LiteratureReview", "DataCollection", TemporalRelation.Before, TimeSpan.FromDays(7)),
            
            // Data collection must complete before analysis
            new TemporalConstraint("DataCollection", "DataAnalysis", TemporalRelation.MustFinishBefore, TimeSpan.FromDays(14)),
            
            // Analysis and writing can overlap
            new TemporalConstraint("DataAnalysis", "WritePaper", TemporalRelation.Overlaps),
            
            // Peer review happens after writing
            new TemporalConstraint("WritePaper", "PeerReview", TemporalRelation.Before, TimeSpan.FromDays(21))
        };

        Console.WriteLine("GOAL: Complete a research project with temporal constraints\n");
        Console.WriteLine("=== TEMPORAL PLANNING ===");

        Result<TemporalPlan, string> temporalResult = await planner.PlanWithConstraintsAsync(
            "Complete research project with optimal scheduling",
            constraints);

        temporalResult.Match(
            plan =>
            {
                Console.WriteLine($"✓ Temporal Plan created with {plan.Tasks.Count} scheduled tasks:");
                Console.WriteLine($"  Total Duration: {plan.TotalDuration.TotalDays:F1} days\n");

                foreach (var task in plan.Tasks.OrderBy(t => t.StartTime))
                {
                    Console.WriteLine($"  Task: {task.Name}");
                    Console.WriteLine($"    Start: {task.StartTime:yyyy-MM-dd HH:mm}");
                    Console.WriteLine($"    End:   {task.EndTime:yyyy-MM-dd HH:mm}");
                    Console.WriteLine($"    Duration: {(task.EndTime - task.StartTime).TotalDays:F1} days");
                    if (task.Dependencies.Any())
                    {
                        Console.WriteLine($"    Depends on: {string.Join(", ", task.Dependencies)}");
                    }
                    Console.WriteLine();
                }
            },
            error =>
            {
                Console.WriteLine($"✗ Temporal Planning failed: {error}");
            });

        Console.WriteLine(new string('=', 60) + "\n");
    }

    /// <summary>
    /// Demonstrates plan repair with different repair strategies.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunPlanRepairExample()
    {
        Console.WriteLine("=== Plan Repair Example ===\n");

        // Setup components
        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithEmbedding(embedModel)
            .Build();

        var planner = new HierarchicalPlanner(orchestrator, chatModel);

        // Create a broken plan
        var brokenPlan = new Plan(
            "Deploy web application",
            new List<PlanStep>
            {
                new PlanStep("BuildApplication", new Dictionary<string, object>(), "Compiled binary", 0.9),
                new PlanStep("RunTests", new Dictionary<string, object>(), "All tests pass", 0.8),
                new PlanStep("DeployToStaging", new Dictionary<string, object>(), "Deployed to staging", 0.7),
                new PlanStep("RunSmokeTests", new Dictionary<string, object>(), "Smoke tests pass", 0.8),
                new PlanStep("DeployToProduction", new Dictionary<string, object>(), "Deployed to production", 0.9)
            },
            new Dictionary<string, double> { ["overall"] = 0.85 },
            DateTime.UtcNow);

        // Simulate execution trace with failure at staging deployment
        var executionTrace = new ExecutionTrace(
            new List<ExecutedStep>
            {
                new ExecutedStep("BuildApplication", true, TimeSpan.FromMinutes(5), new Dictionary<string, object>()),
                new ExecutedStep("RunTests", true, TimeSpan.FromMinutes(3), new Dictionary<string, object>()),
                new ExecutedStep("DeployToStaging", false, TimeSpan.FromMinutes(2), new Dictionary<string, object>())
            },
            FailedAtIndex: 2,
            FailureReason: "Staging server connection timeout");

        Console.WriteLine("ORIGINAL PLAN: Deploy web application");
        Console.WriteLine($"  Total Steps: {brokenPlan.Steps.Count}");
        Console.WriteLine($"  Failed at: Step {executionTrace.FailedAtIndex + 1} ({brokenPlan.Steps[executionTrace.FailedAtIndex].Action})");
        Console.WriteLine($"  Failure Reason: {executionTrace.FailureReason}\n");

        // Try different repair strategies
        var strategies = new[]
        {
            RepairStrategy.Patch,
            RepairStrategy.Replan,
            RepairStrategy.CaseBased,
            RepairStrategy.Backtrack
        };

        foreach (var strategy in strategies)
        {
            Console.WriteLine($"=== REPAIR STRATEGY: {strategy} ===");

            Result<Plan, string> repairResult = await planner.RepairPlanAsync(
                brokenPlan,
                executionTrace,
                strategy);

            repairResult.Match(
                repairedPlan =>
                {
                    Console.WriteLine($"✓ Plan repaired using {strategy} strategy:");
                    Console.WriteLine($"  New Steps: {repairedPlan.Steps.Count}");
                    for (int i = 0; i < repairedPlan.Steps.Count; i++)
                    {
                        Console.WriteLine($"    {i + 1}. {repairedPlan.Steps[i].Action}");
                    }
                },
                error =>
                {
                    Console.WriteLine($"✗ Repair failed: {error}");
                });

            Console.WriteLine();
        }

        Console.WriteLine(new string('=', 60) + "\n");
    }

    /// <summary>
    /// Demonstrates plan explanation at different levels of detail.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunPlanExplanationExample()
    {
        Console.WriteLine("=== Plan Explanation Example ===\n");

        // Setup components
        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithEmbedding(embedModel)
            .Build();

        var planner = new HierarchicalPlanner(orchestrator, chatModel);

        // Create a sample plan
        var plan = new Plan(
            "Create a machine learning model",
            new List<PlanStep>
            {
                new PlanStep("CollectData", new Dictionary<string, object> { ["source"] = "database" }, "Dataset ready", 0.9),
                new PlanStep("PreprocessData", new Dictionary<string, object> { ["normalize"] = true }, "Clean data", 0.85),
                new PlanStep("TrainModel", new Dictionary<string, object> { ["algorithm"] = "RandomForest" }, "Trained model", 0.8),
                new PlanStep("ValidateModel", new Dictionary<string, object> { ["metric"] = "accuracy" }, "Validation complete", 0.85),
                new PlanStep("DeployModel", new Dictionary<string, object> { ["platform"] = "cloud" }, "Model deployed", 0.9)
            },
            new Dictionary<string, double> { ["overall"] = 0.86 },
            DateTime.UtcNow);

        Console.WriteLine("PLAN: Create a machine learning model\n");

        // Generate explanations at different levels
        var levels = new[]
        {
            ExplanationLevel.Brief,
            ExplanationLevel.Detailed,
            ExplanationLevel.Causal,
            ExplanationLevel.Counterfactual
        };

        foreach (var level in levels)
        {
            Console.WriteLine($"=== EXPLANATION LEVEL: {level} ===");

            Result<string, string> explanationResult = await planner.ExplainPlanAsync(plan, level);

            explanationResult.Match(
                explanation =>
                {
                    Console.WriteLine(explanation);
                },
                error =>
                {
                    Console.WriteLine($"✗ Explanation failed: {error}");
                });

            Console.WriteLine(new string('-', 60) + "\n");
        }

        Console.WriteLine(new string('=', 60) + "\n");
    }

    /// <summary>
    /// Runs all hierarchical planning examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("  HIERARCHICAL PLANNING SYSTEM (F1.4) - COMPREHENSIVE EXAMPLES");
        Console.WriteLine(new string('=', 80) + "\n");

        try
        {
            await RunHTNPlanningExample();
            await RunTemporalPlanningExample();
            await RunPlanRepairExample();
            await RunPlanExplanationExample();

            Console.WriteLine("\n✓ All hierarchical planning examples completed successfully!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Example execution failed: {ex.Message}\n");
            Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
        }
    }
}
