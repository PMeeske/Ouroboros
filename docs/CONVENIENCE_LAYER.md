# Meta-AI Convenience Layer

## Overview

The Meta-AI Convenience Layer provides simplified, easy-to-use methods for quickly setting up and using Meta-AI orchestrators. It eliminates boilerplate code and offers preset configurations for common use cases.

## Quick Start

### Simple One-Liner Question Answering

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

// Create a simple orchestrator
var provider = new OllamaProvider();
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

var orchestratorResult = MetaAIConvenience.CreateSimple(chatModel);
var orchestrator = orchestratorResult.Value;

// Ask a question and get an answer
var answerResult = await orchestrator.AskQuestion("What is functional programming?");

answerResult.Match(
    answer => Console.WriteLine($"Answer: {answer}"),
    error => Console.WriteLine($"Error: {error}"));
```

## Preset Configurations

The convenience layer provides several preset orchestrator configurations:

### 1. Simple Orchestrator
Best for: Quick prototyping and simple tasks

```csharp
var result = MetaAIConvenience.CreateSimple(chatModel);
```

**Features:**
- Minimal configuration
- Lower confidence threshold (0.5)
- No memory or embeddings
- Fast setup

### 2. Standard Orchestrator
Best for: Most production use cases with basic safety and memory

```csharp
var result = MetaAIConvenience.CreateStandard(chatModel, tools, embedding);
```

**Features:**
- Tool support
- Vector store for memory
- Embedding model for semantic search
- Confidence threshold: 0.7
- Isolated permission level

### 3. Advanced Orchestrator
Best for: Complex workflows requiring uncertainty handling and skill learning

```csharp
var result = MetaAIConvenience.CreateAdvanced(chatModel, tools, embedding, confidenceThreshold: 0.8);
```

**Features:**
- Full feature set
- Higher confidence threshold
- Read-only default permissions
- Skill learning enabled

### 4. Research Assistant
Optimized for research and analysis tasks

```csharp
var result = MetaAIConvenience.CreateResearchAssistant(chatModel, tools, embedding);
```

**Features:**
- Confidence threshold: 0.75
- Read-only permissions for safety
- Semantic search enabled
- Tool support

### 5. Code Assistant
Optimized for code generation and analysis

```csharp
var result = MetaAIConvenience.CreateCodeAssistant(chatModel, tools);
```

**Features:**
- High confidence threshold (0.8)
- Isolated execution
- Tool support for code operations
- No embeddings (focused on deterministic tasks)

### 6. Chat Assistant
Optimized for interactive conversations

```csharp
var result = MetaAIConvenience.CreateChatAssistant(chatModel);
```

**Features:**
- Lower confidence threshold (0.6) for natural responses
- Minimal tool set
- Isolated execution
- Fast response time

## Convenience Methods

### AskQuestion
Quick one-liner to ask a question and get an answer with automatic plan-execute cycle.

```csharp
var result = await orchestrator.AskQuestion(
    "Explain monadic composition",
    context: new Dictionary<string, object> { ["level"] = "beginner" });

result.Match(
    answer => Console.WriteLine(answer),
    error => Console.WriteLine($"Failed: {error}"));
```

### AnalyzeText
Analyze text with automatic quality verification.

```csharp
var result = await orchestrator.AnalyzeText(
    text: "Your text here...",
    analysisGoal: "Extract key insights and themes");

result.Match(
    (analysis, quality) => 
    {
        Console.WriteLine($"Analysis: {analysis}");
        Console.WriteLine($"Quality: {quality:P0}");
    },
    error => Console.WriteLine($"Failed: {error}"));
```

### GenerateCode
Generate code with quality assurance.

```csharp
var result = await orchestrator.GenerateCode(
    description: "Create a function to calculate fibonacci numbers",
    language: "C#");

result.Match(
    code => Console.WriteLine(code),
    error => Console.WriteLine($"Failed: {error}"));
```

### CompleteWorkflow
Execute a complete plan-execute-verify-learn cycle with automatic learning.

```csharp
var result = await orchestrator.CompleteWorkflow(
    goal: "Design a caching strategy for a web API",
    context: new Dictionary<string, object> 
    { 
        ["scale"] = "high",
        ["consistency"] = "eventual"
    },
    autoLearn: true);

result.Match(
    verification => 
    {
        Console.WriteLine($"Verified: {verification.Verified}");
        Console.WriteLine($"Quality: {verification.QualityScore:P0}");
    },
    error => Console.WriteLine($"Failed: {error}"));
```

### ProcessBatch
Process multiple tasks efficiently.

```csharp
var tasks = new[] 
{
    "Explain REST API design",
    "Explain GraphQL design",
    "Compare REST vs GraphQL"
};

var results = await orchestrator.ProcessBatch(
    tasks,
    sharedContext: new Dictionary<string, object> { ["format"] = "concise" });

foreach (var result in results)
{
    result.Match(
        output => Console.WriteLine($"✓ {output}"),
        error => Console.WriteLine($"✗ {error}"));
}
```

## Complete Examples

### Example 1: Research Assistant Workflow

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

// Setup
var provider = new OllamaProvider();
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var tools = ToolRegistry.CreateDefault();
var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

// Create Research Assistant
var orchestratorResult = MetaAIConvenience.CreateResearchAssistant(chatModel, tools, embedModel);
var orchestrator = orchestratorResult.Value;

// Analyze research paper
var analysisResult = await orchestrator.AnalyzeText(
    text: paperContent,
    analysisGoal: "Identify main contributions, methodology, and limitations");

analysisResult.Match(
    (analysis, quality) => 
    {
        Console.WriteLine($"Research Analysis (Quality: {quality:P0}):");
        Console.WriteLine(analysis);
    },
    error => Console.WriteLine($"Analysis failed: {error}"));
```

### Example 2: Code Generation Pipeline

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

// Setup Code Assistant
var provider = new OllamaProvider();
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var tools = ToolRegistry.CreateDefault();

var orchestratorResult = MetaAIConvenience.CreateCodeAssistant(chatModel, tools);
var orchestrator = orchestratorResult.Value;

// Generate code with quality verification
var workflowResult = await orchestrator.CompleteWorkflow(
    goal: "Create a thread-safe singleton pattern in C#",
    context: new Dictionary<string, object>
    {
        ["language"] = "C#",
        ["pattern"] = "singleton",
        ["thread_safety"] = true
    },
    autoLearn: true);

workflowResult.Match(
    verification => 
    {
        if (verification.Verified && verification.QualityScore > 0.8)
        {
            Console.WriteLine("High-quality code generated successfully!");
            // Use the code...
        }
    },
    error => Console.WriteLine($"Code generation failed: {error}"));
```

### Example 3: Interactive Chat Session

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

// Setup Chat Assistant
var provider = new OllamaProvider();
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

var orchestratorResult = MetaAIConvenience.CreateChatAssistant(chatModel);
var orchestrator = orchestratorResult.Value;

// Interactive conversation
while (true)
{
    Console.Write("You: ");
    var userInput = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
        break;
    
    var response = await orchestrator.AskQuestion(userInput);
    
    response.Match(
        answer => Console.WriteLine($"Assistant: {answer}"),
        error => Console.WriteLine($"Error: {error}"));
}
```

### Example 4: Batch Processing

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

// Setup
var provider = new OllamaProvider();
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var tools = ToolRegistry.CreateDefault();

var orchestratorResult = MetaAIConvenience.CreateCodeAssistant(chatModel, tools);
var orchestrator = orchestratorResult.Value;

// Process multiple code tasks
var codeTasks = new[]
{
    "Implement a binary search algorithm",
    "Create a linked list data structure",
    "Write a merge sort function"
};

var context = new Dictionary<string, object>
{
    ["language"] = "C#",
    ["include_tests"] = true
};

var results = await orchestrator.ProcessBatch(codeTasks, context);

// Display results
for (int i = 0; i < codeTasks.Length; i++)
{
    Console.WriteLine($"\n=== Task {i + 1}: {codeTasks[i]} ===");
    
    results[i].Match(
        code => Console.WriteLine(code),
        error => Console.WriteLine($"Failed: {error}"));
}
```

## Error Handling

All convenience methods return `Result<T, string>` for safe error handling:

```csharp
// Pattern 1: Match for explicit handling
result.Match(
    success => UseTheValue(success),
    error => HandleError(error));

// Pattern 2: Check and access
if (result.IsSuccess)
{
    var value = result.Value;
    // Use value...
}
else
{
    var error = result.Error;
    // Handle error...
}

// Pattern 3: Chain operations
var finalResult = orchestratorResult
    .Bind(orch => orch.AskQuestion("What is..."))
    .Map(answer => answer.ToUpperInvariant());
```

## Best Practices

### 1. Choose the Right Preset

- **Simple**: For quick experiments and learning
- **Standard**: For most production scenarios
- **Advanced**: For complex, mission-critical workflows
- **Research Assistant**: For analysis and investigation tasks
- **Code Assistant**: For code generation and review
- **Chat Assistant**: For conversational interfaces

### 2. Use Context Effectively

```csharp
var context = new Dictionary<string, object>
{
    ["domain"] = "healthcare",
    ["compliance"] = "HIPAA",
    ["audience"] = "technical"
};

var result = await orchestrator.AskQuestion("Design a patient data system", context);
```

### 3. Enable Auto-Learning for Improvement

```csharp
// The orchestrator learns from successful executions
var result = await orchestrator.CompleteWorkflow(
    goal: "Complex task...",
    autoLearn: true); // Enables continuous improvement
```

### 4. Batch Similar Tasks

```csharp
// More efficient than individual calls
var tasks = GenerateRelatedTasks();
var results = await orchestrator.ProcessBatch(tasks, sharedContext);
```

## Comparison: Traditional vs Convenience Layer

### Traditional Approach

```csharp
// Lots of boilerplate
var orchestrator = MetaAIBuilder.CreateDefault()
    .WithLLM(chatModel)
    .WithTools(tools)
    .WithEmbedding(embedding)
    .WithVectorStore(new TrackedVectorStore())
    .WithConfidenceThreshold(0.7)
    .WithDefaultPermissionLevel(PermissionLevel.Isolated)
    .Build();

var planResult = await orchestrator.PlanAsync(question);
if (!planResult.IsSuccess)
    throw new Exception(planResult.Error);

var execResult = await orchestrator.ExecuteAsync(planResult.Value);
if (!execResult.IsSuccess)
    throw new Exception(execResult.Error);

var output = execResult.Value.FinalOutput;
```

### Convenience Layer Approach

```csharp
// Simple and concise
var orchestrator = MetaAIConvenience.CreateStandard(chatModel, tools, embedding).Value;
var answer = await orchestrator.AskQuestion(question);

answer.Match(
    output => UseTheAnswer(output),
    error => HandleError(error));
```

## Integration with Existing Code

The convenience layer is fully compatible with the underlying Meta-AI system:

```csharp
// Start with convenience layer
var orchestrator = MetaAIConvenience.CreateStandard(chatModel, tools, embedding).Value;

// Use convenience methods
var quickAnswer = await orchestrator.AskQuestion("Quick question");

// Fall back to full API when needed
var customPlan = await orchestrator.PlanAsync(complexGoal, customContext);
var customExecution = await orchestrator.ExecuteAsync(customPlan.Value);
```

## Performance Considerations

### Preset Performance Characteristics

| Preset | Speed | Quality | Safety | Use Case |
|--------|-------|---------|--------|----------|
| Simple | ⚡⚡⚡ | ⭐⭐ | 🔒 | Prototyping |
| Standard | ⚡⚡ | ⭐⭐⭐ | 🔒🔒 | Production |
| Advanced | ⚡ | ⭐⭐⭐⭐ | 🔒🔒🔒 | Critical tasks |
| Research | ⚡⚡ | ⭐⭐⭐⭐ | 🔒🔒 | Analysis |
| Code | ⚡⚡ | ⭐⭐⭐⭐ | 🔒🔒 | Development |
| Chat | ⚡⚡⚡ | ⭐⭐⭐ | 🔒 | Interaction |

## Summary

The Meta-AI Convenience Layer provides:

✅ **Simplified Setup**: Preset configurations for common scenarios  
✅ **One-Liner Methods**: Quick execution of common workflows  
✅ **Monadic Error Handling**: Safe, composable error management  
✅ **Type Safety**: Full C# type system support  
✅ **Composability**: Works seamlessly with existing Meta-AI features  
✅ **Best Practices**: Built-in quality assurance and learning  

Get started with just a few lines of code, and scale to complex workflows when needed!
