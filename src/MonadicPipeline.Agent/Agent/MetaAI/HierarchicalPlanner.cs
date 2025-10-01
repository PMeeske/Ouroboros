// ==========================================================
// Hierarchical Planner - Multi-level plan decomposition
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

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

        try
        {
            // Create top-level plan
            var topLevelResult = await _orchestrator.PlanAsync(goal, context, ct);
            
            if (!topLevelResult.IsSuccess)
            {
                return Result<HierarchicalPlan, string>.Failure(topLevelResult.Error);
            }

            var topLevelPlan = topLevelResult.Value;
            var subPlans = new Dictionary<string, Plan>();

            // Decompose complex steps if needed
            if (topLevelPlan.Steps.Count >= config.MinStepsForDecomposition)
            {
                await DecomposeStepsAsync(
                    topLevelPlan.Steps,
                    subPlans,
                    context,
                    config,
                    currentDepth: 1,
                    ct);
            }

            var hierarchicalPlan = new HierarchicalPlan(
                goal,
                topLevelPlan,
                subPlans,
                MaxDepth: config.MaxDepth,
                CreatedAt: DateTime.UtcNow);

            return Result<HierarchicalPlan, string>.Success(hierarchicalPlan);
        }
        catch (Exception ex)
        {
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
        try
        {
            // Execute top-level plan, replacing complex steps with sub-plan execution
            var expandedPlan = await ExpandPlanAsync(plan, ct);
            
            var executionResult = await _orchestrator.ExecuteAsync(expandedPlan, ct);
            
            return executionResult;
        }
        catch (Exception ex)
        {
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
        if (currentDepth >= config.MaxDepth)
            return;

        foreach (var step in steps)
        {
            // Check if step is complex enough to decompose
            if (IsComplexStep(step, config))
            {
                var subGoal = $"Execute: {step.Action} with {System.Text.Json.JsonSerializer.Serialize(step.Parameters)}";
                
                var subPlanResult = await _orchestrator.PlanAsync(subGoal, context, ct);
                
                if (subPlanResult.IsSuccess)
                {
                    var subPlan = subPlanResult.Value;
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
        var expandedSteps = new List<PlanStep>();

        foreach (var step in hierarchicalPlan.TopLevelPlan.Steps)
        {
            if (hierarchicalPlan.SubPlans.TryGetValue(step.Action, out var subPlan))
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

        var result = new Plan(
            hierarchicalPlan.Goal,
            expandedSteps,
            hierarchicalPlan.TopLevelPlan.ConfidenceScores,
            DateTime.UtcNow);
        
        return Task.FromResult(result);
    }
}
