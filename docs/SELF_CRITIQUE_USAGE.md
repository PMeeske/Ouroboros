# Self-Reflective Agent Usage Examples

This document provides examples of using the new self-reflective agent prompt system in Ouroboros.

## Command-Line Interface (CLI)

### Basic Self-Critique with Pipeline Command

```bash
# Run a pipeline with self-critique mode and 2 iterations
dotnet run --project src/Ouroboros.CLI -- pipeline \
  --dsl "SetTopic('Explain monads') | UseSelfCritique" \
  --critique-iterations 2 \
  --trace

# Use streaming self-critique for real-time output
dotnet run --project src/Ouroboros.CLI -- pipeline \
  --dsl "SetTopic('Design patterns') | UseStreamingSelfCritique('3')" \
  --trace
```

### MeTTa Integration

```bash
# Apply self-critique to MeTTa reasoning output
dotnet run --project src/Ouroboros.CLI -- pipeline \
  --dsl "MottoInit | MottoOllama('msg=Explain functional programming') | MottoSelfCritique('2')" \
  --trace
```

## DSL Usage

### Basic Self-Critique Step

```csharp
// Use the self-critique DSL token in a pipeline
var dsl = "SetTopic('Write a technical article') | UseSelfCritique";

// With specific iteration count
var dsl = "SetTopic('Code review') | UseSelfCritique('3')";
```

### Streaming Self-Critique

```csharp
// Stream output as critique progresses
var dsl = "SetTopic('System design') | UseStreamingSelfCritique('2')";
```

### Composing with Other Steps

```csharp
// Chain with ingestion and retrieval
var dsl = "UseIngest | SetTopic('Summarize') | UseDraft | UseSelfCritique('2')";

// Combine with MeTTa reasoning
var dsl = "MottoInit | MottoChat('Hello') | MottoSelfCritique";
```

## Programmatic API

### Basic Usage

```csharp
using LangChainPipeline.Agent;
using LangChainPipeline.Pipeline.Branches;

// Create the agent
var agent = new SelfCritiqueAgent(llm, tools, embed);

// Generate with critique
Result<SelfCritiqueResult, string> result = await agent.GenerateWithCritiqueAsync(
    branch: initialBranch,
    topic: "Explain design patterns",
    query: "design patterns software",
    iterations: 2);

if (result.IsSuccess)
{
    var critiqueResult = result.Value;
    Console.WriteLine($"Draft: {critiqueResult.Draft}");
    Console.WriteLine($"Critique: {critiqueResult.Critique}");
    Console.WriteLine($"Improved: {critiqueResult.ImprovedResponse}");
    Console.WriteLine($"Confidence: {critiqueResult.Confidence}");
    Console.WriteLine($"Iterations: {critiqueResult.IterationsPerformed}");
}
```

### With Custom Timeout

```csharp
// Create agent with custom timeout per iteration
var agent = new SelfCritiqueAgent(
    llm, 
    tools, 
    embed,
    iterationTimeout: TimeSpan.FromSeconds(60));

// Use the agent
var result = await agent.GenerateWithCritiqueAsync(
    branch, topic, query, iterations: 3);
```

## Output Format

The self-critique process produces structured output showing each stage:

```
=== Self-Critique Result ===
Iterations: 2
Confidence: High

--- Draft ---
[Initial response from the LLM]

--- Critique ---
[Critical analysis of the draft with suggestions]

--- Improved Response ---
[Final improved version incorporating feedback]

=========================
```

## Configuration Options

### Iteration Limits

- **Minimum**: 1 iteration (default)
- **Maximum**: 5 iterations (hard limit for performance)
- **Configurable**: Pass iteration count via CLI or API

### Timeouts

- **Default**: 30 seconds per iteration
- **Configurable**: Set custom timeout via API

### Confidence Ratings

The system automatically computes confidence based on:
- Number of iterations performed
- Quality indicators in critique text
- Possible values: Low, Medium, High

## Advanced Usage

### Event Sourcing

Access the complete event history for debugging or analysis:

```csharp
var result = await agent.GenerateWithCritiqueAsync(branch, topic, query, 3);

if (result.IsSuccess)
{
    // Access all reasoning events
    var events = result.Value.Branch.Events
        .OfType<ReasoningStep>()
        .ToList();
    
    // Analyze the progression
    foreach (var evt in events)
    {
        Console.WriteLine($"{evt.State.Kind}: {evt.State.Text}");
    }
}
```

### Custom Agent Modes

The `AgentPromptMode` enum supports future extensions:

```csharp
public enum AgentPromptMode
{
    Standard,        // Normal response
    SelfCritique,    // Draft → Critique → Improve
    Ouroboros        // Full recursive self-improvement (future)
}
```

## Best Practices

1. **Start with 1-2 iterations** for most use cases
2. **Use streaming** for long-running operations to provide user feedback
3. **Monitor confidence ratings** to identify areas needing more refinement
4. **Leverage event sourcing** for debugging and analysis
5. **Combine with retrieval** for context-aware improvements

## Integration with Existing Features

The self-critique system integrates seamlessly with:

- **RAG (Retrieval Augmented Generation)**: Uses context from vector stores
- **Tool Execution**: Critiques can trigger tool usage
- **MeTTa Reasoning**: Works with symbolic AI outputs
- **Streaming**: Real-time feedback during critique cycles
- **Event Sourcing**: Complete audit trail of reasoning process
