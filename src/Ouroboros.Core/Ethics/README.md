# Ouroboros Ethics Framework

## Overview

The **Ethics Framework** is a foundational security and safety feature for the Ouroboros AI agent system. It provides comprehensive ethical evaluation capabilities that gate ALL agent actions, ensuring the system operates within defined ethical boundaries.

## Core Principles

The framework is built on **10 immutable ethical principles**:

1. **Do No Harm** (Priority: 1.0, Mandatory) - Prevent physical, psychological, economic, or digital harm
2. **Respect Autonomy** (Priority: 0.95, Mandatory) - Respect human agency and decision-making
3. **Honesty** (Priority: 0.90, Mandatory) - Provide truthful and accurate information
4. **Privacy** (Priority: 0.90, Mandatory) - Protect personal data and respect confidentiality
5. **Fairness** (Priority: 0.85, Mandatory) - Ensure equitable treatment without discrimination
6. **Transparency** (Priority: 0.80) - Be clear about capabilities and limitations
7. **Human Oversight** (Priority: 0.95, Mandatory) - Ensure meaningful human control
8. **Prevent Misuse** (Priority: 1.0, Mandatory) - Prevent use for harmful purposes
9. **Safe Self-Improvement** (Priority: 1.0, Mandatory) - Preserve safety during self-modification
10. **Corrigibility** (Priority: 1.0, Mandatory) - Remain receptive to correction and shutdown

## Architecture

### Core Components

#### 1. **IEthicsFramework**
Main interface providing ethical evaluation capabilities:
- `EvaluateActionAsync()` - Evaluate proposed actions
- `EvaluatePlanAsync()` - Evaluate multi-step plans
- `EvaluateGoalAsync()` - Evaluate goal alignment
- `EvaluateSkillAsync()` - Evaluate skill usage
- `EvaluateResearchAsync()` - Evaluate research activities
- `EvaluateSelfModificationAsync()` - Evaluate self-modifications
- `GetCorePrinciples()` - Get immutable principles
- `ReportEthicalConcernAsync()` - Report ethical concerns

#### 2. **ImmutableEthicsFramework**
Sealed implementation with:
- Non-overridable methods (cannot be bypassed)
- Immutable core principles
- Mandatory audit logging
- Internal constructor (factory-only creation)

#### 3. **EthicalClearance**
Result type with levels:
- `Permitted` - Action allowed, no concerns
- `PermittedWithConcerns` - Allowed but flagged
- `RequiresHumanApproval` - Needs human decision
- `Denied` - Blocked due to violations

#### 4. **IEthicsAuditLog**
Audit trail for accountability:
- `LogEvaluationAsync()` - Log all evaluations
- `LogViolationAttemptAsync()` - Log blocked violations
- `GetAuditHistoryAsync()` - Query history

#### 5. **EthicsEnforcementWrapper<TAction, TResult>**
Generic wrapper ensuring no action executes without clearance.

## Usage

### Basic Evaluation

```csharp
// Create framework
var framework = EthicsFrameworkFactory.CreateDefault();

// Define context
var context = new ActionContext
{
    AgentId = "my-agent",
    UserId = "user123",
    Environment = "production",
    State = new Dictionary<string, object>()
};

// Propose an action
var action = new ProposedAction
{
    ActionType = "read_file",
    Description = "Read configuration file",
    Parameters = new Dictionary<string, object> { ["path"] = "/config/app.json" },
    PotentialEffects = new[] { "Load configuration" }
};

// Evaluate
var result = await framework.EvaluateActionAsync(action, context);

if (result.IsSuccess && result.Value.IsPermitted)
{
    // Action is ethically cleared
    await ExecuteAction(action);
}
else
{
    // Action blocked or requires approval
    Console.WriteLine($"Action blocked: {result.Value.Reasoning}");
}
```

### Enforcement Wrapper

```csharp
// Wrap executor with ethics enforcement
var enforcedExecutor = new EthicsEnforcementWrapper<MyAction, MyResult>(
    innerExecutor: myExecutor,
    ethicsFramework: framework,
    actionConverter: action => new ProposedAction
    {
        ActionType = action.Type,
        Description = action.Description,
        Parameters = action.Params,
        PotentialEffects = action.Effects
    },
    context: context
);

// All executions automatically evaluated
var result = await enforcedExecutor.ExecuteAsync(myAction);
// Blocked if ethical violations detected
```

### Self-Modification

```csharp
var request = new SelfModificationRequest
{
    Type = ModificationType.CapabilityAddition,
    Description = "Add new skill",
    Justification = "Improve performance",
    ActionContext = context,
    ExpectedImprovements = new[] { "Faster processing" },
    PotentialRisks = new[] { "Increased complexity" },
    IsReversible = true,
    ImpactLevel = 0.5
};

var result = await framework.EvaluateSelfModificationAsync(request);
// ALL self-modifications require human approval
```

## Security Guarantees

### 1. **Cannot Be Disabled**
- Framework is mandatory for all agent actions
- No configuration to disable
- Enforcement at the code level

### 2. **Cannot Be Bypassed**
- Sealed implementations prevent inheritance
- Internal constructors prevent direct instantiation
- Factory pattern enforces proper creation

### 3. **Immutable Principles**
- Core principles defined at compile-time
- No runtime modification possible
- GetCorePrinciples() returns copies

### 4. **Mandatory Logging**
- All evaluations logged automatically
- Violation attempts recorded
- Audit trail for accountability

### 5. **Ethics Modifications Blocked**
- Attempts to modify ethical constraints are ALWAYS denied
- Critical violation with maximum severity
- Cannot be overridden even by human approval

## Testing

Comprehensive test suite in `src/Ouroboros.Tests/Tests/Ethics/`:

1. **EthicalPrincipleTests** - Principle immutability and properties
2. **EthicsFrameworkTests** - All evaluation methods
3. **EthicsEnforcementTests** - Wrapper blocking behavior
4. **EthicsAuditLogTests** - Audit logging functionality

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~Ethics"
```

## Examples

See `src/Ouroboros.Examples/Examples/EthicsFrameworkDemo.cs` for a working demonstration.

Run demo:
```csharp
await EthicsFrameworkDemo.RunDemoAsync();
```

## Design Decisions

### Why Sealed Classes?
Prevents inheritance-based bypasses. Ethics enforcement cannot be weakened by subclassing.

### Why Factory Pattern?
Ensures proper initialization. Prevents direct instantiation with compromised dependencies.

### Why Immutable Types?
Prevents runtime tampering. Ethical principles and clearances cannot be modified after creation.

### Why Minimal Dependencies?
Located in Ouroboros.Core to avoid circular dependencies. Uses lightweight types for Plan, Goal, Skill.

## Integration Guidelines

### For Action Executors
Wrap all executors with `EthicsEnforcementWrapper<T, R>`:

```csharp
services.AddScoped<IActionExecutor<MyAction, MyResult>>(sp =>
    new EthicsEnforcementWrapper<MyAction, MyResult>(
        innerExecutor: sp.GetRequiredService<MyActionExecutor>(),
        ethicsFramework: sp.GetRequiredService<IEthicsFramework>(),
        actionConverter: ConvertToProposedAction,
        context: sp.GetRequiredService<ActionContext>()
    ));
```

### For Planning Systems
Evaluate plans before execution:

```csharp
var planContext = new PlanContext
{
    Plan = generatedPlan,
    ActionContext = context,
    EstimatedRisk = CalculateRisk(generatedPlan)
};

var clearance = await framework.EvaluatePlanAsync(planContext);
if (!clearance.IsPermitted) return; // Block execution
```

### For Goal Systems
Evaluate goals before pursuit:

```csharp
var clearance = await framework.EvaluateGoalAsync(goal, context);
if (clearance.Level == EthicalClearanceLevel.Denied)
{
    RejectGoal(goal);
}
```

## Performance Considerations

- **Fast Path**: Simple actions with no violations evaluate in microseconds
- **Keyword Matching**: O(k*m) where k = keywords, m = description length
- **Audit Logging**: Async, non-blocking (in-memory default)
- **Caching**: Consider caching clearances for identical actions
- **Scalability**: Stateless framework, safe for concurrent use

## Future Enhancements

1. **Machine Learning Integration** - Train models to detect subtle violations
2. **Persistent Audit Storage** - Database-backed audit logs
3. **Human-in-the-Loop** - Integration with approval workflows
4. **Principle Weighting** - Context-sensitive principle priorities
5. **Explanation Generation** - Detailed explanations of decisions
6. **Federated Learning** - Learn from ethical decisions across deployments

## License

Copyright (c) PlaceholderCompany. All rights reserved.

---

**Remember**: The Ethics Framework is not optional. It is a foundational safety mechanism that protects users, systems, and the integrity of the Ouroboros platform.
