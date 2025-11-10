using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using LangChainPipeline.CLI;
using LangChainPipeline.Domain.Vectors;
using LangChainPipeline.Pipeline.Branches;
using LangChainPipeline.Providers;
using LangChainPipeline.Tools;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the guided installation step and dependency exception handling functionality.
/// </summary>
public static class GuidedInstallStepTests
{
    /// <summary>
    /// Runs all tests for the guided installation step.
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running Guided Install Step Tests ===");

        await TestInstallDependenciesGuidedBasic();
        await TestInstallDependenciesGuidedWithDependencyName();
        await TestInstallDependenciesGuidedWithErrorMessage();
        await TestInstallDependenciesGuidedWithBothParameters();
        await TestHandleDependencyExceptionWithKnownPattern();
        await TestHandleDependencyExceptionWithUnknownPattern();
        await TestHandleDependencyExceptionMultiplePatterns();

        Console.WriteLine("✓ All guided install step tests passed!");
    }

    private static async Task TestInstallDependenciesGuidedBasic()
    {
        Console.WriteLine("Testing InstallDependenciesGuided (basic)...");

        var state = CreateTestState();
        var step = CliSteps.InstallDependenciesGuided();

        var result = await step(state);

        // Verify event was recorded
        var events = result.Branch.Events.OfType<IngestBatch>().ToList();
        if (events.Count == 0)
        {
            throw new Exception("Expected at least one ingest event");
        }

        var lastEvent = events.Last();
        if (!lastEvent.Source.StartsWith("guided-install:triggered:"))
        {
            throw new Exception($"Expected guided-install event, got: {lastEvent.Source}");
        }

        Console.WriteLine("  ✓ Basic InstallDependenciesGuided works correctly");
    }

    private static async Task TestInstallDependenciesGuidedWithDependencyName()
    {
        Console.WriteLine("Testing InstallDependenciesGuided with dependency name...");

        var state = CreateTestState();
        var step = CliSteps.InstallDependenciesGuided("dep=NuGet");

        var result = await step(state);

        var events = result.Branch.Events.OfType<IngestBatch>().ToList();
        var lastEvent = events.Last();

        if (!lastEvent.Source.Contains("NuGet"))
        {
            throw new Exception($"Expected event to contain 'NuGet', got: {lastEvent.Source}");
        }

        Console.WriteLine("  ✓ InstallDependenciesGuided with dependency name works correctly");
    }

    private static async Task TestInstallDependenciesGuidedWithErrorMessage()
    {
        Console.WriteLine("Testing InstallDependenciesGuided with error message...");

        var state = CreateTestState();
        var step = CliSteps.InstallDependenciesGuided("error=Package not found");

        var result = await step(state);

        // Should still create an event even if just error message is provided
        var events = result.Branch.Events.OfType<IngestBatch>().ToList();
        if (events.Count == 0)
        {
            throw new Exception("Expected at least one ingest event");
        }

        Console.WriteLine("  ✓ InstallDependenciesGuided with error message works correctly");
    }

    private static async Task TestInstallDependenciesGuidedWithBothParameters()
    {
        Console.WriteLine("Testing InstallDependenciesGuided with both parameters...");

        var state = CreateTestState();
        var step = CliSteps.InstallDependenciesGuided("dep=NPM|error=Module not found");

        var result = await step(state);

        var events = result.Branch.Events.OfType<IngestBatch>().ToList();
        var lastEvent = events.Last();

        if (!lastEvent.Source.Contains("NPM"))
        {
            throw new Exception($"Expected event to contain 'NPM', got: {lastEvent.Source}");
        }

        Console.WriteLine("  ✓ InstallDependenciesGuided with both parameters works correctly");
    }

    private static async Task TestHandleDependencyExceptionWithKnownPattern()
    {
        Console.WriteLine("Testing HandleDependencyExceptionAsync with known pattern...");

        var state = CreateTestState();
        var exception = new Exception("NuGet package restore failed");

        // Use reflection to call the private method
        var method = typeof(CliSteps).GetMethod(
            "HandleDependencyExceptionAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new Exception("HandleDependencyExceptionAsync method not found");
        }

        var task = (Task<CliPipelineState>?)method.Invoke(null, new object[] { state, exception });
        if (task == null)
        {
            throw new Exception("Method invocation returned null");
        }

        var result = await task;

        // Verify dependency-specific event was created
        var events = result.Branch.Events.OfType<IngestBatch>().ToList();
        var hasDepEvent = events.Any(e => e.Source.Contains("dependency:missing:NuGet"));
        var hasScheduleEvent = events.Any(e => e.Source.Contains("schedule:guided-install"));

        if (!hasDepEvent)
        {
            throw new Exception("Expected dependency:missing event");
        }

        if (!hasScheduleEvent)
        {
            throw new Exception("Expected schedule:guided-install event");
        }

        Console.WriteLine("  ✓ HandleDependencyExceptionAsync with known pattern works correctly");
    }

    private static async Task TestHandleDependencyExceptionWithUnknownPattern()
    {
        Console.WriteLine("Testing HandleDependencyExceptionAsync with unknown pattern...");

        var state = CreateTestState();
        var exception = new Exception("Some random error occurred");

        var method = typeof(CliSteps).GetMethod(
            "HandleDependencyExceptionAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new Exception("HandleDependencyExceptionAsync method not found");
        }

        var task = (Task<CliPipelineState>?)method.Invoke(null, new object[] { state, exception });
        if (task == null)
        {
            throw new Exception("Method invocation returned null");
        }

        var result = await task;

        // Verify generic error event was created
        var events = result.Branch.Events.OfType<IngestBatch>().ToList();
        var hasGenericError = events.Any(e => e.Source.Contains("error:generic"));

        if (!hasGenericError)
        {
            throw new Exception("Expected error:generic event for unknown pattern");
        }

        Console.WriteLine("  ✓ HandleDependencyExceptionAsync with unknown pattern works correctly");
    }

    private static async Task TestHandleDependencyExceptionMultiplePatterns()
    {
        Console.WriteLine("Testing HandleDependencyExceptionAsync with multiple patterns...");

        var state = CreateTestState();
        var testCases = new[]
        {
            ("npm install failed", "NPM"),
            ("ollama model not found", "Ollama"),
            ("pip requirements.txt missing", "Python"),
            ("docker image not found", "Docker")
        };

        foreach (var (errorMsg, expectedDep) in testCases)
        {
            var exception = new Exception(errorMsg);
            var method = typeof(CliSteps).GetMethod(
                "HandleDependencyExceptionAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (method == null)
            {
                throw new Exception("HandleDependencyExceptionAsync method not found");
            }

            var task = (Task<CliPipelineState>?)method.Invoke(null, new object[] { state, exception });
            if (task == null)
            {
                throw new Exception("Method invocation returned null");
            }

            var result = await task;
            var events = result.Branch.Events.OfType<IngestBatch>().ToList();
            var hasExpectedDep = events.Any(e => e.Source.Contains($"dependency:missing:{expectedDep}"));

            if (!hasExpectedDep)
            {
                throw new Exception($"Expected dependency:missing:{expectedDep} for error: {errorMsg}");
            }

            // Reset state for next test
            state = CreateTestState();
        }

        Console.WriteLine("  ✓ HandleDependencyExceptionAsync with multiple patterns works correctly");
    }

    private static CliPipelineState CreateTestState()
    {
        var provider = new OllamaProvider();
        var chat = new OllamaChatModel(provider, "llama3");
        var adapter = new OllamaChatAdapter(chat);
        var embed = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(adapter, tools);
        var branch = new PipelineBranch("test", new TrackedVectorStore(), DataSource.FromPath(Environment.CurrentDirectory));

        return new CliPipelineState
        {
            Branch = branch,
            Llm = llm,
            Tools = tools,
            Embed = embed
        };
    }
}
