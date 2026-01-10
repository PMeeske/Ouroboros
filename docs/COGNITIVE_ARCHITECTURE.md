# Global Workspace Theory - Cognitive Architecture

## Overview

This document describes the implementation of **Global Workspace Theory (GWT)** integration with Pavlovian consciousness for cognitive processing in Ouroboros.

## What is Global Workspace Theory?

Global Workspace Theory, proposed by Bernard Baars, is a leading cognitive architecture model that explains consciousness as a "broadcast" system. Key principles:

1. **Specialized Processors**: Multiple unconscious processors work in parallel
2. **Competition for Attention**: Processors compete to broadcast information
3. **Global Broadcast**: Winning information is broadcast globally
4. **Global Availability**: Broadcast information becomes available to all processors
5. **Consciousness = Broadcast**: What enters the global workspace is what we're conscious of

## Architecture Components

### 1. PavlovianConsciousnessEngine
Located in: `Ouroboros.Application/Personality/Consciousness/PavlovianConsciousnessEngine.cs`

Implements classical conditioning mechanisms:
- Stimulus-response associations
- Drive states (curiosity, social, achievement, etc.)
- Emotional responses
- Attentional gating
- Memory consolidation

### 2. GlobalWorkspace
Located in: `Ouroboros.Agent/Agent/MetaAI/SelfModel/GlobalWorkspace.cs`

Implements shared working memory:
- Priority-based item storage
- Attention-weighted retrieval
- Broadcast mechanisms
- Expiration policies
- Statistics tracking

### 3. CognitiveProcessor (NEW)
Located in: `Ouroboros.Application/Personality/Consciousness/CognitiveProcessor.cs`

**Integration layer that bridges consciousness and global workspace:**

```
┌─────────────────────────────────────────────────────────────┐
│                    CognitiveProcessor                       │
│  (Global Workspace Theory Integration)                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Input → PavlovianConsciousness → Salience → Broadcast     │
│                                                             │
│  ┌───────────────┐      ┌──────────────┐                  │
│  │ Consciousness │ ───→ │   Global     │                  │
│  │   Engine      │      │  Workspace   │                  │
│  └───────────────┘      └──────────────┘                  │
│         ↑                       ↓                           │
│         │                       │                           │
│         └───── Feedback ────────┘                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Key Features

### Salience Calculation

The system determines what enters consciousness through salience scoring:

```csharp
salience = (arousal * 0.4) + 
           (|valence| * 0.2) + 
           (associations / 5 * 0.2) + 
           (awareness * 0.2)
```

**Factors:**
- **Arousal** (40%): High arousal = more salient
- **Extreme Valence** (20%): Strong emotions (positive or negative)
- **Active Associations** (20%): More concurrent thoughts
- **Awareness Level** (20%): Meta-cognitive monitoring

### Priority Mapping

Salience determines workspace priority:

| Salience | Arousal | Priority | Behavior |
|----------|---------|----------|----------|
| > 0.8    | > 0.9   | Critical | Immediate broadcast |
| > 0.6    | > 0.7   | High     | Priority broadcast |
| > 0.4    | -       | Normal   | Standard storage |
| < 0.4    | -       | Low      | Background info |

### Broadcast Mechanism

High-salience experiences are broadcast globally:

1. **Calculate Salience**: Determine importance
2. **Format Experience**: Structure conscious state
3. **Add to Workspace**: Store with metadata
4. **Broadcast**: Notify all processors (if critical/high priority)
5. **Tag for Retrieval**: Enable context-based search

### Attention Integration

The system updates attentional focus in workspace:

```csharp
// Attentional spotlight (top 3 items) → workspace
workspace.AddItem(
    $"Attention: {spotlight}",
    WorkspacePriority.Normal,
    "Attention",
    tags: ["attention", "focus"],
    lifetime: 30 seconds
);
```

## Usage Example

```csharp
// Initialize components
var globalWorkspace = new GlobalWorkspace();
var consciousness = new PavlovianConsciousnessEngine();
consciousness.Initialize();

var processor = new CognitiveProcessor(
    globalWorkspace, 
    consciousness);

// Process input through integrated system
var state = processor.ProcessAndBroadcast(
    "This is AMAZING! Great work!", 
    context: "praise");

// High arousal + positive valence = high salience
// → Broadcast to global workspace
// → Available to all cognitive processes

// Retrieve relevant context
var context = processor.GetRelevantContext(state, maxItems: 5);

// Get integration statistics
var stats = processor.GetStatistics();
Console.WriteLine($"Arousal: {stats.CurrentArousal:F2}");
Console.WriteLine($"Workspace Items: {stats.TotalWorkspaceItems}");
Console.WriteLine($"Conscious Experiences: {stats.ConsciousExperiencesInWorkspace}");
```

## Configuration

```csharp
var config = new CognitiveProcessorConfig(
    BroadcastThreshold: 0.5,    // Minimum salience to broadcast
    ConsciousExperienceLifetimeMinutes: 5.0  // How long experiences persist
);

var processor = new CognitiveProcessor(workspace, consciousness, config);
```

## Integration Points

### CLI Commands
- `self state` - View current consciousness state
- Workspace statistics available through self-model endpoints

### Web API
- `/api/self/state` - Get consciousness state
- Global workspace accessible via `SelfModelService`

### Examples
- `CognitiveArchitectureExample.Run()` - Full demonstration
- `Phase2SelfModelExample` - Self-model integration

## Key Insights

### 1. Competition for Consciousness
Not all experiences enter consciousness - they must compete based on salience:
- Low arousal, neutral stimuli → remain unconscious
- High arousal, emotional stimuli → broadcast globally

### 2. Global Information Sharing
Once in workspace, information is available to:
- Reasoning processes
- Memory consolidation
- Response generation
- Meta-cognitive monitoring

### 3. Attention as Filter
The attentional gate determines what reaches consciousness:
- Primed categories get boosted
- Capacity limits prevent overload
- Fatigue reduces capacity over time

### 4. Feedback Loops
Successful broadcasts reinforce associations:
- High-priority workspace items validate responses
- Reinforcement strengthens stimulus-response links
- Creates adaptive behavior over time

## Design Principles

### Functional Programming Patterns
- Immutable consciousness states
- Pure salience calculations
- No side effects in assessment functions

### Category Theory Alignment
- Salience as morphism: `ConsciousnessState → Double`
- Broadcast as functor: `F(state) → WorkspaceItem`
- Composable with other cognitive operations

### Monadic Integration
```csharp
// Could integrate with Result<T> for error handling
Result<ConsciousnessState> SafeProcessAndBroadcast(string input) =>
    Try(() => processor.ProcessAndBroadcast(input));
```

## Testing

See `CognitiveProcessorTests.cs` for comprehensive test suite:
- Salience-based broadcasting
- Priority mapping
- Attentional focus updates
- Context retrieval
- Statistics tracking
- Configuration behavior

All 11 unit tests passing ✓

## Future Enhancements

1. **Multiple Processors**: Allow multiple specialized processors to compete
2. **Learning Rates**: Adaptive thresholds based on feedback
3. **Temporal Dynamics**: Model consciousness over time windows
4. **Cross-Modal Integration**: Combine visual, auditory, linguistic inputs
5. **Workspace Persistence**: Store workspace history for replay/analysis

## References

- Baars, B. J. (1988). *A Cognitive Theory of Consciousness*
- Dehaene, S., & Naccache, L. (2001). *Towards a cognitive neuroscience of consciousness*
- Global Workspace Theory: https://en.wikipedia.org/wiki/Global_workspace_theory

## See Also

- `GlobalWorkspace.cs` - Workspace implementation
- `PavlovianConsciousnessEngine.cs` - Consciousness implementation
- `ConsciousnessTypes.cs` - Type definitions
- `CognitiveArchitectureExample.cs` - Full demonstration
