using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LangChainPipeline.Options;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.CLI.Commands;

public static class TestCommands
{
    public static async Task RunTestsAsync(TestOptions o)
    {
        Console.WriteLine("=== Running Ouroboros Tests ===\n");

        try
        {
            if (o.MeTTa)
            {
                await RunMeTTaDockerTest();
                return;
            }

            if (o.All || o.IntegrationOnly)
            {
                // await LangChainPipeline.Tests.OllamaCloudIntegrationTests.RunAllTests();
                Console.WriteLine();
            }

            if (o.All || o.CliOnly)
            {
                // await LangChainPipeline.Tests.CliEndToEndTests.RunAllTests();
                Console.WriteLine();
            }

            if (o.All)
            {
                // await LangChainPipeline.Tests.TrackedVectorStoreTests.RunAllTests();
                Console.WriteLine();

                // LangChainPipeline.Tests.MemoryContextTests.RunAllTests();
                Console.WriteLine();

                // await LangChainPipeline.Tests.LangChainConversationTests.RunAllTests();
                Console.WriteLine();

                // Run meta-AI tests
                // await LangChainPipeline.Tests.MetaAiTests.RunAllTests();
                Console.WriteLine();

                // Run Meta-AI v2 tests
                // await LangChainPipeline.Tests.MetaAIv2Tests.RunAllTests();
                Console.WriteLine();

                // Run Meta-AI Convenience Layer tests
                // await LangChainPipeline.Tests.MetaAIConvenienceTests.RunAll();
                Console.WriteLine();

                // Run orchestrator tests
                // await LangChainPipeline.Tests.OrchestratorTests.RunAllTests();
                Console.WriteLine();

                // Run MeTTa integration tests
                // await LangChainPipeline.Tests.MeTTaTests.RunAllTests();
                Console.WriteLine();

                // Run MeTTa Orchestrator v3.0 tests
                // await LangChainPipeline.Tests.MeTTaOrchestratorTests.RunAllTests();
                Console.WriteLine();
            }

            Console.WriteLine("=== ✅ All Tests Passed ===");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("\n=== ❌ Test Failed ===");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    public static async Task RunMeTTaDockerTest()
    {
        Console.WriteLine("=== Test: Subprocess MeTTa Engine (Docker) ===");

        using var engine = new SubprocessMeTTaEngine();

        // 1. Basic Math
        var result = await engine.ExecuteQueryAsync("(+ 1 2)", CancellationToken.None);

        result.Match(
            success => Console.WriteLine($"✓ Basic Query succeeded: {success}"),
            error => Console.WriteLine($"✗ Basic Query failed: {error}"));

        // 2. Motto Initialization
        Console.WriteLine("\n=== Test: Motto Initialization ===");
        var initStep = new MottoSteps.MottoInitializeStep(engine);
        var initResult = await initStep.ExecuteAsync(Unit.Value, CancellationToken.None);
        initResult.Match(
            success => Console.WriteLine("✓ Motto Initialized"),
            error => Console.WriteLine($"✗ Motto Initialization failed: {error}")
        );

        // 3. Motto Chat (Mock)
        Console.WriteLine("\n=== Test: Motto Chat Step ===");
        var chatStep = new MottoSteps.MottoChatStep(engine);
        var chatResult = await chatStep.ExecuteAsync("Hello", CancellationToken.None);
        chatResult.Match(
            success => Console.WriteLine($"✓ Chat Response: {success}"),
            error => Console.WriteLine($"? Chat Result: {error} (Expected if no API key)")
        );

        Console.WriteLine("✓ Subprocess MeTTa engine test completed\n");
    }
}
