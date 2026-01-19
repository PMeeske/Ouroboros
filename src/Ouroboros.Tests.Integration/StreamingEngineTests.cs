using System.Reactive.Linq;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using Ouroboros.Application;

namespace Ouroboros.Tests.UnitTests;

/// <summary>
/// Integration tests for the streaming engine functionality using System.Reactive.
/// Verifies stream creation, windowing, aggregation, and resource cleanup.
/// </summary>
[Trait("Category", "Integration")]
public static class StreamingEngineTests
{
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running Streaming Engine Tests ===");

        // Core infrastructure tests
        TestStreamingContextLifecycle();
        TestStreamingContextConcurrency();
        TestStreamingContextDisposal();

        // Stream creation tests
        await TestGeneratedStreamCreation();
        await TestFileStreamCreation();

        // Window operations tests
        await TestTumblingWindow();
        await TestSlidingWindow();
        await TestTimeBasedWindow();

        // Aggregation tests
        await TestCountAggregate();
        await TestSumAggregate();
        await TestMeanAggregate();
        await TestMultipleAggregates();

        // Integration tests
        await TestCompleteStreamingPipeline();
        await TestBackwardCompatibility();

        Console.WriteLine("✓ All streaming engine tests passed!");
    }

    #region StreamingContext Tests

    private static void TestStreamingContextLifecycle()
    {
        Console.WriteLine("Testing StreamingContext lifecycle...");

        var context = new StreamingContext();

        if (context.IsDisposed)
        {
            throw new Exception("Context should not be disposed on creation");
        }

        var disposableCalled = false;
        context.RegisterCleanup(() => disposableCalled = true);

        context.Dispose();

        if (!context.IsDisposed)
        {
            throw new Exception("Context should be disposed after calling Dispose");
        }

        if (!disposableCalled)
        {
            throw new Exception("Registered cleanup action should be called on disposal");
        }

        // Test idempotent disposal
#pragma warning disable S3966 // Intentionally testing idempotent disposal
        context.Dispose();
#pragma warning restore S3966
        if (!context.IsDisposed)
        {
            throw new Exception("Context should remain disposed after multiple Dispose calls");
        }

        Console.WriteLine("  ✓ StreamingContext lifecycle works correctly");
    }

    private static void TestStreamingContextConcurrency()
    {
        Console.WriteLine("Testing StreamingContext concurrent registration...");

        var context = new StreamingContext();
        var registrationCount = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            context.RegisterCleanup(() =>
            {
                lock (lockObj)
                {
                    registrationCount++;
                }
            });
        })).ToArray();

        Task.WaitAll(tasks);
        context.Dispose();

        if (registrationCount != 100)
        {
            throw new Exception($"Expected 100 cleanup calls, got {registrationCount}");
        }

        Console.WriteLine("  ✓ StreamingContext handles concurrent registration correctly");
    }

    private static void TestStreamingContextDisposal()
    {
        Console.WriteLine("Testing StreamingContext prevents registration after disposal...");

        var context = new StreamingContext();
        context.Dispose();

        var called = false;
        context.RegisterCleanup(() => called = true);

        // Cleanup should be called immediately for disposed context
        if (!called)
        {
            throw new Exception("Cleanup should be called immediately when registered on disposed context");
        }

        Console.WriteLine("  ✓ StreamingContext properly handles post-disposal registration");
    }

    #endregion

    #region Stream Creation Tests

    private static async Task TestGeneratedStreamCreation()
    {
        Console.WriteLine("Testing generated stream creation...");

        var state = CreateTestState();
        var step = StreamingCliSteps.CreateStream("source=generated|count=5|interval=10");

        state = await step(state);

        if (state.ActiveStream == null)
        {
            throw new Exception("ActiveStream should be created");
        }

        var items = new List<object>();
        await state.ActiveStream.Take(5).ForEachAsync(item => items.Add(item));

        if (items.Count != 5)
        {
            throw new Exception($"Expected 5 items, got {items.Count}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Generated stream creation works correctly");
    }

    private static async Task TestFileStreamCreation()
    {
        Console.WriteLine("Testing file stream creation...");

        // Create a temporary test file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_stream_{Guid.NewGuid()}.txt");
        await File.WriteAllLinesAsync(tempFile, new[] { "line1", "line2", "line3" });

        try
        {
            var state = CreateTestState();
            var step = StreamingCliSteps.CreateStream($"source=file|path={tempFile}");

            state = await step(state);

            if (state.ActiveStream == null)
            {
                throw new Exception("ActiveStream should be created");
            }

            var items = new List<object>();
            await state.ActiveStream.ForEachAsync(item => items.Add(item));

            if (items.Count != 3)
            {
                throw new Exception($"Expected 3 items, got {items.Count}");
            }

            state.Streaming?.Dispose();
            Console.WriteLine("  ✓ File stream creation works correctly");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region Window Operation Tests

    private static async Task TestTumblingWindow()
    {
        Console.WriteLine("Testing tumbling window...");

        var state = CreateTestState();

        // Create stream and apply window
        var createStep = StreamingCliSteps.CreateStream("source=generated|count=10|interval=10");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=3");
        state = await windowStep(state);

        var windowCount = 0;
        await state.ActiveStream!.Take(3).ForEachAsync(_ => windowCount++);

        if (windowCount != 3)
        {
            throw new Exception($"Expected 3 windows, got {windowCount}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Tumbling window works correctly");
    }

    private static async Task TestSlidingWindow()
    {
        Console.WriteLine("Testing sliding window...");

        var state = CreateTestState();

        var createStep = StreamingCliSteps.CreateStream("source=generated|count=10|interval=10");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=3|slide=1");
        state = await windowStep(state);

        var windowCount = 0;
        await state.ActiveStream!.Take(5).ForEachAsync(_ => windowCount++);

        if (windowCount != 5)
        {
            throw new Exception($"Expected 5 windows, got {windowCount}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Sliding window works correctly");
    }

    private static async Task TestTimeBasedWindow()
    {
        Console.WriteLine("Testing time-based window...");

        var state = CreateTestState();

        var createStep = StreamingCliSteps.CreateStream("source=generated|count=20|interval=50");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=1s");
        state = await windowStep(state);

        var windowCount = 0;
        await state.ActiveStream!.Take(2).Timeout(TimeSpan.FromSeconds(5)).ForEachAsync(_ => windowCount++);

        if (windowCount < 1)
        {
            throw new Exception("Expected at least 1 time-based window");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Time-based window works correctly");
    }

    #endregion

    #region Aggregation Tests

    private static async Task TestCountAggregate()
    {
        Console.WriteLine("Testing count aggregate...");

        var state = CreateTestState();

        var createStep = StreamingCliSteps.CreateStream("source=generated|count=10|interval=10");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=5");
        state = await windowStep(state);

        var aggregateStep = StreamingCliSteps.ApplyAggregate("count");
        state = await aggregateStep(state);

        var results = new List<object>();
        await state.ActiveStream!.Take(2).ForEachAsync(item => results.Add(item));

        if (results.Count != 2)
        {
            throw new Exception($"Expected 2 aggregated results, got {results.Count}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Count aggregate works correctly");
    }

    private static async Task TestSumAggregate()
    {
        Console.WriteLine("Testing sum aggregate...");

        var state = CreateTestState();

        var createStep = StreamingCliSteps.CreateStream("source=generated|count=10|interval=10");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=5");
        state = await windowStep(state);

        var aggregateStep = StreamingCliSteps.ApplyAggregate("sum");
        state = await aggregateStep(state);

        var results = new List<object>();
        await state.ActiveStream!.Take(2).ForEachAsync(item => results.Add(item));

        if (results.Count != 2)
        {
            throw new Exception($"Expected 2 aggregated results, got {results.Count}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Sum aggregate works correctly");
    }

    private static async Task TestMeanAggregate()
    {
        Console.WriteLine("Testing mean aggregate...");

        var state = CreateTestState();

        var createStep = StreamingCliSteps.CreateStream("source=generated|count=10|interval=10");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=5");
        state = await windowStep(state);

        var aggregateStep = StreamingCliSteps.ApplyAggregate("mean");
        state = await aggregateStep(state);

        var results = new List<object>();
        await state.ActiveStream!.Take(2).ForEachAsync(item => results.Add(item));

        if (results.Count != 2)
        {
            throw new Exception($"Expected 2 aggregated results, got {results.Count}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Mean aggregate works correctly");
    }

    private static async Task TestMultipleAggregates()
    {
        Console.WriteLine("Testing multiple aggregates...");

        var state = CreateTestState();

        var createStep = StreamingCliSteps.CreateStream("source=generated|count=10|interval=10");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=5");
        state = await windowStep(state);

        var aggregateStep = StreamingCliSteps.ApplyAggregate("count,sum,mean");
        state = await aggregateStep(state);

        var results = new List<object>();
        await state.ActiveStream!.Take(2).ForEachAsync(item => results.Add(item));

        if (results.Count != 2)
        {
            throw new Exception($"Expected 2 aggregated results, got {results.Count}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Multiple aggregates work correctly");
    }

    #endregion

    #region Integration Tests

    private static async Task TestCompleteStreamingPipeline()
    {
        Console.WriteLine("Testing complete streaming pipeline...");

        var state = CreateTestState();

        // Build a complete pipeline: Stream -> Window -> Aggregate -> Sink
        var createStep = StreamingCliSteps.CreateStream("source=generated|count=20|interval=10");
        state = await createStep(state);

        var windowStep = StreamingCliSteps.ApplyWindow("size=5");
        state = await windowStep(state);

        var aggregateStep = StreamingCliSteps.ApplyAggregate("count");
        state = await aggregateStep(state);

        if (state.ActiveStream == null)
        {
            throw new Exception("Pipeline should produce an active stream");
        }

        var itemCount = 0;
        await state.ActiveStream.Take(4).ForEachAsync(_ => itemCount++);

        if (itemCount != 4)
        {
            throw new Exception($"Expected 4 items from pipeline, got {itemCount}");
        }

        state.Streaming?.Dispose();
        Console.WriteLine("  ✓ Complete streaming pipeline works correctly");
    }

    private static async Task TestBackwardCompatibility()
    {
        Console.WriteLine("Testing backward compatibility with existing steps...");

        var state = CreateTestState();

        // Use existing steps without streaming
        var topicStep = CliSteps.SetTopic("test-topic");
        state = await topicStep(state);

        if (state.Topic != "test-topic")
        {
            throw new Exception("Existing steps should still work");
        }

        if (state.ActiveStream != null)
        {
            throw new Exception("Non-streaming steps should not create streams");
        }

        Console.WriteLine("  ✓ Backward compatibility maintained");
    }

    #endregion

    #region Helper Methods

    private static CliPipelineState CreateTestState()
    {
        var provider = new OllamaProvider();
        var chat = new OllamaChatModel(provider, "llama3");
        var adapter = new OllamaChatAdapter(chat);
        var embed = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        var tools = new ToolRegistry();
        var llm = new ToolAwareChatModel(adapter, tools);
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath(Environment.CurrentDirectory));

        return new CliPipelineState
        {
            Branch = branch,
            Llm = llm,
            Tools = tools,
            Embed = embed,
            Trace = false
        };
    }

    #endregion
}
