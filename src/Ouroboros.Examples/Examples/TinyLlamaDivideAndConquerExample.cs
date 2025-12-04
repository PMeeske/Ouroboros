// <copyright file="TinyLlamaDivideAndConquerExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.Providers;

/// <summary>
/// Demonstrates high-performance divide-and-conquer pattern using TinyLlama.
/// Shows how to process large documents by splitting into chunks,
/// processing in parallel, and merging results into a unified stream.
/// </summary>
public static class TinyLlamaDivideAndConquerExample
{
    /// <summary>
    /// Demonstrates parallel document summarization using divide-and-conquer.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunParallelSummarizationExample()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   TinyLlama Divide-and-Conquer Orchestration Example             ║");
        Console.WriteLine("║   High-Performance Parallel Document Processing                  ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This example demonstrates divide-and-conquer orchestration:");
        Console.WriteLine("  • Split large document into manageable chunks");
        Console.WriteLine("  • Process chunks in parallel with multiple TinyLlama instances");
        Console.WriteLine("  • Merge results into unified output");
        Console.WriteLine();
        Console.WriteLine($"Model: TinyLlama (1.1B parameters, ~600MB)");
        Console.WriteLine($"Strategy: Divide-and-conquer with {Environment.ProcessorCount / 4} parallel workers");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════\n");

        try
        {
            // Setup TinyLlama with performance-optimized preset
            OllamaProvider provider = new OllamaProvider();
            OllamaChatModel tinyLlama = new OllamaChatModel(provider, "tinyllama");
            
            // Apply the TinyLlamaFast preset for optimal parallel performance
            tinyLlama.Settings = OllamaPresets.TinyLlamaFast;
            
            OllamaChatAdapter model = new OllamaChatAdapter(tinyLlama);

            Console.WriteLine($"✓ TinyLlama initialized with optimized parallel settings");
            Console.WriteLine($"  - Small context window for speed");
            Console.WriteLine($"  - Reserved threads for parallel execution");
            Console.WriteLine($"  - Low VRAM mode enabled");
            Console.WriteLine();

            // Configure divide-and-conquer orchestrator
            DivideAndConquerConfig config = new DivideAndConquerConfig(
                MaxParallelism: Math.Max(2, Environment.ProcessorCount / 4),
                ChunkSize: 500,
                MergeResults: true,
                MergeSeparator: "\n\n");

            DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(model, config);

            Console.WriteLine($"✓ Divide-and-conquer orchestrator configured");
            Console.WriteLine($"  - Max parallelism: {config.MaxParallelism}");
            Console.WriteLine($"  - Chunk size: {config.ChunkSize} chars");
            Console.WriteLine();

            // Sample large document (AI ethics paper excerpt)
            string largeDocument = @"
Artificial Intelligence and Machine Learning have revolutionized how we approach complex problems in science, 
business, and everyday life. These technologies enable computers to learn from data and make decisions with 
minimal human intervention. However, as AI systems become more prevalent, concerns about ethics, bias, and 
accountability have grown significantly.

The development of AI systems requires careful consideration of several ethical principles. First, transparency 
is crucial - users should understand how AI systems make decisions that affect them. Second, fairness must be 
ensured across different demographic groups to prevent algorithmic bias. Third, accountability mechanisms need 
to be in place so that someone is responsible when AI systems cause harm.

Machine learning algorithms learn patterns from historical data, which means they can perpetuate and amplify 
existing biases in that data. For example, if training data reflects historical discrimination, the resulting 
model may make biased predictions. Researchers have documented cases where facial recognition systems perform 
worse on certain demographic groups, and hiring algorithms discriminate against particular candidates.

Privacy is another critical concern in the AI era. Machine learning systems often require vast amounts of data 
to function effectively, raising questions about data collection, storage, and usage. The tension between 
collecting enough data for AI to work well and protecting individual privacy rights remains unresolved.

Looking forward, the AI community must work together to develop robust ethical frameworks, create diverse and 
representative datasets, and build accountability into AI systems from the ground up. Only through collaborative 
effort can we ensure that AI benefits all of humanity while minimizing potential harms.
";

            Console.WriteLine("Step 1: Dividing document into chunks");
            Console.WriteLine("───────────────────────────────────────────────────────");
            
            List<string> chunks = orchestrator.DivideIntoChunks(largeDocument);
            
            Console.WriteLine($"Document divided into {chunks.Count} chunks");
            for (int i = 0; i < chunks.Count; i++)
            {
                Console.WriteLine($"  Chunk {i + 1}: {chunks[i].Length} characters");
            }
            Console.WriteLine();

            Console.WriteLine("Step 2: Processing chunks in parallel");
            Console.WriteLine("───────────────────────────────────────────────────────");
            
            string task = "Summarize the following text in 2-3 concise sentences:";
            
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            Result<string, string> result = await orchestrator.ExecuteAsync(task, chunks);
            sw.Stop();

            result.Match(
                success =>
                {
                    Console.WriteLine($"✓ Processing complete in {sw.ElapsedMilliseconds}ms\n");
                    
                    Console.WriteLine("Step 3: Merged Results");
                    Console.WriteLine("───────────────────────────────────────────────────────");
                    Console.WriteLine(success);
                    Console.WriteLine();
                },
                error =>
                {
                    Console.WriteLine($"❌ Processing failed: {error}");
                });

            // Show performance metrics
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");
            Console.WriteLine("Performance Metrics");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");

            IReadOnlyDictionary<string, PerformanceMetrics> metrics = orchestrator.GetMetrics();

            PerformanceMetrics? overallMetrics = metrics.Values
                .FirstOrDefault(m => m.ResourceName == "divide_and_conquer_orchestrator");

            if (overallMetrics != null)
            {
                Console.WriteLine($"\nOverall Orchestration:");
                Console.WriteLine($"  • Total time: {overallMetrics.AverageLatencyMs:F0}ms");
                Console.WriteLine($"  • Success rate: {overallMetrics.SuccessRate:P0}");
            }

            List<PerformanceMetrics> chunkMetrics = metrics.Values
                .Where(m => m.ResourceName.StartsWith("chunk_"))
                .OrderBy(m => m.ResourceName)
                .ToList();

            if (chunkMetrics.Any())
            {
                Console.WriteLine($"\nPer-Chunk Performance:");
                double avgChunkLatency = chunkMetrics.Average(m => m.AverageLatencyMs);
                double minChunkLatency = chunkMetrics.Min(m => m.AverageLatencyMs);
                double maxChunkLatency = chunkMetrics.Max(m => m.AverageLatencyMs);

                Console.WriteLine($"  • Chunks processed: {chunkMetrics.Count}");
                Console.WriteLine($"  • Average latency: {avgChunkLatency:F0}ms");
                Console.WriteLine($"  • Min latency: {minChunkLatency:F0}ms");
                Console.WriteLine($"  • Max latency: {maxChunkLatency:F0}ms");
                Console.WriteLine($"  • Speedup factor: {(avgChunkLatency * chunks.Count) / overallMetrics?.AverageLatencyMs:F2}x");
            }

            Console.WriteLine("\n\n✨ Divide-and-Conquer Orchestration Complete!");
            Console.WriteLine("\n💡 Key Takeaways:");
            Console.WriteLine("   • Multiple TinyLlama instances working in parallel");
            Console.WriteLine("   • Efficient use of CPU/GPU resources across chunks");
            Console.WriteLine("   • Near-linear speedup with parallelism");
            Console.WriteLine("   • Optimal for processing large documents with small models");
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
        {
            Console.WriteLine("\n⚠️  Error: Ollama is not running or TinyLlama is not installed.");
            Console.WriteLine("\nTo run this example:");
            Console.WriteLine("1. Start Ollama: ollama serve");
            Console.WriteLine("2. Pull TinyLlama: ollama pull tinyllama");
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
    /// Demonstrates parallel analysis of multiple documents.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunParallelAnalysisExample()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Multi-Document Parallel Analysis Example                       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            OllamaProvider provider = new OllamaProvider();
            OllamaChatModel tinyLlama = new OllamaChatModel(provider, "tinyllama");
            tinyLlama.Settings = OllamaPresets.TinyLlamaFast;
            OllamaChatAdapter model = new OllamaChatAdapter(tinyLlama);

            DivideAndConquerConfig config = new DivideAndConquerConfig(
                MaxParallelism: 4,
                ChunkSize: 300,
                MergeResults: true);

            DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(model, config);

            Console.WriteLine("✓ TinyLlama orchestrator initialized for parallel analysis\n");

            // Multiple short documents to analyze
            List<string> documents = new List<string>
            {
                "Cloud computing has transformed IT infrastructure by providing on-demand resources.",
                "Quantum computing promises to solve problems that are intractable for classical computers.",
                "Edge computing brings computation closer to data sources, reducing latency.",
                "Serverless architectures allow developers to focus on code without managing infrastructure."
            };

            Console.WriteLine($"Analyzing {documents.Count} documents in parallel...\n");

            string task = "Extract the key technology mentioned and its main benefit:";

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            Result<string, string> result = await orchestrator.ExecuteAsync(task, documents);
            sw.Stop();

            result.Match(
                success =>
                {
                    Console.WriteLine($"✓ Analysis complete in {sw.ElapsedMilliseconds}ms\n");
                    Console.WriteLine("Results:");
                    Console.WriteLine(success);
                },
                error => Console.WriteLine($"❌ Analysis failed: {error}"));

            Console.WriteLine("\n✨ Parallel analysis complete!");
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused"))
        {
            Console.WriteLine("\n⚠️  Ollama not running. Run 'dotnet run -- setup --ollama' for help.");
        }
    }

    /// <summary>
    /// Runs all TinyLlama divide-and-conquer examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n🚀 Starting TinyLlama Divide-and-Conquer Examples\n");

        await RunParallelSummarizationExample();

        Console.WriteLine("\n" + new string('═', 70) + "\n");

        await RunParallelAnalysisExample();

        Console.WriteLine("\n✅ All examples completed!\n");
    }
}
