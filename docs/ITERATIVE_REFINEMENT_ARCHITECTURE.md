# Iterative Refinement Architecture

## Overview

The Ouroboros reasoning system implements a sophisticated **iterative refinement architecture** that enables AI systems to progressively improve their outputs through multiple critique-and-improve cycles. This document explains the architectural design, implementation details, and the principles behind this approach.

## Problem Statement

Traditional AI reasoning pipelines often produce a single output based on an initial prompt. While this works for simple tasks, complex reasoning benefits from iterative refinement:

1. **Initial Draft** may miss important details or contain gaps
2. **Critique** identifies these issues but doesn't fix them
3. **Single Improvement** addresses critique but may still have room for enhancement
4. **Multiple Iterations** compound improvements for higher quality output

The challenge is ensuring that each iteration builds upon the previous improvement rather than starting over from the original draft.

## Architectural Solution

### Core Components

The architecture consists of three primary arrow functions that compose together:

```csharp
// Generate initial response
DraftArrow: PipelineBranch → PipelineBranch
  Input: Branch with context
  Output: Branch + Draft state

// Analyze and critique current state
CritiqueArrow: PipelineBranch → PipelineBranch
  Input: Branch with Draft or FinalSpec
  Output: Branch + Critique state

// Improve based on critique
ImproveArrow: PipelineBranch → PipelineBranch
  Input: Branch with (Draft|FinalSpec) and Critique
  Output: Branch + FinalSpec state
```

### State Progression

The system uses polymorphic reasoning states that all derive from `ReasoningState`:

```csharp
public abstract record ReasoningState(string Kind, string Text);

public sealed record Draft(string DraftText) : ReasoningState("Draft", DraftText);
public sealed record Critique(string CritiqueText) : ReasoningState("Critique", CritiqueText);
public sealed record FinalSpec(string FinalText) : ReasoningState("Final", FinalText);
```

This polymorphism enables uniform processing of both initial drafts and improved specifications.

### Iteration Flow

```
Iteration 0: Initial Draft
  ┌─────────────────────────────────────┐
  │ DraftArrow(branch)                  │
  │   → branch + Draft("initial text")  │
  └─────────────────────────────────────┘
                   ↓

Iteration 1: First Refinement
  ┌──────────────────────────────────────────────────┐
  │ GetMostRecentReasoningState() → Draft           │
  │ CritiqueArrow(branch)                            │
  │   → branch + Critique("gaps and issues")         │
  │ ImproveArrow(branch)                             │
  │   → branch + FinalSpec("improved version 1")     │
  └──────────────────────────────────────────────────┘
                   ↓

Iteration 2: Second Refinement
  ┌──────────────────────────────────────────────────┐
  │ GetMostRecentReasoningState() → FinalSpec₁      │
  │ CritiqueArrow(branch)                            │
  │   → branch + Critique("remaining issues")        │
  │ ImproveArrow(branch)                             │
  │   → branch + FinalSpec("improved version 2")     │
  └──────────────────────────────────────────────────┘
                   ↓
              (repeat N times)
```

## Implementation Details

### GetMostRecentReasoningState

The key architectural component is the helper function that retrieves the most recent reasoning state:

```csharp
private static ReasoningState? GetMostRecentReasoningState(PipelineBranch branch)
{
    var reasoningStates = branch.Events
        .OfType<ReasoningStep>()
        .Select(e => e.State)
        .Where(s => s is Draft or FinalSpec)  // Only draft-like states
        .ToList();
    
    return reasoningStates.LastOrDefault();  // Most recent
}
```

**Design Rationale:**
- **Event Sourcing**: Events are immutable and append-only
- **Type Filtering**: Only Draft and FinalSpec represent content to critique (not Critique itself)
- **Temporal Ordering**: `LastOrDefault()` gets the chronologically latest state
- **Nullable Return**: `null` indicates no baseline exists (error condition)

### CritiqueArrow Refactoring

**Before (Limited to Draft only):**
```csharp
public static Step<PipelineBranch, PipelineBranch> CritiqueArrow(...)
    => async branch =>
    {
        Draft? draft = branch.Events
            .OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<Draft>()
            .LastOrDefault();
        
        if (draft is null) return branch;
        
        // Use draft.DraftText in prompt...
    };
```

**After (Supports iterative refinement):**
```csharp
public static Step<PipelineBranch, PipelineBranch> CritiqueArrow(...)
    => async branch =>
    {
        ReasoningState? currentState = GetMostRecentReasoningState(branch);
        if (currentState is null) return branch;
        
        // Use currentState.Text in prompt (polymorphic)...
    };
```

**Key Improvements:**
1. Works with both `Draft` and `FinalSpec` states
2. Automatically selects the most recent reasoning output
3. Uses polymorphic `Text` property instead of type-specific properties
4. Enables true iterative refinement

### ImproveArrow Refactoring

**Before:**
```csharp
Draft? draft = branch.Events.OfType<ReasoningStep>()
    .Select(e => e.State).OfType<Draft>().LastOrDefault();
Critique? critique = branch.Events.OfType<ReasoningStep>()
    .Select(e => e.State).OfType<Critique>().LastOrDefault();
```

**After:**
```csharp
ReasoningState? currentState = GetMostRecentReasoningState(branch);
Critique? critique = branch.Events.OfType<ReasoningStep>()
    .Select(e => e.State).OfType<Critique>().LastOrDefault();
```

The improvement now uses the most recent reasoning state (Draft or FinalSpec) as the baseline, combined with the most recent critique.

## Monadic Error Handling

The safe variants provide comprehensive error handling using the Result monad:

```csharp
public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeCritiqueArrow(...)
    => async branch =>
    {
        try
        {
            ReasoningState? currentState = GetMostRecentReasoningState(branch);
            if (currentState is null) 
                return Result<PipelineBranch, string>.Failure(
                    "No draft or previous improvement found to critique");
            
            // Process critique...
            return Result<PipelineBranch, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<PipelineBranch, string>.Failure(
                $"Critique generation failed: {ex.Message}");
        }
    };
```

**Benefits:**
- Explicit error handling without exceptions
- Composable error propagation through Kleisli composition
- Clear error messages for debugging
- Type-safe error representation

## Usage Examples

### CLI Interface

```bash
# Single refinement iteration
dotnet run -- pipeline -d "SetTopic('Clean Architecture') | UseRefinementLoop('1')"

# Multiple iterations for higher quality
dotnet run -- pipeline -d "SetTopic('Microservices') | UseRefinementLoop('3')"

# Manual step-by-step for debugging
dotnet run -- pipeline -d "SetTopic('DDD') | UseDraft | UseCritique | UseImprove"
```

### Programmatic Usage

```csharp
var branch = new PipelineBranch("reasoning", store, DataSource.FromPath("."));

// Iteration 0: Create draft
var draftArrow = ReasoningArrows.DraftArrow(llm, tools, embed, topic, query);
branch = await draftArrow(branch);

// Iteration 1-N: Refine iteratively
for (int i = 0; i < iterations; i++)
{
    var critiqueArrow = ReasoningArrows.CritiqueArrow(llm, tools, embed, topic, query);
    branch = await critiqueArrow(branch);
    
    var improveArrow = ReasoningArrows.ImproveArrow(llm, tools, embed, topic, query);
    branch = await improveArrow(branch);
}

// Extract final result
var finalSpec = branch.Events
    .OfType<ReasoningStep>()
    .Select(e => e.State)
    .OfType<FinalSpec>()
    .LastOrDefault();
```

### Safe Monadic Composition

```csharp
var safeRefinement = ReasoningArrows.SafeDraftArrow(llm, tools, embed, topic, query)
    .Then(ReasoningArrows.SafeCritiqueArrow(llm, tools, embed, topic, query))
    .Then(ReasoningArrows.SafeImproveArrow(llm, tools, embed, topic, query));

var result = await safeRefinement(branch);

result.Match(
    success => Console.WriteLine($"Refined: {success}"),
    error => Console.WriteLine($"Failed: {error}")
);
```

## Testing Strategy

The architecture is validated through comprehensive unit tests:

### Test Coverage

1. **CritiqueArrow_ShouldUseDraft_WhenNoFinalSpecExists**
   - Validates baseline behavior with initial draft

2. **CritiqueArrow_ShouldUseMostRecentFinalSpec_WhenItExists**
   - **Core test** for iterative refinement architecture
   - Ensures subsequent iterations use previous improvements

3. **ImproveArrow_ShouldUseMostRecentFinalSpec_WhenItExists**
   - Validates improvement builds on latest state

4. **MultiIterationRefinementLoop_ShouldChainProperly**
   - End-to-end test of 3 full iterations
   - Verifies event sequence: Draft, Critique, Improve, Critique, Improve, Critique, Improve

5. **SafeCritiqueArrow_ShouldReturnFailure_WhenNoReasoningStateExists**
   - Error handling validation

6. **SafeImproveArrow_ShouldReturnFailure_WhenNoReasoningStateExists**
   - Error handling validation

### Test Execution

```bash
# Run all refinement architecture tests
dotnet test --filter "FullyQualifiedName~RefinementLoopArchitectureTests"

# Run all tests
dotnet test
```

## Architectural Benefits

### 1. Progressive Quality Enhancement
Each iteration builds on previous improvements rather than starting over, leading to compounded quality gains.

### 2. Type Safety
Polymorphic states (`ReasoningState`) enable uniform processing while maintaining type safety through the type system.

### 3. Functional Purity
All operations are pure functions returning new immutable instances, enabling:
- Deterministic replay
- Parallel execution
- Time-travel debugging

### 4. Event Sourcing
Complete audit trail of reasoning process:
```
Events: [Draft, Critique₁, FinalSpec₁, Critique₂, FinalSpec₂, ...]
```

### 5. Composability
Kleisli arrows compose naturally:
```csharp
var fullRefinement = DraftArrow
    .Then(CritiqueArrow)
    .Then(ImproveArrow)
    .Then(CritiqueArrow)  // Second iteration
    .Then(ImproveArrow);
```

## Performance Considerations

### LLM Call Optimization

Each refinement iteration makes 2 LLM calls (critique + improve). For `N` iterations:
- **Total LLM calls**: `1 (draft) + 2N (iterations)`
- **Example**: 3 iterations = 7 LLM calls

### Caching Strategies

Consider caching vector store retrievals:
```csharp
var docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
// Reuse 'docs' across iterations if query doesn't change
```

### Parallel Processing

For independent branches, process in parallel:
```csharp
var tasks = branches.Select(async b =>
{
    for (int i = 0; i < iterations; i++)
    {
        b = await CritiqueArrow(llm, tools, embed, topic, query)(b);
        b = await ImproveArrow(llm, tools, embed, topic, query)(b);
    }
    return b;
});

var refined = await Task.WhenAll(tasks);
```

## Future Enhancements

### 1. Adaptive Iteration Count
```csharp
// Stop when quality threshold is met
while (qualityScore < threshold && iterations < maxIterations)
{
    branch = await RefineOnce(branch);
    qualityScore = await EvaluateQuality(branch);
    iterations++;
}
```

### 2. Multi-Branch Refinement
```csharp
// Explore multiple refinement paths
var branches = await ExploreRefinementPaths(initialBranch, 
    branchingFactor: 3, 
    depth: 2);
var best = SelectBestBranch(branches, criteria);
```

### 3. Incremental Prompts
```csharp
// Customize prompts per iteration
var prompt = iteration == 0 
    ? Prompts.InitialCritique 
    : Prompts.RefinementCritique;
```

## Conclusion

The iterative refinement architecture represents a significant advancement in AI reasoning pipelines. By ensuring each iteration builds upon previous improvements through careful state management and functional composition, the system achieves higher quality outputs while maintaining type safety, purity, and auditability.

The architecture is:
- **Theoretically Sound**: Based on category theory and functional programming principles
- **Practically Effective**: Tested with comprehensive unit tests
- **Production Ready**: Used in CLI, Web API, and programmatic interfaces
- **Extensible**: Easy to add new reasoning states or refinement strategies

For implementation details, see:
- `src/Ouroboros.Pipeline/Pipeline/Reasoning/ReasoningArrows.cs`
- `src/Ouroboros.Tests/Tests/RefinementLoopArchitectureTests.cs`
- `src/Ouroboros.CLI/CliSteps.cs` (UseRefinementLoop)
