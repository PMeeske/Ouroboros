# Orchestrator Pipeline Transformation - Implementation Guide

## Overview

This document provides examples and patterns for transforming sequential async operations in orchestrator classes to composable pipeline structures using `Then`/`Map` composition.

## Transformation Pattern

### General Structure

**Before (Imperative Sequential)**:
```csharp
public async Task<Result<Output>> ExecuteWorkflow(Input input)
{
    var step1Result = await Step1Async(input);
    if (step1Result.IsFailure) return Failure(step1Result.Error);
    
    var step2Result = await Step2Async(step1Result.Value);
    if (step2Result.IsFailure) return Failure(step2Result.Error);
    
    var step3Result = await Step3Async(step2Result.Value);
    if (step3Result.IsFailure) return Failure(step3Result.Error);
    
    return Success(new Output(step3Result.Value));
}
```

**After (Monadic Composition)**:
```csharp
public static Step<Unit, Result<Output>> WorkflowPipeline(Dependencies deps) =>
    Step1Arrow(deps)
        .Then(Step2Arrow(deps))
        .Then(Step3Arrow(deps))
        .Map(CreateOutput);

private static Step<Unit, Result<Context>> Step1Arrow(Dependencies deps) =>
    async _ => { /* implementation */ };

private static Step<Result<Context>, Result<Context>> Step2Arrow(Dependencies deps) =>
    async contextResult => { /* implementation */ };
```

## Example 1: EpisodeRunner Transformation

### Original Implementation (Imperative)
```csharp
public async Task<Result<Episode>> RunEpisodeAsync(int maxSteps)
{
    var episodeId = Guid.NewGuid();
    var steps = new List<EnvironmentStep>();
    var startTime = DateTime.UtcNow;

    // Reset environment
    var resetResult = await environment.ResetAsync();
    if (resetResult.IsFailure)
        return Result<Episode>.Failure($"Failed to reset: {resetResult.Error}");

    var currentState = resetResult.Value;
    var totalReward = 0.0;

    while (stepNumber < maxSteps)
    {
        // Get available actions
        var actionsResult = await environment.GetAvailableActionsAsync();
        if (actionsResult.IsFailure)
            return Result<Episode>.Failure($"Failed to get actions: {actionsResult.Error}");

        // Select action
        var actionResult = await policy.SelectActionAsync(currentState, actionsResult.Value);
        if (actionResult.IsFailure)
            return Result<Episode>.Failure($"Failed to select action: {actionResult.Error}");

        // Execute action
        var observationResult = await environment.ExecuteActionAsync(actionResult.Value);
        if (observationResult.IsFailure)
            return Result<Episode>.Failure($"Failed to execute: {observationResult.Error}");

        // Update state
        currentState = observationResult.Value.State;
        totalReward += observationResult.Value.Reward;
        stepNumber++;
    }

    return Result<Episode>.Success(new Episode(...));
}
```

### Transformed Implementation (Pipeline)
```csharp
public static Step<Unit, Result<Episode>> EpisodePipeline(
    IEnvironmentActor environment,
    IPolicy policy,
    string environmentName,
    int maxSteps) =>
    InitializeEpisodeArrow(environment, policy, environmentName, maxSteps)
        .Then(ExecuteEpisodeLoopArrow())
        .Map(FinalizeEpisode);

private static Step<Unit, Result<EpisodeContext>> InitializeEpisodeArrow(...) =>
    async _ =>
    {
        var episodeId = Guid.NewGuid();
        var resetResult = await environment.ResetAsync();
        
        if (resetResult.IsFailure)
            return Result<EpisodeContext>.Failure($"Failed to reset: {resetResult.Error}");

        var context = new EpisodeContext(
            episodeId, environmentName, environment, policy,
            new List<EnvironmentStep>(), resetResult.Value, ...);

        return Result<EpisodeContext>.Success(context);
    };

private static Step<EpisodeContext, Result<EpisodeContext>> ExecuteSingleStepArrow() =>
    GetActionsArrow()
        .Then(SelectActionArrow())
        .Then(ExecuteActionArrow())
        .Then(UpdatePolicyArrow())
        .Then(RecordStepArrow());

private static Step<EpisodeContext, Result<(EpisodeContext, IReadOnlyList<EnvironmentAction>)>> GetActionsArrow() =>
    async context =>
    {
        var actionsResult = await context.Environment.GetAvailableActionsAsync();
        return actionsResult.IsSuccess
            ? Result<...>.Success((context, actionsResult.Value))
            : Result<...>.Failure($"Failed to get actions: {actionsResult.Error}");
    };
```

## Example 2: MultiAgentCoordinator Transformation

### Original Implementation (Imperative)
```csharp
public async Task<Result<CollaborativePlan, string>> PlanCollaborativelyAsync(
    string goal,
    List<AgentId> participants)
{
    if (string.IsNullOrWhiteSpace(goal))
        return Result<CollaborativePlan, string>.Failure("Goal cannot be empty");

    // Get capabilities
    var capabilities = new List<AgentCapabilities>();
    foreach (var agentId in participants)
    {
        var capResult = await agentRegistry.GetAgentCapabilitiesAsync(agentId);
        if (capResult.IsFailure)
            return Result<...>.Failure($"Failed to get capabilities: {capResult.Error}");
        capabilities.Add(capResult.Value);
    }

    // Decompose goal
    var tasks = DecomposeGoalIntoTasks(goal);

    // Allocate tasks
    var allocationResult = await AllocateSkillBasedAsync(tasks, capabilities);
    if (allocationResult.IsFailure)
        return Result<...>.Failure($"Failed to allocate: {allocationResult.Error}");

    // Identify dependencies
    var dependencies = IdentifyDependencies(allocationResult.Value.Values.ToList());

    // Estimate duration
    var duration = EstimateDuration(assignments, dependencies);

    return Result<...>.Success(new CollaborativePlan(goal, assignments, dependencies, duration));
}
```

### Transformed Implementation (Pipeline)
```csharp
public static Step<Unit, Result<CollaborativePlan, string>> CollaborativePlanningPipeline(
    string goal,
    List<AgentId> participants,
    IAgentRegistry agentRegistry) =>
    ValidateInputArrow(goal, participants, agentRegistry)
        .Then(GatherCapabilitiesArrow())
        .Then(DecomposeTasksArrow())
        .Then(AllocateTasksArrow())
        .Then(IdentifyDependenciesArrow())
        .Then(EstimateDurationArrow())
        .Map(CreatePlan);

private static Step<Unit, Result<PlanningContext, string>> ValidateInputArrow(...) =>
    async _ =>
    {
        if (string.IsNullOrWhiteSpace(goal))
            return Result<...>.Failure("Goal cannot be empty");

        var context = new PlanningContext(goal, participants, agentRegistry, ...);
        return Result<...>.Success(context);
    };

private static Step<Result<PlanningContext, string>, Result<PlanningContext, string>> GatherCapabilitiesArrow() =>
    async contextResult =>
    {
        if (contextResult.IsFailure) return contextResult;

        var context = contextResult.Value;
        var capabilities = new List<AgentCapabilities>();

        foreach (var agentId in context.Participants)
        {
            var capResult = await context.AgentRegistry.GetAgentCapabilitiesAsync(agentId);
            if (capResult.IsFailure)
                return Result<...>.Failure($"Failed to get capabilities: {capResult.Error}");
            capabilities.Add(capResult.Value);
        }

        var updatedContext = context with { Capabilities = capabilities };
        return Result<...>.Success(updatedContext);
    };
```

## Key Patterns and Benefits

### 1. Context Pattern
```csharp
public sealed record WorkflowContext(
    string Id,
    Dependencies Dependencies,
    IntermediateState State,
    List<Event> Events);
```

**Benefits**:
- Immutable state throughout pipeline
- Clear data flow between steps
- Easy to add new fields without changing signatures

### 2. Arrow Composition
```csharp
// Sequential composition (Kleisli bind)
Step1Arrow()
    .Then(Step2Arrow())
    .Then(Step3Arrow())

// Functor mapping (transform output)
Step1Arrow()
    .Map(result => TransformOutput(result))

// Side effects without changing data
Step1Arrow()
    .Tap(result => LogProgress(result))
```

### 3. Error Propagation
```csharp
private static Step<Result<Context>, Result<Context>> StepArrow() =>
    async contextResult =>
    {
        if (contextResult.IsFailure)
            return contextResult; // Propagate error

        var context = contextResult.Value;
        // ... perform operation
        
        return operationResult.IsSuccess
            ? Result<Context>.Success(updatedContext)
            : Result<Context>.Failure(operationResult.Error);
    };
```

### 4. Pure Functions for Final Transformation
```csharp
private static Result<Output> FinalizeOutput(Result<Context> contextResult)
{
    if (contextResult.IsFailure)
        return Result<Output>.Failure(contextResult.Error);

    var context = contextResult.Value;
    return Result<Output>.Success(new Output(context));
}
```

## Testing Patterns

### Testing Individual Arrows
```csharp
[Fact]
public async Task Step1Arrow_WithValidInput_ShouldSucceed()
{
    // Arrange
    var arrow = Step1Arrow(dependencies);
    var input = CreateTestInput();

    // Act
    var result = await arrow(input);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
}
```

### Testing Composed Pipelines
```csharp
[Fact]
public async Task CompletePipeline_ShouldExecuteAllSteps()
{
    // Arrange
    var pipeline = WorkflowPipeline(dependencies);

    // Act
    var result = await pipeline(Unit.Value);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeOfType<ExpectedOutput>();
}
```

### Testing Composition
```csharp
[Fact]
public async Task Pipeline_CanBeComposedWithTap()
{
    // Arrange
    var logged = false;
    var pipeline = WorkflowPipeline(dependencies)
        .Tap(result =>
        {
            if (result.IsSuccess) logged = true;
        });

    // Act
    var result = await pipeline(Unit.Value);

    // Assert
    result.IsSuccess.Should().BeTrue();
    logged.Should().BeTrue("Tap should have executed");
}

[Fact]
public async Task Pipeline_CanBeTransformed()
{
    // Arrange
    var pipeline = WorkflowPipeline(dependencies)
        .Map(result => result.IsSuccess
            ? $"Success: {result.Value}"
            : $"Failed: {result.Error}");

    // Act
    var result = await pipeline(Unit.Value);

    // Assert
    result.Should().Contain("Success");
}
```

## Advantages of Pipeline Approach

1. **Composability**: Workflows become first-class values that can be composed, transformed, and reused
2. **Testability**: Each arrow can be tested independently
3. **Type Safety**: Compiler enforces correct data flow
4. **Error Handling**: Automatic error propagation through Result monads
5. **Observability**: Easy to add logging, metrics, tracing via Tap
6. **Flexibility**: Steps can be reordered, replaced, or parallelized
7. **Readability**: Clear visualization of workflow structure
8. **Maintainability**: Changes to individual steps don't affect composition

## Common Pitfalls and Solutions

### Pitfall 1: Too Many Context Fields
**Problem**: Context becomes unwieldy with many fields
**Solution**: Use nested records or split into multiple contexts

### Pitfall 2: Breaking Composition
**Problem**: Returning concrete types instead of Result<T>
**Solution**: Always use Result<T> for error handling

### Pitfall 3: Side Effects in Arrows
**Problem**: Arrows perform side effects that can't be tested
**Solution**: Pass dependencies explicitly, use Tap for side effects

### Pitfall 4: Complex Conditional Logic
**Problem**: Arrows contain complex branching logic
**Solution**: Create separate arrow factories for different paths

## Next Steps

To apply this pattern to new orchestrators:

1. Identify sequential async operations
2. Define a Context record with all intermediate state
3. Extract each step into an arrow function
4. Compose arrows using Then/Map
5. Write tests for individual arrows and complete pipeline
6. Document the pipeline structure

For more examples, see:
- `EpisodeRunnerPipeline.cs`
- `MultiAgentCoordinatorPipeline.cs`
- `EpisodeRunnerPipelineTests.cs`
- `MultiAgentCoordinatorPipelineTests.cs`
