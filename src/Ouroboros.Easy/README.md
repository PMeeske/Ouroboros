# Ouroboros.Easy

**Simplified fluent API for Ouroboros** - An easy-to-use interface that abstracts away the monadic core complexity while still allowing advanced composability for power users.

## Overview

Ouroboros.Easy provides a beginner-friendly entry point to the Ouroboros AI pipeline system. It offers a fluent builder API that internally delegates to the sophisticated monadic DSL and Kleisli arrow composition system.

## Quick Start

```csharp
using Ouroboros.Easy;
using Ouroboros.Providers;

// Configure models
var chatModel = new ToolAwareChatModel(/* your LLM provider */);
var embeddingModel = new OllamaEmbeddingModel(/* your embedding config */);

// Create and run a simple pipeline
var result = await Pipeline.Create("quantum computing")
    .Draft()
    .Critique()
    .Improve()
    .WithChatModel(chatModel)
    .WithEmbeddingModel(embeddingModel)
    .RunAsync();

Console.WriteLine(result.Output);
```

## Core Concepts

### Pipeline Creation

Start with `Pipeline.Create()` or use one of the convenient factory methods:

```csharp
// Basic creation
var pipeline = Pipeline.Create("machine learning");

// Pre-configured patterns
var basic = Pipeline.BasicReasoning("neural networks");
var full = Pipeline.FullReasoning("transformer architecture");
var summary = Pipeline.Summarize("GPT-4 paper");
var iterative = Pipeline.IterativeReasoning("quantum algorithms", iterations: 3);
```

### Pipeline Operations

Chain operations to build your reasoning pipeline:

- **`Think()`** - Generates a reasoning process before drafting
- **`Draft()`** - Creates an initial response based on context
- **`Critique()`** - Analyzes and critiques the previous output
- **`Improve()`** - Generates an improved version based on critique
- **`Summarize()`** - Creates a concise summary of the results

```csharp
var pipeline = Pipeline.Create("AI safety")
    .Think()           // Initial reasoning
    .Draft()           // Create first draft
    .Critique()        // Analyze the draft
    .Improve()         // Improve based on critique
    .Critique()        // Second critique (iterative)
    .Improve()         // Further improvement
    .Summarize();      // Final summary
```

### Configuration

Configure the pipeline with various options:

```csharp
var pipeline = Pipeline.Create("distributed systems")
    .Draft()
    .Critique()
    .Improve()
    .WithModel("llama3")                    // Model selection
    .WithTemperature(0.7)                   // Generation temperature
    .WithContextDocuments(10)               // RAG document count
    .WithChatModel(chatModel)               // Required: chat model
    .WithEmbeddingModel(embeddingModel)     // Required: embedding model
    .WithVectorStore(customVectorStore)     // Optional: custom vector store
    .WithDataSource(customDataSource)       // Optional: custom data source
    .WithTools(toolRegistry);               // Optional: tool registry
```

### Execution

Execute the pipeline and access results:

```csharp
var result = await pipeline.RunAsync(cancellationToken);

if (result.Success)
{
    Console.WriteLine($"Output: {result.Output}");
    
    // Access reasoning steps
    foreach (var step in result.GetReasoningSteps())
    {
        Console.WriteLine($"Step: {step.Kind}");
    }
    
    // Access tool executions
    foreach (var tool in result.GetToolExecutions())
    {
        Console.WriteLine($"Tool: {tool.Name}, Result: {tool.Result}");
    }
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

## Learning Progression: Easy → DSL → Core

Ouroboros.Easy is designed to help you learn the system progressively:

### Level 1: Easy API (Beginners)

```csharp
var result = await Pipeline.Create("quantum computing")
    .Draft()
    .Critique()
    .Improve()
    .WithChatModel(chatModel)
    .WithEmbeddingModel(embeddingModel)
    .RunAsync();
```

**Who**: Beginners, rapid prototyping, simple use cases  
**Focus**: Get results quickly without understanding internals

### Level 2: DSL Representation (Intermediate)

```csharp
var pipeline = Pipeline.Create("quantum computing")
    .Draft()
    .Critique()
    .Improve();

// View the DSL representation
string dsl = pipeline.ToDSL();
Console.WriteLine(dsl);
```

Output:
```
Pipeline.About("quantum computing")
  .Draft()
  .Critique()
  .Improve()
  .WithModel("llama3")
  .WithTemperature(0.7)
  .WithContextDocuments(8)
  .RunAsync()
```

**Who**: Intermediate users wanting to understand pipeline structure  
**Focus**: Learn the DSL syntax and pipeline composition

### Level 3: Core Monadic Architecture (Advanced)

```csharp
var pipeline = Pipeline.Create("quantum computing")
    .Draft()
    .Critique()
    .Improve()
    .WithChatModel(chatModel)
    .WithEmbeddingModel(embeddingModel);

// Extract the underlying Kleisli arrow
Step<PipelineBranch, PipelineBranch> arrow = pipeline.ToCore();

// Now you can compose it with other arrows using monadic operations
var composedArrow = arrow
    .Pipe(customArrow)
    .Map(transformBranch);

// Execute directly
var branch = new PipelineBranch("test", vectorStore, dataSource);
var result = await composedArrow(branch);
```

**Who**: Power users, library developers, complex compositions  
**Focus**: Full control over monadic composition and Kleisli arrows

## Common Patterns

### Iterative Refinement

```csharp
var result = await Pipeline.IterativeReasoning("AI alignment", iterations: 3)
    .WithChatModel(chatModel)
    .WithEmbeddingModel(embeddingModel)
    .RunAsync();
```

### Custom Tool Integration

```csharp
var tools = new ToolRegistry();
tools.RegisterTool(new WebSearchTool());
tools.RegisterTool(new CalculatorTool());

var result = await Pipeline.Create("complex calculation")
    .Draft()
    .Critique()
    .Improve()
    .WithTools(tools)
    .WithChatModel(chatModel)
    .WithEmbeddingModel(embeddingModel)
    .RunAsync();
```

### Streaming Results

For streaming results, you can use the underlying core API:

```csharp
var pipeline = Pipeline.Create("quantum computing")
    .Draft()
    .WithChatModel(chatModel)
    .WithEmbeddingModel(embeddingModel);

// Convert to core arrow for streaming capabilities
var arrow = pipeline.ToCore();

// Use ReasoningArrows.StreamingDraftArrow() for streaming
// (See Ouroboros.Pipeline documentation for streaming examples)
```

## Error Handling

```csharp
var result = await pipeline.RunAsync();

if (!result.Success)
{
    Console.WriteLine($"Pipeline failed: {result.Error}");
    
    // Access partial results from the branch
    var completedSteps = result.Branch.Events.OfType<ReasoningStep>();
    Console.WriteLine($"Completed {completedSteps.Count()} steps before failure");
}
```

## Best Practices

1. **Always configure required models**: `WithChatModel()` and `WithEmbeddingModel()` are required
2. **Start simple**: Begin with `BasicReasoning()` or `FullReasoning()` patterns
3. **Iterate gradually**: Use `IterativeReasoning()` for progressive refinement
4. **Use cancellation tokens**: Pass `CancellationToken` to `RunAsync()` for long-running operations
5. **Handle errors gracefully**: Check `result.Success` before accessing output
6. **Progress to core gradually**: Use `ToDSL()` and `ToCore()` to understand internals

## Advanced Usage

### Accessing the Pipeline Branch

```csharp
var result = await pipeline.RunAsync();

// The Branch contains full execution history (event sourcing)
PipelineBranch branch = result.Branch;

// Replay events
foreach (var evt in branch.Events)
{
    Console.WriteLine($"Event: {evt.GetType().Name} at {evt.Timestamp}");
}

// Access vector store
var docs = await branch.Store.GetSimilarDocuments(embedModel, "query", 5);
```

### Custom Vector Store

```csharp
var customStore = new MyCustomVectorStore();

var result = await Pipeline.Create("topic")
    .Draft()
    .WithVectorStore(customStore)
    .WithChatModel(chatModel)
    .WithEmbeddingModel(embeddingModel)
    .RunAsync();
```

## Comparison with Core API

| Feature | Easy API | Core API |
|---------|----------|----------|
| Learning Curve | Low | High |
| Code Verbosity | Low | Medium |
| Type Safety | High | Very High |
| Composability | Good | Excellent |
| Flexibility | Good | Excellent |
| Error Handling | Simplified | Full Control |
| Recommended For | Beginners, Prototypes | Production, Complex Pipelines |

## Examples

See the `examples/` directory for complete working examples:
- Basic pipeline usage
- Iterative refinement
- Tool integration
- Custom configuration
- Progression from Easy to Core

## Next Steps

1. Try the Quick Start example
2. Explore pre-configured pipeline patterns
3. Experiment with configuration options
4. Use `ToDSL()` to understand the DSL representation
5. Use `ToCore()` to access Kleisli arrows
6. Read the main Ouroboros documentation for deep dives

## Related Documentation

- [Main Ouroboros README](../../README.md) - Complete system documentation
- [Core Architecture](../../docs/ARCHITECTURAL_LAYERS.md) - Understanding the monadic core
- [Pipeline Documentation](../Ouroboros.Pipeline/README.md) - Advanced pipeline features
- [Examples](../../examples/) - Complete working examples

## Philosophy

Ouroboros.Easy embodies the principle: **"Simple things should be simple, complex things should be possible"**

- **For beginners**: A gentle introduction without monadic complexity
- **For learners**: Clear progression path from simple to advanced
- **For power users**: Full access to underlying architecture when needed

The Easy API doesn't hide the power of Ouroboros—it provides a friendly doorway to it.
