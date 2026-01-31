# Ethics Framework Integration

## Overview

This document describes the integration of the Ethics Framework with key Ouroboros components.

## Integrated Components

### 1. MetaAIPlannerOrchestrator

**File:** `src/Ouroboros.Agent/Agent/MetaAI/MetaAIPlannerOrchestrator.cs`

**Changes:**
- Added `IEthicsFramework` as a required dependency (constructor parameter)
- Ethics evaluation is performed **after** safety checks in `PlanAsync` method
- Plans are evaluated using `IEthicsFramework.EvaluatePlanAsync()`
- Plans are blocked if:
  - Ethics evaluation fails (IsFailure)
  - Not permitted (IsPermitted = false)
  - Requires human approval (Level = RequiresHumanApproval)

**Type Mapping:**
```csharp
// Agent's Plan -> Ethics Plan
var planContext = new PlanContext
{
    Plan = new Core.Ethics.Plan
    {
        Goal = goal,
        Steps = plan.Steps.Select(s => new Core.Ethics.PlanStep
        {
            Action = s.Action,
            Parameters = s.Parameters,
            ExpectedOutcome = s.ExpectedOutcome,
            ConfidenceScore = s.ConfidenceScore
        }).ToArray()
    },
    ActionContext = new ActionContext { ... }
};
```

### 2. EmbodiedAgent

**File:** `src/Ouroboros.Application/Application/Embodied/EmbodiedAgent.cs`

**Changes:**
- Added `IEthicsFramework` as a required dependency (constructor parameter)
- Ethics evaluation is performed **before** action execution in `ActAsync` method
- Actions are evaluated using `IEthicsFramework.EvaluateActionAsync()`
- Actions are blocked if:
  - Ethics evaluation fails (IsFailure)
  - Not permitted (IsPermitted = false)
  - Requires human approval (Level = RequiresHumanApproval)

**Type Mapping:**
```csharp
// EmbodiedAction -> ProposedAction
var proposedAction = new ProposedAction
{
    ActionType = "embodied_action",
    Description = action.ActionName ?? "Embodied action in environment",
    Parameters = new Dictionary<string, object>
    {
        ["movement"] = action.Movement,
        ["rotation"] = action.Rotation,
        ["custom_actions"] = action.CustomActions
    },
    PotentialEffects = new[] { ... }
};
```

### 3. SkillExtractor

**File:** `src/Ouroboros.Agent/Agent/MetaAI/SelfImprovement/SkillExtractor.cs`

**Changes:**
- Added `IEthicsFramework` as a required dependency (constructor parameter)
- Ethics evaluation is performed **before** skill registration in `ExtractSkillAsync` method
- Skills are evaluated using `IEthicsFramework.EvaluateSkillAsync()`
- Skills are rejected if:
  - Ethics evaluation fails (IsFailure)
  - Not permitted (IsPermitted = false)
  - Requires human approval (Level = RequiresHumanApproval)

**Type Mapping:**
```csharp
// Agent's Skill -> Ethics Skill
var skillContext = new SkillUsageContext
{
    Skill = new Core.Ethics.Skill
    {
        Name = skill.Name,
        Description = skill.Description,
        Prerequisites = skill.Prerequisites,
        Steps = skill.Steps.Select(s => new Core.Ethics.PlanStep { ... }).ToArray(),
        SuccessRate = skill.SuccessRate,
        UsageCount = skill.UsageCount
    },
    ActionContext = new ActionContext { ... },
    Goal = execution.Plan.Goal
};
```

### 4. Builder Updates

**Files:**
- `src/Ouroboros.Agent/Agent/MetaAI/MetaAIBuilder.cs`
- `src/Ouroboros.Agent/Agent/MetaAI/SelfImprovement/Phase2OrchestratorBuilder.cs`

**Changes:**
- Added `WithEthicsFramework()` method to allow custom ethics frameworks
- Default ethics framework is created using `EthicsFrameworkFactory.CreateDefault()` if not provided
- Ethics framework is passed to `MetaAIPlannerOrchestrator` and `SkillExtractor` constructors

## Integration Principles

### 1. Minimal Changes
- Only added ethics evaluation calls
- No refactoring of existing code
- Preserved all existing behavior

### 2. Fail-Safe Design
- Ethics evaluation happens **before** critical operations
- Failures block the operation and return descriptive error messages
- Default ethics framework is always provided if not explicitly configured

### 3. Type Safety
- Proper type mapping between domain types and ethics types
- Used fully qualified names to avoid namespace collisions (e.g., `Domain.Embodied.Plan` vs `Core.Ethics.Plan`)
- All ethics types are immutable records

### 4. Dependency Injection
- Ethics framework injected via constructor
- Required dependency (not optional)
- Factory pattern used for default instantiation

## Usage Examples

### Creating a MetaAI Orchestrator with Ethics

```csharp
var orchestrator = new MetaAIBuilder()
    .WithLLM(llm)
    .WithTools(tools)
    .WithEthicsFramework(EthicsFrameworkFactory.CreateDefault()) // Optional - default used if not provided
    .Build();
```

### Creating an EmbodiedAgent with Ethics

```csharp
var agent = new EmbodiedAgent(
    environmentManager,
    EthicsFrameworkFactory.CreateDefault(),
    logger
);
```

## Testing

The integration preserves all existing tests. Ethics evaluation can be tested by:

1. **Unit Testing:** Mock `IEthicsFramework` to return specific clearance levels
2. **Integration Testing:** Use `EthicsFrameworkFactory.CreateDefault()` for real evaluation
3. **Scenario Testing:** Test with actions that should be denied, permitted, or require approval

## Error Handling

When ethics evaluation blocks an operation, the system returns a `Result.Failure` with a descriptive message:

- `"Plan failed ethics evaluation: {error}"` - Evaluation failed
- `"Plan blocked by ethics framework: {reasoning}"` - Not permitted
- `"Plan requires human approval: {reasoning}"` - Requires approval

## Performance Considerations

- Ethics evaluation is async and can be optimized with caching
- Evaluation happens once per plan/action/skill
- No additional overhead in the happy path beyond the evaluation call

## Security Guarantees

- **Cannot be bypassed:** Ethics framework is a required constructor parameter
- **Immutable:** All ethics types are immutable records
- **Auditable:** All evaluations are logged via the audit log (if configured)
- **Fail-safe:** Any failure blocks the operation

## Future Enhancements

1. **Caching:** Cache evaluation results for identical actions/plans
2. **Async Approval:** Support human-in-the-loop approval workflows
3. **Learning:** Track ethics violations to improve agent behavior
4. **Monitoring:** Add metrics for ethics evaluation performance
5. **Configuration:** Support dynamic principle updates (with safeguards)
