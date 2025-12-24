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
