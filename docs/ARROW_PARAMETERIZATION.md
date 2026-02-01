# Arrow Parameterization Pattern

## Overview

This document describes the arrow parameterization pattern implemented in Ouroboros, which transforms traditional constructor dependency injection (DI) into explicit parameter passing at composition time.

## Motivation

Traditional object-oriented dependency injection has several drawbacks:
- **Hidden Dependencies**: Dependencies are buried in constructors and stored as private fields
- **Stateful Instances**: Objects hold mutable state, making reasoning about behavior difficult
- **Implicit Composition**: Dependency relationships aren't visible at the call site
- **Testing Complexity**: Requires mock frameworks and DI container configuration

## The Arrow Parameterization Pattern

### Core Principles

1. **Explicit Dependencies**: All dependencies are visible as function parameters
2. **Stateless Functions**: Arrows are pure functions with no hidden state
3. **Composition Transparency**: Data flow is clear at composition sites
4. **Testability**: Simple parameter passing makes testing trivial

### Transformation Example

#### Before (Constructor DI)

```csharp
public class CouncilOrchestrator : ICouncilOrchestrator
{
    private readonly List<IAgentPersona> _agents = [];
    private readonly ToolAwareChatModel _llm;

    public CouncilOrchestrator(ToolAwareChatModel llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    public async Task<Result<CouncilDecision, string>> ConveneCouncilAsync(
        CouncilTopic topic,
        CouncilConfig config,
        CancellationToken ct = default)
    {
        // Uses _llm and _agents stored as instance state
        foreach (var agent in _agents)
        {
            await agent.GenerateProposalAsync(topic, _llm, ct);
        }
        // ...
    }
}

// Usage - dependencies hidden
var orchestrator = new CouncilOrchestrator(llm);
orchestrator.AddAgent(new OptimistAgent());
var result = await orchestrator.ConveneCouncilAsync(topic);
```

#### After (Arrow Parameterization)

```csharp
public static class CouncilOrchestratorArrows
{
    public static Step<PipelineBranch, PipelineBranch> ConveneCouncilArrow(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        CouncilConfig? config = null)
        => async branch =>
        {
            // Dependencies are explicit parameters
            config ??= CouncilConfig.Default;
            
            // No hidden state - everything is visible
            foreach (var agent in agents)
            {
                await agent.GenerateProposalAsync(topic, llm, ct);
            }
            // ...
        };
}

// Usage - dependencies visible at composition site
var agents = new List<IAgentPersona> 
{ 
    new OptimistAgent(), 
    new PragmatistAgent() 
};

var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic);
var result = await arrow(branch);
```

## Implemented Transformations

### 1. CouncilOrchestrator

**Location**: `src/Ouroboros.Pipeline/Pipeline/Council/CouncilOrchestratorArrows.cs`

**Benefits**:
- LLM and agents are explicit arrow parameters
- No hidden instance state
- Easy to create pre-configured arrow factories
- Testable with simple parameter passing

**Example**:
```csharp
// Create pre-configured council for reuse
var councilFactory = CouncilOrchestratorArrows.CreateConfiguredCouncil(llm);

// Use for different topics
var arrow1 = councilFactory(topic1);
var arrow2 = councilFactory(topic2);

// Compose arrows
var result = await arrow1(branch);
var result2 = await arrow2(result);
```

### 2. EpisodicMemoryEngine

**Location**: `src/Ouroboros.Pipeline/Pipeline/Memory/EpisodicMemoryArrows.cs`

**Benefits**:
- QdrantClient and EmbeddingModel are explicit arrow parameters
- No class instantiation overhead for stateless operations
- Pre-configured memory systems for common use cases
- Clear data flow from storage to retrieval

**Example**:
```csharp
// Create pre-configured memory system
var memorySystem = EpisodicMemoryArrows.CreateConfiguredMemorySystem(
    qdrantClient,
    embeddingModel,
    "my_collection");

// Use system to create arrows
var storeArrow = memorySystem.StoreEpisode(context, result, metadata);
var retrieveArrow = memorySystem.RetrieveSimilarEpisodes("query", topK: 5);
var planArrow = memorySystem.PlanWithExperience("goal", topK: 5);

// Compose operations
var (updatedBranch, plan) = await planArrow(await storeArrow(branch));
```

### 3. ConsolidatedMind

**Location**: `src/Ouroboros.Agent/Agent/ConsolidatedMind/ConsolidatedMindArrowsExtensions.cs`

**Benefits**:
- MindConfig and specialists are explicit arrow parameters
- Configuration visible at composition time
- Easy to create different configurations for different contexts
- Clear separation between configuration and execution

**Example**:
```csharp
// Create configured system with explicit specialists and config
var config = new MindConfig(
    EnableThinking: true,
    EnableVerification: true,
    MaxParallelism: 4);

var system = ConsolidatedMindArrowsExtensions.CreateConfiguredSystem(
    endpoint,
    apiKey,
    config,
    useHighQuality: true);

// Create arrows from system
var reasoningArrow = system.CreateReasoningArrow(embed, topic, query);
var complexTaskArrow = system.CreateComplexTaskArrow(embed, task);

// Compose multiple arrows
var result = await reasoningArrow(await complexTaskArrow(branch));
```

## Benefits Summary

### 1. Explicit Dependencies
All dependencies are visible at composition time:
```csharp
// You can see exactly what's needed
var arrow = ConveneCouncilArrow(llm, agents, topic, config);
```

### 2. No Hidden State
No mutable instance fields means easier reasoning:
```csharp
// Pure function - same inputs always produce same outputs (modulo async effects)
var arrow = StoreEpisodeArrow(qdrantClient, embeddingModel, context, result, metadata);
```

### 3. Composition Transparency
Data flow is clear:
```csharp
// Obvious data flow from one arrow to the next
var result = await arrow1(await arrow2(await arrow3(branch)));

// Or using functional composition
var composed = arrow1.After(arrow2).After(arrow3);
var result = await composed(branch);
```

### 4. Simple Testing
No DI containers or mock frameworks needed:
```csharp
[Fact]
public async Task TestArrow()
{
    // Just pass test dependencies as parameters
    var mockLlm = new MockLlm();
    var testAgents = new List<IAgentPersona> { new TestAgent() };
    
    var arrow = ConveneCouncilArrow(mockLlm, testAgents, topic);
    var result = await arrow(testBranch);
    
    result.Should().NotBeNull();
}
```

### 5. Pre-Configured Systems
Create reusable configurations:
```csharp
// Configure once
var system = CreateConfiguredMemorySystem(qdrantClient, embeddingModel);

// Use many times with different parameters
var arrow1 = system.StoreEpisode(context1, result1, metadata1);
var arrow2 = system.RetrieveSimilarEpisodes(query2, topK: 10);
```

## Migration Guide

### When to Use Arrow Parameterization

✅ **Use for**:
- Stateless operations (most pipeline operations)
- Functional composition patterns
- Clear dependency visibility is important
- Simple testing is desired
- Building reusable arrow factories

❌ **Consider alternatives for**:
- Highly stateful components that genuinely need mutable state
- Components with complex lifecycle management
- When integration with existing DI frameworks is required

### Migration Steps

1. **Identify the dependencies**: Look for `private readonly` fields set in constructor
2. **Create static arrow factory method**: Take dependencies as explicit parameters
3. **Return arrow function**: Return `Step<TInput, TOutput>` or similar arrow type
4. **Update call sites**: Pass dependencies explicitly at composition time
5. **Create pre-configured systems**: For common dependency combinations
6. **Update tests**: Remove DI container setup, use simple parameter passing

### Example Migration

```csharp
// BEFORE
public class MyFeature
{
    private readonly IService _service;
    
    public MyFeature(IService service)
    {
        _service = service;
    }
    
    public async Task<string> ExecuteAsync(string input)
    {
        return await _service.ProcessAsync(input);
    }
}

// AFTER
public static class MyFeatureArrows
{
    public static Step<PipelineBranch, PipelineBranch> ExecuteArrow(
        IService service,
        string input)
        => async branch =>
        {
            var result = await service.ProcessAsync(input);
            return branch.WithResult(result);
        };
    
    // Optional: Pre-configured factory
    public static Func<string, Step<PipelineBranch, PipelineBranch>> CreateFeatureFactory(
        IService service)
        => input => ExecuteArrow(service, input);
}
```

## Best Practices

1. **Name arrow factories with "Arrow" suffix**: `StoreEpisodeArrow`, `ConveneCouncilArrow`
2. **Group related arrows in static classes**: `CouncilOrchestratorArrows`, `EpisodicMemoryArrows`
3. **Provide pre-configured systems**: For common dependency combinations
4. **Document dependencies**: Make it clear what each parameter is for
5. **Use Result types**: For error handling in arrows
6. **Keep arrows composable**: Design for functional composition

## Testing Patterns

### Unit Testing
```csharp
[Fact]
public async Task Arrow_WithValidInput_ShouldProcess()
{
    // Arrange
    var mockService = new MockService();
    var testInput = "test";
    var branch = CreateTestBranch();
    
    // Act
    var arrow = ExecuteArrow(mockService, testInput);
    var result = await arrow(branch);
    
    // Assert
    result.Should().NotBeNull();
}
```

### Integration Testing
```csharp
[Fact]
public async Task ArrowComposition_ShouldWork()
{
    // Arrange
    var realService = new RealService();
    var arrow1 = ExecuteArrow1(realService, "input1");
    var arrow2 = ExecuteArrow2(realService, "input2");
    var branch = CreateTestBranch();
    
    // Act
    var result = await arrow2(await arrow1(branch));
    
    // Assert
    result.Events.Should().HaveCount(2);
}
```

## Conclusion

The arrow parameterization pattern provides a functional alternative to constructor dependency injection, offering:
- **Explicit dependencies** at composition time
- **Stateless functions** for easier reasoning
- **Transparent composition** with clear data flow
- **Simple testing** without mock frameworks
- **Reusable configurations** through factory functions

This pattern aligns with Ouroboros's functional programming principles and makes the codebase more maintainable and testable.
