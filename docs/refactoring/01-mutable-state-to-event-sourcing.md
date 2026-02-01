# Refactoring Guide: Mutable State to PipelineBranch Event Sourcing

## Overview

This guide demonstrates how to convert imperative class-based features with mutable state into the functional Ouroboros pattern using `PipelineBranch` immutable event sourcing. This pattern leverages category theory principles and monadic composition to create robust, reproducible AI pipelines.

## Table of Contents

- [Why Event Sourcing?](#why-event-sourcing)
- [Before Pattern: Imperative with Mutable State](#before-pattern-imperative-with-mutable-state)
- [After Pattern: PipelineBranch Event Sourcing](#after-pattern-pipelinebranch-event-sourcing)
- [Key Concepts](#key-concepts)
  - [Immutable State Transitions](#immutable-state-transitions)
  - [Event Types](#event-types)
  - [Querying State History](#querying-state-history)
- [Step-by-Step Refactoring](#step-by-step-refactoring)
- [When NOT to Use This Pattern](#when-not-to-use-this-pattern)
- [Complete Example](#complete-example)

## Why Event Sourcing?

The `PipelineBranch` event sourcing pattern provides several critical benefits:

1. **Complete Audit Trail**: Every state change is recorded as an immutable event
2. **Time Travel**: Replay the entire pipeline from any point in history
3. **Referential Transparency**: Pure functions with no side effects
4. **Debugging**: Understand exactly what happened and when
5. **Reproducibility**: Re-run the exact same pipeline with the same results
6. **Parallelization**: Safe concurrent execution without race conditions

## Before Pattern: Imperative with Mutable State

Traditional imperative code uses mutable state that changes in-place:

```csharp
/// <summary>
/// Traditional imperative document processor with mutable state.
/// This pattern has several problems:
/// - State changes are hidden and untrackable
/// - No audit trail of what happened
/// - Difficult to debug or reproduce issues
/// - Not thread-safe without locking
/// - Side effects make testing harder
/// </summary>
public class DocumentProcessor
{
    private string _currentDocument;
    private List<string> _history = new();
    private ProcessingStatus _status;
    private int _processCount = 0;

    public void Process(string document)
    {
        // Mutate state in-place - no record of changes
        _currentDocument = Transform(document);
        _history.Add(_currentDocument);
        _processCount++;
        _status = ProcessingStatus.Completed;
    }

    public void AddContext(string context)
    {
        // More mutation - tracking changes is manual and error-prone
        _currentDocument += "\n\n" + context;
        _history.Add(_currentDocument);
    }

    public string GetCurrent() => _currentDocument;
    
    public List<string> GetHistory() => _history; // Exposes mutable list!
    
    private string Transform(string doc) => doc.ToUpper();
}

// Usage problems:
var processor = new DocumentProcessor();
processor.Process("initial doc");
processor.AddContext("extra info");
// No way to know what state changes occurred or when
// No way to replay or undo
// Hard to test intermediate states
```

### Problems with This Approach

1. **Hidden State Changes**: No visibility into when or how state changed
2. **No Audit Trail**: Can't see the history of transformations
3. **Not Thread-Safe**: Multiple threads accessing the same instance causes race conditions
4. **Hard to Test**: Need to mock and track internal state
5. **No Replay**: Can't recreate the exact sequence of events
6. **Side Effects**: Methods mutate state, breaking referential transparency

## After Pattern: PipelineBranch Event Sourcing

The functional pattern uses immutable state transitions with event sourcing:

```csharp
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Core;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.States;

/// <summary>
/// Functional document processor using PipelineBranch event sourcing.
/// Benefits:
/// - Immutable state transitions
/// - Complete audit trail via events
/// - Thread-safe by design
/// - Easy to test and debug
/// - Reproducible executions
/// </summary>
public static class DocumentProcessorArrows
{
    /// <summary>
    /// Creates an arrow that processes a document by retrieving similar context
    /// and generating a transformed version.
    /// Returns a NEW immutable PipelineBranch with the event recorded.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> ProcessDocumentArrow(
        ToolAwareChatModel llm,
        IEmbeddingModel embed,
        string query) =>
        async branch =>
        {
            // Retrieve similar documents from vector store
            var docs = await branch.Store.GetSimilarDocuments(embed, query, amount: 8);
            var context = string.Join("\n---\n", docs.Select(d => d.PageContent));
            
            // Generate transformed document
            var transformed = await llm.GenerateAsync(context);
            
            // Return NEW immutable branch with reasoning event
            // Original branch is unchanged (referential transparency)
            return branch.WithReasoning(
                new Draft(transformed), 
                $"Processed document with query: {query}");
        };

    /// <summary>
    /// Creates an arrow that adds contextual information to the pipeline.
    /// Demonstrates composability - can be chained with other arrows.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> AddContextArrow(
        ToolAwareChatModel llm,
        string additionalContext) =>
        async branch =>
        {
            // Get the last reasoning state (if any)
            var lastState = branch.Events
                .OfType<ReasoningStep>()
                .Select(e => e.State)
                .LastOrDefault();

            var currentText = lastState?.Text ?? "";
            var enhanced = await llm.GenerateAsync($"{currentText}\n\nContext: {additionalContext}");
            
            // Return new branch with enhancement recorded
            return branch.WithReasoning(
                new Draft(enhanced),
                $"Added context: {additionalContext}");
        };

    /// <summary>
    /// Creates an arrow that ingests documents into the vector store.
    /// Records the ingestion as an event for audit trail.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> IngestDocumentsArrow(
        IEmbeddingModel embed,
        IEnumerable<string> documents,
        string sourcePath) =>
        async branch =>
        {
            // Ingest documents into vector store
            var ids = new List<string>();
            foreach (var doc in documents)
            {
                var id = await branch.Store.AddDocumentAsync(embed, doc);
                ids.Add(id);
            }
            
            // Return new branch with ingestion event recorded
            return branch.WithIngestEvent(sourcePath, ids);
        };
}

// Usage - functional composition:
var branch = new PipelineBranch("doc-processor", vectorStore, dataSource);

// Compose arrows using monadic bind
var pipeline = 
    DocumentProcessorArrows.ProcessDocumentArrow(llm, embed, "machine learning")
    .Bind(DocumentProcessorArrows.AddContextArrow(llm, "Focus on practical applications"))
    .Bind(DocumentProcessorArrows.IngestDocumentsArrow(embed, ["summary"], "/output"));

// Execute pipeline - returns new branch with all events
var resultBranch = await pipeline(branch);

// Query the complete history
var allReasoningSteps = resultBranch.Events
    .OfType<ReasoningStep>()
    .ToList();

// Replay from scratch if needed
var replayedBranch = await pipeline(new PipelineBranch("replay", vectorStore, dataSource));
```

### Benefits of This Approach

1. **Immutable State**: Each operation returns a new `PipelineBranch`, original is unchanged
2. **Event Trail**: All events stored in `branch.Events` with timestamps
3. **Thread-Safe**: Immutability eliminates race conditions
4. **Testable**: Pure functions are easy to unit test
5. **Composable**: Arrows can be chained using `Bind`, `Map`, etc.
6. **Reproducible**: Save and replay the exact sequence of operations

## Key Concepts

### Immutable State Transitions

Every mutation returns a NEW `PipelineBranch` instance. The original branch remains unchanged, providing referential transparency.

```csharp
var branch1 = new PipelineBranch("example", store, source);
var branch2 = branch1.WithReasoning(new Draft("first"), "prompt1");
var branch3 = branch2.WithReasoning(new Draft("second"), "prompt2");

// branch1, branch2, branch3 are all distinct instances
// branch1 has 0 events
// branch2 has 1 event  
// branch3 has 2 events
```

Internally, `PipelineBranch` uses `ImmutableList<PipelineEvent>` for efficient structural sharing:

```csharp
// From PipelineBranch.cs
public PipelineBranch WithReasoning(ReasoningState state, string prompt, List<ToolExecution>? tools = null)
{
    ReasoningStep newEvent = new ReasoningStep(Guid.NewGuid(), state.Kind, state, DateTime.UtcNow, prompt, tools);
    return new PipelineBranch(Name, Store, Source, _events.Add(newEvent));
    // Returns NEW branch, original unchanged
}
```

### Event Types

`PipelineBranch` supports several event types, all derived from `PipelineEvent`:

#### ReasoningStep

Captures reasoning state changes (Draft, Critique, FinalSpec, etc.):

```csharp
public sealed record ReasoningStep(
    Guid Id,
    string StepKind,
    ReasoningState State,  // Draft, Critique, FinalSpec, etc.
    DateTime Timestamp,
    string Prompt,
    List<ToolExecution>? ToolCalls = null
) : PipelineEvent(Id, "Reasoning", Timestamp);

// Usage
var branch = branch.WithReasoning(
    new Draft("Initial response"),
    "What is functional programming?",
    toolCalls);
```

#### IngestBatch

Captures document ingestion events:

```csharp
public sealed record IngestBatch(
    Guid Id,
    string Source,          // Source path or identifier
    IReadOnlyList<string> Ids,  // Document IDs ingested
    DateTime Timestamp
) : PipelineEvent(Id, "Ingest", Timestamp);

// Usage
var branch = branch.WithIngestEvent(
    "/data/documents", 
    new[] { "doc1", "doc2", "doc3" });
```

#### EpisodeEvent

Captures embodied agent episodes (Phase 1 Embodiment):

```csharp
var branch = branch.WithEpisode(episodeInstance);
```

#### Custom Events

You can add custom events using the generic `WithEvent` method:

```csharp
public sealed record CustomProcessingEvent(
    Guid Id,
    string ProcessType,
    string Result,
    DateTime Timestamp
) : PipelineEvent(Id, "CustomProcessing", Timestamp);

var customEvent = new CustomProcessingEvent(
    Guid.NewGuid(), 
    "DataTransform", 
    "Success", 
    DateTime.UtcNow);
    
var branch = branch.WithEvent(customEvent);
```

### Querying State History

The immutable event list can be queried using LINQ:

```csharp
// Get the last reasoning state
var lastState = branch.Events
    .OfType<ReasoningStep>()
    .Select(e => e.State)
    .LastOrDefault();

// Get all drafts in chronological order
var allDrafts = branch.Events
    .OfType<ReasoningStep>()
    .Select(e => e.State)
    .OfType<Draft>()
    .ToList();

// Get all reasoning steps with tool calls
var stepsWithTools = branch.Events
    .OfType<ReasoningStep>()
    .Where(e => e.ToolCalls != null && e.ToolCalls.Any())
    .ToList();

// Count how many documents were ingested
var totalIngested = branch.Events
    .OfType<IngestBatch>()
    .Sum(e => e.Ids.Count);

// Get events in a time range
var recentEvents = branch.Events
    .Where(e => e.Timestamp > DateTime.UtcNow.AddHours(-1))
    .ToList();

// Check if any critique step exists
var hasCritique = branch.Events
    .OfType<ReasoningStep>()
    .Any(e => e.State is Critique);
```

### PipelineBranch Methods Reference

```csharp
// Create new branch
var branch = new PipelineBranch(name, vectorStore, dataSource);

// Add reasoning event (Draft, Critique, FinalSpec, etc.)
branch = branch.WithReasoning(reasoningState, prompt, toolCalls);

// Add ingestion event
branch = branch.WithIngestEvent(sourcePath, documentIds);

// Add generic event
branch = branch.WithEvent(customEvent);

// Fork the branch (copy with new name/store)
var forkedBranch = branch.Fork("new-name", newVectorStore);

// Change data source (preserves events)
branch = branch.WithSource(newDataSource);

// Access immutable events list
IReadOnlyList<PipelineEvent> events = branch.Events;
```

## Step-by-Step Refactoring

### Step 1: Identify Mutable State

Look for:
- Private fields that change over time (`_field`)
- Methods that modify instance state (`void Process()`)
- Properties with setters
- Collections that are modified in-place

### Step 2: Convert to Static Arrow Functions

Replace instance methods with static functions that:
- Take `PipelineBranch` as input
- Return `Step<PipelineBranch, PipelineBranch>`
- Are pure (no side effects)

```csharp
// Before
public void ProcessDocument(string doc)
{
    _currentDoc = Transform(doc);
    _history.Add(_currentDoc);
}

// After
public static Step<PipelineBranch, PipelineBranch> ProcessDocumentArrow(string doc) =>
    async branch =>
    {
        var transformed = Transform(doc);
        return branch.WithReasoning(new Draft(transformed), $"Processed: {doc}");
    };
```

### Step 3: Replace Mutations with WithReasoning/WithIngestEvent

Replace state mutations with immutable operations:

```csharp
// Before - mutation
_results.Add(result);
_status = Status.Completed;

// After - immutable return
return branch.WithReasoning(
    new Draft(result),
    promptUsed,
    toolExecutions);
```

### Step 4: Compose Arrows

Chain operations using monadic composition:

```csharp
var pipeline = Step.Pure<PipelineBranch>()
    .Bind(LoadDataArrow(dataSource))
    .Bind(ProcessDocumentArrow(query))
    .Bind(CritiqueArrow(llm))
    .Bind(ImproveArrow(llm));

var result = await pipeline(initialBranch);
```

### Step 5: Query History Instead of Tracking State

Replace state fields with event queries:

```csharp
// Before - mutable field
private int _processCount;

// After - query events
var processCount = branch.Events.OfType<ReasoningStep>().Count();
```

## When NOT to Use This Pattern

While powerful, event sourcing isn't always the right choice:

### ❌ Don't Use For:

1. **Entry Points** (Program.cs, Main methods)
   - Simple sequential setup code doesn't need event sourcing
   - Use for business logic, not initialization

2. **Infrastructure Configuration**
   - Loading config files
   - Setting up DI containers
   - Configuring middleware

3. **Simple CRUD Operations**
   - Basic database reads/writes
   - File I/O without business logic
   - Simple data transformations

4. **Performance-Critical Hot Paths**
   - Tight loops processing millions of items
   - Real-time systems with microsecond requirements
   - Low-level hardware interaction

5. **Truly Ephemeral Operations**
   - Temporary file cleanup
   - Cache invalidation
   - Logging/metrics (unless you need audit trail)

### ✅ Do Use For:

1. **AI Pipeline Operations**
   - LLM reasoning steps
   - Document processing workflows
   - Multi-step agent executions

2. **Business Logic with Audit Requirements**
   - Decision-making processes
   - Approval workflows
   - Compliance-sensitive operations

3. **Complex State Machines**
   - Multi-phase workflows
   - Conditional branching logic
   - Stateful agent behaviors

4. **Reproducible Computations**
   - Research experiments
   - A/B testing scenarios
   - Debugging production issues

## Complete Example

See the runnable example in:
- **Source**: `src/Ouroboros.Examples/Examples/RefactoringExamples/MutableStateToEventSourcingExample.cs`

The example demonstrates:
- Before/After pattern comparison
- Creating arrows with `WithReasoning` and `WithIngestEvent`
- Querying event history
- Immutability guarantees
- Composing arrows with `Bind`

## Related Documentation

- [PipelineBranch Implementation](../../src/Ouroboros.Pipeline/Pipeline/Branches/PipelineBranch.cs)
- [Event Types](../../src/Ouroboros.Domain/Domain/Events/)
- [Reasoning States](../../src/Ouroboros.Domain/Domain/States/)
- [Reasoning Arrows Examples](../../src/Ouroboros.Pipeline/Pipeline/Reasoning/ReasoningArrows.cs)
- [Architecture Documentation](../ARCHITECTURE.md)
- [Cognitive Architecture](../COGNITIVE_ARCHITECTURE.md)

## Summary

Converting mutable state to PipelineBranch event sourcing:

1. ✅ **Provides complete audit trail** of all operations
2. ✅ **Enables time travel and replay** for debugging
3. ✅ **Guarantees thread safety** through immutability
4. ✅ **Improves testability** with pure functions
5. ✅ **Supports functional composition** using monadic operations

The pattern is essential for building robust, reproducible AI pipelines in Ouroboros while maintaining mathematical rigor through category theory principles.
