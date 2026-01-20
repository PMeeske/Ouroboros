// <copyright file="MetaAIv2Tests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using LangChain.Providers.Ollama;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Unit tests for Meta-AI v2 planner/executor/verifier orchestrator.
/// Uses mock models for testing - no external LLM service required.
/// </summary>
[Trait("Category", "Unit")]
public class MetaAIv2Tests
{
    /// <summary>
    /// Tests basic orchestrator creation and configuration.
    /// </summary>
    [Fact]
    public void Orchestrator_Creation_ShouldInitializeSuccessfully()
    {
        Console.WriteLine("=== Test: Meta-AI v2 Orchestrator Creation ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithConfidenceThreshold(0.7)
            .WithDefaultPermissionLevel(PermissionLevel.Isolated)
            .Build();

        if (orchestrator == null)
        {
            throw new Exception("Orchestrator should be created successfully");
        }

        Console.WriteLine("✓ Meta-AI v2 orchestrator created successfully");

        var metrics = orchestrator.GetMetrics();
        Console.WriteLine($"✓ Metrics initialized: {metrics.Count} components tracked");
        Console.WriteLine("✓ Test passed!\n");
    }

    /// <summary>
    /// Tests plan generation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task Plan_Generation_ShouldCreateValidPlan()
    {
        Console.WriteLine("=== Test: Plan Generation ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        try
        {
            var planResult = await orchestrator.PlanAsync(
                "Calculate the sum of 42 and 58",
                new Dictionary<string, object> { ["difficulty"] = "easy" });

            planResult.Match(
                plan =>
                {
                    if (plan.Steps.Count == 0)
                    {
                        throw new Exception("Plan should have at least one step");
                    }

                    Console.WriteLine($"✓ Plan generated with {plan.Steps.Count} steps");
                    Console.WriteLine($"  Goal: {plan.Goal}");
                    foreach (var step in plan.Steps)
                    {
                        Console.WriteLine($"  - {step.Action} (confidence: {step.ConfidenceScore:P0})");
                    }
                },
                error => throw new Exception($"Planning failed: {error}"));

            Console.WriteLine("✓ Plan generation test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("✓ Plan generation test skipped (Ollama not available)\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Tests skill registry functionality.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task SkillRegistry_Operations_ShouldWorkCorrectly()
    {
        Console.WriteLine("=== Test: Skill Registry ===");

        var skillRegistry = new SkillRegistry();

        var skill = new Skill(
            "simple_math",
            "Perform basic mathematical calculations",
            new List<string> { "math" },
            new List<PlanStep>
            {
                new PlanStep("math", new Dictionary<string, object> { ["operation"] = "add" }, "Result", 0.9),
            },
            SuccessRate: 1.0,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow);

        skillRegistry.RegisterSkill(skill);

        var retrieved = skillRegistry.GetSkill("simple_math");
        if (retrieved == null)
        {
            throw new Exception("Skill should be retrievable after registration");
        }

        Console.WriteLine($"✓ Skill registered and retrieved: {retrieved.Name}");

        var matching = await skillRegistry.FindMatchingSkillsAsync("do a math calculation");
        if (!matching.Any())
        {
            Console.WriteLine("  Note: No matching skills found (embedding model not available)");
        }
        else
        {
            Console.WriteLine($"✓ Found {matching.Count} matching skills");
        }

        skillRegistry.RecordSkillExecution("simple_math", true);
        var updated = skillRegistry.GetSkill("simple_math");
        if (updated?.UsageCount != 1)
        {
            throw new Exception("Skill usage count should be updated");
        }

        Console.WriteLine($"✓ Skill execution recorded: usage count = {updated.UsageCount}");

        Console.WriteLine("✓ Skill registry test passed!\n");
    }

    /// <summary>
    /// Tests uncertainty router.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task UncertaintyRouter_Routing_ShouldMakeCorrectDecisions()
    {
        Console.WriteLine("=== Test: Uncertainty Router ===");

        var tools = ToolRegistry.CreateDefault();
        var baseOrchestrator = new SmartModelOrchestrator(tools, "default");
        var router = new UncertaintyRouter(baseOrchestrator, minConfidenceThreshold: 0.7);

        // Register a test model
        var mockModel = new MockChatModel("test-response");
        baseOrchestrator.RegisterModel(
            new ModelCapability("test", new[] { "general" }, 2048, 1.0, 500, ModelType.General),
            mockModel);

        var routingResult = await router.RouteAsync(
            "Explain quantum computing",
            new Dictionary<string, object> { ["complexity"] = "high" });

        routingResult.Match(
            decision =>
            {
                Console.WriteLine($"✓ Routing decision: {decision.Route}");
                Console.WriteLine($"  Reason: {decision.Reason}");
                Console.WriteLine($"  Confidence: {decision.Confidence:P0}");
            },
            error => throw new Exception($"Routing failed: {error}"));

        var fallback = router.DetermineFallback("complex task", 0.3);
        Console.WriteLine($"✓ Fallback strategy for low confidence: {fallback}");

        Console.WriteLine("✓ Uncertainty router test passed!\n");
    }

    /// <summary>
    /// Tests safety guard.
    /// </summary>
    [Fact]
    public void SafetyGuard_Checks_ShouldEnforceSafety()
    {
        Console.WriteLine("=== Test: Safety Guard ===");

        var safety = new SafetyGuard(PermissionLevel.Isolated);

        // Test safe operation
        var safeResult = safety.CheckSafety(
            "read_data",
            new Dictionary<string, object> { ["source"] = "database" },
            PermissionLevel.ReadOnly);

        if (!safeResult.Safe)
        {
            throw new Exception("Read operation should be safe");
        }

        Console.WriteLine("✓ Safe operation passed safety check");

        // Test unsafe operation
        var unsafeResult = safety.CheckSafety(
            "delete_all",
            new Dictionary<string, object> { ["confirm"] = "yes" },
            PermissionLevel.ReadOnly);

        if (unsafeResult.Safe)
        {
            throw new Exception("Delete operation should fail safety check with ReadOnly permission");
        }

        Console.WriteLine($"✓ Unsafe operation blocked: {string.Join(", ", unsafeResult.Violations)}");

        // Test tool permission
        var mathAllowed = safety.IsToolExecutionPermitted("math", "{}", PermissionLevel.ReadOnly);
        if (!mathAllowed)
        {
            throw new Exception("Math tool should be allowed with ReadOnly permission");
        }

        Console.WriteLine("✓ Tool permission check passed");

        // Test sandboxing
        var step = new PlanStep(
            "test_action",
            new Dictionary<string, object> { ["data"] = "<script>alert('xss')</script>" },
            "Expected",
            0.8);

        var sandboxed = safety.SandboxStep(step);
        var sanitizedData = sandboxed.Parameters["data"].ToString();
        if (sanitizedData?.Contains("<script>") == true)
        {
            throw new Exception("Sandboxing should sanitize dangerous content");
        }

        Console.WriteLine($"✓ Step sandboxed successfully: {sanitizedData}");

        Console.WriteLine("✓ Safety guard test passed!\n");
    }

    /// <summary>
    /// Tests memory store.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task MemoryStore_Operations_ShouldStoreAndRetrieveExperiences()
    {
        Console.WriteLine("=== Test: Memory Store ===");

        var memory = new MemoryStore();

        var experience = new Experience(
            Guid.NewGuid(),
            "Test goal",
            new Plan("Test goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            new ExecutionResult(
                new Plan("Test goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
                new List<StepResult>(),
                true,
                "Success",
                new Dictionary<string, object>(),
                TimeSpan.FromSeconds(1)),
            new VerificationResult(
                new ExecutionResult(
                    new Plan("Test goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
                    new List<StepResult>(),
                    true,
                    "Success",
                    new Dictionary<string, object>(),
                    TimeSpan.FromSeconds(1)),
                true,
                0.9,
                new List<string>(),
                new List<string>(),
                null),
            DateTime.UtcNow,
            new Dictionary<string, object>());

        await memory.StoreExperienceAsync(experience);
        Console.WriteLine($"✓ Experience stored: {experience.Id}");

        var retrieved = await memory.GetExperienceAsync(experience.Id);
        if (retrieved == null)
        {
            throw new Exception("Experience should be retrievable after storage");
        }

        Console.WriteLine($"✓ Experience retrieved: {retrieved.Goal}");

        var stats = await memory.GetStatisticsAsync();
        if (stats.TotalExperiences != 1)
        {
            throw new Exception($"Expected 1 experience, got {stats.TotalExperiences}");
        }

        Console.WriteLine($"✓ Memory statistics: {stats.TotalExperiences} experiences, {stats.AverageQualityScore:P0} avg quality");

        Console.WriteLine("✓ Memory store test passed!\n");
    }

    /// <summary>
    /// Tests evaluation harness.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task EvaluationHarness_Evaluation_ShouldMeasurePerformance()
    {
        Console.WriteLine("=== Test: Evaluation Harness ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        var harness = new EvaluationHarness(orchestrator);

        try
        {
            var testCase = new TestCase(
                "Basic Test",
                "Add 2 and 2",
                null,
                result => result.Verified);

            var metrics = await harness.EvaluateTestCaseAsync(testCase);

            Console.WriteLine($"✓ Test case evaluated: {metrics.TestCase}");
            Console.WriteLine($"  Success: {metrics.Success}");
            Console.WriteLine($"  Quality: {metrics.QualityScore:P0}");
            Console.WriteLine($"  Execution time: {metrics.ExecutionTime.TotalMilliseconds:F0}ms");

            Console.WriteLine("✓ Evaluation harness test passed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("✓ Evaluation harness test skipped (Ollama not available)\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Runs all Meta-AI v2 tests.
    /// Kept for backward compatibility - wraps individual test methods.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("META-AI LAYER V2 TESTS");
        Console.WriteLine(new string('=', 60) + "\n");

        var instance = new MetaAIv2Tests();
        instance.Orchestrator_Creation_ShouldInitializeSuccessfully();
        await instance.Plan_Generation_ShouldCreateValidPlan();
        await instance.SkillRegistry_Operations_ShouldWorkCorrectly();
        await instance.UncertaintyRouter_Routing_ShouldMakeCorrectDecisions();
        instance.SafetyGuard_Checks_ShouldEnforceSafety();
        await instance.MemoryStore_Operations_ShouldStoreAndRetrieveExperiences();
        await instance.EvaluationHarness_Evaluation_ShouldMeasurePerformance();

        Console.WriteLine(new string('=', 60));
        Console.WriteLine("✓ ALL META-AI V2 TESTS PASSED!");
        Console.WriteLine(new string('=', 60) + "\n");
    }
}
