// <copyright file="SelfImprovingAgentExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;

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
        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();

        // Create enhanced memory store with consolidation
        var memoryConfig = new PersistentMemoryConfig(
            ShortTermCapacity: 50,
            LongTermCapacity: 500,
            ConsolidationThreshold: 0.8,
            EnableForgetting: true);

        var memory = new PersistentMemoryStore(config: memoryConfig);
        var skillRegistry = new SkillRegistry();
        var skillExtractor = new SkillExtractor(chatModel, skillRegistry);

        // Build Meta-AI orchestrator with skill extraction
        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithMemoryStore(memory)
            .WithSkillRegistry(skillRegistry)
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

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var skillRegistry = new SkillRegistry();

        // Custom extraction configuration
        var extractionConfig = new SkillExtractionConfig(
            MinQualityThreshold: 0.75,      // Lower threshold for more skills
            MinStepsForExtraction: 2,        // Require at least 2 steps
            MaxStepsPerSkill: 8,             // Allow more complex skills
            EnableAutoParameterization: true,
            EnableSkillVersioning: true);

        var skillExtractor = new SkillExtractor(chatModel, skillRegistry);

        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Min Quality: {extractionConfig.MinQualityThreshold:P0}");
        Console.WriteLine($"  Min Steps: {extractionConfig.MinStepsForExtraction}");
        Console.WriteLine($"  Max Steps: {extractionConfig.MaxStepsPerSkill}");
        Console.WriteLine($"  Auto Parameterization: {extractionConfig.EnableAutoParameterization}");

        // Create mock execution for demonstration
        var plan = new Plan(
            "Multi-step analysis task",
            new List<PlanStep>
            {
                new PlanStep("analyze_input", new Dictionary<string, object> { ["data"] = "sample" }, "analyzed", 0.85),
                new PlanStep("process_data", new Dictionary<string, object> { ["input"] = "analyzed" }, "processed", 0.80),
                new PlanStep("generate_output", new Dictionary<string, object> { ["data"] = "processed" }, "output", 0.90),
            },
            new Dictionary<string, double> { ["overall"] = 0.85 },
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            plan.Steps.Select(s => new StepResult(s, true, "success", null, TimeSpan.FromMilliseconds(100), new())).ToList(),
            true,
            "Final output",
            new(),
            TimeSpan.FromMilliseconds(300));

        var verification = new VerificationResult(
            execution,
            Verified: true,
            QualityScore: 0.85,
            Issues: new(),
            Improvements: new(),
            RevisedPlan: null);

        // Extract skill with custom config
        var result = await skillExtractor.ExtractSkillAsync(execution, verification, extractionConfig);

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

        var config = new PersistentMemoryConfig(
            ShortTermCapacity: 10,
            LongTermCapacity: 50,
            ConsolidationThreshold: 0.7,
            ConsolidationInterval: TimeSpan.FromSeconds(5),
            EnableForgetting: true,
            ForgettingThreshold: 0.4);

        var memory = new PersistentMemoryStore(config: config);

        Console.WriteLine("Memory Configuration:");
        Console.WriteLine($"  Short-term capacity: {config.ShortTermCapacity}");
        Console.WriteLine($"  Long-term capacity: {config.LongTermCapacity}");
        Console.WriteLine($"  Consolidation threshold: {config.ConsolidationThreshold:P0}");
        Console.WriteLine($"  Forgetting enabled: {config.EnableForgetting}\n");

        // Store various quality experiences
        Console.WriteLine("Storing experiences with varying quality...");
        for (int i = 0; i < 20; i++)
        {
            var quality = i % 3 == 0 ? 0.3 : (i % 3 == 1 ? 0.75 : 0.95);
            var experience = CreateExperience($"Task {i}", quality);
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

        var stats = await memory.GetStatisticsAsync();
        Console.WriteLine($"\n✓ Memory Statistics:");
        Console.WriteLine($"  Total experiences: {stats.TotalExperiences}");
        Console.WriteLine($"  Successful: {stats.SuccessfulExecutions}");
        Console.WriteLine($"  Failed: {stats.FailedExecutions}");
        Console.WriteLine($"  Average quality: {stats.AverageQualityScore:P0}");

        var episodic = memory.GetExperiencesByType(MemoryType.Episodic);
        var semantic = memory.GetExperiencesByType(MemoryType.Semantic);

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
            var planResult = await orchestrator.PlanAsync(goal);
            if (!planResult.IsSuccess)
            {
                Console.WriteLine($"✗ Planning failed: {planResult.Error}");
                return;
            }

            var plan = planResult.Value;
            Console.WriteLine($"✓ Plan created with {plan.Steps.Count} steps");

            // Execute
            var execResult = await orchestrator.ExecuteAsync(plan);
            if (!execResult.IsSuccess)
            {
                Console.WriteLine($"✗ Execution failed: {execResult.Error}");
                return;
            }

            var execution = execResult.Value;
            Console.WriteLine($"✓ Execution completed: {execution.FinalOutput}");

            // Verify
            var verifyResult = await orchestrator.VerifyAsync(execution);
            if (!verifyResult.IsSuccess)
            {
                Console.WriteLine($"✗ Verification failed: {verifyResult.Error}");
                return;
            }

            var verification = verifyResult.Value;
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
        var skills = skillRegistry.GetAllSkills();

        if (skills.Count == 0)
        {
            Console.WriteLine("No skills learned yet.");
            return;
        }

        Console.WriteLine($"Total skills learned: {skills.Count}\n");

        foreach (var skill in skills.Take(10))
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
        var stats = await memory.GetStatisticsAsync();

        Console.WriteLine($"Total Experiences: {stats.TotalExperiences}");
        Console.WriteLine($"Successful: {stats.SuccessfulExecutions}");
        Console.WriteLine($"Failed: {stats.FailedExecutions}");
        Console.WriteLine($"Average Quality: {stats.AverageQualityScore:P0}");

        if (stats.GoalCounts.Any())
        {
            Console.WriteLine("\nTop Goals by Frequency:");
            foreach (var (goal, count) in stats.GoalCounts.OrderByDescending(kv => kv.Value).Take(5))
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
        var plan = new Plan(
            goal,
            new List<PlanStep> { new PlanStep("action", new(), "outcome", 0.8) },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            new List<StepResult> { new StepResult(plan.Steps[0], true, "result", null, TimeSpan.FromMilliseconds(10), new()) },
            true,
            "result",
            new(),
            TimeSpan.FromMilliseconds(10));

        var verification = new VerificationResult(
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
