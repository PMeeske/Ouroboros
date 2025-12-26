// <copyright file="MeTTaIntegrationExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using LangChain.Providers.Ollama;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Providers;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Demonstrates MeTTa symbolic reasoning integration with Ouroboros.
/// Shows how to use MeTTa for symbolic querying, rule application, and plan verification.
/// </summary>
public static class MeTTaIntegrationExample
{
    /// <summary>
    /// Demonstrates basic MeTTa symbolic reasoning capabilities.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunBasicMeTTaExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   MeTTa Symbolic Reasoning - Basic Example          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        // Create a subprocess-based MeTTa engine (default)
        // Note: Requires 'metta' executable in PATH
        using SubprocessMeTTaEngine engine = new SubprocessMeTTaEngine();

        Console.WriteLine("Step 1: Adding facts to the knowledge base");
        Console.WriteLine("─────────────────────────────────────────────────────");

        // Add some facts
        string[] facts = new[]
        {
            "(human Socrates)",
            "(human Plato)",
            "(mortal $x) :- (human $x)",
        };

        foreach (string? fact in facts)
        {
            Result<Unit, string> result = await engine.AddFactAsync(fact);
            result.Match(
                _ => Console.WriteLine($"✓ Added: {fact}"),
                error => Console.WriteLine($"✗ Failed to add fact: {error}"));
        }

        Console.WriteLine("\nStep 2: Querying the knowledge base");
        Console.WriteLine("─────────────────────────────────────────────────────");

        string query = "!(match &self (mortal Socrates) $result)";
        Result<string, string> queryResult = await engine.ExecuteQueryAsync(query);

        queryResult.Match(
            success => Console.WriteLine($"✓ Query result: {success}"),
            error => Console.WriteLine($"✗ Query failed: {error}"));

        Console.WriteLine("\nStep 3: Applying inference rules");
        Console.WriteLine("─────────────────────────────────────────────────────");

        string rule = "!(match &self (human $x) (mortal $x))";
        Result<string, string> ruleResult = await engine.ApplyRuleAsync(rule);

        ruleResult.Match(
            success => Console.WriteLine($"✓ Rule result: {success}"),
            error => Console.WriteLine($"✗ Rule failed: {error}"));

        Console.WriteLine("\n✓ Basic MeTTa example completed!\n");
    }

    /// <summary>
    /// Demonstrates MeTTa integration with the tool system.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunMeTTaToolsExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   MeTTa Tools Integration Example                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        // Create tools registry with MeTTa tools
        SubprocessMeTTaEngine engine = new SubprocessMeTTaEngine();
        ToolRegistry tools = ToolRegistry.CreateDefault()
            .WithMeTTaTools(engine);

        Console.WriteLine($"Tool Registry: {tools.Count} tools registered");
        Console.WriteLine("\nMeTTa Tools:");
        Console.WriteLine("─────────────────────────────────────────────────────");

        List<ITool> mettaTools = tools.All.Where(t => t.Name.StartsWith("metta_")).ToList();
        foreach (ITool? tool in mettaTools)
        {
            Console.WriteLine($"  • {tool.Name}");
            Console.WriteLine($"    {tool.Description}");
        }

        Console.WriteLine("\nUsing MeTTa tools:");
        Console.WriteLine("─────────────────────────────────────────────────────");

        // Use the query tool
        Option<ITool> queryTool = tools.GetTool("metta_query");
        if (queryTool.HasValue)
        {
            ITool tool = queryTool.GetValueOrDefault(null!);
            Result<string, string> result = await tool.InvokeAsync("(+ 2 3)");
            result.Match(
                success => Console.WriteLine($"✓ metta_query result: {success}"),
                error => Console.WriteLine($"Note: {error}"));
        }
        else
        {
            Console.WriteLine("Query tool not found");
        }

        // Use the fact tool
        Option<ITool> factTool = tools.GetTool("metta_add_fact");
        if (factTool.HasValue)
        {
            ITool tool = factTool.GetValueOrDefault(null!);
            Result<string, string> result = await tool.InvokeAsync("(likes Alice coding)");
            result.Match(
                success => Console.WriteLine($"✓ metta_add_fact: {success}"),
                error => Console.WriteLine($"Note: {error}"));
        }
        else
        {
            Console.WriteLine("Fact tool not found");
        }

        engine.Dispose();
        Console.WriteLine("\n✓ MeTTa tools example completed!\n");
    }

    /// <summary>
    /// Demonstrates HTTP-based MeTTa client for Python Hyperon service.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunHttpMeTTaExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   HTTP MeTTa Client Example (Python Hyperon)        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        // Configure HTTP client for Python Hyperon service
        string serviceUrl = "http://localhost:8000"; // Default Python service URL
        string? apiKey = Environment.GetEnvironmentVariable("METTA_API_KEY");

        Console.WriteLine($"Connecting to MeTTa service at: {serviceUrl}");
        Console.WriteLine($"API Key configured: {!string.IsNullOrEmpty(apiKey)}");
        Console.WriteLine();

        using HttpMeTTaEngine engine = new HttpMeTTaEngine(serviceUrl, apiKey);

        // Try a simple query
        Result<string, string> result = await engine.ExecuteQueryAsync("(+ 1 1)");

        result.Match(
            success => Console.WriteLine($"✓ HTTP query succeeded: {success}"),
            error => Console.WriteLine($"Note: Service not available - {error.Substring(0, Math.Min(60, error.Length))}..."));

        Console.WriteLine("\n✓ HTTP MeTTa example completed!");
        Console.WriteLine("  To use this example, start a Python Hyperon service on port 8000\n");
    }

    /// <summary>
    /// Demonstrates integrating MeTTa with Meta-AI orchestrator.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunMeTTaOrchestratorExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   MeTTa + Meta-AI Orchestrator Integration          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        try
        {
            // Setup LLM and tools
            OllamaProvider provider = new OllamaProvider();
            OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
            OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

            // Create tools with MeTTa integration
            SubprocessMeTTaEngine engine = new SubprocessMeTTaEngine();
            ToolRegistry tools = ToolRegistry.CreateDefault()
                .WithMeTTaTools(engine);

            Console.WriteLine($"✓ Tool registry initialized with {tools.Count} tools");

            // Build Meta-AI orchestrator with MeTTa tools
            MetaAIPlannerOrchestrator orchestrator = MetaAIBuilder.CreateDefault()
                .WithLLM(chatModel)
                .WithTools(tools)
                .WithEmbedding(embedModel)
                .Build();

            Console.WriteLine("✓ Meta-AI orchestrator with MeTTa tools initialized\n");

            // Example: Use MeTTa for plan verification
            Console.WriteLine("Scenario: Symbolic plan verification");
            Console.WriteLine("─────────────────────────────────────────────────────");

            string goal = "Analyze whether a plan to teach functional programming is valid";
            Console.WriteLine($"Goal: {goal}\n");

            // Add domain knowledge to MeTTa
            await engine.AddFactAsync("(teaches functional-programming requires (prerequisite basic-programming))");
            await engine.AddFactAsync("(teaches functional-programming requires (concept higher-order-functions))");
            await engine.AddFactAsync("(teaches functional-programming requires (concept immutability))");

            Console.WriteLine("✓ Domain knowledge added to MeTTa");

            // The orchestrator can now use MeTTa tools for symbolic reasoning
            Result<Plan, string> planResult = await orchestrator.PlanAsync(goal);

            planResult.Match(
                plan =>
                {
                    Console.WriteLine($"\n✓ Plan created with {plan.Steps.Count} steps");
                    Console.WriteLine("  (Plan can be verified using metta_verify_plan tool)");
                },
                error => Console.WriteLine($"Note: Planning skipped - {error}"));

            engine.Dispose();
            Console.WriteLine("\n✓ Orchestrator integration example completed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("Note: Ollama not available - example skipped");
                Console.WriteLine("  Install Ollama to run this example\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Demonstrates bridging orchestrator memory to MeTTa facts.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunMemoryBridgeExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   MeTTa Memory Bridge Example                       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        try
        {
            // Setup components
            OllamaProvider provider = new OllamaProvider();
            OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

            MemoryStore memory = new MemoryStore(embedModel);
            SubprocessMeTTaEngine engine = new SubprocessMeTTaEngine();

            Console.WriteLine("✓ Memory store and MeTTa engine initialized");

            // Create the bridge
            MeTTaMemoryBridge bridge = memory.CreateMeTTaBridge(engine);

            Console.WriteLine("✓ Memory bridge created\n");

            // Sync memory to MeTTa
            Console.WriteLine("Syncing orchestrator memory to MeTTa...");
            Result<int, string> syncResult = await bridge.SyncAllExperiencesAsync();

            syncResult.Match(
                count => Console.WriteLine($"✓ Synchronized {count} facts from memory to MeTTa"),
                error => Console.WriteLine($"Note: {error}"));

            // Query experiences using MeTTa
            Console.WriteLine("\nQuerying experiences with symbolic reasoning:");
            Result<string, string> queryResult = await bridge.QueryExperiencesAsync("!(match &self (memory-stats $total $quality) $result)");

            queryResult.Match(
                result => Console.WriteLine($"✓ Query result: {result}"),
                error => Console.WriteLine($"Note: {error}"));

            engine.Dispose();
            Console.WriteLine("\n✓ Memory bridge example completed!\n");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("Note: Ollama not available - example skipped\n");
            }
            else
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Runs all MeTTa integration examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n");
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                      ║");
        Console.WriteLine("║    Ouroboros - MeTTa Integration Examples      ║");
        Console.WriteLine("║                                                      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();

        await RunBasicMeTTaExample();
        await RunMeTTaToolsExample();
        await RunHttpMeTTaExample();
        await RunMeTTaOrchestratorExample();
        await RunMemoryBridgeExample();

        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   All MeTTa integration examples completed!          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");
    }
}
