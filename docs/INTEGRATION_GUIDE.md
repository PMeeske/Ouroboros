# Ouroboros Full System Integration Guide

## Overview

The Ouroboros integration layer provides a unified interface to all Tier 1, Tier 2, and Tier 3 features, enabling seamless orchestration of the complete AGI system with dependency injection, autonomous operation, and end-to-end workflows.

## Quick Start

### Basic Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Application.Integration;

// Create service collection
var services = new ServiceCollection();

// Add Ouroboros with all features (one-liner)
services.AddOuroborosFull();

// Build service provider and resolve
var serviceProvider = services.BuildServiceProvider();
var ouroboros = serviceProvider.GetRequiredService<IOuroborosCore>();
```

### Custom Configuration

```csharp
services.AddOuroboros(builder =>
{
    builder
        .WithEpisodicMemory(opts =>
        {
            opts.VectorStoreConnectionString = "http://localhost:6333";
            opts.MaxMemorySize = 10000;
        })
        .WithMeTTaReasoning(opts =>
        {
            opts.MeTTaExecutablePath = "./metta";
            opts.EnableAbduction = true;
        })
        .WithHierarchicalPlanning(opts =>
        {
            opts.MaxPlanningDepth = 10;
            opts.EnableHTN = true;
        })
        .WithCausalReasoning()
        .WithConsciousness()
        .WithCognitiveLoop();
});
```

## Core Features

### 1. Unified Goal Execution

Execute goals using the full cognitive pipeline with episodic memory, hierarchical planning, and causal reasoning:

```csharp
var config = new ExecutionConfig(
    UseEpisodicMemory: true,
    UseCausalReasoning: true,
    UseHierarchicalPlanning: true,
    MaxPlanningDepth: 5
);

var result = await ouroboros.ExecuteGoalAsync(
    "Analyze system performance and suggest improvements",
    config
);

result.Match(
    success =>
    {
        Console.WriteLine($"Success: {success.Output}");
        Console.WriteLine($"Duration: {success.Duration}");
        Console.WriteLine($"Episodes: {success.GeneratedEpisodes.Count}");
    },
    error => Console.WriteLine($"Error: {error}")
);
```

### 2. Learning from Experience

Consolidate memories, extract rules, and update adapters:

```csharp
var learningConfig = new LearningConfig(
    ConsolidateMemories: true,
    UpdateAdapters: true,
    ExtractRules: true,
    ConsolidationStrategy: ConsolidationStrategy.Abstract
);

var result = await ouroboros.LearnFromExperienceAsync(
    episodes,
    learningConfig
);

result.Match(
    success =>
    {
        Console.WriteLine($"Episodes Processed: {success.EpisodesProcessed}");
        Console.WriteLine($"Rules Learned: {success.RulesLearned}");
        Console.WriteLine($"Performance Improvement: {success.PerformanceImprovement:P}");
    },
    error => Console.WriteLine($"Error: {error}")
);
```

### 3. Unified Reasoning

Combine symbolic, causal, and abductive reasoning:

```csharp
var reasoningConfig = new ReasoningConfig(
    UseSymbolicReasoning: true,
    UseCausalInference: true,
    UseAbduction: true,
    MaxInferenceSteps: 100
);

var result = await ouroboros.ReasonAboutAsync(
    "What are the root causes of the performance issue?",
    reasoningConfig
);

result.Match(
    success =>
    {
        Console.WriteLine($"Answer: {success.Answer}");
        Console.WriteLine($"Certainty: {success.Certainty}");
        Console.WriteLine($"Facts: {success.SupportingFacts.Count}");
    },
    error => Console.WriteLine($"Error: {error}")
);
```

### 4. Consciousness Integration

Interact with the global workspace and metacognitive monitoring:

```csharp
// Broadcast to workspace
var broadcastResult = await ouroboros.Consciousness.BroadcastToWorkspaceAsync(
    "Analysis completed successfully",
    WorkspacePriority.High,
    "AnalysisEngine"
);

// Get attended items
var attentionResult = await ouroboros.Consciousness.GetAttendedItemsAsync(
    WorkspacePriority.Normal
);

// Get metacognitive state
var metaResult = await ouroboros.Consciousness.GetMetaCognitiveStateAsync();
metaResult.Match(
    meta =>
    {
        Console.WriteLine($"Self-Awareness: {meta.SelfAwarenessLevel:P}");
        Console.WriteLine($"Active Goals: {meta.CurrentGoals.Count}");
    },
    error => Console.WriteLine($"Error: {error}")
);
```

### 5. Cognitive Loop

Start autonomous operation with continuous perception-reason-act cycles:

```csharp
var cognitiveLoop = serviceProvider.GetRequiredService<ICognitiveLoop>();

// Start the loop
await cognitiveLoop.StartAsync(cancellationToken);

// Monitor state
var stateResult = await cognitiveLoop.GetStateAsync();
stateResult.Match(
    state =>
    {
        Console.WriteLine($"Running: {state.IsRunning}");
        Console.WriteLine($"Cycle Count: {state.CycleCount}");
        Console.WriteLine($"Active Goals: {state.ActiveGoals.Count}");
    },
    error => Console.WriteLine($"Error: {error}")
);

// Pause/Resume
await cognitiveLoop.PauseAsync();
await cognitiveLoop.ResumeAsync();

// Stop
await cognitiveLoop.StopAsync(cancellationToken);
```

## Kleisli Pipeline Extensions

Compose cognitive pipelines using functional programming:

```csharp
using Ouroboros.Application.Integration;
using Ouroboros.Core.Steps;

// Create a full cognitive pipeline
Step<PipelineBranch, PipelineBranch> fullPipeline =
    Step.Pure<PipelineBranch>()
        .WithFullCognitivePipeline(ouroboros);

// Or compose individual steps
Step<PipelineBranch, PipelineBranch> customPipeline =
    Step.Pure<PipelineBranch>()
        .WithEpisodicMemoryRetrieval(ouroboros.EpisodicMemory)
        .WithCausalAnalysis(ouroboros.CausalReasoning)
        .WithSymbolicReasoning(ouroboros.MeTTaReasoning)
        .WithHierarchicalPlanning(ouroboros.HierarchicalPlanner)
        .WithEpisodeStorage(ouroboros.EpisodicMemory);

// Execute pipeline
var result = await customPipeline(initialBranch);
```

## Event-Driven Communication

Use the event bus for cross-feature communication:

```csharp
var eventBus = serviceProvider.GetRequiredService<IEventBus>();

// Subscribe to events
eventBus.Subscribe<GoalExecutedEvent>()
    .Subscribe(evt =>
    {
        Console.WriteLine($"Goal '{evt.Goal}' executed: {evt.Success}");
    });

// Publish events
eventBus.Publish(new GoalExecutedEvent(
    Guid.NewGuid(),
    DateTime.UtcNow,
    "Analysis",
    "Analyze performance",
    true,
    TimeSpan.FromSeconds(2.5)
));
```

## Available Engines

The `IOuroborosCore` interface provides access to all engines:

### Tier 1 - Core Engines
- **EpisodicMemory**: Long-term memory with semantic retrieval
- **AdapterLearning**: LoRA/PEFT adapter management
- **MeTTaReasoning**: Symbolic AI and theorem proving
- **HierarchicalPlanner**: Multi-level task decomposition
- **Reflection**: Meta-cognitive analysis and self-improvement
- **Benchmarks**: Comprehensive evaluation suite

### Tier 2 - Advanced Engines
- **ProgramSynthesis**: Code generation and library learning
- **WorldModel**: Model-based reinforcement learning
- **MultiAgent**: Collaborative intelligence and coordination
- **CausalReasoning**: Pearl's causal inference framework

### Tier 3 - Meta-Cognitive Engines
- **MetaLearning**: Fast task adaptation (MAML, Reptile)
- **EmbodiedAgent**: Grounded cognition and sensorimotor learning
- **Consciousness**: Global workspace and attention management

## Configuration Options

Each engine has dedicated configuration options:

```csharp
builder
    .WithEpisodicMemory(opts =>
    {
        opts.VectorStoreConnectionString = "http://localhost:6333";
        opts.MaxMemorySize = 10000;
        opts.ConsolidationInterval = TimeSpan.FromHours(1);
        opts.EnableAutoConsolidation = true;
    })
    .WithMeTTaReasoning(opts =>
    {
        opts.MeTTaExecutablePath = "./metta";
        opts.MaxInferenceSteps = 100;
        opts.EnableTypeChecking = true;
        opts.EnableAbduction = true;
    })
    .WithHierarchicalPlanning(opts =>
    {
        opts.MaxPlanningDepth = 10;
        opts.MaxPlanningTime = 30;
        opts.EnableHTN = true;
        opts.EnableTemporalPlanning = true;
    })
    // ... and 10 more engines with similar configuration
```

## Architecture

The integration follows functional programming principles:

- **Monadic Error Handling**: `Result<T, TError>` for all operations
- **Immutability**: Pure functions and immutable data structures
- **Kleisli Composition**: `Step<TIn, TOut>` for pipeline operations
- **Dependency Injection**: Constructor injection throughout
- **Event-Driven**: Reactive patterns with `IObservable<T>`

## Best Practices

1. **Use Result Monads**: Always handle success and error cases using `Match()`
2. **Compose Pipelines**: Build complex workflows from simple steps
3. **Configure Consciously**: Only enable engines you need
4. **Monitor Consciousness**: Check metacognitive state for self-awareness
5. **Handle Events**: Subscribe to events for cross-feature coordination
6. **Test Incrementally**: Use unit tests for individual engines before full integration

## Examples

See `/src/Ouroboros.Examples/Examples/FullSystemIntegrationExample.cs` for complete working examples.

## Further Reading

- [Functional Programming Patterns](../../docs/FUNCTIONAL_PATTERNS.md)
- [Monadic Composition](../../docs/MONADS.md)
- [Kleisli Arrows](../../docs/KLEISLI.md)
- [Architecture Overview](../../docs/ARCHITECTURAL_LAYERS.md)
