// <copyright file="MetaAIv2Example.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Demonstrates Meta-AI v2 capabilities with plan-execute-verify loop,
/// skill acquisition, uncertainty routing, and safety guards.
/// </summary>
public static class MetaAIv2Example
{
    /// <summary>
    /// Demonstrates basic Meta-AI v2 orchestration with plan-execute-verify.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunBasicOrchestrationExample()
    {
        Console.WriteLine("=== Meta-AI v2 Basic Orchestration Example ===\n");

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
            .WithConfidenceThreshold(0.7)
            .WithDefaultPermissionLevel(PermissionLevel.Isolated)
            .Build();

        Console.WriteLine("✓ Meta-AI v2 orchestrator initialized\n");

        try
        {
            // Goal to accomplish
            string goal = "Create a detailed plan to explain functional programming concepts to a beginner";

            Console.WriteLine($"GOAL: {goal}\n");

            // Step 1: Plan
            Console.WriteLine("=== PLANNING ===");
            Result<Plan, string> planResult = await orchestrator.PlanAsync(goal);

            Plan? plan = null;
            planResult.Match(
                p =>
                {
                    plan = p;
                    Console.WriteLine($"✓ Plan created with {p.Steps.Count} steps:");
                    for (int i = 0; i < p.Steps.Count; i++)
                    {
                        Console.WriteLine($"  {i + 1}. {p.Steps[i].Action}");
                        Console.WriteLine($"     Expected: {p.Steps[i].ExpectedOutcome}");
                        Console.WriteLine($"     Confidence: {p.Steps[i].ConfidenceScore:P0}");
                    }
                },
                error =>
                {
                    Console.WriteLine($"✗ Planning failed: {error}");
                    return;
                });

            if (plan == null)
            {
                return;
            }

            // Step 2: Execute
            Console.WriteLine("\n=== EXECUTING ===");
            Result<ExecutionResult, string> execResult = await orchestrator.ExecuteAsync(plan);

            ExecutionResult? execution = null;
            execResult.Match(
                e =>
                {
                    execution = e;
                    Console.WriteLine($"✓ Execution completed in {e.Duration.TotalSeconds:F1}s");
                    Console.WriteLine($"  Status: {(e.Success ? "Success" : "Failed")}");
                    Console.WriteLine($"  Steps completed: {e.StepResults.Count}/{plan.Steps.Count}");
                    Console.WriteLine($"\nFinal Output:\n{e.FinalOutput.Substring(0, Math.Min(500, e.FinalOutput.Length))}...");
                },
                error =>
                {
                    Console.WriteLine($"✗ Execution failed: {error}");
                    return;
                });

            if (execution == null)
            {
                return;
            }

            // Step 3: Verify
            Console.WriteLine("\n=== VERIFYING ===");
            Result<VerificationResult, string> verifyResult = await orchestrator.VerifyAsync(execution);

            VerificationResult? verification = null;
            verifyResult.Match(
                v =>
                {
                    verification = v;
                    Console.WriteLine($"✓ Verification completed");
                    Console.WriteLine($"  Verified: {v.Verified}");
                    Console.WriteLine($"  Quality Score: {v.QualityScore:P0}");
                    if (v.Issues.Any())
                    {
                        Console.WriteLine($"  Issues: {string.Join(", ", v.Issues)}");
                    }

                    if (v.Improvements.Any())
                    {
                        Console.WriteLine($"  Improvements: {string.Join(", ", v.Improvements)}");
                    }
                },
                error =>
                {
                    Console.WriteLine($"✗ Verification failed: {error}");
                    return;
                });

            if (verification == null)
            {
                return;
            }

            // Step 4: Learn
            Console.WriteLine("\n=== LEARNING ===");
            orchestrator.LearnFromExecution(verification);
            Console.WriteLine("✓ Experience stored in memory for future use");

            // Show metrics
            Console.WriteLine("\n=== METRICS ===");
            IReadOnlyDictionary<string, PerformanceMetrics> metrics = orchestrator.GetMetrics();
            foreach ((string component, PerformanceMetrics metric) in metrics)
            {
                Console.WriteLine($"{component}:");
                Console.WriteLine($"  Executions: {metric.ExecutionCount}");
                Console.WriteLine($"  Success Rate: {metric.SuccessRate:P0}");
                Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs:F0}ms");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Example skipped (Ollama not available)");
            }
            else
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Demonstrates skill acquisition and reuse.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunSkillAcquisitionExample()
    {
        Console.WriteLine("\n=== Meta-AI v2 Skill Acquisition Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        // Create components
        MemoryStore memory = new MemoryStore(embedModel, new TrackedVectorStore());
        SkillRegistry skills = new SkillRegistry(embedModel);

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .WithMemoryStore(memory)
            .WithSkillRegistry(skills)
            .Build();

        Console.WriteLine("✓ Orchestrator with skill registry initialized\n");

        try
        {
            // First task - will be learned as a skill
            string goal1 = "Explain what a monad is in functional programming";
            Console.WriteLine($"TASK 1: {goal1}");

            VerificationResult? result1 = await ExecuteFullCycle(orchestrator, goal1);

            if (result1 != null && result1.Verified && result1.QualityScore > 0.8)
            {
                // Extract skill
                Result<Skill, string> skillResult = await skills.ExtractSkillAsync(
                    result1.Execution,
                    "explain_monad",
                    "Explains functional programming concepts like monads");

                skillResult.Match(
                    skill => Console.WriteLine($"✓ Skill extracted: {skill.Name}\n"),
                    error => Console.WriteLine($"  Could not extract skill: {error}\n"));
            }

            // Second task - similar to first, should use learned skill
            string goal2 = "Explain functional programming pattern: functors";
            Console.WriteLine($"\nTASK 2: {goal2}");

            List<Skill> matchingSkills = await skills.FindMatchingSkillsAsync(goal2);
            if (matchingSkills.Any())
            {
                Console.WriteLine($"✓ Found {matchingSkills.Count} matching skills to reuse:");
                foreach (Skill? skill in matchingSkills.Take(3))
                {
                    Console.WriteLine($"  - {skill.Name} (success rate: {skill.SuccessRate:P0})");
                }
            }

            await ExecuteFullCycle(orchestrator, goal2);

            Console.WriteLine("\n✓ Skill acquisition example completed");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Example skipped (Ollama not available)");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Demonstrates evaluation harness for benchmarking.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunEvaluationExample()
    {
        Console.WriteLine("\n=== Meta-AI v2 Evaluation Example ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
            .WithLLM(chatModel)
            .WithTools(tools)
            .Build();

        EvaluationHarness harness = new EvaluationHarness(orchestrator);

        Console.WriteLine("✓ Evaluation harness initialized\n");

        try
        {
            Console.WriteLine("Running benchmark suite...\n");
            EvaluationResults results = await harness.RunBenchmarkAsync();

            Console.WriteLine("=== BENCHMARK RESULTS ===");
            Console.WriteLine($"Total Tests: {results.TotalTests}");
            Console.WriteLine($"Successful: {results.SuccessfulTests} ({results.SuccessfulTests / (double)results.TotalTests:P0})");
            Console.WriteLine($"Failed: {results.FailedTests}");
            Console.WriteLine($"Average Quality: {results.AverageQualityScore:P0}");
            Console.WriteLine($"Average Confidence: {results.AverageConfidence:P0}");
            Console.WriteLine($"Average Execution Time: {results.AverageExecutionTime.TotalMilliseconds:F0}ms");

            Console.WriteLine("\n=== INDIVIDUAL RESULTS ===");
            foreach (EvaluationMetrics test in results.TestResults)
            {
                string status = test.Success ? "✓" : "✗";
                Console.WriteLine($"{status} {test.TestCase}:");
                Console.WriteLine($"  Quality: {test.QualityScore:P0}, Time: {test.ExecutionTime.TotalMilliseconds:F0}ms");
            }

            Console.WriteLine("\n✓ Evaluation completed");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("⚠ Example skipped (Ollama not available)");
            }
            else
            {
                throw;
            }
        }
    }

    private static async Task<VerificationResult?> ExecuteFullCycle(
        IMetaAIPlannerOrchestrator orchestrator,
        string goal)
    {
        Result<Plan, string> planResult = await orchestrator.PlanAsync(goal);
        if (!planResult.IsSuccess)
        {
            return null;
        }

        Plan? plan = planResult.Match(p => p, _ => (Plan?)null);
        if (plan == null)
        {
            return null;
        }

        Result<ExecutionResult, string> execResult = await orchestrator.ExecuteAsync(plan);
        if (!execResult.IsSuccess)
        {
            return null;
        }

        ExecutionResult? execution = execResult.Match(e => e, _ => (ExecutionResult?)null);
        if (execution == null)
        {
            return null;
        }

        Result<VerificationResult, string> verifyResult = await orchestrator.VerifyAsync(execution);
        if (!verifyResult.IsSuccess)
        {
            return null;
        }

        VerificationResult? verification = verifyResult.Match(v => v, _ => (VerificationResult?)null);
        if (verification != null)
        {
            orchestrator.LearnFromExecution(verification);
        }

        return verification;
    }

    /// <summary>
    /// Runs all Meta-AI v2 examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        await RunBasicOrchestrationExample();
        await RunSkillAcquisitionExample();
        await RunEvaluationExample();
    }
}
