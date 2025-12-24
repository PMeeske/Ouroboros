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
