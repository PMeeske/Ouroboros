# Laws of Form Utilities

This directory contains practical implementations of three-valued logic based on G. Spencer-Brown's Laws of Form.

## Overview

Laws of Form introduces a three-valued logic system:
- **Mark** (⊤): Certain positive state
- **Void** (⊥): Certain negative state  
- **Imaginary** (i): Indeterminate/oscillating state

## Components

### 1. Form & FormExtensions (Base)

The foundational three-valued logic type with algebraic operations.

```csharp
// Basic operations
Form.Mark.Cross() // → Form.Void (complement)
Form.Mark.And(Form.Void) // → Form.Void
Form.Mark.Or(Form.Imaginary) // → Form.Imaginary

// Conversions
true.ToForm() // → Form.Mark
((bool?)null).ToForm() // → Form.Imaginary
Form.Imaginary.IsCertain() // → false
```

### 2. AuditableDecision<T>

For regulated domains requiring explicit uncertainty tracking and audit trails.

```csharp
// Approval
var approved = AuditableDecision<Application>.Approve(
    application,
    "All KYC checks passed",
    new[] { "ID verified", "Address confirmed", "Credit check passed" });

// Rejection
var rejected = AuditableDecision<Application>.Reject(
    "Failed identity verification",
    new[] { "Document mismatch" });

// Inconclusive - requires human review
var uncertain = AuditableDecision<Application>.Inconclusive(
    0.65, // confidence phase
    "Conflicting signals detected",
    new[] { "Credit score borderline", "Recent address change" });

// Check status
if (decision.RequiresHumanReview)
{
    Console.WriteLine($"Manual review required: {decision.ComplianceStatus}");
}

// Generate audit log
var auditEntry = decision.ToAuditEntry();
```

### 3. DecisionPipeline

Compose multiple decision criteria with proper three-valued logic.

```csharp
var criteria = new Func<Application, AuditableDecision<Application>>[]
{
    CheckCreditScore,
    CheckIdentityVerification,
    CheckFraudIndicators
};

// All criteria must pass
var decision = DecisionPipeline.Evaluate(
    application,
    criteria,
    app => new ApprovedAccount(app));

// At least one criterion must pass
var decision = DecisionPipeline.EvaluateAny(application, criteria);

// Sequential chain - stops at first failure
var final = DecisionPipeline.Chain(
    initialDecision,
    step1,
    step2,
    step3);
```

### 4. TriState & HierarchicalConfig

Three-valued configuration for inheritance hierarchies.

```csharp
// TriState: On, Off, or Inherit
var userSetting = TriState.Inherit;
var resolved = userSetting.Resolve(parentValue: true); // → true

// Hierarchical configuration
var config = new HierarchicalConfig(
    systemDefault: false,
    organizationOverride: TriState.On,
    teamOverride: TriState.Inherit,
    userOverride: TriState.Inherit);

// Resolve for specific levels
var userValue = config.ResolveForUser(); // → true (inherits from org)
var teamValue = config.ResolveForTeam(); // → true (inherits from org)

// Feature flag scenario
var featureEnabled = config.ResolveForUser();
if (featureEnabled)
{
    EnableFeature();
}
```

### 5. FormStateMachine<TState>

State machine with explicit support for indeterminate states.

```csharp
enum ServerRole { Follower, Candidate, Leader }

// Initialize
var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

// Transition to new state
machine.TransitionTo(ServerRole.Candidate, "Starting election");

// Enter indeterminate state (e.g., during leader election)
machine.EnterIndeterminateState(0.5, "Waiting for votes");

// Check state
if (machine.IsIndeterminate)
{
    Console.WriteLine($"Election in progress (phase: {machine.OscillationPhase})");
}

// Update oscillation phase as confidence changes
machine.UpdatePhase(0.75);

// Resolve when certainty is achieved
machine.ResolveState(ServerRole.Leader, "Won majority votes");

// Execute only when certain
var result = machine.WhenCertain(role => 
{
    Console.WriteLine($"Current role: {role}");
    return PerformLeaderOperation();
});

// Sample oscillating state
var sampledState = machine.SampleAt(
    ServerRole.Leader,
    ServerRole.Follower,
    timeStep: 0.25);
```

## Use Cases

### Compliance & Audit (AuditableDecision)
- KYC (Know Your Customer) processes
- Loan approval workflows
- Medical diagnosis systems
- Security clearance decisions

### Configuration Management (TriState/HierarchicalConfig)
- Feature flags with organizational hierarchy
- Permission inheritance systems
- Multi-tenant configuration
- User preference cascading

### Distributed Systems (FormStateMachine)
- Leader election in Raft/Paxos
- Network partition handling
- Consensus protocols
- Split-brain detection and recovery

## Algebraic Properties

The implementation preserves Laws of Form properties:

```csharp
// Law of Calling (idempotence)
x.And(x) == x
x.Or(x) == x

// Law of Crossing (double negation)
x.Cross().Cross() == x

// Commutativity
x.And(y) == y.And(x)
x.Or(y) == y.Or(x)

// Associativity
(x.And(y)).And(z) == x.And(y.And(z))
(x.Or(y)).Or(z) == x.Or(y.Or(z))

// Uncertainty propagation
Form.Mark.And(Form.Imaginary) == Form.Imaginary
Form.Void.Or(Form.Imaginary) == Form.Imaginary
```

## Testing

Comprehensive test suite with 104 tests covering:
- Algebraic law verification
- Property-based testing
- Integration scenarios (KYC, feature flags, leader election)
- Edge cases and error handling

See test files in `src/Ouroboros.Tests/Tests/`:
- `FormTests.cs`
- `AuditableDecisionTests.cs`
- `DecisionPipelineTests.cs`
- `TriStateTests.cs`
- `HierarchicalConfigTests.cs`
- `FormStateMachineTests.cs`
# Laws of Form Integration Layer

## Overview

This integration layer adds **safe, auditable AI tool execution** using Spencer-Brown's Laws of Form mathematics. It provides three-valued certainty logic (Mark/Void/Imaginary) for AI safety decisions, replacing binary allow/deny with:

- **Mark (Certain Safe)**: Execute immediately
- **Void (Certain Unsafe)**: Block and log
- **Imaginary (Uncertain)**: Escalate to human approval

## Components

### 1. Foundation Types (`Core/LawsOfForm/`)

#### `Form` - Three-Valued Logic
```csharp
var confident = Form.Cross();        // Mark (⌐)
var certain = Form.Void;             // Void (∅)
var uncertain = Form.Imaginary;      // Imaginary (i)

// Laws of Form operations
var negated = !form;                 // ⌐⌐ = void
var combined = form1 & form2;        // Conjunction (AND)
var either = form1 | form2;          // Disjunction (OR)
```

#### `AuditableDecision<T>` - Decision with Evidence Trail
```csharp
var decision = AuditableDecision<ToolResult>.Approve(
    result,
    "All safety criteria passed",
    new Evidence("auth", Form.Cross(), "User authorized"),
    new Evidence("rate", Form.Cross(), "Within rate limits"));

// Full audit trail for compliance
var auditLog = decision.ToAuditEntry();
```

### 2. Tool Safety Layer

#### `SafeToolExecutor` - Criterion-Based Gating
```csharp
var executor = new SafeToolExecutor(toolLookup)
    .AddCriterion("authorization", (call, ctx) => 
        ctx.User.HasPermission(call.ToolName).ToForm())
    .AddCriterion("rate_limit", (call, ctx) => 
        ctx.RateLimiter.IsAllowed(call).ToForm())
    .AddCriterion("content_safety", (call, ctx) => 
        ContentFilter.Analyze(call.Arguments) switch
        {
            SafetyLevel.Safe => Form.Cross(),
            SafetyLevel.Unsafe => Form.Void,
            SafetyLevel.Uncertain => Form.Imaginary
        })
    .OnUncertain(async call => await approvalQueue.EnqueueAndWait(call));

// Execute with full audit
var decision = await executor.ExecuteWithAudit(toolCall, context);
```

#### `ToolApprovalQueue` - Human-in-the-Loop
```csharp
var queue = new ToolApprovalQueue();

// Enqueue for review
var queueId = queue.Enqueue(toolCall, uncertainDecision);

// Human reviews and resolves
await queue.Resolve(queueId, approved: true, "Reviewed and approved");

// Or with timeout
var decision = await queue.EnqueueAndWait(toolCall, decision, TimeSpan.FromMinutes(5));
```

### 3. LLM Integration Layer

#### `ConfidenceGatedPipeline` - Confidence-Based Routing
```csharp
// Gate by confidence
var gate = ConfidenceGatedPipeline.GateByConfidence(threshold: 0.8);
var maybeResponse = await gate(llmResponse);

// Route by confidence level
var router = ConfidenceGatedPipeline.RouteByConfidence<string>(
    onHighConfidence: r => "proceed",
    onLowConfidence: r => "retry",
    onUncertain: r => "escalate");

// Aggregate multiple model opinions
var consensus = ConfidenceGatedPipeline.AggregateResponses(responses);
```

#### `ContradictionDetector` - Hallucination Detection
```csharp
var detector = new ContradictionDetector(claimExtractor);

// Check single response for self-contradictions
var consistency = detector.Analyze(llmResponse);

if (consistency.IsImaginary()) 
{
    // Response contains contradictions - handle carefully
}

// Check multiple responses for cross-model contradictions
var crossCheck = detector.AnalyzeMultiple(responses);
```

## Example: Full Safety Pipeline

```csharp
// Setup safety pipeline
var executor = new SafeToolExecutor(toolRegistry)
    .AddCriterion("authorization", (call, ctx) =>
        ctx.User.HasPermission(call.ToolName).ToForm())
    .AddCriterion("rate_limit", (call, ctx) =>
        ctx.RateLimiter.IsAllowed(call).ToForm())
    .AddCriterion("content_safety", (call, ctx) =>
        ctx.ContentFilter.Analyze(call.Arguments) switch
        {
            SafetyLevel.Safe => Form.Cross(),
            SafetyLevel.Unsafe => Form.Void,
            SafetyLevel.Uncertain => Form.Imaginary
        })
    .AddCriterion("model_confidence", (call, ctx) =>
        call.Confidence.ToForm(highThreshold: 0.85, lowThreshold: 0.4))
    .OnUncertain(async call =>
    {
        var queueId = await approvalQueue.Enqueue(call);
        return await WaitForHumanApproval(queueId);
    });

// In your LLM pipeline
var pipeline = Step.Pure<UserQuery>()
    .Bind(query => llm.GenerateWithTools(query))
    .Bind(async response =>
    {
        // Check for contradictions first
        var consistency = contradictionDetector.Analyze(response);
        if (consistency.IsImaginary())
            return Result<Response>.Failure("Response contains contradictions");
        
        // Execute any tool calls with safety gates
        foreach (var toolCall in response.ToolCalls)
        {
            var decision = await executor.ExecuteWithAudit(toolCall, context);
            
            // Log for compliance
            auditLog.Record(decision.ToAuditEntry());
            
            if (decision.Certainty.IsVoid())
                return Result<Response>.Failure($"Tool {toolCall.ToolName} blocked");
        }
        
        return Result<Response>.Success(response);
    });
```

## Laws of Form Properties

The system implements fundamental Laws of Form axioms:

1. **Double Negation**: `⌐⌐ = void` (negating twice returns to emptiness)
2. **Re-entry**: `f = ⌐f → Imaginary` (self-contradiction is paradoxical)
3. **Conjunction**: `Mark ∧ Mark = Mark`, `Mark ∧ Void = Void`, `* ∧ Imaginary = Imaginary`
4. **Disjunction**: `Mark ∨ * = Mark`, `Void ∨ Void = Void`, `Void ∨ Imaginary = Imaginary`

## Testing

Comprehensive test suite with 57+ tests:

- `FormTests` - Laws of Form axioms and operations
- `FormExtensionsTests` - Utility conversions and combinations
- `AuditableDecisionTests` - Decision lifecycle and evidence trails
- `SafeToolExecutorTests` - Multi-criteria safety gating
- `ToolApprovalQueueTests` - Human-in-the-loop workflows
- `ConfidenceGatedPipelineTests` - Confidence-based routing
- `ContradictionDetectorTests` - Hallucination detection
- `LawsOfFormIntegrationTests` - End-to-end safety pipeline

## Benefits

1. **Mathematical Foundation**: Based on Spencer-Brown's Laws of Form calculus
2. **Complete Audit Trail**: Every decision has full evidence chain for compliance
3. **Three-Valued Logic**: Handles uncertainty explicitly (not just true/false)
4. **Human-in-the-Loop**: Seamless escalation for uncertain decisions
5. **Composable Safety**: Multiple criteria combine using logical operations
6. **Type-Safe**: Full C# type system guarantees at compile time
7. **Monadic**: Integrates with existing Result/Option patterns

## References

- Spencer-Brown, G. (1969). *Laws of Form*
- Three-valued logic for AI safety
- Re-entry and self-referential paradoxes
