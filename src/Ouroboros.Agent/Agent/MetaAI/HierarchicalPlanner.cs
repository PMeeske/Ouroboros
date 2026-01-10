#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Hierarchical Planner - Multi-level plan decomposition
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a hierarchical plan with multiple levels.
/// </summary>
public sealed record HierarchicalPlan(
    string Goal,
    Plan TopLevelPlan,
    Dictionary<string, Plan> SubPlans,
    int MaxDepth,
    DateTime CreatedAt);

/// <summary>
/// Represents configuration for hierarchical planning.
/// </summary>
public sealed record HierarchicalPlanningConfig(
    int MaxDepth = 3,
    int MinStepsForDecomposition = 3,
    double ComplexityThreshold = 0.7);

// ==========================================================
// HTN (Hierarchical Task Network) Planning Types
// ==========================================================

/// <summary>
/// Represents an abstract task that can be decomposed into subtasks.
/// </summary>
public sealed record AbstractTask(
    string Name,
    List<string> Preconditions,
    List<TaskDecomposition> PossibleDecompositions);

/// <summary>
/// Represents a decomposition of an abstract task into concrete subtasks.
/// </summary>
public sealed record TaskDecomposition(
    string AbstractTask,
    List<string> SubTasks,
    List<string> OrderingConstraints);

/// <summary>
/// Represents a concrete plan for executing an abstract task.
/// </summary>
public sealed record ConcretePlan(
    string AbstractTaskName,
    List<string> ConcreteSteps);

/// <summary>
/// Represents an HTN hierarchical plan with abstract and concrete decompositions.
/// </summary>
public sealed record HtnHierarchicalPlan(
    string Goal,
    List<AbstractTask> AbstractTasks,
    List<ConcretePlan> Refinements);

// ==========================================================
// Temporal Planning Types
// ==========================================================

/// <summary>
/// Defines the temporal relationship between two tasks.
/// </summary>
public enum TemporalRelation
{
    /// <summary>Task A must complete before Task B starts.</summary>
    Before,
    
    /// <summary>Task A must start after Task B completes.</summary>
    After,
    
    /// <summary>Task A must execute during Task B's execution.</summary>
    During,
    
    /// <summary>Task A and Task B must have overlapping execution.</summary>
    Overlaps,
    
    /// <summary>Task A must finish before Task B starts.</summary>
    MustFinishBefore,
    
    /// <summary>Task A and Task B must execute simultaneously.</summary>
    Simultaneous
}

/// <summary>
/// Represents a temporal constraint between two tasks.
/// </summary>
public sealed record TemporalConstraint(
    string TaskA,
    string TaskB,
    TemporalRelation Relation,
    TimeSpan? Duration = null);

/// <summary>
/// Represents a task scheduled with specific start and end times.
/// </summary>
public sealed record ScheduledTask(
    string Name,
    DateTime StartTime,
    DateTime EndTime,
    List<string> Dependencies);

/// <summary>
/// Represents a plan with temporal constraints and scheduling.
/// </summary>
public sealed record TemporalPlan(
    string Goal,
    List<ScheduledTask> Tasks,
    TimeSpan TotalDuration);

// ==========================================================
// Plan Repair Types
// ==========================================================

/// <summary>
/// Defines the strategy for repairing a broken plan.
/// </summary>
public enum RepairStrategy
{
    /// <summary>Full replanning from current state.</summary>
    Replan,
    
    /// <summary>Minimal local fixes to the plan.</summary>
    Patch,
    
    /// <summary>Use similar past repairs as templates.</summary>
    CaseBased,
    
    /// <summary>Undo and retry with alternative decompositions.</summary>
    Backtrack
}

/// <summary>
/// Represents a step that has been executed with its result.
/// </summary>
public sealed record ExecutedStep(
    string StepName,
    bool Success,
    TimeSpan Duration,
    Dictionary<string, object> Outputs);

/// <summary>
/// Represents the execution trace of a plan including failure information.
/// </summary>
public sealed record ExecutionTrace(
    List<ExecutedStep> Steps,
    int FailedAtIndex,
    string FailureReason);

// ==========================================================
// Plan Explanation Types
// ==========================================================

/// <summary>
/// Defines the level of detail for plan explanations.
/// </summary>
public enum ExplanationLevel
{
    /// <summary>One-line summary of the plan.</summary>
    Brief,
    
    /// <summary>Step-by-step explanation of each action.</summary>
    Detailed,
    
    /// <summary>Explanation of why each step is necessary.</summary>
    Causal,
    
    /// <summary>Explanation of what would happen without each step.</summary>
    Counterfactual
}

/// <summary>
/// Interface for hierarchical planning capabilities.
/// </summary>
public interface IHierarchicalPlanner
{
    /// <summary>
    /// Creates a hierarchical plan by decomposing complex tasks.
    /// </summary>
    Task<Result<HierarchicalPlan, string>> CreateHierarchicalPlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        HierarchicalPlanningConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a hierarchical plan recursively.
    /// </summary>
    Task<Result<ExecutionResult, string>> ExecuteHierarchicalAsync(
        HierarchicalPlan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Creates an HTN hierarchical plan by decomposing abstract tasks using task networks.
    /// </summary>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="taskNetwork">Dictionary of task decompositions keyed by task name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An HTN hierarchical plan with abstract tasks and refinements.</returns>
    Task<Result<HtnHierarchicalPlan, string>> PlanHierarchicalAsync(
        string goal,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a temporal plan that satisfies the given temporal constraints.
    /// </summary>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="constraints">List of temporal constraints between tasks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A temporal plan with scheduled tasks.</returns>
    Task<Result<TemporalPlan, string>> PlanWithConstraintsAsync(
        string goal,
        List<TemporalConstraint> constraints,
        CancellationToken ct = default);

    /// <summary>
    /// Repairs a broken plan using the specified repair strategy.
    /// </summary>
    /// <param name="brokenPlan">The plan that failed during execution.</param>
    /// <param name="trace">The execution trace showing what was executed and where it failed.</param>
    /// <param name="strategy">The repair strategy to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A repaired plan that addresses the failure.</returns>
    Task<Result<Plan, string>> RepairPlanAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        RepairStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Generates an explanation of a plan at the specified level of detail.
    /// </summary>
    /// <param name="plan">The plan to explain.</param>
    /// <param name="level">The level of detail for the explanation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A textual explanation of the plan.</returns>
    Task<Result<string, string>> ExplainPlanAsync(
        Plan plan,
        ExplanationLevel level,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of hierarchical planner for complex task decomposition.
/// </summary>
public sealed class HierarchicalPlanner : IHierarchicalPlanner
{
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private readonly IChatCompletionModel _llm;

    public HierarchicalPlanner(
        IMetaAIPlannerOrchestrator orchestrator,
        IChatCompletionModel llm)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    /// <summary>
    /// Creates a hierarchical plan by decomposing complex tasks.
    /// </summary>
    public async Task<Result<HierarchicalPlan, string>> CreateHierarchicalPlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        HierarchicalPlanningConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new HierarchicalPlanningConfig();

        using var activity = OrchestrationTracing.StartPlanCreation(goal, config.MaxDepth);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(goal))
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
            return Result<HierarchicalPlan, string>.Failure("Goal cannot be empty");
        }

        if (config.MaxDepth < 1)
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
            return Result<HierarchicalPlan, string>.Failure("MaxDepth must be at least 1");
        }

        Dictionary<string, object> safeContext = context ?? new Dictionary<string, object>();

        try
        {
            // Create top-level plan
            Result<Plan, string> topLevelResult = await _orchestrator.PlanAsync(goal, safeContext, ct);

            if (!topLevelResult.IsSuccess)
            {
                stopwatch.Stop();
                OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
                return Result<HierarchicalPlan, string>.Failure(topLevelResult.Error);
            }

            Plan topLevelPlan = topLevelResult.Value;
            Dictionary<string, Plan> subPlans = new Dictionary<string, Plan>();

            // Decompose complex steps if needed
            if (topLevelPlan.Steps.Count >= config.MinStepsForDecomposition)
            {
                await DecomposeStepsAsync(
                    topLevelPlan.Steps,
                    subPlans,
                    safeContext,
                    config,
                    currentDepth: 1,
                    ct);
            }

            HierarchicalPlan hierarchicalPlan = new HierarchicalPlan(
                goal,
                topLevelPlan,
                subPlans,
                MaxDepth: config.MaxDepth,
                CreatedAt: DateTime.UtcNow);

            stopwatch.Stop();
            int totalSteps = topLevelPlan.Steps.Count + subPlans.Values.Sum(p => p.Steps.Count);
            OrchestrationTracing.CompletePlanCreation(activity, totalSteps, subPlans.Count > 0 ? 2 : 1, stopwatch.Elapsed);

            return Result<HierarchicalPlan, string>.Success(hierarchicalPlan);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
            return Result<HierarchicalPlan, string>.Failure($"Hierarchical planning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a hierarchical plan recursively.
    /// </summary>
    public async Task<Result<ExecutionResult, string>> ExecuteHierarchicalAsync(
        HierarchicalPlan plan,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        int totalSteps = plan.TopLevelPlan.Steps.Count + plan.SubPlans.Values.Sum(p => p.Steps.Count);
        using var activity = OrchestrationTracing.StartPlanExecution(Guid.NewGuid(), totalSteps);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            ct.ThrowIfCancellationRequested();

            // Execute top-level plan, replacing complex steps with sub-plan execution
            Plan expandedPlan = await ExpandPlanAsync(plan, ct);

            Result<ExecutionResult, string> executionResult = await _orchestrator.ExecuteAsync(expandedPlan, ct);

            stopwatch.Stop();
            executionResult.Match(
                result =>
                {
                    int completed = result.StepResults.Count(r => r.Success);
                    int failed = result.StepResults.Count(r => !r.Success);
                    OrchestrationTracing.CompletePlanExecution(activity, completed, failed, stopwatch.Elapsed, result.Success);
                },
                _ =>
                {
                    OrchestrationTracing.CompletePlanExecution(activity, 0, 0, stopwatch.Elapsed, success: false);
                });

            return executionResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanExecution(activity, 0, 0, stopwatch.Elapsed, success: false);
            OrchestrationTracing.RecordError(activity, "execute_plan", ex);
            return Result<ExecutionResult, string>.Failure($"Hierarchical execution failed: {ex.Message}");
        }
    }

    private async Task DecomposeStepsAsync(
        List<PlanStep> steps,
        Dictionary<string, Plan> subPlans,
        Dictionary<string, object>? context,
        HierarchicalPlanningConfig config,
        int currentDepth,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (currentDepth >= config.MaxDepth)
            return;

        foreach (PlanStep step in steps)
        {
            ct.ThrowIfCancellationRequested();

            // Check if step is complex enough to decompose
            if (IsComplexStep(step, config))
            {
                string subGoal = $"Execute: {step.Action} with {System.Text.Json.JsonSerializer.Serialize(step.Parameters)}";

                Result<Plan, string> subPlanResult = await _orchestrator.PlanAsync(subGoal, context ?? new Dictionary<string, object>(), ct);

                if (subPlanResult.IsSuccess)
                {
                    Plan subPlan = subPlanResult.Value;
                    subPlans[step.Action] = subPlan;

                    // Recursively decompose sub-plan steps
                    if (subPlan.Steps.Count >= config.MinStepsForDecomposition)
                    {
                        await DecomposeStepsAsync(
                            subPlan.Steps,
                            subPlans,
                            context,
                            config,
                            currentDepth + 1,
                            ct);
                    }
                }
            }
        }
    }

    private bool IsComplexStep(PlanStep step, HierarchicalPlanningConfig config)
    {
        // Step is complex if it has low confidence or many parameters
        return step.ConfidenceScore < config.ComplexityThreshold ||
               step.Parameters.Count > 3;
    }

    private Task<Plan> ExpandPlanAsync(HierarchicalPlan hierarchicalPlan, CancellationToken ct)
    {
        List<PlanStep> expandedSteps = new List<PlanStep>();

        foreach (PlanStep step in hierarchicalPlan.TopLevelPlan.Steps)
        {
            if (hierarchicalPlan.SubPlans.TryGetValue(step.Action, out Plan? subPlan))
            {
                // Replace step with sub-plan steps
                expandedSteps.AddRange(subPlan.Steps);
            }
            else
            {
                // Keep original step
                expandedSteps.Add(step);
            }
        }

        Plan result = new Plan(
            hierarchicalPlan.Goal,
            expandedSteps,
            hierarchicalPlan.TopLevelPlan.ConfidenceScores,
            DateTime.UtcNow);

        return Task.FromResult(result);
    }

    // ==========================================================
    // HTN Planning Implementation
    // ==========================================================

    /// <summary>
    /// Creates an HTN hierarchical plan by decomposing abstract tasks using task networks.
    /// </summary>
    public async Task<Result<HtnHierarchicalPlan, string>> PlanHierarchicalAsync(
        string goal,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<HtnHierarchicalPlan, string>.Failure("Goal cannot be empty");
        }

        if (taskNetwork == null || taskNetwork.Count == 0)
        {
            return Result<HtnHierarchicalPlan, string>.Failure("Task network cannot be empty");
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            // Build abstract task hierarchy
            var abstractTasks = new List<AbstractTask>();
            var visitedTasks = new HashSet<string>();
            var taskQueue = new Queue<string>();
            
            // Start with the goal as the root abstract task
            taskQueue.Enqueue(goal);
            
            while (taskQueue.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                
                string currentTask = taskQueue.Dequeue();
                if (visitedTasks.Contains(currentTask))
                    continue;
                    
                visitedTasks.Add(currentTask);

                // Find all decompositions for this task
                var decompositions = taskNetwork
                    .Where(kvp => kvp.Value.AbstractTask == currentTask)
                    .Select(kvp => kvp.Value)
                    .ToList();

                if (decompositions.Count > 0)
                {
                    var abstractTask = new AbstractTask(
                        currentTask,
                        Preconditions: new List<string>(),
                        PossibleDecompositions: decompositions);
                    
                    abstractTasks.Add(abstractTask);

                    // Add subtasks to the queue for further decomposition
                    foreach (var decomposition in decompositions)
                    {
                        foreach (var subTask in decomposition.SubTasks)
                        {
                            if (!visitedTasks.Contains(subTask))
                            {
                                taskQueue.Enqueue(subTask);
                            }
                        }
                    }
                }
            }

            // Generate concrete refinements by selecting decompositions
            var refinements = await GenerateRefinementsAsync(goal, abstractTasks, taskNetwork, ct);

            var htnPlan = new HtnHierarchicalPlan(
                goal,
                abstractTasks,
                refinements);

            return Result<HtnHierarchicalPlan, string>.Success(htnPlan);
        }
        catch (OperationCanceledException)
        {
            return Result<HtnHierarchicalPlan, string>.Failure("HTN planning was cancelled");
        }
        catch (Exception ex)
        {
            return Result<HtnHierarchicalPlan, string>.Failure($"HTN planning failed: {ex.Message}");
        }
    }

    private async Task<List<ConcretePlan>> GenerateRefinementsAsync(
        string goal,
        List<AbstractTask> abstractTasks,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct)
    {
        var refinements = new List<ConcretePlan>();

        foreach (var abstractTask in abstractTasks)
        {
            ct.ThrowIfCancellationRequested();

            // Select the first decomposition (could be made more sophisticated)
            if (abstractTask.PossibleDecompositions.Count > 0)
            {
                var selectedDecomposition = abstractTask.PossibleDecompositions[0];
                var concreteSteps = await ExpandDecompositionAsync(
                    selectedDecomposition,
                    abstractTasks,
                    taskNetwork,
                    ct);

                var concretePlan = new ConcretePlan(
                    abstractTask.Name,
                    concreteSteps);

                refinements.Add(concretePlan);
            }
        }

        return refinements;
    }

    private Task<List<string>> ExpandDecompositionAsync(
        TaskDecomposition decomposition,
        List<AbstractTask> abstractTasks,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct)
    {
        var expandedSteps = new List<string>();

        foreach (var subTask in decomposition.SubTasks)
        {
            ct.ThrowIfCancellationRequested();

            // Check if subtask is abstract or primitive
            var isAbstract = abstractTasks.Any(at => at.Name == subTask);
            
            if (isAbstract)
            {
                // For abstract tasks, add a reference (could be expanded further)
                expandedSteps.Add($"[Abstract: {subTask}]");
            }
            else
            {
                // Primitive task - add directly
                expandedSteps.Add(subTask);
            }
        }

        return Task.FromResult(expandedSteps);
    }

    // ==========================================================
    // Temporal Planning Implementation
    // ==========================================================

    /// <summary>
    /// Creates a temporal plan that satisfies the given temporal constraints.
    /// </summary>
    public async Task<Result<TemporalPlan, string>> PlanWithConstraintsAsync(
        string goal,
        List<TemporalConstraint> constraints,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<TemporalPlan, string>.Failure("Goal cannot be empty");
        }

        if (constraints == null)
        {
            constraints = new List<TemporalConstraint>();
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            // First, create a basic plan using the orchestrator
            var planResult = await _orchestrator.PlanAsync(goal, context: null, ct);
            
            if (!planResult.IsSuccess)
            {
                return Result<TemporalPlan, string>.Failure(planResult.Error);
            }

            var plan = planResult.Value;

            // Build dependency graph from constraints
            var taskDependencies = BuildDependencyGraph(plan.Steps, constraints);

            // Schedule tasks using topological sort and constraint satisfaction
            var scheduledTasks = await ScheduleTasksAsync(plan.Steps, constraints, taskDependencies, ct);

            if (scheduledTasks.Count == 0)
            {
                return Result<TemporalPlan, string>.Failure("Failed to create valid schedule - constraints may be unsatisfiable");
            }

            // Calculate total duration
            var totalDuration = scheduledTasks.Max(t => t.EndTime) - scheduledTasks.Min(t => t.StartTime);

            var temporalPlan = new TemporalPlan(
                goal,
                scheduledTasks,
                totalDuration);

            return Result<TemporalPlan, string>.Success(temporalPlan);
        }
        catch (OperationCanceledException)
        {
            return Result<TemporalPlan, string>.Failure("Temporal planning was cancelled");
        }
        catch (Exception ex)
        {
            return Result<TemporalPlan, string>.Failure($"Temporal planning failed: {ex.Message}");
        }
    }

    private Dictionary<string, List<string>> BuildDependencyGraph(
        List<PlanStep> steps,
        List<TemporalConstraint> constraints)
    {
        var dependencies = new Dictionary<string, List<string>>();

        // Initialize empty dependency lists
        foreach (var step in steps)
        {
            dependencies[step.Action] = new List<string>();
        }

        // Build dependencies from constraints
        foreach (var constraint in constraints)
        {
            switch (constraint.Relation)
            {
                case TemporalRelation.Before:
                case TemporalRelation.MustFinishBefore:
                    // TaskB depends on TaskA
                    if (dependencies.ContainsKey(constraint.TaskB))
                    {
                        dependencies[constraint.TaskB].Add(constraint.TaskA);
                    }
                    break;

                case TemporalRelation.After:
                    // TaskA depends on TaskB
                    if (dependencies.ContainsKey(constraint.TaskA))
                    {
                        dependencies[constraint.TaskA].Add(constraint.TaskB);
                    }
                    break;

                // Other relations don't create hard dependencies
                case TemporalRelation.During:
                case TemporalRelation.Overlaps:
                case TemporalRelation.Simultaneous:
                    // These require special handling during scheduling
                    break;
            }
        }

        return dependencies;
    }

    private async Task<List<ScheduledTask>> ScheduleTasksAsync(
        List<PlanStep> steps,
        List<TemporalConstraint> constraints,
        Dictionary<string, List<string>> dependencies,
        CancellationToken ct)
    {
        var scheduledTasks = new List<ScheduledTask>();
        var completionTimes = new Dictionary<string, DateTime>();
        var currentTime = DateTime.UtcNow;
        var defaultDuration = TimeSpan.FromMinutes(5); // Default task duration

        // Topological sort to determine execution order
        var sorted = TopologicalSort(steps.Select(s => s.Action).ToList(), dependencies);

        foreach (var taskName in sorted)
        {
            ct.ThrowIfCancellationRequested();

            var step = steps.FirstOrDefault(s => s.Action == taskName);
            if (step == null)
                continue;

            // Find the earliest start time based on dependencies
            var startTime = currentTime;
            var taskDeps = dependencies.ContainsKey(taskName) ? dependencies[taskName] : new List<string>();

            if (taskDeps.Count > 0)
            {
                var maxDepCompletionTime = taskDeps
                    .Where(dep => completionTimes.ContainsKey(dep))
                    .Select(dep => completionTimes[dep])
                    .DefaultIfEmpty(currentTime)
                    .Max();
                
                startTime = maxDepCompletionTime;
            }

            // Determine duration from constraints or use default
            var duration = constraints
                .FirstOrDefault(c => c.TaskA == taskName && c.Duration.HasValue)
                ?.Duration ?? defaultDuration;

            var endTime = startTime + duration;

            var scheduledTask = new ScheduledTask(
                taskName,
                startTime,
                endTime,
                taskDeps);

            scheduledTasks.Add(scheduledTask);
            completionTimes[taskName] = endTime;
        }

        return scheduledTasks;
    }

    private List<string> TopologicalSort(List<string> tasks, Dictionary<string, List<string>> dependencies)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(string task)
        {
            if (visited.Contains(task))
                return;

            if (visiting.Contains(task))
            {
                // Cycle detected - break it by continuing
                return;
            }

            visiting.Add(task);

            if (dependencies.ContainsKey(task))
            {
                foreach (var dep in dependencies[task])
                {
                    Visit(dep);
                }
            }

            visiting.Remove(task);
            visited.Add(task);
            result.Add(task);
        }

        foreach (var task in tasks)
        {
            Visit(task);
        }

        return result;
    }

    // ==========================================================
    // Plan Repair Implementation
    // ==========================================================

    /// <summary>
    /// Repairs a broken plan using the specified repair strategy.
    /// </summary>
    public async Task<Result<Plan, string>> RepairPlanAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        RepairStrategy strategy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(brokenPlan);
        ArgumentNullException.ThrowIfNull(trace);

        try
        {
            ct.ThrowIfCancellationRequested();

            return strategy switch
            {
                RepairStrategy.Replan => await ReplanStrategyAsync(brokenPlan, trace, ct),
                RepairStrategy.Patch => await PatchStrategyAsync(brokenPlan, trace, ct),
                RepairStrategy.CaseBased => await CaseBasedStrategyAsync(brokenPlan, trace, ct),
                RepairStrategy.Backtrack => await BacktrackStrategyAsync(brokenPlan, trace, ct),
                _ => Result<Plan, string>.Failure($"Unknown repair strategy: {strategy}")
            };
        }
        catch (OperationCanceledException)
        {
            return Result<Plan, string>.Failure("Plan repair was cancelled");
        }
        catch (Exception ex)
        {
            return Result<Plan, string>.Failure($"Plan repair failed: {ex.Message}");
        }
    }

    private async Task<Result<Plan, string>> ReplanStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        // Full replanning from current state
        // Build context from successful steps
        var context = new Dictionary<string, object>
        {
            ["executed_steps"] = trace.Steps.Where(s => s.Success).Select(s => s.StepName).ToList(),
            ["failure_reason"] = trace.FailureReason,
            ["failed_step"] = trace.FailedAtIndex < trace.Steps.Count ? trace.Steps[trace.FailedAtIndex].StepName : "unknown"
        };

        // Create a new plan from scratch considering current state
        var replanResult = await _orchestrator.PlanAsync(brokenPlan.Goal, context, ct);
        
        if (!replanResult.IsSuccess)
        {
            return Result<Plan, string>.Failure($"Replanning failed: {replanResult.Error}");
        }

        return Result<Plan, string>.Success(replanResult.Value);
    }

    private async Task<Result<Plan, string>> PatchStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        // Minimal local fixes - replace only the failed step and those that depend on it
        var newSteps = new List<PlanStep>();

        // Keep all steps before the failure
        for (int i = 0; i < trace.FailedAtIndex && i < brokenPlan.Steps.Count; i++)
        {
            newSteps.Add(brokenPlan.Steps[i]);
        }

        // Generate alternative step for the failed action
        if (trace.FailedAtIndex < brokenPlan.Steps.Count)
        {
            var failedStep = brokenPlan.Steps[trace.FailedAtIndex];
            
            // Create a modified step with adjusted parameters
            var patchedStep = new PlanStep(
                failedStep.Action + "_alt",
                new Dictionary<string, object>(failedStep.Parameters) { ["retry"] = true },
                failedStep.ExpectedOutcome,
                failedStep.ConfidenceScore * 0.8); // Lower confidence for patched step

            newSteps.Add(patchedStep);
        }

        // Keep remaining steps
        for (int i = trace.FailedAtIndex + 1; i < brokenPlan.Steps.Count; i++)
        {
            newSteps.Add(brokenPlan.Steps[i]);
        }

        var patchedPlan = new Plan(
            brokenPlan.Goal,
            newSteps,
            brokenPlan.ConfidenceScores,
            DateTime.UtcNow);

        return Result<Plan, string>.Success(patchedPlan);
    }

    private async Task<Result<Plan, string>> CaseBasedStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        // Use similar past repairs as templates
        // For now, this is a simplified implementation that combines replan and patch
        
        // Try to find a similar pattern from the execution trace
        var hasRecentFailure = trace.Steps.Count(s => !s.Success) > 1;

        if (hasRecentFailure)
        {
            // Multiple failures suggest structural issue - replan
            return await ReplanStrategyAsync(brokenPlan, trace, ct);
        }
        else
        {
            // Single failure - patch might work
            return await PatchStrategyAsync(brokenPlan, trace, ct);
        }
    }

    private async Task<Result<Plan, string>> BacktrackStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        // Undo and retry with alternatives
        var newSteps = new List<PlanStep>();

        // Find the last successful checkpoint (e.g., last 3 successful steps)
        var checkpointIndex = Math.Max(0, trace.FailedAtIndex - 3);
        
        // Keep steps up to checkpoint
        for (int i = 0; i < checkpointIndex && i < brokenPlan.Steps.Count; i++)
        {
            newSteps.Add(brokenPlan.Steps[i]);
        }

        // Generate alternative path from checkpoint
        var remainingGoal = $"Complete: {brokenPlan.Goal} (from step {checkpointIndex})";
        var context = new Dictionary<string, object>
        {
            ["checkpoint_index"] = checkpointIndex,
            ["previous_failure"] = trace.FailureReason
        };

        var alternativeResult = await _orchestrator.PlanAsync(remainingGoal, context, ct);
        
        if (alternativeResult.IsSuccess)
        {
            newSteps.AddRange(alternativeResult.Value.Steps);
        }
        else
        {
            // Fallback to original steps if alternative planning fails
            for (int i = checkpointIndex; i < brokenPlan.Steps.Count; i++)
            {
                newSteps.Add(brokenPlan.Steps[i]);
            }
        }

        var repairedPlan = new Plan(
            brokenPlan.Goal,
            newSteps,
            brokenPlan.ConfidenceScores,
            DateTime.UtcNow);

        return Result<Plan, string>.Success(repairedPlan);
    }

    // ==========================================================
    // Plan Explanation Implementation
    // ==========================================================

    /// <summary>
    /// Generates an explanation of a plan at the specified level of detail.
    /// </summary>
    public async Task<Result<string, string>> ExplainPlanAsync(
        Plan plan,
        ExplanationLevel level,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        try
        {
            ct.ThrowIfCancellationRequested();

            var explanation = level switch
            {
                ExplanationLevel.Brief => await GenerateBriefExplanationAsync(plan, ct),
                ExplanationLevel.Detailed => await GenerateDetailedExplanationAsync(plan, ct),
                ExplanationLevel.Causal => await GenerateCausalExplanationAsync(plan, ct),
                ExplanationLevel.Counterfactual => await GenerateCounterfactualExplanationAsync(plan, ct),
                _ => $"Unknown explanation level: {level}"
            };

            return Result<string, string>.Success(explanation);
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Plan explanation was cancelled");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Plan explanation failed: {ex.Message}");
        }
    }

    private Task<string> GenerateBriefExplanationAsync(Plan plan, CancellationToken ct)
    {
        var summary = $"Plan to achieve '{plan.Goal}' in {plan.Steps.Count} steps";
        return Task.FromResult(summary);
    }

    private Task<string> GenerateDetailedExplanationAsync(Plan plan, CancellationToken ct)
    {
        var explanation = new System.Text.StringBuilder();
        explanation.AppendLine($"Goal: {plan.Goal}");
        explanation.AppendLine($"Total Steps: {plan.Steps.Count}");
        explanation.AppendLine();
        explanation.AppendLine("Step-by-Step Plan:");

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            explanation.AppendLine($"{i + 1}. {step.Action}");
            explanation.AppendLine($"   Parameters: {string.Join(", ", step.Parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            explanation.AppendLine($"   Expected Outcome: {step.ExpectedOutcome}");
            explanation.AppendLine($"   Confidence: {step.ConfidenceScore:P0}");
            explanation.AppendLine();
        }

        return Task.FromResult(explanation.ToString());
    }

    private Task<string> GenerateCausalExplanationAsync(Plan plan, CancellationToken ct)
    {
        var explanation = new System.Text.StringBuilder();
        explanation.AppendLine($"Causal Explanation for: {plan.Goal}");
        explanation.AppendLine();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            explanation.AppendLine($"Step {i + 1}: {step.Action}");
            
            // Explain why this step is necessary
            if (i == 0)
            {
                explanation.AppendLine($"   Why: This is the initial step required to begin working toward '{plan.Goal}'");
            }
            else
            {
                var previousStep = plan.Steps[i - 1];
                explanation.AppendLine($"   Why: This step builds on '{previousStep.Action}' to progress toward the goal");
            }

            explanation.AppendLine($"   Contribution: {step.ExpectedOutcome}");
            explanation.AppendLine();
        }

        return Task.FromResult(explanation.ToString());
    }

    private Task<string> GenerateCounterfactualExplanationAsync(Plan plan, CancellationToken ct)
    {
        var explanation = new System.Text.StringBuilder();
        explanation.AppendLine($"Counterfactual Analysis for: {plan.Goal}");
        explanation.AppendLine();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            explanation.AppendLine($"Step {i + 1}: {step.Action}");
            
            // Explain what would happen without this step
            if (i == plan.Steps.Count - 1)
            {
                explanation.AppendLine($"   Without this step: The goal '{plan.Goal}' would not be achieved");
            }
            else
            {
                var nextStep = plan.Steps[i + 1];
                explanation.AppendLine($"   Without this step: Cannot proceed to '{nextStep.Action}' as preconditions wouldn't be met");
            }

            explanation.AppendLine($"   Critical because: {step.ExpectedOutcome}");
            explanation.AppendLine();
        }

        return Task.FromResult(explanation.ToString());
    }
}
