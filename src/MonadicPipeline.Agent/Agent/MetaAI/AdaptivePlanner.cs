#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Adaptive Planner - Real-time plan adaptation during execution
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents an adaptation trigger condition.
/// </summary>
public sealed record AdaptationTrigger(
    string Name,
    Func<ExecutionContext, bool> Condition,
    AdaptationStrategy Strategy);

/// <summary>
/// Adaptation strategy enumeration.
/// </summary>
public enum AdaptationStrategy
{
    Retry,
    ReplaceStep,
    AddStep,
    Replan,
    Abort
}

/// <summary>
/// Represents execution context for adaptation decisions.
/// </summary>
public sealed record ExecutionContext(
    Plan OriginalPlan,
    List<StepResult> CompletedSteps,
    PlanStep CurrentStep,
    int CurrentStepIndex,
    Dictionary<string, object> Metadata);

/// <summary>
/// Represents an adaptation action.
/// </summary>
public sealed record AdaptationAction(
    AdaptationStrategy Strategy,
    string Reason,
    Plan? RevisedPlan = null,
    PlanStep? ReplacementStep = null);

/// <summary>
/// Configuration for adaptive planning.
/// </summary>
public sealed record AdaptivePlanningConfig(
    int MaxRetries = 3,
    bool EnableAutoReplan = true,
    double FailureThreshold = 0.5);

/// <summary>
/// Interface for adaptive planning capabilities.
/// </summary>
public interface IAdaptivePlanner
{
    /// <summary>
    /// Executes a plan with real-time adaptation.
    /// </summary>
    Task<Result<ExecutionResult, string>> ExecuteWithAdaptationAsync(
        Plan plan,
        AdaptivePlanningConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Registers a custom adaptation trigger.
    /// </summary>
    void RegisterTrigger(AdaptationTrigger trigger);

    /// <summary>
    /// Evaluates if adaptation is needed.
    /// </summary>
    Task<AdaptationAction?> EvaluateAdaptationAsync(
        ExecutionContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of adaptive planner for real-time plan adjustment.
/// </summary>
public sealed class AdaptivePlanner : IAdaptivePlanner
{
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private readonly IChatCompletionModel _llm;
    private readonly List<AdaptationTrigger> _triggers = new();

    public AdaptivePlanner(
        IMetaAIPlannerOrchestrator orchestrator,
        IChatCompletionModel llm)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));

        // Register default triggers
        RegisterDefaultTriggers();
    }

    /// <summary>
    /// Executes a plan with real-time adaptation.
    /// </summary>
    public async Task<Result<ExecutionResult, string>> ExecuteWithAdaptationAsync(
        Plan plan,
        AdaptivePlanningConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new AdaptivePlanningConfig();

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        Plan currentPlan = plan;
        List<StepResult> allStepResults = new List<StepResult>();
        List<string> adaptationHistory = new List<string>();

        try
        {
            for (int i = 0; i < currentPlan.Steps.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    break;

                PlanStep step = currentPlan.Steps[i];

                // Create execution context
                ExecutionContext context = new ExecutionContext(
                    plan,
                    allStepResults,
                    step,
                    i,
                    new Dictionary<string, object>
                    {
                        ["total_adaptations"] = adaptationHistory.Count
                    });

                // Check if adaptation is needed before execution
                AdaptationAction? adaptationAction = await EvaluateAdaptationAsync(context, ct);

                if (adaptationAction != null)
                {
                    adaptationHistory.Add($"Step {i}: {adaptationAction.Strategy} - {adaptationAction.Reason}");

                    switch (adaptationAction.Strategy)
                    {
                        case AdaptationStrategy.Replan:
                            if (config.EnableAutoReplan && adaptationAction.RevisedPlan != null)
                            {
                                currentPlan = adaptationAction.RevisedPlan;
                                i = -1; // Restart from beginning
                                continue;
                            }
                            break;

                        case AdaptationStrategy.ReplaceStep:
                            if (adaptationAction.ReplacementStep != null)
                            {
                                step = adaptationAction.ReplacementStep;
                                currentPlan.Steps[i] = step;
                            }
                            break;

                        case AdaptationStrategy.Abort:
                            sw.Stop();
                            return Result<ExecutionResult, string>.Failure($"Execution aborted: {adaptationAction.Reason}");

                        case AdaptationStrategy.AddStep:
                            // Add additional step after current
                            if (adaptationAction.ReplacementStep != null)
                            {
                                currentPlan.Steps.Insert(i + 1, adaptationAction.ReplacementStep);
                            }
                            break;
                    }
                }

                // Execute step with retry logic
                StepResult stepResult = await ExecuteStepWithRetryAsync(step, config.MaxRetries, ct);
                allStepResults.Add(stepResult);

                // Check if we need to adapt after execution
                ExecutionContext postContext = context with { CompletedSteps = allStepResults };
                AdaptationAction? postAdaptation = await EvaluateAdaptationAsync(postContext, ct);

                if (postAdaptation?.Strategy == AdaptationStrategy.Replan && config.EnableAutoReplan)
                {
                    // Generate new plan for remaining steps
                    string remainingGoal = $"Continue from step {i + 1}: {plan.Goal}";
                    Result<Plan, string> replanResult = await _orchestrator.PlanAsync(remainingGoal, null, ct);

                    if (replanResult.IsSuccess)
                    {
                        adaptationHistory.Add($"Replanned after step {i}");
                        currentPlan = replanResult.Value;
                        i = -1; // Restart
                        continue;
                    }
                }
            }

            sw.Stop();

            bool overallSuccess = allStepResults.All(r => r.Success);
            string finalOutput = string.Join("\n", allStepResults.Select(r => r.Output));

            ExecutionResult execution = new ExecutionResult(
                currentPlan,
                allStepResults,
                overallSuccess,
                finalOutput,
                new Dictionary<string, object>
                {
                    ["adaptations"] = adaptationHistory,
                    ["adaptive_execution"] = true
                },
                sw.Elapsed);

            return Result<ExecutionResult, string>.Success(execution);
        }
        catch (Exception ex)
        {
            return Result<ExecutionResult, string>.Failure($"Adaptive execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers a custom adaptation trigger.
    /// </summary>
    public void RegisterTrigger(AdaptationTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        _triggers.Add(trigger);
    }

    /// <summary>
    /// Evaluates if adaptation is needed.
    /// </summary>
    public async Task<AdaptationAction?> EvaluateAdaptationAsync(
        ExecutionContext context,
        CancellationToken ct = default)
    {
        // Check all registered triggers
        foreach (AdaptationTrigger trigger in _triggers)
        {
            if (trigger.Condition(context))
            {
                // Trigger matched - determine adaptation action
                AdaptationAction? action = await CreateAdaptationActionAsync(trigger, context, ct);
                if (action != null)
                {
                    return action;
                }
            }
        }

        return null;
    }

    private async Task<StepResult> ExecuteStepWithRetryAsync(
        PlanStep step,
        int maxRetries,
        CancellationToken ct)
    {
        int attempts = 0;
        StepResult? lastResult = null;

        while (attempts < maxRetries)
        {
            attempts++;

            // Execute step (simplified - in real implementation would use orchestrator)
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Simulate execution
                await Task.Delay(50, ct);

                sw.Stop();

                lastResult = new StepResult(
                    step,
                    true,
                    $"Executed (attempt {attempts})",
                    null,
                    sw.Elapsed,
                    new Dictionary<string, object> { ["attempts"] = attempts });

                if (lastResult.Success)
                    break;
            }
            catch (Exception ex)
            {
                sw.Stop();
                lastResult = new StepResult(
                    step,
                    false,
                    "",
                    ex.Message,
                    sw.Elapsed,
                    new Dictionary<string, object> { ["attempts"] = attempts });
            }
        }

        return lastResult ?? new StepResult(
            step,
            false,
            "",
            "Max retries exceeded",
            TimeSpan.Zero,
            new Dictionary<string, object> { ["attempts"] = attempts });
    }

    private async Task<AdaptationAction?> CreateAdaptationActionAsync(
        AdaptationTrigger trigger,
        ExecutionContext context,
        CancellationToken ct)
    {
        AdaptationStrategy strategy = trigger.Strategy;
        string reason = $"Trigger '{trigger.Name}' activated";

        switch (strategy)
        {
            case AdaptationStrategy.Replan:
                // Generate revised plan
                Result<Plan, string> replanResult = await _orchestrator.PlanAsync(
                    context.OriginalPlan.Goal,
                    new Dictionary<string, object>
                    {
                        ["completed_steps"] = context.CompletedSteps.Count,
                        ["context"] = "replanning_due_to_issues"
                    },
                    ct);

                if (replanResult.IsSuccess)
                {
                    return new AdaptationAction(strategy, reason, replanResult.Value);
                }
                break;

            case AdaptationStrategy.ReplaceStep:
                // Generate replacement step using LLM
                string prompt = $@"The following step failed: {context.CurrentStep.Action}
Parameters: {System.Text.Json.JsonSerializer.Serialize(context.CurrentStep.Parameters)}
Expected: {context.CurrentStep.ExpectedOutcome}

Suggest an alternative approach to achieve the same outcome.";

                string suggestion = await _llm.GenerateTextAsync(prompt, ct);

                // Parse suggestion into a new step (simplified)
                PlanStep replacementStep = new PlanStep(
                    "alternative_approach",
                    new Dictionary<string, object> { ["suggestion"] = suggestion },
                    context.CurrentStep.ExpectedOutcome,
                    0.6);

                return new AdaptationAction(strategy, reason, ReplacementStep: replacementStep);
        }

        return new AdaptationAction(strategy, reason);
    }

    private void RegisterDefaultTriggers()
    {
        // Trigger: Multiple consecutive failures
        _triggers.Add(new AdaptationTrigger(
            "consecutive_failures",
            ctx => ctx.CompletedSteps.TakeLast(3).Count(s => !s.Success) >= 2,
            AdaptationStrategy.Replan));

        // Trigger: Low confidence step about to execute
        _triggers.Add(new AdaptationTrigger(
            "low_confidence",
            ctx => ctx.CurrentStep.ConfidenceScore < 0.3,
            AdaptationStrategy.ReplaceStep));

        // Trigger: High failure rate
        _triggers.Add(new AdaptationTrigger(
            "high_failure_rate",
            ctx =>
            {
                if (ctx.CompletedSteps.Count < 3) return false;
                double failureRate = ctx.CompletedSteps.Count(s => !s.Success) / (double)ctx.CompletedSteps.Count;
                return failureRate > 0.5;
            },
            AdaptationStrategy.Replan));
    }
}
