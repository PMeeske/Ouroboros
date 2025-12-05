# Orchestration Guide

This guide covers the AI orchestration layer in Ouroboros, including model selection, routing, planning, and optimization strategies.

## Table of Contents

1. [Overview](#overview)
2. [Core Components](#core-components)
3. [Model Selection](#model-selection)
4. [Routing Strategies](#routing-strategies)
5. [Hierarchical Planning](#hierarchical-planning)
6. [Dynamic Tool Selection](#dynamic-tool-selection)
7. [Request Caching](#request-caching)
8. [A/B Testing](#ab-testing)
9. [Observability](#observability)
10. [Performance Tuning](#performance-tuning)
11. [Troubleshooting](#troubleshooting)

---

## Overview

The Ouroboros orchestration layer provides intelligent routing of AI requests to optimal models and tools based on:

- **Use case classification**: Automatic detection of task type (code generation, reasoning, creative, etc.)
- **Performance metrics**: Historical success rates, latency, and cost data
- **Confidence scoring**: Multi-factor scoring for model selection
- **Fallback strategies**: Graceful degradation when primary selections fail

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Request                              │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Orchestration Cache                            │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │ Prompt Hash  │───▶│ Cache Lookup │───▶│ Hit/Miss     │       │
│  └──────────────┘    └──────────────┘    └──────────────┘       │
└─────────────────────────────────────────────────────────────────┘
                               │ (Cache Miss)
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                SmartModelOrchestrator                            │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │ Use Case     │───▶│ Model Score  │───▶│ Tool Select  │       │
│  │ Classification│    │ Calculation  │    │              │       │
│  └──────────────┘    └──────────────┘    └──────────────┘       │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                   UncertaintyRouter                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │ Confidence   │───▶│ Route Type   │───▶│ Fallback     │       │
│  │ Threshold    │    │ Decision     │    │ Strategy     │       │
│  └──────────────┘    └──────────────┘    └──────────────┘       │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                Selected Model + Tools                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### SmartModelOrchestrator

The central orchestrator that analyzes prompts and selects optimal models.

```csharp
// Create orchestrator with persistent metrics
var metricsStore = new PersistentMetricsStore("./metrics.json");
var tools = ToolRegistry.CreateDefault();
var orchestrator = new SmartModelOrchestrator(tools, metricsStore);

// Register available models
orchestrator.RegisterModel(new ModelCapability(
    ModelName: "gpt-4",
    Strengths: new[] { "reasoning", "complex-tasks" },
    MaxTokens: 8192,
    AverageCost: 0.03,
    AverageLatencyMs: 2000,
    Type: ModelType.Reasoning));

// Select model for a prompt
var result = await orchestrator.SelectModelAsync(
    "Write a Python function to sort a list");

result.Match(
    success => Console.WriteLine($"Selected: {success.ModelName}"),
    error => Console.WriteLine($"Error: {error}"));
```

### UncertaintyRouter

Handles low-confidence scenarios with fallback strategies.

```csharp
var router = new UncertaintyRouter(
    highConfidenceThreshold: 0.8,
    mediumConfidenceThreshold: 0.5);

var routingResult = await router.RouteAsync(
    task: "Analyze this complex legal document",
    confidence: 0.45,
    context: new Dictionary<string, object>
    {
        ["domain"] = "legal",
        ["complexity"] = "high"
    });

routingResult.Match(
    success => HandleRouting(success.Route, success.Strategy),
    error => HandleError(error));
```

### HierarchicalPlanner

Decomposes complex tasks into manageable steps.

```csharp
var planner = new HierarchicalPlanner(llm, tools, maxDepth: 3);

var plan = await planner.CreateHierarchicalPlanAsync(
    "Build a complete user authentication system");

foreach (var step in plan.Steps)
{
    Console.WriteLine($"Step {step.Order}: {step.Description}");
    foreach (var sub in step.SubTasks)
    {
        Console.WriteLine($"  - {sub}");
    }
}

// Execute the plan
var result = await planner.ExecuteHierarchicalAsync(plan);
```

---

## Model Selection

### Use Case Types

The orchestrator classifies prompts into these use case types:

| Type | Description | Example Prompts |
|------|-------------|-----------------|
| `CodeGeneration` | Programming and code tasks | "Write a function...", "Debug this code..." |
| `Reasoning` | Logical analysis and problem-solving | "Analyze...", "Compare...", "Explain why..." |
| `Creative` | Creative writing and content generation | "Write a story...", "Create a poem..." |
| `Summarization` | Text summarization | "Summarize...", "TL;DR..." |
| `Analysis` | Data and document analysis | "Analyze the data...", "What patterns..." |
| `Conversation` | Chat and dialogue | Casual conversation |
| `ToolUse` | Tasks requiring external tools | "Search for...", "Calculate..." |

### Scoring Algorithm

Model selection uses a weighted multi-factor score:

```
Score = (TypeMatch × 0.35) + (Capability × 0.25) + (Performance × 0.25) + (Cost × 0.15)
```

Where:
- **TypeMatch**: How well the model type matches the use case (0.0-1.0)
- **Capability**: Model's capability alignment score
- **Performance**: Historical success rate and latency
- **Cost**: Inverse cost score (lower cost = higher score)

### Example: Customizing Weights

```csharp
// For cost-sensitive deployments, you can influence selection
// by adjusting use case weights
var useCase = orchestrator.ClassifyUseCase("Summarize this article");
// useCase.CostWeight can be used to prefer cheaper models
```

---

## Routing Strategies

### Confidence Thresholds

| Confidence | Route Type | Strategy |
|------------|------------|----------|
| ≥ 0.8 | Direct | Execute with selected model |
| 0.5-0.8 | Assisted | Use with enhanced prompting |
| < 0.5 | Fallback | Apply fallback strategy |

### Fallback Strategies

1. **Ensemble**: Combine results from multiple models
2. **Decompose**: Break task into simpler subtasks
3. **Clarify**: Request user clarification
4. **Escalate**: Route to human operator

```csharp
// Configure fallback behavior
var router = new UncertaintyRouter(
    highConfidenceThreshold: 0.8,
    mediumConfidenceThreshold: 0.5);

// The router automatically selects strategy based on confidence
var result = await router.RouteAsync(task, confidence, context);
```

---

## Dynamic Tool Selection

The `DynamicToolSelector` intelligently selects relevant tools based on use case:

### Tool Categories

| Category | Description | Example Tools |
|----------|-------------|---------------|
| `Code` | Code analysis and generation | syntax_check, lint, refactor |
| `FileSystem` | File operations | read_file, write_file, list_dir |
| `Web` | HTTP and API access | fetch, api_call |
| `Knowledge` | Search and lookup | search, query_db |
| `Analysis` | Data analysis | analyze, statistics |
| `Validation` | Verification | validate, verify |
| `Text` | Text processing | summarize, translate |
| `Creative` | Content generation | generate_image, compose |

### Usage

```csharp
var selector = new DynamicToolSelector(baseTools);

// Select tools for a use case
var useCase = orchestrator.ClassifyUseCase("Write Python code");
var selectedTools = selector.SelectToolsForUseCase(useCase);

// Or select based on prompt analysis
var tools = selector.SelectToolsForPrompt("Search for and summarize articles");

// Get recommendations with scores
var recommendations = selector.GetToolRecommendations(useCase, prompt);
foreach (var rec in recommendations.Where(r => r.IsHighlyRecommended))
{
    Console.WriteLine($"{rec.ToolName}: {rec.RelevanceScore:P0}");
}
```

### Context-Based Filtering

```csharp
var context = new ToolSelectionContext
{
    MaxTools = 5,
    RequiredCategories = new() { ToolCategory.Code, ToolCategory.Validation },
    ExcludedCategories = new() { ToolCategory.Web }
};

var tools = selector.SelectToolsForUseCase(useCase, context);
```

---

## Request Caching

Improve performance by caching orchestration decisions:

### Basic Usage

```csharp
// Create cache
var cache = new InMemoryOrchestrationCache(
    maxEntries: 10000,
    cleanupIntervalSeconds: 60);

// Wrap orchestrator with caching
var cachedOrchestrator = orchestrator.WithCaching(
    cache,
    ttl: TimeSpan.FromMinutes(5));

// First call - cache miss, calls underlying orchestrator
await cachedOrchestrator.SelectModelAsync("Write a function");

// Second call - cache hit, returns immediately
await cachedOrchestrator.SelectModelAsync("Write a function");
```

### Cache Statistics

```csharp
var stats = cachedOrchestrator.GetCacheStatistics();

Console.WriteLine($"Entries: {stats.TotalEntries}/{stats.MaxEntries}");
Console.WriteLine($"Hit Rate: {stats.HitRate:P1}");
Console.WriteLine($"Memory: {stats.MemoryEstimateBytes / 1024}KB");
Console.WriteLine($"Healthy: {stats.IsHealthy}");
```

### Cache Invalidation

```csharp
// Invalidate specific entry
await cache.InvalidateAsync(promptHash);

// Clear entire cache
await cache.ClearAsync();
```

---

## A/B Testing

Compare orchestration strategies empirically:

### Running an Experiment

```csharp
var experiment = new OrchestrationExperiment();

// Create variant orchestrators
var variants = new List<IModelOrchestrator>
{
    new SmartModelOrchestrator(tools), // Control
    new SmartModelOrchestrator(tools, optimizedMetrics) // Treatment
};

// Test prompts
var testPrompts = new List<string>
{
    "Write a sorting algorithm",
    "Explain quantum computing",
    "Create a haiku about coding"
};

// Run experiment
var result = await experiment.RunExperimentAsync(
    "optimization-test-v1",
    variants,
    testPrompts);

result.Match(
    success => AnalyzeResults(success),
    error => Console.WriteLine($"Experiment failed: {error}"));
```

### Analyzing Results

```csharp
void AnalyzeResults(ExperimentResult result)
{
    Console.WriteLine($"Duration: {result.Duration}");
    Console.WriteLine($"Status: {result.Status}");
    
    foreach (var variant in result.VariantResults)
    {
        var m = variant.Metrics;
        Console.WriteLine($"\n{variant.VariantId}:");
        Console.WriteLine($"  Success Rate: {m.SuccessRate:P1}");
        Console.WriteLine($"  Avg Latency: {m.AverageLatencyMs:F0}ms");
        Console.WriteLine($"  P95 Latency: {m.P95LatencyMs:F0}ms");
        Console.WriteLine($"  Avg Confidence: {m.AverageConfidence:P0}");
    }
    
    if (result.Winner != null)
    {
        Console.WriteLine($"\nWinner: {result.Winner}");
        Console.WriteLine($"Significance: {result.Analysis!.IsSignificant}");
        Console.WriteLine($"Effect Size: {result.Analysis.EffectSize:F2}");
    }
}
```

---

## Observability

### OpenTelemetry Integration

The orchestration layer provides OpenTelemetry-compatible tracing:

```csharp
// Activities are automatically created for:
// - Model selection operations
// - Routing decisions  
// - Plan creation and execution

// Configure OpenTelemetry exporter
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Ouroboros.Orchestration")
    .AddJaegerExporter()
    .Build();
```

### Key Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `orchestrator.model_selections` | Counter | Number of model selections |
| `orchestrator.routing_decisions` | Counter | Routing decisions made |
| `orchestrator.plan_creations` | Counter | Plans created |
| `orchestrator.model_selection_latency_ms` | Histogram | Selection latency |
| `orchestrator.routing_latency_ms` | Histogram | Routing latency |
| `orchestrator.confidence_score` | Histogram | Decision confidence scores |

### Tracing Tags

| Tag | Description |
|-----|-------------|
| `orchestrator.operation` | Operation type (model_selection, routing, etc.) |
| `orchestrator.selected_model` | Chosen model name |
| `orchestrator.use_case` | Classified use case type |
| `orchestrator.confidence` | Decision confidence |
| `orchestrator.success` | Whether operation succeeded |
| `error.type` | Exception type (on failure) |
| `error.message` | Error details |

---

## Performance Tuning

### Recommended Settings

| Setting | Development | Production |
|---------|-------------|------------|
| Cache Max Entries | 1,000 | 50,000 |
| Cache TTL | 1 minute | 5-10 minutes |
| High Confidence Threshold | 0.7 | 0.8 |
| Medium Confidence Threshold | 0.4 | 0.5 |
| Metrics Persistence | Disabled | Enabled |
| Cleanup Interval | 30s | 60s |

### Optimization Tips

1. **Enable Caching**: Use `CachingModelOrchestrator` for production
2. **Persist Metrics**: Use `PersistentMetricsStore` to retain learning
3. **Tune Thresholds**: Adjust confidence thresholds based on A/B testing
4. **Monitor Hit Rate**: Target > 50% cache hit rate
5. **Tool Filtering**: Use `MaxTools` context to limit tool selection overhead

---

## Troubleshooting

### Common Issues

#### Low Confidence Scores

**Symptoms**: Most requests route to fallback strategies

**Causes**:
- Insufficient model registration
- Prompt doesn't match known patterns
- Historical metrics show poor performance

**Solutions**:
1. Register more model capabilities
2. Lower confidence thresholds temporarily
3. Clear stale metrics and restart learning

```csharp
// Check registered models
var models = orchestrator.GetRegisteredModels();
Console.WriteLine($"Registered models: {models.Count}");

// Check metrics
var metrics = orchestrator.GetMetrics();
foreach (var (model, metric) in metrics)
{
    Console.WriteLine($"{model}: {metric.SuccessRate:P0} success");
}
```

#### High Cache Miss Rate

**Symptoms**: Cache hit rate below 30%

**Causes**:
- TTL too short
- High prompt variation
- Insufficient cache size

**Solutions**:
1. Increase TTL
2. Implement prompt normalization
3. Increase max entries

```csharp
var stats = cache.GetStatistics();
if (stats.HitRate < 0.3 && stats.TotalEntries < stats.MaxEntries * 0.5)
{
    // TTL may be too short
    Console.WriteLine("Consider increasing cache TTL");
}
```

#### Memory Usage Growing

**Symptoms**: Memory consumption increases over time

**Causes**:
- Cache not evicting properly
- Metrics accumulating
- No cleanup intervals

**Solutions**:
1. Check cleanup timer is running
2. Limit metrics history
3. Monitor cache utilization

```csharp
var stats = cache.GetStatistics();
Console.WriteLine($"Cache utilization: {stats.UtilizationPercent:F1}%");
Console.WriteLine($"Estimated memory: {stats.MemoryEstimateBytes / 1024 / 1024}MB");
```

### Debug Logging

Enable detailed logging for troubleshooting:

```csharp
// Using Microsoft.Extensions.Logging
services.AddLogging(config =>
{
    config.AddFilter("Ouroboros.Orchestration", LogLevel.Debug);
});
```

### Health Checks

```csharp
// Check overall orchestration health
bool IsHealthy()
{
    var cacheStats = cache.GetStatistics();
    var metrics = orchestrator.GetMetrics();
    
    // Check cache health
    if (!cacheStats.IsHealthy) return false;
    
    // Check at least one model has good performance
    var hasGoodModel = metrics.Values.Any(m => m.SuccessRate > 0.9);
    if (!hasGoodModel && metrics.Any()) return false;
    
    return true;
}
```

---

## API Reference

See the XML documentation in the source code for complete API details:

- `SmartModelOrchestrator` - Main orchestrator class
- `UncertaintyRouter` - Confidence-based routing
- `HierarchicalPlanner` - Task decomposition
- `DynamicToolSelector` - Intelligent tool selection
- `InMemoryOrchestrationCache` - Request caching
- `OrchestrationExperiment` - A/B testing
- `OrchestrationTracing` - Observability hooks
