using LangChainPipeline.Core.Monads;
using LangChainPipeline.Core.Processing;

namespace LangChainPipeline.Examples.RecursiveChunking;

/// <summary>
/// Example demonstrating RecursiveChunkProcessor for processing large documents.
/// This example shows how to:
/// 1. Process documents that exceed model context windows
/// 2. Use adaptive chunking to optimize processing
/// 3. Implement map-reduce pattern for parallel processing
/// 4. Combine chunk results hierarchically
/// </summary>
public static class RecursiveChunkingExample
{
    /// <summary>
    /// Runs the recursive chunking example with a large document.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== RecursiveChunkProcessor Example ===");
        Console.WriteLine();

        // Simulate a large document (e.g., 100-page document)
        var largeDocument = GenerateLargeDocument();
        Console.WriteLine($"Document size: {largeDocument.Length} characters (~{largeDocument.Length / 4} tokens)");
        Console.WriteLine();

        // Example 1: Document Summarization with Fixed Chunking
        await SummarizeDocumentWithFixedChunkingAsync(largeDocument);
        Console.WriteLine();

        // Example 2: Document Summarization with Adaptive Chunking
        await SummarizeDocumentWithAdaptiveChunkingAsync(largeDocument);
        Console.WriteLine();

        // Example 3: Multi-Document Q&A
        await MultiDocumentQuestionAnsweringAsync();
        Console.WriteLine();

        // Example 4: Code Analysis
        await AnalyzeLargeCodebaseAsync();
    }

    /// <summary>
    /// Example 1: Summarize a large document using fixed chunk size.
    /// </summary>
    private static async Task SummarizeDocumentWithFixedChunkingAsync(string document)
    {
        Console.WriteLine("Example 1: Document Summarization (Fixed Chunking)");
        Console.WriteLine("---------------------------------------------------");

        // Define processing function for each chunk
        Func<string, Task<Result<string>>> processChunk = async chunk =>
        {
            // Simulate LLM processing (in real scenario, call actual LLM)
            await Task.Delay(50); // Simulate API call
            var summary = $"Summary of {chunk.Length} chars: {chunk.Substring(0, Math.Min(100, chunk.Length))}...";
            return Result<string>.Success(summary);
        };

        // Define function to combine chunk summaries
        Func<IEnumerable<string>, Task<Result<string>>> combineResults = async summaries =>
        {
            await Task.Delay(50); // Simulate combining
            var combined = string.Join("\n\n", summaries.Select((s, i) => $"Section {i + 1}: {s}"));
            return Result<string>.Success($"=== Final Summary ===\n{combined}");
        };

        var processor = new RecursiveChunkProcessor(processChunk, combineResults);

        var startTime = DateTime.UtcNow;
        var result = await processor.ProcessLargeContextAsync<string, string>(
            document,
            maxChunkSize: 512,
            strategy: ChunkingStrategy.Fixed);

        var elapsed = DateTime.UtcNow - startTime;

        if (result.IsSuccess)
        {
            Console.WriteLine("✓ Successfully processed document");
            Console.WriteLine($"Processing time: {elapsed.TotalSeconds:F2}s");
            Console.WriteLine($"Result preview: {result.Value.Substring(0, Math.Min(200, result.Value.Length))}...");
        }
        else
        {
            Console.WriteLine($"✗ Failed: {result.Error}");
        }
    }

    /// <summary>
    /// Example 2: Summarize a large document using adaptive chunking.
    /// </summary>
    private static async Task SummarizeDocumentWithAdaptiveChunkingAsync(string document)
    {
        Console.WriteLine("Example 2: Document Summarization (Adaptive Chunking)");
        Console.WriteLine("------------------------------------------------------");

        var attemptCount = 0;

        Func<string, Task<Result<string>>> processChunk = async chunk =>
        {
            await Task.Delay(50);
            attemptCount++;

            // Simulate occasional failures with very large chunks
            if (chunk.Length > 3000 && new Random().NextDouble() < 0.2)
            {
                return Result<string>.Failure("Chunk too large for processing");
            }

            return Result<string>.Success($"Processed chunk {attemptCount}: {chunk.Length} chars");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineResults = async summaries =>
        {
            await Task.Delay(50);
            return Result<string>.Success($"Combined {summaries.Count()} summaries using adaptive learning");
        };

        var processor = new RecursiveChunkProcessor(processChunk, combineResults);

        // Process multiple times to demonstrate adaptive learning
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine($"\nAttempt {i + 1}:");
            var result = await processor.ProcessLargeContextAsync<string, string>(
                document,
                maxChunkSize: 1024,
                strategy: ChunkingStrategy.Adaptive);

            if (result.IsSuccess)
            {
                Console.WriteLine($"  ✓ Success: {result.Value}");
            }
            else
            {
                Console.WriteLine($"  ✗ Failed: {result.Error}");
            }
        }

        Console.WriteLine($"\nTotal processing attempts: {attemptCount}");
        Console.WriteLine("Note: Adaptive strategy learns optimal chunk size over time");
    }

    /// <summary>
    /// Example 3: Multi-document question answering.
    /// </summary>
    private static async Task MultiDocumentQuestionAnsweringAsync()
    {
        Console.WriteLine("Example 3: Multi-Document Question Answering");
        Console.WriteLine("---------------------------------------------");

        var documents = new[]
        {
            "Document 1: MonadicPipeline is a functional programming-based AI pipeline system...",
            "Document 2: IONOS Cloud provides enterprise-grade Kubernetes infrastructure...",
            "Document 3: RecursiveChunkProcessor enables processing of large contexts..."
        };

        var allDocs = string.Join("\n\n", documents);
        var question = "What are the key features of the system?";

        Console.WriteLine($"Question: {question}");
        Console.WriteLine($"Processing {documents.Length} documents...");

        Func<string, Task<Result<string>>> findAnswersInChunk = async chunk =>
        {
            await Task.Delay(30);
            // Simulate finding relevant information
            if (chunk.Contains("MonadicPipeline") || chunk.Contains("RecursiveChunkProcessor"))
            {
                return Result<string>.Success($"Found: {chunk.Substring(0, Math.Min(100, chunk.Length))}");
            }
            return Result<string>.Success("No relevant information");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineAnswers = async answers =>
        {
            await Task.Delay(30);
            var relevant = answers.Where(a => !a.Contains("No relevant")).ToList();
            return Result<string>.Success($"Answer based on {relevant.Count} relevant chunks:\n{string.Join("\n", relevant)}");
        };

        var processor = new RecursiveChunkProcessor(findAnswersInChunk, combineAnswers);
        var result = await processor.ProcessLargeContextAsync<string, string>(
            allDocs,
            maxChunkSize: 256,
            strategy: ChunkingStrategy.Fixed);

        if (result.IsSuccess)
        {
            Console.WriteLine($"✓ Answer: {result.Value}");
        }
        else
        {
            Console.WriteLine($"✗ Failed: {result.Error}");
        }
    }

    /// <summary>
    /// Example 4: Analyze a large codebase.
    /// </summary>
    private static async Task AnalyzeLargeCodebaseAsync()
    {
        Console.WriteLine("Example 4: Large Codebase Analysis");
        Console.WriteLine("-----------------------------------");

        var codebase = GenerateSampleCodebase();
        Console.WriteLine($"Analyzing codebase: {codebase.Length} characters");

        Func<string, Task<Result<string>>> analyzeChunk = async chunk =>
        {
            await Task.Delay(40);
            var functions = chunk.Split("public").Length - 1;
            var classes = chunk.Split("class").Length - 1;
            return Result<string>.Success($"Found {classes} classes, {functions} functions");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineAnalysis = async analyses =>
        {
            await Task.Delay(40);
            return Result<string>.Success($"Code analysis complete:\n{string.Join("\n", analyses)}");
        };

        var processor = new RecursiveChunkProcessor(analyzeChunk, combineAnalysis);
        var result = await processor.ProcessLargeContextAsync<string, string>(
            codebase,
            maxChunkSize: 768,
            strategy: ChunkingStrategy.Adaptive);

        if (result.IsSuccess)
        {
            Console.WriteLine($"✓ Analysis results:\n{result.Value}");
        }
        else
        {
            Console.WriteLine($"✗ Failed: {result.Error}");
        }
    }

    /// <summary>
    /// Generates a simulated large document for testing.
    /// </summary>
    private static string GenerateLargeDocument()
    {
        var sections = new[]
        {
            "Introduction: MonadicPipeline is an advanced AI pipeline system built on functional programming principles.",
            "Architecture: The system uses category theory, monadic composition, and Kleisli arrows for type-safe operations.",
            "Features: Includes support for LangChain integration, vector stores, and distributed tracing.",
            "Deployment: Can be deployed to Kubernetes clusters including Azure AKS, AWS EKS, GCP GKE, and IONOS Cloud.",
            "RecursiveChunkProcessor: Enables processing of large contexts by splitting them into manageable chunks.",
            "Adaptive Learning: The system learns optimal chunk sizes through conditioned stimulus learning.",
            "Performance: Optimized for parallel processing with configurable resource limits.",
            "Use Cases: Document intelligence, developer tools, content creation, and multi-document QA.",
        };

        // Repeat sections to create a ~100-page document simulation
        var paragraphs = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            foreach (var section in sections)
            {
                paragraphs.Add($"{section} {new string('x', 200)}"); // Pad to simulate paragraphs
            }
        }

        return string.Join("\n\n", paragraphs);
    }

    /// <summary>
    /// Generates a simulated codebase for testing.
    /// </summary>
    private static string GenerateSampleCodebase()
    {
        var code = @"
using System;

namespace Sample
{
    public class DataProcessor
    {
        public void ProcessData() { }
        public string FormatData(string input) => input;
    }

    public class ResultHandler
    {
        public void HandleSuccess() { }
        public void HandleFailure() { }
    }
}";

        // Repeat code to simulate larger codebase
        return string.Join("\n\n", Enumerable.Repeat(code, 20));
    }
}
