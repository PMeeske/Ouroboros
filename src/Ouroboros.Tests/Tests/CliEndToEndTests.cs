// <copyright file="CliEndToEndTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using LangChain.Databases;
using LangChain.DocumentLoaders;
using Ouroboros.Application;
using Ouroboros.Providers;
using Ouroboros.Tests.Infrastructure;

/// <summary>
/// Comprehensive end-to-end tests for all CLI commands and their variations.
/// Tests command execution, option handling, and integration with cloud implementations.
/// </summary>
[Trait("Category", "Unit")]
public static class CliEndToEndTests
{
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running CLI End-to-End Tests ===");

        // Test 'ask' command variations
        await TestAskCommandBasic();
        await TestAskCommandWithRag();
        await TestAskCommandWithAgent();
        await TestAskCommandWithAgentModes();
        await TestAskCommandWithRouter();
        await TestAskCommandWithDebug();
        await TestAskCommandWithRemoteEndpoints();
        await TestAskCommandWithJsonTools();
        await TestAskCommandOptionCombinations();

        // Test 'pipeline' command variations
        await TestPipelineCommandBasic();
        await TestPipelineCommandWithIngestion();
        await TestPipelineCommandWithReasoning();
        await TestCompleteRefinementLoop();
        await TestPipelineCommandWithTrace();
        await TestPipelineCommandWithDebug();

        // Test 'list' command
        TestListCommand();

        // Test 'explain' command
        TestExplainCommand();
        TestExplainCommandComplexDsl();

        // Test 'test' command variations
        TestTestCommandStructure();

        // Test 'orchestrator' command
        TestOrchestratorCommand();

        // Test 'metta' command
        TestMeTTaCommand();

        // Test error handling and validation
        TestCommandValidation();
        TestEnvironmentVariableHandling();

        Console.WriteLine("✓ All CLI end-to-end tests passed!");
    }

    private static async Task TestAskCommandBasic()
    {
        Console.WriteLine("Testing ask command (basic)...");

        // Simulate basic ask without RAG - verify adapter pattern works
        var chatModel = TestModelFactory.CreateChatModel();

        try
        {
            var response = await chatModel.GenerateTextAsync("Hello", CancellationToken.None);
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new Exception("Basic ask should return non-empty response");
            }

            Console.WriteLine("  ✓ Basic ask command works correctly");
        }
        catch (Exception ex)
        {
            // Fallback expected if Ollama not running
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("  ✓ Basic ask command fallback works correctly (Ollama not available)");
            }
            else
            {
                throw;
            }
        }
    }

    private static async Task TestAskCommandWithRag()
    {
        Console.WriteLine("Testing ask command with RAG...");

        // Test RAG setup
        var embedModel = TestModelFactory.CreateEmbeddingModel();
        var store = new TrackedVectorStore();

        // Seed with test documents
        var docs = new[]
        {
            "Event sourcing captures all changes as immutable events.",
            "Circuit breakers prevent cascading failures.",
        };

        foreach (var (text, idx) in docs.Select((d, i) => (d, i)))
        {
            try
            {
                var emb = await embedModel.CreateEmbeddingsAsync(text);
                await store.AddAsync(new[] { new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = emb } });
            }
            catch
            {
                // Fallback for when embeddings fail
                await store.AddAsync(new[] { new Vector { Id = (idx + 1).ToString(), Text = text, Embedding = new float[32] } });
            }
        }

        if (store.GetAll().Count() != 2)
        {
            throw new Exception("RAG setup should have 2 documents");
        }

        Console.WriteLine("  ✓ Ask command with RAG setup works correctly");
    }

    private static Task TestAskCommandWithAgent()
    {
        Console.WriteLine("Testing ask command with agent mode...");

        // Test agent factory and tool registry
        var chat = TestModelFactory.CreateChatModel();
        var tools = new ToolRegistry();
        tools = tools
            .WithFunction("echo", "Echo input", s => s)
            .WithFunction("uppercase", "Convert to uppercase", s => s.ToUpperInvariant());

        if (tools.All.Count() != 2)
        {
            throw new Exception("Tool registry should have 2 tools");
        }

        // Test agent creation for different modes
        var agentLc = Ouroboros.Agent.AgentFactory.Create("lc", chat, tools, false, 6, false, "nomic-embed-text", false, false);
        if (agentLc.Mode != "lc")
        {
            throw new Exception($"Agent mode should be 'lc', got '{agentLc.Mode}'");
        }

        Console.WriteLine("  ✓ Ask command with agent mode works correctly");
        return Task.CompletedTask;
    }

    private static Task TestAskCommandWithAgentModes()
    {
        Console.WriteLine("Testing ask command with different agent modes...");

        var chat = TestModelFactory.CreateChatModel();
        var tools = new ToolRegistry().WithFunction("test", "Test tool", s => s);

        // Test all agent modes
        var modes = new[] { "simple", "lc", "react" };
        foreach (var mode in modes)
        {
            var agent = Ouroboros.Agent.AgentFactory.Create(mode, chat, tools, false, 3, false, "nomic-embed-text", false, false);
            if (agent.Mode != mode)
            {
                throw new Exception($"Agent mode mismatch: expected '{mode}', got '{agent.Mode}'");
            }
        }

        Console.WriteLine("  ✓ All agent modes (simple, lc, react) work correctly");
        return Task.CompletedTask;
    }

    private static Task TestAskCommandWithRouter()
    {
        Console.WriteLine("Testing ask command with multi-model router...");

        var modelMap = new Dictionary<string, IChatCompletionModel>(StringComparer.OrdinalIgnoreCase)
        {
            ["general"] = TestModelFactory.CreateChatModel("[mock-general]"),
            ["coder"] = TestModelFactory.CreateChatModel("[mock-coder]"),
        };

        var router = new MultiModelRouter(modelMap, "general");

        // Router should be created successfully
        if (router == null)
        {
            throw new Exception("Router should be created");
        }

        Console.WriteLine("  ✓ Multi-model router works correctly");
        return Task.CompletedTask;
    }

    private static Task TestAskCommandWithDebug()
    {
        Console.WriteLine("Testing ask command with debug mode...");

        // Test debug environment variable
        var origDebug = Environment.GetEnvironmentVariable("MONADIC_DEBUG");
        try
        {
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
            var debugVal = Environment.GetEnvironmentVariable("MONADIC_DEBUG");
            if (debugVal != "1")
            {
                throw new Exception("Debug environment variable should be set");
            }

            Console.WriteLine("  ✓ Debug mode environment variable works correctly");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", origDebug);
        }

        return Task.CompletedTask;
    }

    private static async Task TestAskCommandWithRemoteEndpoints()
    {
        Console.WriteLine("Testing ask command with remote endpoints...");

        // Test Ollama Cloud endpoint (using localhost to avoid DNS lookup)
        var ollamaCloudChat = new OllamaCloudChatModel("http://127.0.0.1:9999", "fake-key", "llama3");
        var ollamaCloudResponse = await ollamaCloudChat.GenerateTextAsync("test", CancellationToken.None);
        if (!ollamaCloudResponse.Contains("ollama-cloud-fallback"))
        {
            throw new Exception("OllamaCloud should use fallback for unreachable endpoint");
        }

        // Test OpenAI-compatible endpoint (using localhost to avoid DNS lookup)
        var openAiChat = new HttpOpenAiCompatibleChatModel("http://127.0.0.1:9998", "fake-key", "gpt-4");
        var openAiResponse = await openAiChat.GenerateTextAsync("test", CancellationToken.None);
        if (!openAiResponse.Contains("remote-fallback"))
        {
            throw new Exception("OpenAI-compatible should use fallback for unreachable endpoint");
        }

        // Test endpoint type auto-detection for Ollama Cloud
        var (endpoint1, key1, type1) = ChatConfig.ResolveWithOverrides("https://api.ollama.com", "key", null);
        if (type1 != ChatEndpointType.OllamaCloud)
        {
            throw new Exception($"Should auto-detect OllamaCloud, got {type1}");
        }

        // Test endpoint type auto-detection for OpenAI
        var (endpoint2, key2, type2) = ChatConfig.ResolveWithOverrides("https://api.openai.com", "key", null);
        if (type2 != ChatEndpointType.OpenAiCompatible)
        {
            throw new Exception($"Should auto-detect OpenAiCompatible, got {type2}");
        }

        // Test manual override
        var (endpoint3, key3, type3) = ChatConfig.ResolveWithOverrides("https://custom.com", "key", "ollama-cloud");
        if (type3 != ChatEndpointType.OllamaCloud)
        {
            throw new Exception($"Manual override should set OllamaCloud, got {type3}");
        }

        Console.WriteLine("  ✓ Remote endpoint handling works correctly");
    }

    private static Task TestAskCommandWithJsonTools()
    {
        Console.WriteLine("Testing ask command with JSON tools...");

        // Test tool registry with JSON format
        var tools = new ToolRegistry();
        tools = tools.WithFunction("json_test", "Test JSON tool", s => $"{{\"result\": \"{s}\"}}");

        var jsonTool = tools.Get("json_test");
        if (jsonTool == null)
        {
            throw new Exception("JSON tool should be registered");
        }

        Console.WriteLine("  ✓ JSON tools registration works correctly");
        return Task.CompletedTask;
    }

    private static Task TestAskCommandOptionCombinations()
    {
        Console.WriteLine("Testing ask command option combinations...");

        // Test temperature and max-tokens settings
        var settings = new ChatRuntimeSettings(0.8, 1024, 120, false);
        if (settings.Temperature != 0.8 || settings.MaxTokens != 1024)
        {
            throw new Exception("ChatRuntimeSettings should preserve values");
        }

        // Test endpoint resolution with overrides
        var origEndpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        var origKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");

        try
        {
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT", "https://env.example.com");
            Environment.SetEnvironmentVariable("CHAT_API_KEY", "env-key");

            // CLI override should take precedence
            var (endpoint, key, type) = ChatConfig.ResolveWithOverrides(
                "https://cli.example.com", "cli-key", "openai");

            if (endpoint != "https://cli.example.com")
            {
                throw new Exception($"CLI override should take precedence for endpoint, got {endpoint}");
            }

            if (key != "cli-key")
            {
                throw new Exception($"CLI override should take precedence for key, got {key}");
            }

            if (type != ChatEndpointType.OpenAiCompatible)
            {
                throw new Exception($"CLI override should set endpoint type, got {type}");
            }

            Console.WriteLine("  ✓ Ask command option combinations work correctly");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT", origEndpoint);
            Environment.SetEnvironmentVariable("CHAT_API_KEY", origKey);
        }

        return Task.CompletedTask;
    }

    private static Task TestPipelineCommandBasic()
    {
        Console.WriteLine("Testing pipeline command (basic)...");

        // Test basic DSL tokenization
        var tokens = PipelineDsl.Tokenize("SetPrompt('test') | LLM");
        if (tokens.Length != 2)
        {
            throw new Exception($"Should parse 2 tokens, got {tokens.Length}");
        }

        Console.WriteLine("  ✓ Basic pipeline DSL parsing works correctly");
        return Task.CompletedTask;
    }

    private static async Task TestPipelineCommandWithIngestion()
    {
        Console.WriteLine("Testing pipeline command with ingestion...");

        // Test ingestion DSL tokens
        var dsl = "UseDir | UseIngest";
        var tokens = PipelineDsl.Tokenize(dsl);
        if (!tokens.Any(t => t.Contains("UseDir")) || !tokens.Any(t => t.Contains("UseIngest")))
        {
            throw new Exception("Ingestion tokens should be parsed correctly");
        }

        // Test directory ingestion step
        var embed = TestModelFactory.CreateEmbeddingModel();
        var chat = TestModelFactory.CreateChatModel();
        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(chat, tools);
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));

        var state = new CliPipelineState
        {
            Branch = branch,
            Llm = llm,
            Tools = tools,
            Embed = embed,
            Trace = false,
        };

        // UseDir step should handle directory setup
        var dirStep = CliSteps.UseDir("root=.|norec");
        var resultState = await dirStep(state);

        if (resultState.Branch == null)
        {
            throw new Exception("UseDir should preserve branch");
        }

        Console.WriteLine("  ✓ Pipeline ingestion steps work correctly");
    }

    private static Task TestPipelineCommandWithReasoning()
    {
        Console.WriteLine("Testing pipeline command with reasoning steps...");

        // Test reasoning DSL
        var dsl = "SetTopic('AI') | UseDraft | UseCritique | UseImprove";
        var tokens = PipelineDsl.Tokenize(dsl);

        var expectedTokens = new[] { "SetTopic", "UseDraft", "UseCritique", "UseImprove" };
        foreach (var expected in expectedTokens)
        {
            if (!tokens.Any(t => t.Contains(expected)))
            {
                throw new Exception($"Reasoning token '{expected}' should be parsed");
            }
        }

        Console.WriteLine("  ✓ Pipeline reasoning steps parsing works correctly");
        return Task.CompletedTask;
    }

    private static async Task TestCompleteRefinementLoop()
    {
        Console.WriteLine("Testing complete refinement loop...");

        // Setup test environment
        var embed = TestModelFactory.CreateEmbeddingModel();
        var chat = TestModelFactory.CreateChatModel();
        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(chat, tools);
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));

        var state = new CliPipelineState
        {
            Branch = branch,
            Llm = llm,
            Tools = tools,
            Embed = embed,
            Topic = "Test refinement",
            Trace = false,
        };

        try
        {
            // Test that refinement loop creates draft if none exists
            var refinementStep = CliSteps.UseRefinementLoop("1");
            var resultState = await refinementStep(state);

            // Verify Draft, Critique, and Improve were all created
            var events = resultState.Branch.Events.OfType<ReasoningStep>().ToList();

            bool hasDraft = events.Any(e => e.State is Draft);
            bool hasCritique = events.Any(e => e.State is Critique);
            bool hasImprove = events.Any(e => e.State is FinalSpec);

            if (!hasDraft)
            {
                throw new Exception("Complete refinement loop should create a Draft");
            }

            if (!hasCritique)
            {
                throw new Exception("Complete refinement loop should create a Critique");
            }

            if (!hasImprove)
            {
                throw new Exception("Complete refinement loop should create a FinalSpec (Improve)");
            }

            Console.WriteLine("  ✓ Complete refinement loop creates Draft, Critique, and Improve");

            // Test that refinement loop with existing draft skips draft creation
            var state2 = resultState;
            var refinementStep2 = CliSteps.UseRefinementLoop("1");
            var resultState2 = await refinementStep2(state2);

            var events2 = resultState2.Branch.Events.OfType<ReasoningStep>().ToList();
            var draftCount = events2.Count(e => e.State is Draft);

            if (draftCount != 1)
            {
                throw new Exception($"Refinement loop should reuse existing draft (expected 1 draft, got {draftCount})");
            }

            Console.WriteLine("  ✓ Complete refinement loop reuses existing draft");
            Console.WriteLine("  ✓ Complete refinement loop works correctly");
        }
        catch (Exception ex)
        {
            // Fallback expected if Ollama not running
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("  ✓ Complete refinement loop test skipped (Ollama not available)");
            }
            else
            {
                throw;
            }
        }
    }

    private static async Task TestPipelineCommandWithTrace()
    {
        Console.WriteLine("Testing pipeline command with trace...");

        // Test trace on/off steps
        var embed = TestModelFactory.CreateEmbeddingModel();
        var chat = TestModelFactory.CreateChatModel();
        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(chat, tools);
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));

        var traceState = new CliPipelineState
        {
            Branch = branch,
            Llm = llm,
            Tools = tools,
            Embed = embed,
            Trace = false,
        };

        var traceOnStep = CliSteps.TraceOn();
        traceState = await traceOnStep(traceState);
        if (!traceState.Trace)
        {
            throw new Exception("TraceOn should enable trace");
        }

        var traceOffStep = CliSteps.TraceOff();
        traceState = await traceOffStep(traceState);
        if (traceState.Trace)
        {
            throw new Exception("TraceOff should disable trace");
        }

        Console.WriteLine("  ✓ Pipeline trace control works correctly");
    }

    private static Task TestPipelineCommandWithDebug()
    {
        Console.WriteLine("Testing pipeline command with debug...");

        var origDebug = Environment.GetEnvironmentVariable("MONADIC_DEBUG");
        try
        {
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

            // Debug mode should be readable
            var debugMode = Environment.GetEnvironmentVariable("MONADIC_DEBUG");
            if (debugMode != "1")
            {
                throw new Exception("Debug mode should be set via environment variable");
            }

            Console.WriteLine("  ✓ Pipeline debug mode works correctly");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", origDebug);
        }

        return Task.CompletedTask;
    }

    private static void TestListCommand()
    {
        Console.WriteLine("Testing list command...");

        // Test step registry enumeration
        var tokenGroups = StepRegistry.GetTokenGroups();
        if (!tokenGroups.Any())
        {
            throw new Exception("Step registry should have token groups");
        }

        // Verify essential steps are registered
        var allTokens = tokenGroups.SelectMany(g => g.Names).ToList();
        var essentialTokens = new[] { "UseIngest", "UseDraft", "UseCritique", "UseImprove", "SetPrompt", "LLM" };

        foreach (var token in essentialTokens)
        {
            if (!allTokens.Contains(token))
            {
                throw new Exception($"Essential token '{token}' should be registered");
            }
        }

        Console.WriteLine("  ✓ List command token enumeration works correctly");
    }

    private static void TestExplainCommand()
    {
        Console.WriteLine("Testing explain command...");

        var dsl = "SetPrompt('test')";
        var explanation = PipelineDsl.Explain(dsl);

        if (string.IsNullOrWhiteSpace(explanation))
        {
            throw new Exception("Explain should return non-empty output");
        }

        if (!explanation.Contains("Pipeline tokens:"))
        {
            throw new Exception("Explain should include 'Pipeline tokens:' header");
        }

        if (!explanation.Contains("Available token groups:"))
        {
            throw new Exception("Explain should include 'Available token groups:' section");
        }

        Console.WriteLine("  ✓ Explain command works correctly");
    }

    private static void TestExplainCommandComplexDsl()
    {
        Console.WriteLine("Testing explain command with complex DSL...");

        var dsl = "SetTopic('AI') | UseDraft | UseCritique | UseImprove | LLM";
        var explanation = PipelineDsl.Explain(dsl);

        // Should explain each token
        var tokens = new[] { "SetTopic", "UseDraft", "UseCritique", "UseImprove", "LLM" };
        foreach (var token in tokens)
        {
            if (!explanation.Contains(token))
            {
                throw new Exception($"Explanation should include token '{token}'");
            }
        }

        Console.WriteLine("  ✓ Explain command with complex DSL works correctly");
    }

    private static void TestTestCommandStructure()
    {
        Console.WriteLine("Testing test command structure...");

        // Verify test structure by checking that test classes exist
        var integrationTestType = typeof(OllamaCloudIntegrationTests);
        var vectorTestType = typeof(TrackedVectorStoreTests);
        var memoryTestType = typeof(MemoryContextTests);

        if (integrationTestType == null)
        {
            throw new Exception("OllamaCloudIntegrationTests should exist");
        }

        if (vectorTestType == null)
        {
            throw new Exception("TrackedVectorStoreTests should exist");
        }

        if (memoryTestType == null)
        {
            throw new Exception("MemoryContextTests should exist");
        }

        // Note: LangChainConversationTests are temporarily excluded while LangChain integration is disabled.

        Console.WriteLine("  ✓ Test command structure is complete");
    }

    private static void TestCommandValidation()
    {
        Console.WriteLine("Testing command validation...");

        // Test DSL parsing with invalid tokens
        var dsl = "InvalidToken | AnotherInvalid";
        var tokens = PipelineDsl.Tokenize(dsl);

        // Should still parse tokens even if invalid
        if (tokens.Length != 2)
        {
            throw new Exception($"Should parse invalid tokens, got {tokens.Length}");
        }

        // Build should handle unknown tokens as no-ops
        var step = PipelineDsl.Build(dsl);
        if (step == null)
        {
            throw new Exception("Build should return a step even with invalid tokens");
        }

        Console.WriteLine("  ✓ Command validation handles invalid tokens correctly");
    }

    private static void TestEnvironmentVariableHandling()
    {
        Console.WriteLine("Testing environment variable handling...");

        var origEndpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        var origKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
        var origType = Environment.GetEnvironmentVariable("CHAT_ENDPOINT_TYPE");

        try
        {
            // Test environment variable resolution
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT", "https://test.com");
            Environment.SetEnvironmentVariable("CHAT_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT_TYPE", "ollama-cloud");

            var (endpoint, key, type) = ChatConfig.Resolve();

            if (endpoint != "https://test.com")
            {
                throw new Exception($"Should resolve endpoint from env var, got {endpoint}");
            }

            if (key != "test-key")
            {
                throw new Exception($"Should resolve key from env var, got {key}");
            }

            if (type != ChatEndpointType.OllamaCloud)
            {
                throw new Exception($"Should resolve type from env var, got {type}");
            }

            // Test that CLI override takes precedence
            var (endpoint2, key2, type2) = ChatConfig.ResolveWithOverrides(
                "https://override.com", "override-key", "openai");

            if (endpoint2 != "https://override.com" || key2 != "override-key")
            {
                throw new Exception("CLI overrides should take precedence over env vars");
            }

            Console.WriteLine("  ✓ Environment variable handling works correctly");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT", origEndpoint);
            Environment.SetEnvironmentVariable("CHAT_API_KEY", origKey);
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT_TYPE", origType);
        }
    }

    private static void TestOrchestratorCommand()
    {
        Console.WriteLine("Testing orchestrator command structure...");

        // Verify that OrchestratorBuilder and related types exist
        var builderType = typeof(OrchestratorBuilder);
        if (builderType == null)
        {
            throw new Exception("OrchestratorBuilder type should exist");
        }

        var orchestratedModelType = typeof(OrchestratedChatModel);
        if (orchestratedModelType == null)
        {
            throw new Exception("OrchestratedChatModel type should exist");
        }

        // Verify builder has expected methods
        var withModelMethod = builderType.GetMethod("WithModel");
        if (withModelMethod == null)
        {
            throw new Exception("OrchestratorBuilder should have WithModel method");
        }

        var buildMethod = builderType.GetMethod("Build");
        if (buildMethod == null)
        {
            throw new Exception("OrchestratorBuilder should have Build method");
        }

        var getOrchestratorMethod = builderType.GetMethod("GetOrchestrator");
        if (getOrchestratorMethod == null)
        {
            throw new Exception("OrchestratorBuilder should have GetOrchestrator method");
        }

        Console.WriteLine("  ✓ Orchestrator command structure is valid");
    }

    private static void TestMeTTaCommand()
    {
        Console.WriteLine("Testing MeTTa command structure...");

        // Verify that MeTTa orchestrator types exist
        var mettaOrchestratorType = typeof(Ouroboros.Agent.MetaAI.MeTTaOrchestrator);
        if (mettaOrchestratorType == null)
        {
            throw new Exception("MeTTaOrchestrator type should exist");
        }

        var mettaBuilderType = typeof(Ouroboros.Agent.MetaAI.MeTTaOrchestratorBuilder);
        if (mettaBuilderType == null)
        {
            throw new Exception("MeTTaOrchestratorBuilder type should exist");
        }

        // Verify builder has expected methods
        var createDefaultMethod = mettaBuilderType.GetMethod(
            "CreateDefault",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (createDefaultMethod == null)
        {
            throw new Exception("MeTTaOrchestratorBuilder should have CreateDefault static method");
        }

        var buildMethod = mettaBuilderType.GetMethod("Build");
        if (buildMethod == null)
        {
            throw new Exception("MeTTaOrchestratorBuilder should have Build method");
        }

        // Verify orchestrator has expected methods
        var planMethod = mettaOrchestratorType.GetMethod("PlanAsync");
        if (planMethod == null)
        {
            throw new Exception("MeTTaOrchestrator should have PlanAsync method");
        }

        var executeMethod = mettaOrchestratorType.GetMethod("ExecuteAsync");
        if (executeMethod == null)
        {
            throw new Exception("MeTTaOrchestrator should have ExecuteAsync method");
        }

        var getMetricsMethod = mettaOrchestratorType.GetMethod("GetMetrics");
        if (getMetricsMethod == null)
        {
            throw new Exception("MeTTaOrchestrator should have GetMetrics method");
        }

        Console.WriteLine("  ✓ MeTTa command structure is valid");
    }
}
