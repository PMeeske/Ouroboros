// ==========================================================
// Streaming Pipeline Examples
// Demonstrates System.Reactive-based streaming with live
// aggregations, windowing, and real-time data processing
// ==========================================================

using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;
using LangChainPipeline.CLI;
using LangChainPipeline.Domain.Vectors;
using LangChainPipeline.Providers;

namespace LangChainPipeline.Examples;

/// <summary>
/// Demonstrates streaming capabilities with System.Reactive.
/// Shows real-time data processing, windowing, aggregation, and live metrics.
/// </summary>
public static class StreamingPipelineExample
{
    /// <summary>
    /// Example 1: Basic streaming with windowing and aggregation.
    /// Creates a stream of generated data, applies a tumbling window, and counts items.
    /// </summary>
    public static async Task RunBasicStreamingExample()
    {
        Console.WriteLine("=== Streaming Pipeline - Basic Example ===\n");
        Console.WriteLine("This example shows a simple streaming pipeline with windowing and counting.\n");

        CliPipelineState state = CreateTestState();

        // Create a stream of 20 items with 50ms intervals
        Console.WriteLine("Creating stream with 20 items...");
        Step<CliPipelineState, CliPipelineState> createStep = StreamingCliSteps.CreateStream("source=generated|count=20|interval=50");
        state = await createStep(state);

        // Apply a tumbling window of size 5
        Console.WriteLine("Applying tumbling window (size=5)...");
        Step<CliPipelineState, CliPipelineState> windowStep = StreamingCliSteps.ApplyWindow("size=5");
        state = await windowStep(state);

        // Count items in each window
        Console.WriteLine("Aggregating with count...");
        Step<CliPipelineState, CliPipelineState> aggregateStep = StreamingCliSteps.ApplyAggregate("count");
        state = await aggregateStep(state);

        // Sink to console
        Console.WriteLine("Outputting results to console...\n");
        Step<CliPipelineState, CliPipelineState> sinkStep = StreamingCliSteps.ApplySink("console");
        state = await sinkStep(state);

        // Give it time to process
        await Task.Delay(2000);

        // Cleanup
        state.Streaming?.Dispose();
        Console.WriteLine("\n✓ Basic streaming example completed!");
    }

    /// <summary>
    /// Example 2: Multiple aggregations on the same stream.
    /// Shows count, sum, mean, min, and max calculations in real-time.
    /// </summary>
    public static async Task RunMultipleAggregationsExample()
    {
        Console.WriteLine("\n=== Streaming Pipeline - Multiple Aggregations ===\n");
        Console.WriteLine("This example demonstrates multiple aggregations (count, sum, mean, min, max).\n");

        CliPipelineState state = CreateTestState();

        // Create stream
        Console.WriteLine("Creating stream...");
        state = await StreamingCliSteps.CreateStream("source=generated|count=30|interval=30")(state);

        // Window
        Console.WriteLine("Applying window...");
        state = await StreamingCliSteps.ApplyWindow("size=10")(state);

        // Multiple aggregations
        Console.WriteLine("Computing count, sum, mean, min, max...\n");
        state = await StreamingCliSteps.ApplyAggregate("count,sum,mean,min,max")(state);

        // Output to console
        state = await StreamingCliSteps.ApplySink("console")(state);

        await Task.Delay(2000);
        state.Streaming?.Dispose();
        Console.WriteLine("\n✓ Multiple aggregations example completed!");
    }

    /// <summary>
    /// Example 3: Time-based windowing.
    /// Uses time windows instead of count-based windows for real-time aggregation.
    /// </summary>
    public static async Task RunTimeBasedWindowExample()
    {
        Console.WriteLine("\n=== Streaming Pipeline - Time-Based Windows ===\n");
        Console.WriteLine("This example uses 2-second time windows for aggregation.\n");

        CliPipelineState state = CreateTestState();

        // Create stream with longer duration
        Console.WriteLine("Creating continuous stream...");
        state = await StreamingCliSteps.CreateStream("source=generated|count=50|interval=50")(state);

        // Time-based window
        Console.WriteLine("Applying 2-second time window...");
        state = await StreamingCliSteps.ApplyWindow("size=2s")(state);

        // Aggregate
        Console.WriteLine("Counting items per time window...\n");
        state = await StreamingCliSteps.ApplyAggregate("count")(state);

        // Output
        state = await StreamingCliSteps.ApplySink("console")(state);

        await Task.Delay(5000);
        state.Streaming?.Dispose();
        Console.WriteLine("\n✓ Time-based window example completed!");
    }

    /// <summary>
    /// Example 4: Sliding windows for overlapping aggregations.
    /// Shows how to create overlapping windows for smoother metrics.
    /// </summary>
    public static async Task RunSlidingWindowExample()
    {
        Console.WriteLine("\n=== Streaming Pipeline - Sliding Windows ===\n");
        Console.WriteLine("This example uses sliding windows (size=5, slide=2) for overlapping aggregation.\n");

        CliPipelineState state = CreateTestState();

        // Create stream
        Console.WriteLine("Creating stream...");
        state = await StreamingCliSteps.CreateStream("source=generated|count=30|interval=40")(state);

        // Sliding window: size=5, slide=2
        Console.WriteLine("Applying sliding window (overlapping)...");
        state = await StreamingCliSteps.ApplyWindow("size=5|slide=2")(state);

        // Aggregate
        Console.WriteLine("Computing mean for each window...\n");
        state = await StreamingCliSteps.ApplyAggregate("mean")(state);

        // Output
        state = await StreamingCliSteps.ApplySink("console")(state);

        await Task.Delay(2000);
        state.Streaming?.Dispose();
        Console.WriteLine("\n✓ Sliding window example completed!");
    }

    /// <summary>
    /// Example 5: Live dashboard with metrics.
    /// Demonstrates the dashboard feature for real-time monitoring.
    /// </summary>
    public static async Task RunDashboardExample()
    {
        Console.WriteLine("\n=== Streaming Pipeline - Live Dashboard ===\n");
        Console.WriteLine("This example shows a live dashboard with streaming metrics.");
        Console.WriteLine("Watch the dashboard update in real-time!\n");

        CliPipelineState state = CreateTestState();

        // Create a longer stream for dashboard visualization
        state = await StreamingCliSteps.CreateStream("source=generated|count=100|interval=50")(state);

        // Show dashboard
        state = await StreamingCliSteps.ShowDashboard("refresh=1s|items=10")(state);

        // Let it run for demonstration
        await Task.Delay(7000);

        state.Streaming?.Dispose();
        Console.WriteLine("\n✓ Dashboard example completed!");
    }

    /// <summary>
    /// Example 6: File-based streaming.
    /// Reads from a file and processes line by line.
    /// </summary>
    public static async Task RunFileStreamExample()
    {
        Console.WriteLine("\n=== Streaming Pipeline - File Processing ===\n");
        Console.WriteLine("This example processes a file line by line using streaming.\n");

        // Create a temporary test file
        string tempFile = Path.Combine(Path.GetTempPath(), $"streaming_test_{Guid.NewGuid()}.txt");
        IEnumerable<string> lines = Enumerable.Range(1, 20).Select(i => $"Line {i}: Data value {i * 10}");
        await File.WriteAllLinesAsync(tempFile, lines);

        try
        {
            CliPipelineState state = CreateTestState();

            Console.WriteLine($"Reading from: {tempFile}");
            state = await StreamingCliSteps.CreateStream($"source=file|path={tempFile}")(state);

            // Window the lines
            state = await StreamingCliSteps.ApplyWindow("size=5")(state);

            // Count lines per window
            state = await StreamingCliSteps.ApplyAggregate("count")(state);

            // Output
            Console.WriteLine("\nProcessing file...\n");
            state = await StreamingCliSteps.ApplySink("console")(state);

            await Task.Delay(2000);
            state.Streaming?.Dispose();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        Console.WriteLine("\n✓ File stream example completed!");
    }

    /// <summary>
    /// Example 7: DSL-based streaming pipeline.
    /// Shows how to use the DSL to build streaming pipelines declaratively.
    /// </summary>
    public static async Task RunDslStreamingExample()
    {
        Console.WriteLine("\n=== Streaming Pipeline - DSL Example ===\n");
        Console.WriteLine("This example uses the DSL to define a complete streaming pipeline.\n");

        CliPipelineState state = CreateTestState();

        // Build pipeline using DSL
        string dsl = "Stream('source=generated|count=25|interval=40') | Window('size=5') | Aggregate('count,mean') | Sink('console')";
        Console.WriteLine($"DSL Pipeline: {dsl}\n");

        Step<CliPipelineState, CliPipelineState> pipeline = PipelineDsl.Build(dsl);
        state = await pipeline(state);

        await Task.Delay(2500);
        state.Streaming?.Dispose();
        Console.WriteLine("\n✓ DSL streaming example completed!");
    }

    /// <summary>
    /// Example 8: Complete streaming scenario.
    /// Demonstrates a real-world use case with multiple transformations.
    /// </summary>
    public static async Task RunCompleteStreamingScenario()
    {
        Console.WriteLine("\n=== Streaming Pipeline - Complete Scenario ===\n");
        Console.WriteLine("This example demonstrates a complete end-to-end streaming pipeline.\n");

        CliPipelineState state = CreateTestState();

        Console.WriteLine("1. Creating data stream...");
        state = await StreamingCliSteps.CreateStream("source=generated|count=50|interval=30")(state);

        Console.WriteLine("2. Applying filter (identity for demo)...");
        state = await StreamingCliSteps.ApplyFilter()(state);

        Console.WriteLine("3. Applying map transformation...");
        state = await StreamingCliSteps.ApplyMap()(state);

        Console.WriteLine("4. Creating 3-second time windows...");
        state = await StreamingCliSteps.ApplyWindow("size=3s")(state);

        Console.WriteLine("5. Computing multiple aggregations...");
        state = await StreamingCliSteps.ApplyAggregate("count,sum,mean")(state);

        Console.WriteLine("6. Outputting results...\n");
        state = await StreamingCliSteps.ApplySink("console")(state);

        await Task.Delay(5000);
        state.Streaming?.Dispose();
        Console.WriteLine("\n✓ Complete streaming scenario finished!");
    }

    /// <summary>
    /// Runs all streaming examples in sequence.
    /// </summary>
    public static async Task RunAllExamples()
    {
        await RunBasicStreamingExample();
        await Task.Delay(1000);

        await RunMultipleAggregationsExample();
        await Task.Delay(1000);

        await RunTimeBasedWindowExample();
        await Task.Delay(1000);

        await RunSlidingWindowExample();
        await Task.Delay(1000);

        await RunDashboardExample();
        await Task.Delay(1000);

        await RunFileStreamExample();
        await Task.Delay(1000);

        await RunDslStreamingExample();
        await Task.Delay(1000);

        await RunCompleteStreamingScenario();

        Console.WriteLine("\n========================================");
        Console.WriteLine("All streaming examples completed!");
        Console.WriteLine("========================================\n");
    }

    #region Helper Methods

    private static CliPipelineState CreateTestState()
    {
        OllamaProvider provider = new OllamaProvider();
        OllamaChatModel chat = new OllamaChatModel(provider, "llama3");
        OllamaChatAdapter adapter = new OllamaChatAdapter(chat);
        OllamaEmbeddingAdapter embed = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        ToolRegistry tools = new ToolRegistry();
        ToolAwareChatModel llm = new ToolAwareChatModel(adapter, tools);
        TrackedVectorStore store = new TrackedVectorStore();
        PipelineBranch branch = new PipelineBranch("streaming-example", store, DataSource.FromPath(Environment.CurrentDirectory));

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
