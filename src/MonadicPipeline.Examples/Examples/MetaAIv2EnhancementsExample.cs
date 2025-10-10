// ==========================================================
// Meta-AI v2 Enhancements Example
// Demonstrates all new capabilities: parallel execution, hierarchical planning,
// experience replay, skill composition, distributed orchestration, 
// adaptive planning, cost-aware routing, and human-in-the-loop
// ==========================================================

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;

namespace LangChainPipeline.Examples;

/// <summary>
/// Comprehensive example demonstrating Meta-AI v2 enhancements.
/// </summary>
public static class MetaAIv2EnhancementsExample
{
    /// <summary>
    /// Demonstrates parallel execution of independent steps.
    /// </summary>
    public static async Task DemonstrateParallelExecution()
    {
        Console.WriteLine("=== Parallel Execution Example ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        // Create a plan with independent steps
        var planResult = await orchestrator.PlanAsync(
            "Analyze three different datasets concurrently",
            new Dictionary<string, object>
            {
                ["datasets"] = new[] { "sales.csv", "inventory.csv", "customers.csv" }
            });

        if (planResult.IsSuccess)
        {
            var plan = planResult.Value;
            Console.WriteLine($"Plan created with {plan.Steps.Count} steps");

            // Execute with automatic parallel detection
            var execResult = await orchestrator.ExecuteAsync(plan);

            if (execResult.IsSuccess)
            {
                var execution = execResult.Value;
                var isParallel = execution.Metadata.GetValueOrDefault("parallel_execution", false);
                var speedup = execution.Metadata.GetValueOrDefault("estimated_speedup", 1.0);

                Console.WriteLine($"Parallel execution: {isParallel}");
                Console.WriteLine($"Estimated speedup: {speedup:F2}x");
                Console.WriteLine($"Duration: {execution.Duration.TotalMilliseconds:F0}ms");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates hierarchical planning for complex tasks.
    /// </summary>
    public static async Task DemonstrateHierarchicalPlanning()
    {
        Console.WriteLine("=== Hierarchical Planning Example ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        var hierarchicalPlanner = new HierarchicalPlanner(orchestrator, chatModel);

        var config = new HierarchicalPlanningConfig(
            MaxDepth: 3,
            MinStepsForDecomposition: 3,
            ComplexityThreshold: 0.6);

        var result = await hierarchicalPlanner.CreateHierarchicalPlanAsync(
            "Design and implement a complete REST API with database",
            null,
            config);

        if (result.IsSuccess)
        {
            var plan = result.Value;
            Console.WriteLine($"Hierarchical plan created:");
            Console.WriteLine($"  Top-level steps: {plan.TopLevelPlan.Steps.Count}");
            Console.WriteLine($"  Sub-plans: {plan.SubPlans.Count}");
            Console.WriteLine($"  Max depth: {plan.MaxDepth}");

            foreach (var (stepName, subPlan) in plan.SubPlans.Take(3))
            {
                Console.WriteLine($"  Sub-plan '{stepName}': {subPlan.Steps.Count} steps");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates experience replay for continual learning.
    /// </summary>
    public static async Task DemonstrateExperienceReplay()
    {
        Console.WriteLine("=== Experience Replay Example ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var memory = new MemoryStore();
        var skills = new SkillRegistry();

        var replay = new ExperienceReplay(memory, skills, chatModel);

        // Simulate storing some experiences
        Console.WriteLine("Storing experiences...");
        for (int i = 0; i < 5; i++)
        {
            var experience = CreateSampleExperience($"Task {i + 1}", 0.75 + (i * 0.04));
            await memory.StoreExperienceAsync(experience);
        }

        var config = new ExperienceReplayConfig(
            BatchSize: 3,
            MinQualityScore: 0.75,
            PrioritizeHighQuality: true);

        var result = await replay.TrainOnExperiencesAsync(config);

        if (result.IsSuccess)
        {
            var training = result.Value;
            Console.WriteLine($"Training completed:");
            Console.WriteLine($"  Experiences processed: {training.ExperiencesProcessed}");
            Console.WriteLine($"  Patterns discovered: {training.ImprovedMetrics.GetValueOrDefault("patterns_discovered", 0)}");
            Console.WriteLine($"  Skills extracted: {training.ImprovedMetrics.GetValueOrDefault("skills_extracted", 0)}");
            Console.WriteLine($"  Average quality: {training.ImprovedMetrics.GetValueOrDefault("avg_quality", 0):P0}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates skill composition.
    /// </summary>
    public static async Task DemonstrateSkillComposition()
    {
        Console.WriteLine("=== Skill Composition Example ===\n");

        var skills = new SkillRegistry();
        var memory = new MemoryStore();
        var composer = new SkillComposer(skills, memory);

        // Register base skills
        var extractSkill = new Skill(
            "extract_data",
            "Extract data from source",
            new List<string>(),
            new List<PlanStep> { CreateSampleStep("extract") },
            0.9,
            10,
            DateTime.UtcNow,
            DateTime.UtcNow);

        var transformSkill = new Skill(
            "transform_data",
            "Transform and clean data",
            new List<string>(),
            new List<PlanStep> { CreateSampleStep("transform") },
            0.85,
            8,
            DateTime.UtcNow,
            DateTime.UtcNow);

        var loadSkill = new Skill(
            "load_data",
            "Load data to destination",
            new List<string>(),
            new List<PlanStep> { CreateSampleStep("load") },
            0.88,
            12,
            DateTime.UtcNow,
            DateTime.UtcNow);

        skills.RegisterSkill(extractSkill);
        skills.RegisterSkill(transformSkill);
        skills.RegisterSkill(loadSkill);

        // Compose into ETL pipeline skill
        var compositeResult = await composer.ComposeSkillsAsync(
            "etl_pipeline",
            "Complete ETL data pipeline",
            new List<string> { "extract_data", "transform_data", "load_data" });

        if (compositeResult.IsSuccess)
        {
            var composite = compositeResult.Value;
            Console.WriteLine($"Composite skill created: {composite.Name}");
            Console.WriteLine($"  Description: {composite.Description}");
            Console.WriteLine($"  Total steps: {composite.Steps.Count}");
            Console.WriteLine($"  Success rate: {composite.SuccessRate:P0}");

            // Demonstrate decomposition
            var decomposeResult = composer.DecomposeSkill("etl_pipeline");
            if (decomposeResult.IsSuccess)
            {
                Console.WriteLine($"  Components: {string.Join(", ", decomposeResult.Value.Select(s => s.Name))}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates distributed orchestration.
    /// </summary>
    public static async Task DemonstrateDistributedOrchestration()
    {
        Console.WriteLine("=== Distributed Orchestration Example ===\n");

        var safety = new SafetyGuard(PermissionLevel.Isolated);
        var orchestrator = new DistributedOrchestrator(safety);

        // Register agents with different capabilities
        var agents = new[]
        {
            new AgentInfo("compute-1", "Compute Agent 1", new HashSet<string> { "compute", "analysis" }, AgentStatus.Available, DateTime.UtcNow),
            new AgentInfo("storage-1", "Storage Agent", new HashSet<string> { "storage", "database" }, AgentStatus.Available, DateTime.UtcNow),
            new AgentInfo("compute-2", "Compute Agent 2", new HashSet<string> { "compute", "ml" }, AgentStatus.Available, DateTime.UtcNow),
        };

        foreach (var agent in agents)
        {
            orchestrator.RegisterAgent(agent);
        }

        Console.WriteLine($"Registered {agents.Length} agents");

        // Create a plan for distributed execution
        var plan = new Plan(
            "Distributed data processing",
            new List<PlanStep>
            {
                new PlanStep("compute", new Dictionary<string, object> { ["task"] = "analyze" }, "analysis result", 0.9),
                new PlanStep("storage", new Dictionary<string, object> { ["task"] = "store" }, "storage result", 0.9),
                new PlanStep("ml", new Dictionary<string, object> { ["task"] = "predict" }, "predictions", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.9 },
            DateTime.UtcNow);

        var result = await orchestrator.ExecuteDistributedAsync(plan);

        if (result.IsSuccess)
        {
            var execution = result.Value;
            Console.WriteLine($"Distributed execution completed:");
            Console.WriteLine($"  Agents used: {execution.Metadata.GetValueOrDefault("agents_used", 0)}");
            Console.WriteLine($"  Success: {execution.Success}");
            Console.WriteLine($"  Duration: {execution.Duration.TotalMilliseconds:F0}ms");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates adaptive planning with real-time adjustments.
    /// </summary>
    public static async Task DemonstrateAdaptivePlanning()
    {
        Console.WriteLine("=== Adaptive Planning Example ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        var adaptivePlanner = new AdaptivePlanner(orchestrator, chatModel);

        // Create a plan with varying confidence
        var plan = new Plan(
            "Process data with error handling",
            new List<PlanStep>
            {
                new PlanStep("validate_input", new Dictionary<string, object>(), "validated", 0.9),
                new PlanStep("risky_operation", new Dictionary<string, object>(), "processed", 0.3), // Low confidence
                new PlanStep("finalize", new Dictionary<string, object>(), "final", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.7 },
            DateTime.UtcNow);

        var config = new AdaptivePlanningConfig(
            MaxRetries: 2,
            EnableAutoReplan: false,
            FailureThreshold: 0.5);

        var result = await adaptivePlanner.ExecuteWithAdaptationAsync(plan, config);

        if (result.IsSuccess)
        {
            var execution = result.Value;
            Console.WriteLine($"Adaptive execution completed:");
            Console.WriteLine($"  Success: {execution.Success}");

            if (execution.Metadata.TryGetValue("adaptations", out var adaptations))
            {
                var adaptList = (List<string>)adaptations;
                Console.WriteLine($"  Adaptations made: {adaptList.Count}");
                foreach (var adaptation in adaptList)
                {
                    Console.WriteLine($"    - {adaptation}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates cost-aware routing.
    /// </summary>
    public static async Task DemonstrateCostAwareRouting()
    {
        Console.WriteLine("=== Cost-Aware Routing Example ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var baseOrchestrator = new SmartModelOrchestrator(tools, "default");

        // Register a real model
        baseOrchestrator.RegisterModel(
            new ModelCapability("llama3", new[] { "general" }, 2048, 1.0, 500, ModelType.General),
            chatModel);

        var uncertaintyRouter = new UncertaintyRouter(baseOrchestrator, 0.7);

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        var costRouter = new CostAwareRouter(uncertaintyRouter, orchestrator);

        // Try different optimization strategies
        var strategies = new[]
        {
            CostOptimizationStrategy.MinimizeCost,
            CostOptimizationStrategy.MaximizeQuality,
            CostOptimizationStrategy.Balanced,
            CostOptimizationStrategy.MaximizeValue
        };

        foreach (var strategy in strategies)
        {
            var config = new CostAwareRoutingConfig(
                MaxCostPerPlan: 0.5,
                MinAcceptableQuality: 0.7,
                Strategy: strategy);

            var result = await costRouter.RouteWithCostAwarenessAsync(
                "Process complex data analysis",
                null,
                config);

            if (result.IsSuccess)
            {
                var analysis = result.Value;
                Console.WriteLine($"Strategy: {strategy}");
                Console.WriteLine($"  Route: {analysis.RecommendedRoute}");
                Console.WriteLine($"  Cost: ${analysis.EstimatedCost:F6}");
                Console.WriteLine($"  Quality: {analysis.EstimatedQuality:P0}");
                Console.WriteLine($"  Value: {analysis.ValueScore:F3}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates human-in-the-loop workflows.
    /// </summary>
    public static async Task DemonstrateHumanInTheLoop()
    {
        Console.WriteLine("=== Human-in-the-Loop Example ===\n");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        // Use auto-approving mock for demonstration
        var mockProvider = new AutoApprovingFeedbackProvider();
        var hitlOrchestrator = new HumanInTheLoopOrchestrator(orchestrator, mockProvider);

        var plan = new Plan(
            "Database maintenance",
            new List<PlanStep>
            {
                new PlanStep("backup_database", new Dictionary<string, object>(), "backup created", 0.95),
                new PlanStep("delete_old_records", new Dictionary<string, object>(), "records deleted", 0.9), // Critical
                new PlanStep("optimize_tables", new Dictionary<string, object>(), "tables optimized", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.91 },
            DateTime.UtcNow);

        var config = new HumanInTheLoopConfig(
            RequireApprovalForCriticalSteps: true,
            EnableInteractiveRefinement: false,
            DefaultTimeout: TimeSpan.FromMinutes(2),
            CriticalActionPatterns: new List<string> { "delete", "drop", "remove" });

        var result = await hitlOrchestrator.ExecuteWithHumanOversightAsync(plan, config);

        if (result.IsSuccess)
        {
            var execution = result.Value;
            Console.WriteLine($"Human oversight execution:");
            Console.WriteLine($"  Success: {execution.Success}");

            if (execution.Metadata.TryGetValue("approvals", out var approvals))
            {
                var approvalList = (List<string>)approvals;
                Console.WriteLine($"  Approval events: {approvalList.Count}");
                foreach (var approval in approvalList)
                {
                    Console.WriteLine($"    - {approval}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Runs all enhancement demonstrations.
    /// </summary>
    public static async Task RunAllDemonstrations()
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine("META-AI V2 ENHANCEMENTS DEMONSTRATION");
        Console.WriteLine(new string('=', 70) + "\n");

        try
        {
            await DemonstrateParallelExecution();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parallel execution demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateHierarchicalPlanning();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hierarchical planning demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateExperienceReplay();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Experience replay demo skipped: {ex.Message}\n");
        }

        await DemonstrateSkillComposition();
        await DemonstrateDistributedOrchestration();

        try
        {
            await DemonstrateAdaptivePlanning();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Adaptive planning demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateCostAwareRouting();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cost-aware routing demo skipped: {ex.Message}\n");
        }

        try
        {
            await DemonstrateHumanInTheLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Human-in-the-loop demo skipped: {ex.Message}\n");
        }

        Console.WriteLine(new string('=', 70));
        Console.WriteLine("DEMONSTRATIONS COMPLETED");
        Console.WriteLine(new string('=', 70) + "\n");
    }

    // Helper methods

    private static Experience CreateSampleExperience(string goal, double quality)
    {
        var plan = new Plan(
            goal,
            new List<PlanStep> { CreateSampleStep("action") },
            new Dictionary<string, double> { ["overall"] = quality },
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            new List<StepResult>
            {
                new StepResult(
                    plan.Steps[0],
                    true,
                    "Success",
                    null,
                    TimeSpan.FromSeconds(1),
                    new Dictionary<string, object>())
            },
            true,
            "Success",
            new Dictionary<string, object>(),
            TimeSpan.FromSeconds(1));

        var verification = new VerificationResult(
            execution,
            true,
            quality,
            new List<string>(),
            new List<string>(),
            null);

        return new Experience(
            Guid.NewGuid(),
            goal,
            plan,
            execution,
            verification,
            DateTime.UtcNow,
            new Dictionary<string, object>());
    }

    private static PlanStep CreateSampleStep(string action)
    {
        return new PlanStep(
            action,
            new Dictionary<string, object> { ["param"] = "value" },
            $"{action} result",
            0.85);
    }

    private class AutoApprovingFeedbackProvider : IHumanFeedbackProvider
    {
        public Task<HumanFeedbackResponse> RequestFeedbackAsync(
            HumanFeedbackRequest request,
            CancellationToken ct = default)
        {
            return Task.FromResult(new HumanFeedbackResponse(
                request.RequestId,
                "approve",
                null,
                DateTime.UtcNow));
        }

        public Task<ApprovalResponse> RequestApprovalAsync(
            ApprovalRequest request,
            CancellationToken ct = default)
        {
            Console.WriteLine($"  [Auto-approved] {request.Action}");
            return Task.FromResult(new ApprovalResponse(
                request.RequestId,
                true,
                "Auto-approved for demo",
                null,
                DateTime.UtcNow));
        }
    }
}
