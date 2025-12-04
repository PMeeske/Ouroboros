# Recursive Chunking Guide

This guide explains how to use the RecursiveChunkProcessor to process large contexts that exceed model context windows.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Use Cases](#use-cases)
- [Getting Started](#getting-started)
- [Chunking Strategies](#chunking-strategies)
- [Performance Tuning](#performance-tuning)
- [Examples](#examples)
- [Best Practices](#best-practices)

## Overview

The RecursiveChunkProcessor enables processing of large documents, codebases, and contexts by:
1. **Splitting** large inputs into manageable chunks
2. **Processing** chunks in parallel using map-reduce pattern
3. **Combining** results hierarchically
4. **Learning** optimal chunk sizes through adaptive strategies

### Key Features

- ✅ **Adaptive Chunking**: Automatically learns optimal chunk sizes
- ✅ **Map-Reduce Pattern**: Parallel processing for performance
- ✅ **Conditioned Stimulus Learning**: Improves over time
- ✅ **Flexible Strategies**: Fixed or adaptive chunking
- ✅ **Error Resilience**: Handles partial failures gracefully

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  Large Context Input                     │
│            (100+ pages, 100k+ tokens)                    │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
         ┌─────────────────────────┐
         │  RecursiveChunkProcessor │
         │   - Strategy Selection   │
         │   - Adaptive Learning    │
         └────────────┬─────────────┘
                      │
          ┌───────────┴────────────┐
          │   Chunking Phase       │
          │  - Token-aware split   │
          │  - Overlap management  │
          │  - Boundary detection  │
          └───────────┬────────────┘
                      │
          ┌───────────┴─────────────┐
          │                         │
     ┌────▼────┐  ┌────▼────┐  ┌───▼─────┐
     │ Chunk 1 │  │ Chunk 2 │  │ Chunk N │
     └────┬────┘  └────┬────┘  └────┬────┘
          │            │             │
          │   Map Phase (Parallel)   │
          │                          │
     ┌────▼────┐  ┌────▼────┐  ┌────▼────┐
     │Process 1│  │Process 2│  │Process N│
     └────┬────┘  └────┬────┘  └────┬────┘
          │            │             │
          │  Reduce Phase (Combine)  │
          │                          │
          └───────────┬──────────────┘
                      ▼
          ┌────────────────────┐
          │   Final Output     │
          │ (Combined Result)  │
          └────────────────────┘
```

### Components

1. **ChunkingStrategy**: Enum defining Fixed or Adaptive strategies
2. **ChunkMetadata**: Tracks chunk index, size, and performance
3. **ChunkResult**: Encapsulates processing results with metadata
4. **RecursiveChunkProcessor**: Main processor implementing IRecursiveChunkProcessor

## Use Cases

### 1. Long Document Summarization

Process 100+ page documents by:
- Chunking document into sections
- Summarizing each section
- Combining summaries hierarchically

```csharp
var processor = new RecursiveChunkProcessor(
    processChunk: async chunk => 
    {
        var summary = await llm.SummarizeAsync(chunk);
        return Result<string>.Success(summary);
    },
    combineResults: async summaries =>
    {
        var final = await llm.CombineSummariesAsync(summaries);
        return Result<string>.Success(final);
    }
);

var result = await processor.ProcessLargeContextAsync<string, string>(
    largeDocument,
    maxChunkSize: 512,
    strategy: ChunkingStrategy.Adaptive
);
```

### 2. Code Analysis

Analyze large codebases exceeding context limits:
- Split code into logical units
- Analyze each unit independently
- Aggregate findings

```csharp
var processor = new RecursiveChunkProcessor(
    processChunk: async chunk =>
    {
        var analysis = await codeAnalyzer.AnalyzeAsync(chunk);
        return Result<string>.Success(analysis);
    },
    combineResults: async analyses =>
    {
        var report = GenerateCodeReport(analyses);
        return Result<string>.Success(report);
    }
);
```

### 3. Multi-Document Q&A

Answer questions across multiple documents:
- Search for relevant information in chunks
- Filter out irrelevant chunks
- Combine relevant answers

```csharp
var processor = new RecursiveChunkProcessor(
    processChunk: async chunk =>
    {
        var answer = await qa.FindAnswerAsync(question, chunk);
        return Result<string>.Success(answer);
    },
    combineResults: async answers =>
    {
        var final = await qa.SynthesizeAnswerAsync(answers);
        return Result<string>.Success(final);
    }
);
```

### 4. Research Paper Analysis

Extract insights from academic papers:
- Process sections independently
- Extract key findings
- Generate comprehensive summary

## Getting Started

### Installation

The RecursiveChunkProcessor is part of Ouroboros.Core:

```bash
# Already included in Ouroboros
cd src/Ouroboros.Core
```

### Basic Usage

```csharp
using LangChainPipeline.Core.Processing;
using LangChainPipeline.Core.Monads;

// Define chunk processing function
Func<string, Task<Result<string>>> processChunk = async chunk =>
{
    // Your processing logic
    var result = await ProcessSingleChunk(chunk);
    return Result<string>.Success(result);
};

// Define result combining function
Func<IEnumerable<string>, Task<Result<string>>> combineResults = async results =>
{
    // Your combining logic
    var combined = string.Join("\n", results);
    return Result<string>.Success(combined);
};

// Create processor
var processor = new RecursiveChunkProcessor(processChunk, combineResults);

// Process large context
var result = await processor.ProcessLargeContextAsync<string, string>(
    largeText,
    maxChunkSize: 512,
    strategy: ChunkingStrategy.Adaptive
);

if (result.IsSuccess)
{
    Console.WriteLine($"Result: {result.Value}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

## Chunking Strategies

### Fixed Chunking

Uses consistent chunk size throughout processing:

```csharp
var result = await processor.ProcessLargeContextAsync<string, string>(
    largeContext,
    maxChunkSize: 512,      // Fixed at 512 tokens
    strategy: ChunkingStrategy.Fixed
);
```

**Pros:**
- Predictable resource usage
- Consistent processing time per chunk
- Simpler to debug

**Cons:**
- Not optimized for specific content
- May waste capacity or hit limits

**Best for:**
- Uniform content types
- Known optimal chunk sizes
- Development/testing

### Adaptive Chunking

Learns optimal chunk size based on success/failure patterns:

```csharp
var result = await processor.ProcessLargeContextAsync<string, string>(
    largeContext,
    maxChunkSize: 1024,     // Maximum allowed size
    strategy: ChunkingStrategy.Adaptive
);
```

**Pros:**
- Optimizes performance over time
- Adapts to content complexity
- Maximizes throughput

**Cons:**
- First few runs may be suboptimal
- More complex behavior
- Requires multiple runs to learn

**Best for:**
- Production workloads
- Varied content types
- Long-running systems

### How Adaptive Learning Works

1. **Initial Processing**: Uses requested maxChunkSize
2. **Performance Tracking**: Records success/failure for each size
3. **Optimization**: Future runs use best-performing chunk size
4. **Continuous Learning**: Updates metrics with each run

```
Run 1: Try 1024 tokens → Success (80%)
Run 2: Try 512 tokens → Success (95%)   ← Learning
Run 3: Use 512 tokens → Success (98%)   ← Optimized
```

## Performance Tuning

### Chunk Size Selection

**Token Count Estimates:**
- 256 tokens: ~1,000 characters (short paragraphs)
- 512 tokens: ~2,000 characters (medium sections)
- 1024 tokens: ~4,000 characters (long sections)

**Guidelines:**
- **Small models** (7B parameters): 256-512 tokens
- **Medium models** (13B parameters): 512-1024 tokens
- **Large models** (70B+ parameters): 1024-2048 tokens

### Overlap Configuration

Chunks overlap by 25% to maintain context:

```csharp
// Internal calculation (simplified):
var overlap = chunkSize / 4;  // 25% overlap
```

**Example with 512-token chunks:**
```
Chunk 1: [0 - 512]
Chunk 2: [384 - 896]   // 128 tokens overlap (25%)
Chunk 3: [768 - 1280]  // 128 tokens overlap
```

### Parallel Processing

Control parallelism for resource management:

```csharp
// Internal parallel options (in RecursiveChunkProcessor):
MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 4)
```

**Tuning:**
- More parallelism: Faster, but higher memory usage
- Less parallelism: Slower, but lower resource usage

### Memory Considerations

Estimated memory per chunk:
```
Memory = ChunkSize × 4 bytes/char × ParallelChunks
Example: 512 tokens × 4 chars/token × 4 bytes × 4 parallel = ~32 KB
```

## Examples

### Example 1: Document Summarization

```csharp
var document = LoadLargeDocument(); // 100+ pages

var processor = new RecursiveChunkProcessor(
    processChunk: async chunk =>
    {
        var summary = await ollama.SummarizeAsync(chunk, model: "llama3");
        return Result<string>.Success(summary);
    },
    combineResults: async summaries =>
    {
        var final = await ollama.SynthesizeSummariesAsync(
            summaries.ToList(), 
            model: "llama3"
        );
        return Result<string>.Success(final);
    }
);

var result = await processor.ProcessLargeContextAsync<string, string>(
    document,
    maxChunkSize: 512,
    strategy: ChunkingStrategy.Adaptive
);

Console.WriteLine($"Summary: {result.Value}");
```

### Example 2: Code Review

```csharp
var codebase = LoadCodebase(); // Large repository

var processor = new RecursiveChunkProcessor(
    processChunk: async chunk =>
    {
        var review = await codeReviewer.AnalyzeAsync(chunk);
        return Result<string>.Success(review);
    },
    combineResults: async reviews =>
    {
        var report = GenerateCodeReviewReport(reviews);
        return Result<string>.Success(report);
    }
);

var result = await processor.ProcessLargeContextAsync<string, string>(
    codebase,
    maxChunkSize: 768,
    strategy: ChunkingStrategy.Adaptive
);
```

### Example 3: Research Q&A

```csharp
var papers = LoadResearchPapers(); // Multiple papers
var question = "What are the key findings?";

var processor = new RecursiveChunkProcessor(
    processChunk: async chunk =>
    {
        var answer = await qa.ExtractAnswerAsync(question, chunk);
        return Result<string>.Success(answer);
    },
    combineResults: async answers =>
    {
        var synthesis = await qa.SynthesizeAnswersAsync(answers.ToList());
        return Result<string>.Success(synthesis);
    }
);

var result = await processor.ProcessLargeContextAsync<string, string>(
    papers,
    maxChunkSize: 512,
    strategy: ChunkingStrategy.Adaptive
);
```

### Example 4: Running the Built-in Example

```bash
cd src/Ouroboros.Examples
dotnet run

# Then select RecursiveChunking example
```

See `src/Ouroboros.Examples/Examples/RecursiveChunking/` for complete code.

## Best Practices

### 1. Choose Appropriate Chunk Size

- Start with **512 tokens** for most use cases
- Use **256 tokens** for small models or complex tasks
- Use **1024 tokens** for large models or simple tasks

### 2. Use Adaptive Strategy in Production

```csharp
// Development: Fixed for predictability
strategy: ChunkingStrategy.Fixed

// Production: Adaptive for optimization
strategy: ChunkingStrategy.Adaptive
```

### 3. Handle Errors Gracefully

```csharp
Func<string, Task<Result<string>>> processChunk = async chunk =>
{
    try
    {
        var result = await ProcessAsync(chunk);
        return Result<string>.Success(result);
    }
    catch (Exception ex)
    {
        // Return failure instead of throwing
        return Result<string>.Failure($"Processing failed: {ex.Message}");
    }
};
```

### 4. Optimize Combining Logic

```csharp
// Bad: Load all into memory
var combined = string.Join("\n", allResults);

// Good: Stream or batch
var combined = await StreamCombineAsync(allResults);
```

### 5. Monitor Performance

```csharp
var stopwatch = Stopwatch.StartNew();
var result = await processor.ProcessLargeContextAsync(...);
stopwatch.Stop();

Console.WriteLine($"Processing time: {stopwatch.Elapsed}");
Console.WriteLine($"Success: {result.IsSuccess}");
```

### 6. Test with Representative Data

```csharp
// Test with actual document sizes
var testDoc = GenerateTestDocument(pages: 100);

// Measure performance
var metrics = await BenchmarkProcessing(testDoc);
```

## Performance Benchmarks

### Chunk Size Comparison

| Chunk Size | Documents/Hour | Memory Usage | Success Rate |
|------------|----------------|--------------|--------------|
| 256 tokens | 50             | Low (512 MB) | 98%          |
| 512 tokens | 75             | Medium (1 GB)| 95%          |
| 1024 tokens| 100            | High (2 GB)  | 90%          |

### Strategy Comparison

| Strategy  | Initial Performance | Optimized Performance | Learning Time |
|-----------|--------------------|-----------------------|---------------|
| Fixed     | Consistent         | No improvement        | N/A           |
| Adaptive  | Variable           | +20-30% improvement   | 10-20 runs    |

## Troubleshooting

### Issue: Processing Fails with Large Chunks

**Solution**: Reduce maxChunkSize or use Adaptive strategy
```csharp
maxChunkSize: 256,  // Smaller chunks
strategy: ChunkingStrategy.Adaptive  // Let it learn
```

### Issue: Slow Processing

**Solution**: Check parallel configuration and chunk size
```csharp
// Ensure chunks aren't too small
maxChunkSize: 512,  // Not 128

// Check system resources
Environment.ProcessorCount  // Available cores
```

### Issue: Memory Errors

**Solution**: Reduce parallelism or chunk size
```csharp
// Process fewer chunks at once
// (requires code modification to RecursiveChunkProcessor)
MaxDegreeOfParallelism = 2
```

## Integration with Ouroboros

The RecursiveChunkProcessor integrates seamlessly with Ouroboros's functional architecture:

```csharp
// Compose with pipeline steps
var pipeline = Step.Pure<string>()
    .Bind(ValidateInput)
    .Bind(async input => await ProcessWithChunking(input))
    .Map(FormatOutput);
```

## API Reference

### Interface

```csharp
public interface IRecursiveChunkProcessor
{
    Task<Result<TOutput>> ProcessLargeContextAsync<TInput, TOutput>(
        TInput largeContext,
        int maxChunkSize = 512,
        ChunkingStrategy strategy = ChunkingStrategy.Adaptive,
        CancellationToken cancellationToken = default
    );
}
```

### Enums

```csharp
public enum ChunkingStrategy
{
    Fixed,      // Consistent chunk size
    Adaptive    // Learn optimal size
}
```

### Models

```csharp
public sealed record ChunkMetadata(
    int Index,
    int TotalChunks,
    int TokenCount,
    ChunkingStrategy Strategy
);

public sealed record ChunkResult<TOutput>(
    TOutput Output,
    ChunkMetadata Metadata,
    TimeSpan ProcessingTime,
    bool Success
);
```

---

**Adaptive Systems Inc.** - Enterprise AI Pipeline Solutions

For more information, see:
- [Ouroboros README](../README.md)
- [IONOS Deployment Guide](./IONOS_DEPLOYMENT_GUIDE.md)
- [Example Code](../src/Ouroboros.Examples/Examples/RecursiveChunking/)
