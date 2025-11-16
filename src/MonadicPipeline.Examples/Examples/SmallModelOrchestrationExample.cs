// <copyright file="SmallModelOrchestrationExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.CLI;

/// <summary>
/// Demonstrates efficient orchestration of complex tasks using small, specialized models.
/// Shows how combining multiple lightweight models (phi3, qwen, deepseek) can handle
/// sophisticated workflows that would typically require large, expensive models.
/// </summary>
public static class SmallModelOrchestrationExample
{
    /// <summary>
    /// Demonstrates a complex code review workflow using small models.
    /// This example shows how to break down a complex task (code review)
    /// into specialized sub-tasks handled by different small models.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunCodeReviewOrchestrationExample()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Complex Task Orchestration with Small Models Example           ║");
        Console.WriteLine("║   Task: Comprehensive Code Review                                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This example demonstrates how to orchestrate a complex code review");
        Console.WriteLine("using multiple small, specialized models instead of one large model.");
        Console.WriteLine();
        Console.WriteLine("Models Used:");
        Console.WriteLine("  • phi3:mini (2.3GB)        - Overall coordination & summaries");
        Console.WriteLine("  • deepseek-coder:1.3b (800MB) - Code analysis & bug detection");
        Console.WriteLine("  • qwen2.5:3b (2GB)         - Architecture reasoning & best practices");
        Console.WriteLine();
        Console.WriteLine("Total: ~5GB vs. GPT-5 equivalent: 175GB+");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

        try
        {
            // Setup small, specialized models
            OllamaProvider provider = new OllamaProvider();

            // General purpose model for coordination (phi3:mini - 2.3GB)
            OllamaChatAdapter generalModel = new OllamaChatAdapter(new OllamaChatModel(provider, "phi3"));

            // Code-specialized model for syntax/bug detection (deepseek-coder:1.3b - 800MB)
            OllamaChatAdapter coderModel = new OllamaChatAdapter(new OllamaChatModel(provider, "deepseek-coder:1.3b"));

            // Reasoning model for architecture decisions (qwen2.5:3b - 2GB)
            OllamaChatAdapter reasoningModel = new OllamaChatAdapter(new OllamaChatModel(provider, "qwen2.5:3b"));

            // Setup tools for code analysis
            ToolRegistry tools = ToolRegistry.CreateDefault();
            Console.WriteLine($"✓ Initialized {tools.Count} analysis tools\n");

            // Build orchestrator with specialized small models
            OrchestratorBuilder orchestratorBuilder = new OrchestratorBuilder(tools, "general")
                .WithModel(
                    "general",
                    generalModel,
                    ModelType.General,
                    new[] { "coordination", "summary", "general-purpose" },
                    maxTokens: 2048,
                    avgLatencyMs: 800)
                .WithModel(
                    "coder",
                    coderModel,
                    ModelType.Code,
                    new[] { "code", "syntax", "bugs", "programming", "debugging" },
                    maxTokens: 4096,
                    avgLatencyMs: 1200)
                .WithModel(
                    "reasoner",
                    reasoningModel,
                    ModelType.Reasoning,
                    new[] { "architecture", "design", "patterns", "best-practices", "reasoning" },
                    maxTokens: 3072,
                    avgLatencyMs: 1000)
                .WithMetricTracking(true);

            OrchestratedChatModel orchestrator = orchestratorBuilder.Build();
            IModelOrchestrator underlyingOrchestrator = orchestratorBuilder.GetOrchestrator();

            Console.WriteLine("✓ Orchestrator configured with 3 specialized small models\n");

            // Example code to review (intentionally has issues)
            string codeToReview = @"
def process_user_data(user_id):
    # Fetch user from database
    user = db.query('SELECT * FROM users WHERE id = ' + user_id)
    
    # Calculate score
    score = 0
    for item in user.items:
        score = score + item.value
    
    # Update database
    db.execute('UPDATE users SET score = ' + str(score) + ' WHERE id = ' + user_id)
    
    return score
";

            Console.WriteLine("Step 1: Syntax & Bug Detection (deepseek-coder:1.3b)");
            Console.WriteLine("───────────────────────────────────────────────────────");
            string syntaxPrompt = $"Analyze this Python code for syntax errors and security vulnerabilities:\n\n{codeToReview}";

            string syntaxAnalysis = await orchestrator.GenerateTextAsync(syntaxPrompt);
            Console.WriteLine($"Analysis: {TruncateOutput(syntaxAnalysis, 300)}\n");

            Console.WriteLine("Step 2: Architecture Review (qwen2.5:3b)");
            Console.WriteLine("───────────────────────────────────────────────────────");
            string architecturePrompt = $"Review this code's architecture and suggest improvements following best practices:\n\n{codeToReview}";

            string architectureReview = await orchestrator.GenerateTextAsync(architecturePrompt);
            Console.WriteLine($"Review: {TruncateOutput(architectureReview, 300)}\n");

            Console.WriteLine("Step 3: Summary & Recommendations (phi3:mini)");
            Console.WriteLine("───────────────────────────────────────────────────────");
            string summaryPrompt = $"Summarize the key issues and provide actionable recommendations based on:\n\nSyntax Analysis:\n{syntaxAnalysis}\n\nArchitecture Review:\n{architectureReview}";

            string summary = await orchestrator.GenerateTextAsync(summaryPrompt);
            Console.WriteLine($"Summary: {TruncateOutput(summary, 400)}\n");

            // Show performance metrics
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine("Performance Metrics");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");

            IReadOnlyDictionary<string, PerformanceMetrics> metrics = underlyingOrchestrator.GetMetrics();

            foreach ((string modelName, PerformanceMetrics metric) in metrics)
            {
                Console.WriteLine($"\n{modelName} Model:");
                Console.WriteLine($"  • Executions: {metric.ExecutionCount}");
                Console.WriteLine($"  • Avg Latency: {metric.AverageLatencyMs:F0}ms");
                Console.WriteLine($"  • Success Rate: {metric.SuccessRate:P0}");
            }

            Console.WriteLine("\n\n✨ Orchestration Complete!");
            Console.WriteLine("\n💡 Key Takeaways:");
            Console.WriteLine("   • Used 3 small models (~5GB total) vs 1 large model (175GB+)");
            Console.WriteLine("   • Each model specialized for specific aspects of the task");
            Console.WriteLine("   • Achieved comprehensive review through intelligent orchestration");
            Console.WriteLine("   • Cost-effective and faster than using a single large model");
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
        {
            Console.WriteLine("\n⚠️  Error: Ollama is not running or models are not installed.");
            Console.WriteLine("\nTo run this example:");
            Console.WriteLine("1. Start Ollama: ollama serve");
            Console.WriteLine("2. Pull required models:");
            Console.WriteLine("   ollama pull phi3:mini");
            Console.WriteLine("   ollama pull deepseek-coder:1.3b");
            Console.WriteLine("   ollama pull qwen2.5:3b");
            Console.WriteLine("\nOr run the guided setup:");
            Console.WriteLine("   dotnet run -- setup --all");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            if (Environment.GetEnvironmentVariable("DEBUG") == "1")
            {
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// Demonstrates a multi-step research task orchestration using small models.
    /// Shows how to coordinate information gathering, analysis, and synthesis
    /// across multiple specialized models.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunResearchOrchestrationExample()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Research Task Orchestration Example                             ║");
        Console.WriteLine("║   Task: AI Safety Analysis                                        ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Demonstrating coordinated research using small models:");
        Console.WriteLine("  • Data gathering (phi3)");
        Console.WriteLine("  • Deep analysis (qwen2.5:3b)");
        Console.WriteLine("  • Synthesis & conclusions (phi3)");
        Console.WriteLine();

        try
        {
            OllamaProvider provider = new OllamaProvider();
            OllamaChatAdapter generalModel = new OllamaChatAdapter(new OllamaChatModel(provider, "phi3"));
            OllamaChatAdapter reasoningModel = new OllamaChatAdapter(new OllamaChatModel(provider, "qwen2.5:3b"));

            ToolRegistry tools = ToolRegistry.CreateDefault();

            OrchestratorBuilder orchestratorBuilder = new OrchestratorBuilder(tools, "general")
                .WithModel("general", generalModel, ModelType.General,
                    new[] { "data-gathering", "synthesis", "summary" }, 2048, 800)
                .WithModel("reasoner", reasoningModel, ModelType.Reasoning,
                    new[] { "analysis", "reasoning", "evaluation", "critical-thinking" }, 3072, 1000)
                .WithMetricTracking(true);

            OrchestratedChatModel orchestrator = orchestratorBuilder.Build();

            Console.WriteLine("✓ Research orchestrator initialized\n");

            string researchTopic = "AI alignment and value alignment challenges";

            Console.WriteLine($"Research Topic: {researchTopic}\n");

            // Phase 1: Information Gathering
            Console.WriteLine("Phase 1: Information Gathering (phi3)");
            Console.WriteLine("─────────────────────────────────────");
            string gatherPrompt = $"List the key concepts and challenges related to: {researchTopic}";
            string keyPoints = await orchestrator.GenerateTextAsync(gatherPrompt);
            Console.WriteLine($"{TruncateOutput(keyPoints, 250)}\n");

            // Phase 2: Deep Analysis
            Console.WriteLine("Phase 2: Deep Analysis (qwen2.5:3b)");
            Console.WriteLine("─────────────────────────────────────");
            string analysisPrompt = $"Analyze the following points in depth and identify critical challenges:\n\n{keyPoints}";
            string analysis = await orchestrator.GenerateTextAsync(analysisPrompt);
            Console.WriteLine($"{TruncateOutput(analysis, 250)}\n");

            // Phase 3: Synthesis
            Console.WriteLine("Phase 3: Synthesis & Conclusions (phi3)");
            Console.WriteLine("─────────────────────────────────────");
            string synthesisPrompt = $"Synthesize the following analysis into key conclusions and recommendations:\n\n{analysis}";
            string synthesis = await orchestrator.GenerateTextAsync(synthesisPrompt);
            Console.WriteLine($"{TruncateOutput(synthesis, 300)}\n");

            Console.WriteLine("✨ Research orchestration complete!\n");
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused"))
        {
            Console.WriteLine("\n⚠️  Ollama not running. Run 'dotnet run -- setup --ollama' for installation help.");
        }
    }

    /// <summary>
    /// Truncates output for display purposes.
    /// </summary>
    private static string TruncateOutput(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Runs all small model orchestration examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n🚀 Starting Small Model Orchestration Examples\n");

        await RunCodeReviewOrchestrationExample();

        Console.WriteLine("\n" + new string('═', 70) + "\n");

        await RunResearchOrchestrationExample();

        Console.WriteLine("\n✅ All examples completed!\n");
    }
}
