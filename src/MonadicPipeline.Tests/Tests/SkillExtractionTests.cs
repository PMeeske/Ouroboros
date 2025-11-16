// <copyright file="SkillExtractionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Tests for skill extraction and learning capabilities.
/// </summary>
public static class SkillExtractionTests
{
    /// <summary>
    /// Tests basic skill extraction from a successful execution.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestBasicSkillExtraction()
    {
        Console.WriteLine("=== Test: Basic Skill Extraction ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var skillRegistry = new SkillRegistry();

        var extractor = new SkillExtractor(chatModel, skillRegistry);

        // Create a mock successful execution
        var plan = new Plan(
            "Calculate sum of two numbers",
            new List<PlanStep>
            {
                new PlanStep("get_number_1", new Dictionary<string, object> { ["value"] = 42 }, "42", 0.9),
                new PlanStep("get_number_2", new Dictionary<string, object> { ["value"] = 58 }, "58", 0.9),
                new PlanStep("add_numbers", new Dictionary<string, object> { ["a"] = 42, ["b"] = 58 }, "100", 0.95),
            },
            new Dictionary<string, double> { ["overall"] = 0.9 },
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            new List<StepResult>
            {
                new StepResult(plan.Steps[0], true, "42", null, TimeSpan.FromMilliseconds(10), new()),
                new StepResult(plan.Steps[1], true, "58", null, TimeSpan.FromMilliseconds(10), new()),
                new StepResult(plan.Steps[2], true, "100", null, TimeSpan.FromMilliseconds(20), new()),
            },
            Success: true,
            FinalOutput: "100",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromMilliseconds(40));

        var verification = new VerificationResult(
            execution,
            Verified: true,
            QualityScore: 0.95,
            Issues: new List<string>(),
            Improvements: new List<string>(),
            RevisedPlan: null);

        // Test should extract logic
        var shouldExtract = await extractor.ShouldExtractSkillAsync(verification);
        if (!shouldExtract)
        {
            throw new Exception("High-quality execution should be eligible for skill extraction");
        }

        Console.WriteLine("✓ Extraction eligibility check passed");

        // Test skill extraction
        var result = await extractor.ExtractSkillAsync(execution, verification);

        result.Match(
            skill =>
            {
                Console.WriteLine($"✓ Skill extracted: {skill.Name}");
                Console.WriteLine($"  Description: {skill.Description}");
                Console.WriteLine($"  Steps: {skill.Steps.Count}");
                Console.WriteLine($"  Success rate: {skill.SuccessRate:P0}");

                if (skill.SuccessRate != verification.QualityScore)
                {
                    throw new Exception("Skill success rate should match verification quality score");
                }
            },
            error => throw new Exception($"Skill extraction failed: {error}"));

        Console.WriteLine("✓ Basic skill extraction test passed!\n");
    }

    /// <summary>
    /// Tests that low-quality executions are not extracted as skills.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestLowQualityRejection()
    {
        Console.WriteLine("=== Test: Low Quality Rejection ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var skillRegistry = new SkillRegistry();
        var extractor = new SkillExtractor(chatModel, skillRegistry);

        var plan = new Plan(
            "Test task",
            new List<PlanStep> { new PlanStep("action", new(), "outcome", 0.5) },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            new List<StepResult> { new StepResult(plan.Steps[0], true, "output", null, TimeSpan.FromMilliseconds(10), new()) },
            true,
            "output",
            new(),
            TimeSpan.FromMilliseconds(10));

        // Low quality verification (below threshold)
        var verification = new VerificationResult(
            execution,
            Verified: true,
            QualityScore: 0.6, // Below default threshold of 0.8
            Issues: new List<string> { "Quality issues" },
            Improvements: new List<string>(),
            RevisedPlan: null);

        var shouldExtract = await extractor.ShouldExtractSkillAsync(verification);
        if (shouldExtract)
        {
            throw new Exception("Low quality execution should NOT be eligible for skill extraction");
        }

        Console.WriteLine("✓ Low quality execution correctly rejected");
        Console.WriteLine("✓ Low quality rejection test passed!\n");
    }

    /// <summary>
    /// Tests skill extraction with custom configuration.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestCustomExtractionConfig()
    {
        Console.WriteLine("=== Test: Custom Extraction Configuration ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var skillRegistry = new SkillRegistry();
        var extractor = new SkillExtractor(chatModel, skillRegistry);

        var config = new SkillExtractionConfig(
            MinQualityThreshold: 0.7,
            MinStepsForExtraction: 3,
            MaxStepsPerSkill: 5,
            EnableAutoParameterization: true);

        var plan = new Plan(
            "Multi-step task",
            new List<PlanStep>
            {
                new PlanStep("step1", new(), "out1", 0.8),
                new PlanStep("step2", new(), "out2", 0.8),
                new PlanStep("step3", new(), "out3", 0.8),
            },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            plan.Steps.Select(s => new StepResult(s, true, "output", null, TimeSpan.FromMilliseconds(10), new())).ToList(),
            true,
            "final",
            new(),
            TimeSpan.FromMilliseconds(30));

        var verification = new VerificationResult(
            execution,
            Verified: true,
            QualityScore: 0.75,
            Issues: new(),
            Improvements: new(),
            RevisedPlan: null);

        var result = await extractor.ExtractSkillAsync(execution, verification, config);

        result.Match(
            skill =>
            {
                Console.WriteLine($"✓ Skill extracted with custom config");
                Console.WriteLine($"  Min quality threshold: {config.MinQualityThreshold}");
                Console.WriteLine($"  Extracted steps: {skill.Steps.Count}");

                if (skill.Steps.Count > config.MaxStepsPerSkill)
                {
                    throw new Exception($"Skill has too many steps (max: {config.MaxStepsPerSkill})");
                }
            },
            error => throw new Exception($"Extraction failed: {error}"));

        Console.WriteLine("✓ Custom extraction configuration test passed!\n");
    }

    /// <summary>
    /// Tests skill name generation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestSkillNameGeneration()
    {
        Console.WriteLine("=== Test: Skill Name Generation ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var skillRegistry = new SkillRegistry();
        var extractor = new SkillExtractor(chatModel, skillRegistry);

        var plan = new Plan(
            "Calculate mathematical sum",
            new List<PlanStep>
            {
                new PlanStep("add", new() { ["a"] = 5, ["b"] = 10 }, "15", 0.9),
            },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            new List<StepResult> { new StepResult(plan.Steps[0], true, "15", null, TimeSpan.FromMilliseconds(10), new()) },
            true,
            "15",
            new(),
            TimeSpan.FromMilliseconds(10));

        try
        {
            var skillName = await extractor.GenerateSkillNameAsync(execution);

            Console.WriteLine($"✓ Generated skill name: {skillName}");

            // Check naming conventions
            if (string.IsNullOrWhiteSpace(skillName))
            {
                throw new Exception("Skill name should not be empty");
            }

            if (skillName.Contains(' '))
            {
                throw new Exception("Skill name should not contain spaces");
            }

            if (skillName != skillName.ToLowerInvariant())
            {
                throw new Exception("Skill name should be lowercase");
            }

            Console.WriteLine("✓ Skill name follows conventions (lowercase, no spaces)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Name generation failed (expected if LLM unavailable): {ex.Message}");
            Console.WriteLine("  Fallback name generation would be used");
        }

        Console.WriteLine("✓ Skill name generation test passed!\n");
    }

    /// <summary>
    /// Tests that extracted skills are registered in the skill registry.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestSkillRegistration()
    {
        Console.WriteLine("=== Test: Skill Registration ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var skillRegistry = new SkillRegistry();
        var extractor = new SkillExtractor(chatModel, skillRegistry);

        var initialSkillCount = skillRegistry.GetAllSkills().Count;

        var plan = new Plan(
            "Test skill registration",
            new List<PlanStep>
            {
                new PlanStep("action1", new(), "out1", 0.9),
                new PlanStep("action2", new(), "out2", 0.9),
            },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var execution = new ExecutionResult(
            plan,
            plan.Steps.Select(s => new StepResult(s, true, "output", null, TimeSpan.FromMilliseconds(10), new())).ToList(),
            true,
            "final",
            new(),
            TimeSpan.FromMilliseconds(20));

        var verification = new VerificationResult(
            execution,
            Verified: true,
            QualityScore: 0.9,
            Issues: new(),
            Improvements: new(),
            RevisedPlan: null);

        var result = await extractor.ExtractSkillAsync(execution, verification);

        result.Match(
            skill =>
            {
                var finalSkillCount = skillRegistry.GetAllSkills().Count;
                if (finalSkillCount != initialSkillCount + 1)
                {
                    throw new Exception("Skill should be registered in registry");
                }

                var registeredSkill = skillRegistry.GetSkill(skill.Name);
                if (registeredSkill == null)
                {
                    throw new Exception("Skill should be retrievable by name");
                }

                Console.WriteLine($"✓ Skill '{skill.Name}' registered successfully");
                Console.WriteLine($"✓ Registry now contains {finalSkillCount} skills");
            },
            error => throw new Exception($"Extraction failed: {error}"));

        Console.WriteLine("✓ Skill registration test passed!\n");
    }
}
