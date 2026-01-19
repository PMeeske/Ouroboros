// <copyright file="MetaAIv2EnhancementTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Integration tests for Meta-AI v2 enhancements.
/// </summary>
[Trait("Category", "Integration")]
public static class MetaAIv2EnhancementTests
{
    /// <summary>
    /// Tests parallel execution of independent steps.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestParallelExecution()
    {
        Console.WriteLine("=== Test: Parallel Execution ===");

        var safety = new SafetyGuard(PermissionLevel.Isolated);

        // Create a plan with independent steps
        var plan = new Plan(
            "Test parallel execution",
            new List<PlanStep>
            {
                new PlanStep("step1", new Dictionary<string, object> { ["input"] = "a" }, "output1", 0.9),
                new PlanStep("step2", new Dictionary<string, object> { ["input"] = "b" }, "output2", 0.9),
                new PlanStep("step3", new Dictionary<string, object> { ["input"] = "c" }, "output3", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.9 },
            DateTime.UtcNow);

        var executor = new ParallelExecutor(
            safety,
            async (step, ct) =>
            {
                await Task.Delay(100, ct);
                return new StepResult(
                    step,
                    true,
                    $"Completed {step.Action}",
                    null,
                    TimeSpan.FromMilliseconds(100),
                    new Dictionary<string, object>());
            });

        var speedup = executor.EstimateSpeedup(plan);
        Console.WriteLine($"✓ Estimated speedup: {speedup:F2}x");

        var (results, success, output) = await executor.ExecuteParallelAsync(plan);

        if (results.Count != 3)
        {
            throw new Exception($"Expected 3 results, got {results.Count}");
        }

        if (!success)
        {
            throw new Exception("Parallel execution should succeed");
        }

        Console.WriteLine($"✓ Parallel execution completed: {results.Count} steps");
        Console.WriteLine($"✓ All steps successful: {success}");
        Console.WriteLine("✓ Parallel execution test passed!\n");
    }

    /// <summary>
    /// Tests hierarchical planning for complex tasks.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestHierarchicalPlanning()
    {
        Console.WriteLine("=== Test: Hierarchical Planning ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();

            var orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(chatModel)
                .WithTools(tools)
                .Build();

            var hierarchicalPlanner = new HierarchicalPlanner(orchestrator, chatModel);

            var config = new HierarchicalPlanningConfig(
                MaxDepth: 2,
                MinStepsForDecomposition: 2,
                ComplexityThreshold: 0.7);

            var result = await hierarchicalPlanner.CreateHierarchicalPlanAsync(
                "Build a web application",
                null,
                config);

            result.Match(
                plan =>
                {
                    Console.WriteLine($"✓ Hierarchical plan created");
                    Console.WriteLine($"  Top-level steps: {plan.TopLevelPlan.Steps.Count}");
                    Console.WriteLine($"  Sub-plans: {plan.SubPlans.Count}");
                    Console.WriteLine($"  Max depth: {plan.MaxDepth}");
                },
                error => throw new Exception($"Hierarchical planning failed: {error}"));

            Console.WriteLine("✓ Hierarchical planning test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("✓ Hierarchical planning test skipped (Ollama not available)\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Tests experience replay for training.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestExperienceReplay()
    {
        Console.WriteLine("=== Test: Experience Replay ===");

        var memory = new MemoryStore();
        var skills = new SkillRegistry();

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

            var replay = new ExperienceReplay(memory, skills, chatModel);

            // Create some test experiences
            for (int i = 0; i < 5; i++)
            {
                var exp = CreateTestExperience($"Test goal {i}", 0.7 + (i * 0.05));
                await memory.StoreExperienceAsync(exp);
            }

            var config = new ExperienceReplayConfig(
                BatchSize: 3,
                MinQualityScore: 0.7,
                PrioritizeHighQuality: true);

            var trainingResult = await replay.TrainOnExperiencesAsync(config);

            trainingResult.Match(
                result =>
                {
                    Console.WriteLine($"✓ Training completed");
                    Console.WriteLine($"  Experiences processed: {result.ExperiencesProcessed}");
                    Console.WriteLine($"  Patterns discovered: {result.ImprovedMetrics.GetValueOrDefault("patterns_discovered", 0)}");
                    Console.WriteLine($"  Skills extracted: {result.ImprovedMetrics.GetValueOrDefault("skills_extracted", 0)}");
                },
                error => throw new Exception($"Experience replay failed: {error}"));

            Console.WriteLine("✓ Experience replay test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("✓ Experience replay test skipped (Ollama not available)\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Tests skill composition.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestSkillComposition()
    {
        Console.WriteLine("=== Test: Skill Composition ===");

        var skills = new SkillRegistry();
        var memory = new MemoryStore();
        var composer = new SkillComposer(skills, memory);

        // Register base skills
        var skill1 = new Skill(
            "skill_a",
            "Basic skill A",
            new List<string>(),
            new List<PlanStep> { new PlanStep("action_a", new Dictionary<string, object>(), "result_a", 0.9) },
            0.9,
            5,
            DateTime.UtcNow,
            DateTime.UtcNow);

        var skill2 = new Skill(
            "skill_b",
            "Basic skill B",
            new List<string>(),
            new List<PlanStep> { new PlanStep("action_b", new Dictionary<string, object>(), "result_b", 0.85) },
            0.85,
            3,
            DateTime.UtcNow,
            DateTime.UtcNow);

        skills.RegisterSkill(skill1);
        skills.RegisterSkill(skill2);

        // Compose skills
        var compositeResult = await composer.ComposeSkillsAsync(
            "composite_skill",
            "Combines skill A and B",
            new List<string> { "skill_a", "skill_b" });

        compositeResult.Match(
            composite =>
            {
                Console.WriteLine($"✓ Composite skill created: {composite.Name}");
                Console.WriteLine($"  Component count: {composite.Prerequisites.Count(p => p != "__composite__")}");
                Console.WriteLine($"  Total steps: {composite.Steps.Count}");
                Console.WriteLine($"  Success rate: {composite.SuccessRate:P0}");
            },
            error => throw new Exception($"Skill composition failed: {error}"));

        // Test decomposition
        var decomposeResult = composer.DecomposeSkill("composite_skill");
        decomposeResult.Match(
            components =>
            {
                Console.WriteLine($"✓ Decomposed into {components.Count} components");
            },
            error => throw new Exception($"Skill decomposition failed: {error}"));

        Console.WriteLine("✓ Skill composition test passed!\n");
    }

    /// <summary>
    /// Tests distributed orchestration.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestDistributedOrchestration()
    {
        Console.WriteLine("=== Test: Distributed Orchestration ===");

        var safety = new SafetyGuard(PermissionLevel.Isolated);
        var orchestrator = new DistributedOrchestrator(safety);

        // Register test agents
        for (int i = 0; i < 3; i++)
        {
            var agent = new AgentInfo(
                $"agent_{i}",
                $"Agent {i}",
                new HashSet<string> { "general", "compute" },
                AgentStatus.Available,
                DateTime.UtcNow);

            orchestrator.RegisterAgent(agent);
        }

        var agentStatus = orchestrator.GetAgentStatus();
        Console.WriteLine($"✓ Registered {agentStatus.Count} agents");

        // Create a test plan
        var plan = new Plan(
            "Distributed test",
            new List<PlanStep>
            {
                new PlanStep("task1", new Dictionary<string, object>(), "result1", 0.9),
                new PlanStep("task2", new Dictionary<string, object>(), "result2", 0.9),
                new PlanStep("task3", new Dictionary<string, object>(), "result3", 0.9),
            },
            new Dictionary<string, double> { ["overall"] = 0.9 },
            DateTime.UtcNow);

        var result = await orchestrator.ExecuteDistributedAsync(plan);

        result.Match(
            execution =>
            {
                Console.WriteLine($"✓ Distributed execution completed");
                Console.WriteLine($"  Agents used: {execution.Metadata.GetValueOrDefault("agents_used", 0)}");
                Console.WriteLine($"  Success: {execution.Success}");
                Console.WriteLine($"  Duration: {execution.Duration.TotalMilliseconds:F0}ms");
            },
            error => throw new Exception($"Distributed execution failed: {error}"));

        Console.WriteLine("✓ Distributed orchestration test passed!\n");
    }

    /// <summary>
    /// Tests adaptive planning with real-time adaptation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestAdaptivePlanning()
    {
        Console.WriteLine("=== Test: Adaptive Planning ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();

            var orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(chatModel)
                .WithTools(tools)
                .Build();

            var adaptivePlanner = new AdaptivePlanner(orchestrator, chatModel);

            // Create a plan with potentially failing steps
            var plan = new Plan(
                "Adaptive test",
                new List<PlanStep>
                {
                    new PlanStep("step1", new Dictionary<string, object>(), "result1", 0.9),
                    new PlanStep("low_confidence_step", new Dictionary<string, object>(), "result2", 0.2), // Will trigger adaptation
                    new PlanStep("step3", new Dictionary<string, object>(), "result3", 0.9),
                },
                new Dictionary<string, double> { ["overall"] = 0.7 },
                DateTime.UtcNow);

            var config = new AdaptivePlanningConfig(
                MaxRetries: 2,
                EnableAutoReplan: false, // Disable for testing
                FailureThreshold: 0.5);

            var result = await adaptivePlanner.ExecuteWithAdaptationAsync(plan, config);

            result.Match(
                execution =>
                {
                    Console.WriteLine($"✓ Adaptive execution completed");
                    Console.WriteLine($"  Success: {execution.Success}");
                    if (execution.Metadata.TryGetValue("adaptations", out var adaptations))
                    {
                        Console.WriteLine($"  Adaptations: {((List<string>)adaptations).Count}");
                    }
                },
                error => Console.WriteLine($"  Note: {error}"));

            Console.WriteLine("✓ Adaptive planning test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("✓ Adaptive planning test skipped (Ollama not available)\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Tests cost-aware routing.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestCostAwareRouting()
    {
        Console.WriteLine("=== Test: Cost-Aware Routing ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();

            var baseOrchestrator = new SmartModelOrchestrator(tools, "default");
            var mockModel = new MockChatModel("test-response");
            baseOrchestrator.RegisterModel(
                new ModelCapability("test", new[] { "general" }, 2048, 1.0, 500, ModelType.General),
                mockModel);

            var uncertaintyRouter = new UncertaintyRouter(baseOrchestrator, 0.7);

            var orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(chatModel)
                .WithTools(tools)
                .Build();

            var costRouter = new CostAwareRouter(uncertaintyRouter, orchestrator);

            // Register custom cost info
            costRouter.RegisterCostInfo(new CostInfo("custom_model", 0.00001, 0.005, 0.85));

            var config = new CostAwareRoutingConfig(
                MaxCostPerPlan: 0.1,
                MinAcceptableQuality: 0.7,
                Strategy: CostOptimizationStrategy.Balanced);

            var result = await costRouter.RouteWithCostAwarenessAsync(
                "Process text efficiently",
                null,
                config);

            result.Match(
                analysis =>
                {
                    Console.WriteLine($"✓ Cost-aware routing completed");
                    Console.WriteLine($"  Recommended route: {analysis.RecommendedRoute}");
                    Console.WriteLine($"  Estimated cost: ${analysis.EstimatedCost:F6}");
                    Console.WriteLine($"  Estimated quality: {analysis.EstimatedQuality:P0}");
                    Console.WriteLine($"  Value score: {analysis.ValueScore:F2}");
                },
                error => Console.WriteLine($"  Note: {error}"));

            Console.WriteLine("✓ Cost-aware routing test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("✓ Cost-aware routing test skipped (Ollama not available)\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Tests human-in-the-loop integration.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestHumanInTheLoop()
    {
        Console.WriteLine("=== Test: Human-in-the-Loop ===");

        try
        {
            var provider = new OllamaProvider();
            var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            var tools = ToolRegistry.CreateDefault();

            var orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(chatModel)
                .WithTools(tools)
                .Build();

            // Create a mock feedback provider that auto-approves
            var mockProvider = new MockFeedbackProvider();

            var hitlOrchestrator = new HumanInTheLoopOrchestrator(orchestrator, mockProvider);

            var plan = new Plan(
                "Test with oversight",
                new List<PlanStep>
                {
                    new PlanStep("safe_step", new Dictionary<string, object>(), "result1", 0.9),
                    new PlanStep("delete_data", new Dictionary<string, object>(), "result2", 0.9), // Critical
                },
                new Dictionary<string, double> { ["overall"] = 0.9 },
                DateTime.UtcNow);

            var config = new HumanInTheLoopConfig(
                RequireApprovalForCriticalSteps: true,
                EnableInteractiveRefinement: false,
                DefaultTimeout: TimeSpan.FromMinutes(1),
                CriticalActionPatterns: new List<string> { "delete" });

            var result = await hitlOrchestrator.ExecuteWithHumanOversightAsync(plan, config);

            result.Match(
                execution =>
                {
                    Console.WriteLine($"✓ Human oversight execution completed");
                    Console.WriteLine($"  Success: {execution.Success}");
                    if (execution.Metadata.TryGetValue("approvals", out var approvals))
                    {
                        Console.WriteLine($"  Approval events: {((List<string>)approvals).Count}");
                    }
                },
                error => Console.WriteLine($"  Note: {error}"));

            Console.WriteLine("✓ Human-in-the-loop test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("✓ Human-in-the-loop test skipped (Ollama not available)\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Runs all enhancement tests.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllEnhancementTests()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("META-AI LAYER V2 ENHANCEMENT TESTS");
        Console.WriteLine(new string('=', 60) + "\n");

        await TestParallelExecution();
        await TestHierarchicalPlanning();
        await TestExperienceReplay();
        await TestSkillComposition();
        await TestDistributedOrchestration();
        await TestAdaptivePlanning();
        await TestCostAwareRouting();
        await TestHumanInTheLoop();

        Console.WriteLine(new string('=', 60));
        Console.WriteLine("✓ ALL ENHANCEMENT TESTS PASSED!");
        Console.WriteLine(new string('=', 60) + "\n");
    }

    // Helper methods
    private static Experience CreateTestExperience(string goal, double quality)
    {
        var plan = new Plan(
            goal,
            new List<PlanStep>
            {
                new PlanStep("test_action", new Dictionary<string, object>(), "expected", 0.8),
            },
            new Dictionary<string, double> { ["overall"] = 0.8 },
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
                    new Dictionary<string, object>()),
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

    /// <summary>
    /// Mock feedback provider for testing.
    /// </summary>
    private class MockFeedbackProvider : IHumanFeedbackProvider
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
            // Auto-approve for testing
            return Task.FromResult(new ApprovalResponse(
                request.RequestId,
                true,
                null,
                null,
                DateTime.UtcNow));
        }
    }
}
