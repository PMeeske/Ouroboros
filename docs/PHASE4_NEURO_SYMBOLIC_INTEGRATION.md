# Phase 4: Neuro-Symbolic Integration - MeTTa DAG Encoding

This document describes the Phase 4 implementation of neuro-symbolic integration in Ouroboros, which bridges neural and symbolic reasoning by integrating MeTTa-based facts/queries with DAG-driven constraint satisfaction.

## Overview

Phase 4 introduces the ability to encode pipeline DAG (Directed Acyclic Graph) nodes as symbolic facts that can be reasoned about using MeTTa's symbolic logic engine. This enables:

1. **Explainable Plans**: Plans can be selected based on symbolic constraints with full explanations
2. **Constraint Satisfaction**: DAG operations are validated against symbolic rules
3. **Interactive Querying**: REPL interface for exploring the symbolic representation

## MeTTa Encoding Format

### DAG Node Encoding

Pipeline branches and their events are encoded as MeTTa facts following this schema:

#### Branch Facts

```metta
; Branch type declaration
(: (Branch "branch-name") BranchType)

; Branch metadata
(HasEventCount (Branch "branch-name") <count>)
(HasSource (Branch "branch-name") "<source-path>")
```

#### Event Facts

**Reasoning Events:**
```metta
; Event type declaration
(: (Event "<event-id>") ReasoningEvent)

; Event properties
(HasReasoningKind (Event "<event-id>") <StepKind>)
(EventAtIndex (Event "<event-id>") <index>)
(BelongsToBranch (Event "<event-id>") (Branch "branch-name"))

; Tool usage (if applicable)
(UsesTool (Event "<event-id>") "tool-name")
```

**Ingest Events:**
```metta
(: (Event "<event-id>") IngestEvent)
(IngestedCount (Event "<event-id>") <count>)
(EventAtIndex (Event "<event-id>") <index>)
(BelongsToBranch (Event "<event-id>") (Branch "branch-name"))
```

#### Ordering Constraints

```metta
; Temporal ordering between events
(Before (Event "<event-id-1>") (Event "<event-id-2>"))

; Acyclicity constraint (no event can precede itself)
(= (Acyclic $e1 $e2) (and (Before $e1 $e2) (not (Before $e2 $e1))))
```

## Constraint Checking

### Built-in Constraints

The system provides several built-in constraints that can be checked:

1. **Acyclic**: Ensures no cycles in event ordering
   ```metta
   !(and (BelongsToBranch $e1 (Branch "main")) (Acyclic $e1 $e1))
   ```

2. **Valid Ordering**: Verifies events are ordered by index
   ```metta
   !(and (Before $e1 $e2) (EventAtIndex $e1 $i1) (EventAtIndex $e2 $i2) (< $i1 $i2))
   ```

3. **No Tool Conflicts**: Ensures tools are used correctly
   ```metta
   !(and (UsesTool $e1 $tool) (UsesTool $e2 $tool) (not (= $e1 $e2)))
   ```

### Custom Constraints

You can define custom constraints using MeTTa rules:

```metta
; Define a constraint that reasoning must precede ingestion
(= (ValidWorkflow $branch) 
    (and 
        (BelongsToBranch $r $branch)
        (BelongsToBranch $i $branch)
        (: $r ReasoningEvent)
        (: $i IngestEvent)
        (Before $r $i)))
```

## Plan Selection and Validation

### Plan Structure

Plans consist of actions that can be verified symbolically:

```csharp
var plan = new Plan("Read configuration")
    .WithAction(new FileSystemAction("read", "/config.yaml"))
    .WithAction(new NetworkAction("get", "https://api.example.com"));
```

### Symbolic Validation

Plans are validated against security contexts using MeTTa rules:

```metta
; Permission rules for ReadOnly context
(= (Allowed (FileSystemAction "read") ReadOnly) True)
(= (Allowed (FileSystemAction "write") ReadOnly) False)
(= (Allowed (NetworkAction "get") ReadOnly) True)
(= (Allowed (NetworkAction "post") ReadOnly) False)

; FullAccess allows everything
(= (Allowed $action FullAccess) True)
```

### Explainable Plan Selection

The `SymbolicPlanSelector` scores plans based on:

1. **Constraint satisfaction**: -1000 penalty for forbidden actions
2. **Complexity**: -0.5 penalty per action (simpler is better)
3. **Permission**: +10 bonus for each allowed action

Example:
```csharp
var selector = new SymbolicPlanSelector(engine);
await selector.InitializeAsync();

var result = await selector.SelectBestPlanAsync(
    candidates,
    SafeContext.ReadOnly);

result.Match(
    selected => Console.WriteLine($"Selected: {selected.Explanation}"),
    error => Console.WriteLine($"Error: {error}"));
```

## CLI Commands

### Query Command

Execute MeTTa queries against the symbolic knowledge base:

```bash
# Single query
ouroboros self query --event '(+ 1 2)'

# Interactive mode
ouroboros self query --interactive

# In interactive mode:
metta> query (+ 1 2)
Result: 3

metta> fact (human Socrates)
✓ Fact added

metta> rule (= (mortal $x) (human $x))
✓ Rule applied

metta> query (mortal Socrates)
Result: True
```

### Plan Check Command

Validate and select plans with constraint checking:

```bash
# Demonstration mode (uses sample plans)
ouroboros self plan

# Interactive mode
ouroboros self plan --interactive

# In interactive mode:
metta> plan
Enter plan actions (one per line). Type 'done' when finished.

action> filesystem read /config.yaml
✓ Added: (FileSystemAction "read")

action> network get https://api.example.com
✓ Added: (NetworkAction "get")

action> done

Enter plan description: Fetch remote configuration
Checking against ReadOnly context:
  Plan 'Fetch remote configuration' scored 19.00. Action (FileSystemAction "read") is permitted; Action (NetworkAction "get") is permitted; Plan has 2 actions (simpler is better)
```

### Interactive REPL

The interactive mode provides a consistent REPL interface:

```
╔═══════════════════════════════════════════════════════════╗
║       MeTTa Interactive Symbolic Reasoning Mode          ║
║                    Phase 4 Integration                    ║
╚═══════════════════════════════════════════════════════════╝

Type 'help' for available commands, 'exit' to quit.

metta> help

Available Commands:
  help, ?           - Show this help message
  query, q <expr>   - Execute a MeTTa query
  fact, f <fact>    - Add a fact to the knowledge base
  rule, r <rule>    - Apply a reasoning rule
  plan, p           - Interactive plan constraint checking
  reset             - Reset the knowledge base
  exit, quit, q!    - Exit interactive mode

Examples:
  query (+ 1 2)
  fact (human Socrates)
  rule (= (mortal $x) (human $x))
  query (mortal Socrates)
  plan
```

## Code Examples

### Encoding a Branch as MeTTa Facts

```csharp
using LangChainPipeline.Pipeline.Branches;

var store = new TrackedVectorStore();
var source = DataSource.FromPath("/my/data");
var branch = new PipelineBranch("main", store, source);

// Add some events
branch = branch.WithReasoning(
    new Draft("Initial draft"),
    "Generate a draft");
    
branch = branch.WithReasoning(
    new Critique("Needs improvement"),
    "Critique the draft");

// Encode as MeTTa facts
IReadOnlyList<string> facts = branch.ToMeTTaFacts();

// Facts will include:
// (: (Branch "main") BranchType)
// (HasEventCount (Branch "main") 2)
// (: (Event "<id-1>") ReasoningEvent)
// (HasReasoningKind (Event "<id-1>") Draft)
// (Before (Event "<id-1>") (Event "<id-2>"))
```

### Adding Facts to MeTTa Engine

```csharp
using Ouroboros.Tools.MeTTa;
using LangChainPipeline.Pipeline.Branches;

var engine = new SubprocessMeTTaEngine();
var branch = CreateBranch(); // Your branch creation logic

// Add all branch facts and constraint rules
var result = await engine.AddBranchFactsAsync(branch);

result.Match(
    _ => Console.WriteLine("✓ Branch encoded successfully"),
    error => Console.WriteLine($"✗ Error: {error}"));
```

### Verifying DAG Constraints

```csharp
using Ouroboros.Tools.MeTTa;
using LangChainPipeline.Pipeline.Branches;

var engine = new SubprocessMeTTaEngine();
await engine.AddBranchFactsAsync(branch);

// Check acyclicity constraint
var result = await engine.VerifyDagConstraintAsync(
    "main",
    "acyclic");

result.Match(
    isValid => Console.WriteLine($"Acyclic: {isValid}"),
    error => Console.WriteLine($"Error: {error}"));
```

### Creating and Validating Plans

```csharp
using LangChainPipeline.Pipeline.Verification;
using LangChainPipeline.Pipeline.Planning;

// Create plans
var safePlan = new Plan("Read-only operations")
    .WithAction(new FileSystemAction("read"))
    .WithAction(new NetworkAction("get"));

var unsafePlan = new Plan("Modify system")
    .WithAction(new FileSystemAction("write"));

// Create selector and validate
var engine = new SubprocessMeTTaEngine();
var selector = new SymbolicPlanSelector(engine);
await selector.InitializeAsync();

// Get explanation for a plan
var explanation = await selector.ExplainPlanAsync(
    safePlan,
    SafeContext.ReadOnly);

explanation.Match(
    text => Console.WriteLine(text),
    error => Console.WriteLine($"Error: {error}"));

// Select best plan from candidates
var best = await selector.SelectBestPlanAsync(
    new[] { safePlan, unsafePlan },
    SafeContext.ReadOnly);

best.Match(
    selected => {
        Console.WriteLine($"Selected: {selected.Plan.Description}");
        Console.WriteLine($"Score: {selected.Score:F2}");
        Console.WriteLine($"Reason: {selected.Explanation}");
    },
    error => Console.WriteLine($"Error: {error}"));
```

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                 Phase 4 Architecture                     │
└─────────────────────────────────────────────────────────┘

┌──────────────┐
│ PipelineBranch│
│   (DAG)       │
└──────┬────────┘
       │ ToMeTTaFacts()
       ▼
┌──────────────────┐
│  MeTTa Facts     │
│  (Symbolic)      │
└──────┬───────────┘
       │
       ├──────────────────────────────────┐
       │                                  │
       ▼                                  ▼
┌──────────────────┐           ┌──────────────────┐
│ Constraint       │           │ Plan Selection   │
│ Checking         │           │ & Validation     │
│                  │           │                  │
│ • Acyclic        │           │ • Score plans    │
│ • ValidOrdering  │           │ • Explain        │
│ • NoConflicts    │           │ • Select best    │
└──────┬───────────┘           └──────┬───────────┘
       │                              │
       └──────────┬───────────────────┘
                  │
                  ▼
         ┌────────────────┐
         │  IMeTTaEngine  │
         │                │
         │ • Subprocess   │
         │ • HTTP         │
         └────────────────┘
```

## Benefits

1. **Explainability**: Every plan selection includes a symbolic explanation
2. **Safety**: Constraints are enforced deterministically before execution
3. **Flexibility**: New constraints can be added as MeTTa rules
4. **Debugging**: Interactive REPL allows exploration of symbolic representation
5. **Integration**: Seamlessly combines neural (LLM) and symbolic (MeTTa) reasoning

## Testing

The implementation includes comprehensive tests in `DagMeTTaIntegrationTests.cs`:

- DAG encoding tests (5 tests)
- Constraint validation tests (4 tests)
- Plan selection tests (6 tests)

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~DagMeTTaIntegrationTests"
```

## Future Enhancements

- Integration with vector embeddings for hybrid retrieval
- More sophisticated scoring algorithms
- Plan composition from sub-plans
- Temporal reasoning about event sequences
- Multi-branch constraint checking
