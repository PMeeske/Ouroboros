# Self-Improving Agent Architecture

## Overview

MonadicPipeline implements a **self-improving agent architecture** based on Meta-AI v2 principles. The system automatically learns from successful executions, extracts reusable skills, manages memory with consolidation and forgetting, and routes tasks based on confidence levels.

## Architecture Components

### 1. Skill Extraction System

The skill extraction system automatically identifies and extracts reusable patterns from high-quality executions.

#### ISkillExtractor Interface

```csharp
public interface ISkillExtractor
{
    Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        VerificationResult verification,
        SkillExtractionConfig? config = null,
        CancellationToken ct = default);

    Task<bool> ShouldExtractSkillAsync(
        VerificationResult verification,
        SkillExtractionConfig? config = null);

    Task<string> GenerateSkillNameAsync(
        ExecutionResult execution,
        CancellationToken ct = default);

    Task<string> GenerateSkillDescriptionAsync(
        ExecutionResult execution,
        CancellationToken ct = default);
}
```

#### Key Features

- **Automatic Pattern Recognition**: Analyzes successful execution patterns to identify reusable skills
- **LLM-Powered Naming**: Uses the language model to generate descriptive skill names
- **Quality Thresholding**: Only extracts skills from executions with quality scores above threshold (default: 0.8)
- **Parameter Extraction**: Automatically parameterizes steps to make skills more reusable
- **Skill Evolution**: Updates existing skills with new execution data

#### Configuration

```csharp
var config = new SkillExtractionConfig(
    MinQualityThreshold: 0.8,      // Minimum quality score to extract
    MinStepsForExtraction: 2,       // Minimum number of steps required
    MaxStepsPerSkill: 10,           // Maximum complexity per skill
    EnableAutoParameterization: true,
    EnableSkillVersioning: true
);

var extractor = new SkillExtractor(llm, skillRegistry);
var result = await extractor.ExtractSkillAsync(execution, verification, config);
```

#### Example

```csharp
// After a successful execution with quality > 0.8, the orchestrator automatically:
// 1. Checks if skill should be extracted
// 2. Generates skill name using LLM (e.g., "calculate_arithmetic_sum")
// 3. Generates skill description
// 4. Extracts and parameterizes steps
// 5. Registers skill in the registry

orchestrator.LearnFromExecution(verification);
// ✓ Extracted skill: calculate_arithmetic_sum (Quality: 95%)
```

### 2. Persistent Memory Store

Enhanced memory system with short-term/long-term separation, consolidation, and intelligent forgetting.

#### Memory Types

- **Episodic Memory**: Recent, specific execution instances (short-term)
- **Semantic Memory**: Consolidated, generalized patterns (long-term)

#### Key Features

- **Importance Scoring**: Automatically calculates memory importance based on:
  - Quality score (50% weight)
  - Recency (30% weight)
  - Success/failure (20% weight)

- **Memory Consolidation**: Periodically transfers high-importance episodic memories to semantic storage

- **Intelligent Forgetting**: Removes low-importance memories when capacity is reached

- **Vector Search**: Semantic similarity search when embedding model is available

#### Configuration

```csharp
var config = new PersistentMemoryConfig(
    ShortTermCapacity: 100,              // Max episodic memories
    LongTermCapacity: 1000,              // Max semantic memories
    ConsolidationThreshold: 0.7,         // Min importance to consolidate
    ConsolidationInterval: TimeSpan.FromHours(1),
    EnableForgetting: true,
    ForgettingThreshold: 0.3             // Min importance to retain
);

var memory = new PersistentMemoryStore(embedding, vectorStore, config);
```

#### Memory Lifecycle

```
New Experience
     │
     ├──> Store as Episodic (short-term)
     │         │
     │         ├──> Calculate Importance
     │         │
     │         ├──> Time/Capacity Check
     │         │         │
     │         │         ├──> Consolidate (high importance → Semantic)
     │         │         │
     │         │         └──> Forget (low importance → removed)
     │         │
     │         └──> Vector Store (if available)
     │
     └──> Retrievable via Similarity Search
```

### 3. Uncertainty Router

Routes tasks based on confidence analysis with fallback strategies.

#### Routing Strategies

The router analyzes confidence and selects appropriate strategies:

| Confidence | Strategy | Action |
|-----------|----------|--------|
| > 0.7 | Direct | Execute with selected model |
| 0.5 - 0.7 | Ensemble | Use multiple models for consensus |
| 0.3 - 0.5 | Decompose or Ensemble | Break down task or use ensemble |
| < 0.3 | Clarification or Context | Request more information |

#### Features

- **Historical Learning**: Tracks routing outcomes to improve confidence estimates
- **Bayesian-Inspired**: Adjusts confidence based on task complexity and context
- **Fallback Cascading**: Automatically escalates when confidence is low

#### Example

```csharp
var router = new UncertaintyRouter(orchestrator, minConfidenceThreshold: 0.7);

var decision = await router.RouteAsync("Complex analytical task");

decision.Match(
    routing => {
        // routing.Route: Selected model/strategy
        // routing.Confidence: 0.0 - 1.0
        // routing.Reason: Explanation
    },
    error => { /* Handle error */ }
);

// Record outcome for learning
router.RecordRoutingOutcome(decision, wasSuccessful: true);
```

## Integration Example

### Complete Self-Improving Agent Setup

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;

// 1. Configure components
var provider = new OllamaProvider();
var llm = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var tools = ToolRegistry.CreateDefault();

// 2. Create enhanced memory
var memoryConfig = new PersistentMemoryConfig(
    ShortTermCapacity: 50,
    LongTermCapacity: 500,
    ConsolidationThreshold: 0.8,
    EnableForgetting: true
);
var memory = new PersistentMemoryStore(config: memoryConfig);

// 3. Create skill registry and extractor
var skillRegistry = new SkillRegistry();
var skillExtractor = new SkillExtractor(llm, skillRegistry);

// 4. Build orchestrator
var orchestrator = MetaAIBuilder.CreateDefault()
    .WithLLM(llm)
    .WithTools(tools)
    .WithMemoryStore(memory)
    .WithSkillRegistry(skillRegistry)
    .WithSkillExtractor(skillExtractor)
    .WithConfidenceThreshold(0.7)
    .Build();

// 5. Execute and learn
var planResult = await orchestrator.PlanAsync("Calculate sum of 42 and 58");
var execResult = await orchestrator.ExecuteAsync(planResult.Value);
var verifyResult = await orchestrator.VerifyAsync(execResult.Value);

// 6. Automatic learning
orchestrator.LearnFromExecution(verifyResult.Value);
// ✓ Experience stored in memory
// ✓ Skill extracted if quality > 0.8
// ✓ Memory consolidation triggered if needed
```

## Learning Cycle

The complete learning cycle operates as follows:

```
┌──────────────────────────────────────────────────────────┐
│                    LEARNING CYCLE                         │
└──────────────────────────────────────────────────────────┘

1. PLAN
   ├─> Query past experiences (semantic search)
   ├─> Find matching skills
   └─> Generate execution plan

2. EXECUTE
   ├─> Execute steps sequentially
   ├─> Monitor performance
   └─> Collect execution data

3. VERIFY
   ├─> Assess quality (0.0 - 1.0)
   ├─> Identify issues
   └─> Suggest improvements

4. LEARN
   ├─> Store experience in memory
   │   ├─> Calculate importance
   │   ├─> Store as episodic
   │   └─> Vector embedding
   │
   ├─> Extract skill (if quality > 0.8)
   │   ├─> Generate name/description
   │   ├─> Parameterize steps
   │   └─> Register in registry
   │
   └─> Consolidate memory (periodic)
       ├─> Episodic → Semantic
       └─> Forget low-importance
```

## Performance Metrics

The orchestrator tracks performance across multiple dimensions:

```csharp
var metrics = orchestrator.GetMetrics();

foreach (var (component, metric) in metrics)
{
    Console.WriteLine($"{component}:");
    Console.WriteLine($"  Executions: {metric.ExecutionCount}");
    Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs}ms");
    Console.WriteLine($"  Success Rate: {metric.SuccessRate:P0}");
    Console.WriteLine($"  Last Used: {metric.LastUsed}");
}
```

## Advanced Features

### Skill Composition (Implemented)

Combine multiple skills into higher-order skills:

```csharp
var composer = new SkillComposer(skillRegistry, memory);

var result = await composer.ComposeSkillsAsync(
    compositeName: "data_analysis_pipeline",
    description: "Complete data analysis workflow",
    componentSkillNames: new List<string> 
    { 
        "load_data", 
        "clean_data", 
        "analyze_data", 
        "generate_report" 
    }
);
```

### Memory Retrieval Strategies

```csharp
// Similarity-based retrieval
var query = new MemoryQuery(
    Goal: "mathematical calculations",
    Context: new Dictionary<string, object> { ["domain"] = "arithmetic" },
    MaxResults: 10,
    MinSimilarity: 0.7
);

var relevantExperiences = await memory.RetrieveRelevantExperiencesAsync(query);
```

## Best Practices

### 1. Quality Thresholds

Set extraction thresholds based on your use case:
- **High-stakes domains**: 0.9+ (only extract highly verified skills)
- **Exploratory learning**: 0.7+ (more aggressive extraction)
- **Production systems**: 0.85+ (balanced approach)

### 2. Memory Management

Configure memory limits based on available resources:
```csharp
// For resource-constrained environments
var config = new PersistentMemoryConfig(
    ShortTermCapacity: 20,
    LongTermCapacity: 100,
    EnableForgetting: true
);

// For servers with ample memory
var config = new PersistentMemoryConfig(
    ShortTermCapacity: 500,
    LongTermCapacity: 5000,
    EnableForgetting: false  // Keep everything
);
```

### 3. Skill Versioning

Track skill evolution over time:
```csharp
var skill = skillRegistry.GetSkill("calculate_sum");
Console.WriteLine($"Success rate: {skill.SuccessRate:P0}");
Console.WriteLine($"Usage count: {skill.UsageCount}");
Console.WriteLine($"Created: {skill.CreatedAt}");
Console.WriteLine($"Last used: {skill.LastUsed}");
```

### 4. Monitoring and Debugging

Monitor learning behavior:
```csharp
// Memory statistics
var stats = await memory.GetStatisticsAsync();
Console.WriteLine($"Total experiences: {stats.TotalExperiences}");
Console.WriteLine($"Avg quality: {stats.AverageQualityScore:P0}");

// Skill statistics
var skills = skillRegistry.GetAllSkills();
Console.WriteLine($"Total skills: {skills.Count}");
foreach (var skill in skills.OrderByDescending(s => s.SuccessRate))
{
    Console.WriteLine($"  {skill.Name}: {skill.SuccessRate:P0} ({skill.UsageCount} uses)");
}
```

## Future Enhancements

The following capabilities are planned for future releases:

### Phase 2: Self-Model & Metacognition
- **Capability Registry**: Agent understands its own capabilities and limitations
- **Goal Hierarchy**: Hierarchical goal decomposition with priority management
- **Self-Evaluation**: Autonomous performance assessment and improvement suggestions

### Phase 3: Emergent Intelligence
- **Transfer Learning**: Apply learned skills across domains
- **Hypothesis Generation**: Scientific reasoning and experimentation
- **Curiosity-Driven Learning**: Autonomous exploration during idle time

## Safety Considerations

### Skill Extraction Boundaries

The skill extraction system operates within defined safety boundaries:

1. **Quality Gating**: Only high-quality executions (>0.8) are extracted
2. **Capacity Limits**: Maximum skill complexity and registry size
3. **Human Oversight**: All extracted skills can be reviewed and removed
4. **Versioning**: Skill evolution is tracked for auditing

### Memory Management

Memory operations are bounded:

1. **Capacity Limits**: Prevent unbounded memory growth
2. **Importance Thresholds**: Maintain memory quality through forgetting
3. **Consolidation Intervals**: Prevent excessive processing overhead

## References

- **Architecture Spec**: See `IMPLEMENTATION_GUIDE.md` for technical details
- **Examples**: `src/MonadicPipeline.Examples/Examples/SelfImprovingAgentExample.cs`
- **Tests**: `src/MonadicPipeline.Tests/Tests/SkillExtractionTests.cs`
- **Tests**: `src/MonadicPipeline.Tests/Tests/PersistentMemoryStoreTests.cs`

## Contributing

When extending the self-improvement capabilities:

1. Follow monadic error handling patterns (`Result<T, E>`)
2. Maintain immutability in data structures
3. Add comprehensive tests for new learning behaviors
4. Document safety boundaries and limitations
5. Use functional composition for skill operations
