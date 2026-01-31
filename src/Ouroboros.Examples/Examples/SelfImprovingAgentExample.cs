// <copyright file="SelfImprovingAgentExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Ethics;
using AgentPlan = Ouroboros.Agent.MetaAI.Plan;
using AgentPlanStep = Ouroboros.Agent.MetaAI.PlanStep;
using AgentSkill = Ouroboros.Agent.MetaAI.Skill;

/// <summary>
/// Example demonstrating self-improving agent capabilities with automatic skill learning.
/// </summary>
public static class SelfImprovingAgentExample
{
    /// <summary>
    /// Demonstrates the complete skill learning cycle.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunCompleteLearningCycle()
    {
        Console.WriteLine("=== Self-Improving Agent Example ===\n");
        Console.WriteLine("This example demonstrates how the agent automatically learns and reuses skills.\n");

        // Setup
        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        // Create enhanced memory store with consolidation
        PersistentMemoryConfig memoryConfig = new PersistentMemoryConfig(
            ShortTermCapacity: 50,
            LongTermCapacity: 500,
            ConsolidationThreshold: 0.8,
            EnableForgetting: true);

        PersistentMemoryStore memory = new PersistentMemoryStore(config: memoryConfig);
        SkillRegistry skillRegistry = new SkillRegistry();
        IEthicsFramework ethics = EthicsFrameworkFactory.CreateDefault();
        SkillExtractor skillExtractor = new SkillExtractor(chatModel, skillRegistry, ethics);

        // Build Meta-AI orchestrator with skill extraction
        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithMemoryStore(memory)
            .WithSkillRegistry(skillRegistry)
            .WithEthicsFramework(ethics)
            .WithSkillExtractor(skillExtractor)
            .Build();

        Console.WriteLine("✓ Self-improving agent initialized\n");

        // Phase 1: First execution - Learn a new skill
        Console.WriteLine("=== Phase 1: Initial Learning ===");
        await ExecuteAndLearn(orchestrator, "Calculate the sum of 42 and 58", "arithmetic_sum");

        // Phase 2: Similar task - Should reuse learned skill
        Console.WriteLine("\n=== Phase 2: Skill Reuse ===");
        await ExecuteAndLearn(orchestrator, "Add 15 and 27 together", "arithmetic_addition");

        // Phase 3: Different but related task
        Console.WriteLine("\n=== Phase 3: Transfer Learning ===");
        await ExecuteAndLearn(orchestrator, "Calculate the product of 6 and 9", "arithmetic_product");

        // Display learned skills
        Console.WriteLine("\n=== Learned Skills Summary ===");
        DisplayLearnedSkills(skillRegistry);

        // Display memory statistics
        Console.WriteLine("\n=== Memory Statistics ===");
        await DisplayMemoryStats(memory);
    }

    /// <summary>
    /// Demonstrates skill extraction configuration.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunCustomExtractionConfig()
    {
        Console.WriteLine("=== Custom Skill Extraction Configuration ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        SkillRegistry skillRegistry = new SkillRegistry();

        // Custom extraction configuration
        SkillExtractionConfig extractionConfig = new SkillExtractionConfig(
            MinQualityThreshold: 0.75,      // Lower threshold for more skills
            MinStepsForExtraction: 2,        // Require at least 2 steps
            MaxStepsPerSkill: 8,             // Allow more complex skills
            EnableAutoParameterization: true,
            EnableSkillVersioning: true);

        IEthicsFramework ethics = EthicsFrameworkFactory.CreateDefault();
        SkillExtractor skillExtractor = new SkillExtractor(chatModel, skillRegistry, ethics);

        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Min Quality: {extractionConfig.MinQualityThreshold:P0}");
        Console.WriteLine($"  Min Steps: {extractionConfig.MinStepsForExtraction}");
        Console.WriteLine($"  Max Steps: {extractionConfig.MaxStepsPerSkill}");
        Console.WriteLine($"  Auto Parameterization: {extractionConfig.EnableAutoParameterization}");

        // Create mock execution for demonstration
        AgentPlan plan = new AgentPlan(
            "Multi-step analysis task",
            new List<PlanStep>
            {
                new AgentPlanStep("analyze_input", new Dictionary<string, object> { ["data"] = "sample" }, "analyzed", 0.85),
                new AgentPlanStep("process_data", new Dictionary<string, object> { ["input"] = "analyzed" }, "processed", 0.80),
                new AgentPlanStep("generate_output", new Dictionary<string, object> { ["data"] = "processed" }, "output", 0.90),
            },
            new Dictionary<string, double> { ["overall"] = 0.85 },
            DateTime.UtcNow);

        ExecutionResult execution = new ExecutionResult(
            plan,
            plan.Steps.Select(s => new StepResult(s, true, "success", null, TimeSpan.FromMilliseconds(100), new())).ToList(),
            true,
            "Final output",
            new(),
            TimeSpan.FromMilliseconds(300));

        VerificationResult verification = new VerificationResult(
            execution,
            Verified: true,
            QualityScore: 0.85,
            Issues: new(),
            Improvements: new(),
            RevisedPlan: null);

        // Extract skill with custom config
        Result<AgentSkill, string> result = await skillExtractor.ExtractSkillAsync(execution, verification, extractionConfig);

        result.Match(
            skill =>
            {
                Console.WriteLine($"\n✓ Skill extracted: {skill.Name}");
                Console.WriteLine($"  Description: {skill.Description}");
                Console.WriteLine($"  Steps: {skill.Steps.Count}");
                Console.WriteLine($"  Success Rate: {skill.SuccessRate:P0}");
                Console.WriteLine($"  Prerequisites: {string.Join(", ", skill.Prerequisites)}");
            },
            error => Console.WriteLine($"✗ Extraction failed: {error}"));
    }

    /// <summary>
    /// Demonstrates memory consolidation and forgetting.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunMemoryManagementDemo()
    {
        Console.WriteLine("=== Memory Management Demo ===\n");

        PersistentMemoryConfig config = new PersistentMemoryConfig(
            ShortTermCapacity: 10,
            LongTermCapacity: 50,
            ConsolidationThreshold: 0.7,
            ConsolidationInterval: TimeSpan.FromSeconds(5),
            EnableForgetting: true,
            ForgettingThreshold: 0.4);

        PersistentMemoryStore memory = new PersistentMemoryStore(config: config);

        Console.WriteLine("Memory Configuration:");
        Console.WriteLine($"  Short-term capacity: {config.ShortTermCapacity}");
        Console.WriteLine($"  Long-term capacity: {config.LongTermCapacity}");
        Console.WriteLine($"  Consolidation threshold: {config.ConsolidationThreshold:P0}");
        Console.WriteLine($"  Forgetting enabled: {config.EnableForgetting}\n");

        // Store various quality experiences
        Console.WriteLine("Storing experiences with varying quality...");
        for (int i = 0; i < 20; i++)
        {
            double quality = i % 3 == 0 ? 0.3 : (i % 3 == 1 ? 0.75 : 0.95);
            Experience experience = CreateExperience($"Task {i}", quality);
            await memory.StoreExperienceAsync(experience);

            if (i % 5 == 0)
            {
                Console.Write(".");
            }
        }

        Console.WriteLine(" Done!\n");

        // Wait for consolidation
        Console.WriteLine("Waiting for memory consolidation...");
        await Task.Delay(6000);

        MemoryStatistics stats = await memory.GetStatisticsAsync();
        Console.WriteLine($"\n✓ Memory Statistics:");
        Console.WriteLine($"  Total experiences: {stats.TotalExperiences}");
        Console.WriteLine($"  Successful: {stats.SuccessfulExecutions}");
        Console.WriteLine($"  Failed: {stats.FailedExecutions}");
        Console.WriteLine($"  Average quality: {stats.AverageQualityScore:P0}");

        List<Experience> episodic = memory.GetExperiencesByType(MemoryType.Episodic);
        List<Experience> semantic = memory.GetExperiencesByType(MemoryType.Semantic);

        Console.WriteLine($"\n✓ Memory Organization:");
        Console.WriteLine($"  Episodic (short-term): {episodic.Count}");
        Console.WriteLine($"  Semantic (long-term): {semantic.Count}");

        if (config.EnableForgetting)
        {
            Console.WriteLine($"\n✓ Low-quality memories forgotten to maintain quality threshold");
        }
    }

    /// <summary>
    /// Helper method to execute a task and learn from it.
    /// </summary>
    private static async Task ExecuteAndLearn(
        IMetaAIPlannerOrchestrator orchestrator,
        string goal,
        string expectedSkillType)
    {
        Console.WriteLine($"Goal: {goal}");

        try
        {
            // Plan
            Result<AgentPlan, string> planResult = await orchestrator.PlanAsync(goal);
            if (!planResult.IsSuccess)
            {
                Console.WriteLine($"✗ Planning failed: {planResult.Error}");
                return;
            }

            AgentPlan plan = planResult.Value;
            Console.WriteLine($"✓ Plan created with {plan.Steps.Count} steps");

            // Execute
            Result<ExecutionResult, string> execResult = await orchestrator.ExecuteAsync(plan);
            if (!execResult.IsSuccess)
            {
                Console.WriteLine($"✗ Execution failed: {execResult.Error}");
                return;
            }

            ExecutionResult execution = execResult.Value;
            Console.WriteLine($"✓ Execution completed: {execution.FinalOutput}");

            // Verify
            Result<VerificationResult, string> verifyResult = await orchestrator.VerifyAsync(execution);
            if (!verifyResult.IsSuccess)
            {
                Console.WriteLine($"✗ Verification failed: {verifyResult.Error}");
                return;
            }

            VerificationResult verification = verifyResult.Value;
            Console.WriteLine($"✓ Verification: {(verification.Verified ? "PASSED" : "FAILED")} (Quality: {verification.QualityScore:P0})");

            // Learn
            orchestrator.LearnFromExecution(verification);
            Console.WriteLine($"✓ Learning completed (skill extraction triggered if quality > 80%)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Displays learned skills summary.
    /// </summary>
    private static void DisplayLearnedSkills(ISkillRegistry skillRegistry)
    {
        IReadOnlyList<Skill> skills = skillRegistry.GetAllSkills();

        if (skills.Count == 0)
        {
            Console.WriteLine("No skills learned yet.");
            return;
        }

        Console.WriteLine($"Total skills learned: {skills.Count}\n");

        foreach (AgentSkill? skill in skills.Take(10))
        {
            Console.WriteLine($"Skill: {skill.Name}");
            Console.WriteLine($"  Description: {skill.Description}");
            Console.WriteLine($"  Success Rate: {skill.SuccessRate:P0}");
            Console.WriteLine($"  Usage Count: {skill.UsageCount}");
            Console.WriteLine($"  Steps: {skill.Steps.Count}");
            Console.WriteLine($"  Created: {skill.CreatedAt:yyyy-MM-dd HH:mm}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Displays memory statistics.
    /// </summary>
    private static async Task DisplayMemoryStats(IMemoryStore memory)
    {
        MemoryStatistics stats = await memory.GetStatisticsAsync();

        Console.WriteLine($"Total Experiences: {stats.TotalExperiences}");
        Console.WriteLine($"Successful: {stats.SuccessfulExecutions}");
        Console.WriteLine($"Failed: {stats.FailedExecutions}");
        Console.WriteLine($"Average Quality: {stats.AverageQualityScore:P0}");

        if (stats.GoalCounts.Any())
        {
            Console.WriteLine("\nTop Goals by Frequency:");
            foreach ((string goal, int count) in stats.GoalCounts.OrderByDescending(kv => kv.Value).Take(5))
            {
                Console.WriteLine($"  {goal}: {count} times");
            }
        }
    }

    /// <summary>
    /// Helper method to create test experiences.
    /// </summary>
    private static Experience CreateExperience(string goal, double quality)
    {
        AgentPlan plan = new AgentPlan(
            goal,
            new List<PlanStep> { new AgentPlanStep("action", new(), "outcome", 0.8) },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        ExecutionResult execution = new ExecutionResult(
            plan,
            new List<StepResult> { new StepResult(plan.Steps[0], true, "result", null, TimeSpan.FromMilliseconds(10), new()) },
            true,
            "result",
            new(),
            TimeSpan.FromMilliseconds(10));

        VerificationResult verification = new VerificationResult(
            execution,
            quality > 0.5,
            quality,
            new(),
            new(),
            null);

        return new Experience(
            Guid.NewGuid(),
            goal,
            plan,
            execution,
            verification,
            DateTime.UtcNow,
            new());
    }
}
