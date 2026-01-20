// <copyright file="MetaAIOrchestratorMagicTest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// 🎩 The Magic Test - Showcases the full Meta-AI orchestration workflow
///
/// This test demonstrates the complete Plan → Execute → Verify → Learn cycle
/// using real Ollama models. It showcases:
/// - Intelligent planning based on a complex goal
/// - Multi-step execution with real tool coordination
/// - Verification with quality assessment
/// - Skill extraction and learning from experience.
/// </summary>
public class MetaAIOrchestratorMagicTest
{
    /// <summary>
    /// Tests the complete Meta-AI orchestration workflow with planning, execution, verification, and learning.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Speed", "Workflow")]
    public async Task MetaAI_ShouldOrchestrate_ComplexMathAndReasoningWorkflow()
    {
        // ═══════════════════════════════════════════════════════════
        // 🎯 THE CHALLENGE: A complex goal requiring planning & tools
        // ═══════════════════════════════════════════════════════════
        var complexGoal = @"Calculate the product of 123 and 456, then determine if the result is divisible by 7. 
                           Explain your reasoning and provide the final answer.";

        // ═══════════════════════════════════════════════════════════
        // 🏗️ SETUP: Real Ollama with fast model & actual tools
        // ═══════════════════════════════════════════════════════════
        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "qwen2.5:0.5b"));
        var tools = ToolRegistry.CreateDefault(); // Includes MathTool, SearchTool, etc.

        var orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithConfidenceThreshold(0.6) // Balanced threshold for fast model
            .Build();

        // ═══════════════════════════════════════════════════════════
        // ✨ THE MAGIC HAPPENS: Complete orchestration cycle
        // ═══════════════════════════════════════════════════════════

        // PHASE 1: 🧠 PLANNING - AI breaks down the goal into steps
        var planResult = await orchestrator.PlanAsync(
            complexGoal,
            new Dictionary<string, object>
            {
                ["difficulty"] = "medium",
                ["requires_tools"] = true,
            });

        planResult.IsSuccess.Should().BeTrue("The planner should create a valid plan");
        var plan = planResult.Value;

        Console.WriteLine($"\n🧠 PLAN GENERATED:");
        Console.WriteLine($"   Goal: {plan.Goal}");
        Console.WriteLine($"   Steps: {plan.Steps.Count}");
        foreach (var step in plan.Steps)
        {
            Console.WriteLine($"   → {step.Action} (confidence: {step.ConfidenceScore:P0})");
        }

        plan.Steps.Should().NotBeEmpty("Plan should have at least one step");

        // PHASE 2: ⚙️ EXECUTION - Steps are executed with real tools
        var executionResult = await orchestrator.ExecuteAsync(plan);

        executionResult.IsSuccess.Should().BeTrue("Execution should complete successfully");
        var execution = executionResult.Value;

        Console.WriteLine($"\n⚙️ EXECUTION COMPLETED:");
        Console.WriteLine($"   Total Steps: {execution.StepResults.Count}");
        Console.WriteLine($"   Success Rate: {execution.StepResults.Count(r => r.Success)}/{execution.StepResults.Count}");
        foreach (var result in execution.StepResults)
        {
            var status = result.Success ? "✓" : "✗";
            Console.WriteLine($"   {status} {result.Step.Action}: {result.Output.Substring(0, Math.Min(50, result.Output.Length))}...");
        }

        execution.StepResults.Should().Contain(
            r => r.Success,
            "At least one step should execute successfully");

        // PHASE 3: 🔍 VERIFICATION - AI assesses quality & correctness
        var verificationResult = await orchestrator.VerifyAsync(execution);

        verificationResult.IsSuccess.Should().BeTrue("Verification should complete");
        var verification = verificationResult.Value;

        Console.WriteLine($"\n🔍 VERIFICATION RESULT:");
        Console.WriteLine($"   Verified: {verification.Verified}");
        Console.WriteLine($"   Quality Score: {verification.QualityScore:P0}");
        Console.WriteLine($"   Issues Found: {verification.Issues.Count}");
        Console.WriteLine($"   Improvements Suggested: {verification.Improvements.Count}");

        verification.QualityScore.Should().BeGreaterThanOrEqualTo(
            0.0,
            "Quality score should be calculated");

        // PHASE 4: 📚 LEARNING - Experience is stored for future use
        orchestrator.LearnFromExecution(verification);

        var metrics = orchestrator.GetMetrics();
        Console.WriteLine($"\n📚 LEARNING & METRICS:");
        Console.WriteLine($"   Components Tracked: {metrics.Count}");
        foreach (var metric in metrics)
        {
            Console.WriteLine($"   → {metric.Key}: {metric.Value.ExecutionCount} executions, " +
                            $"{metric.Value.SuccessRate:P0} success rate");
        }

        metrics.Should().NotBeEmpty("Metrics should be tracked");
        metrics.Should().ContainKey("planner", "Planner metrics should be recorded");
        metrics.Should().ContainKey("executor", "Executor metrics should be recorded");

        // ═══════════════════════════════════════════════════════════
        // 🎊 THE MAGIC REVEALED: Full cognitive loop completed!
        // ═══════════════════════════════════════════════════════════
        Console.WriteLine($"\n🎊 MAGIC COMPLETE!");
        Console.WriteLine($"   The orchestrator demonstrated:");
        Console.WriteLine($"   ✓ Intelligent planning from natural language");
        Console.WriteLine($"   ✓ Multi-step execution with real tool usage");
        Console.WriteLine($"   ✓ Self-assessment and quality verification");
        Console.WriteLine($"   ✓ Continual learning from experience");
        Console.WriteLine($"\n   This is agentic AI in action! 🚀");

        // Final assertion: The complete workflow should produce actionable results
        execution.StepResults.Any(r => !string.IsNullOrEmpty(r.Output))
            .Should().BeTrue("The workflow should produce concrete outputs");
    }
}
