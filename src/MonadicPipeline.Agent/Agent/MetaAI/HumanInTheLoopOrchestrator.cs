#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Human-in-the-Loop - Interactive refinement and approval
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a human feedback request.
/// </summary>
public sealed record HumanFeedbackRequest(
    string RequestId,
    string Context,
    string Question,
    List<string>? Options,
    DateTime RequestedAt,
    TimeSpan Timeout);

/// <summary>
/// Represents human feedback response.
/// </summary>
public sealed record HumanFeedbackResponse(
    string RequestId,
    string Response,
    Dictionary<string, object>? Metadata,
    DateTime RespondedAt);

/// <summary>
/// Represents an approval request.
/// </summary>
public sealed record ApprovalRequest(
    string RequestId,
    string Action,
    Dictionary<string, object> Parameters,
    string Rationale,
    DateTime RequestedAt);

/// <summary>
/// Represents an approval response.
/// </summary>
public sealed record ApprovalResponse(
    string RequestId,
    bool Approved,
    string? Reason,
    Dictionary<string, object>? Modifications,
    DateTime RespondedAt);

/// <summary>
/// Configuration for human-in-the-loop.
/// </summary>
public sealed record HumanInTheLoopConfig(
    bool RequireApprovalForCriticalSteps = true,
    bool EnableInteractiveRefinement = true,
    TimeSpan DefaultTimeout = default,
    List<string> CriticalActionPatterns = null!);

/// <summary>
/// Interface for human feedback provider.
/// </summary>
public interface IHumanFeedbackProvider
{
    /// <summary>
    /// Requests feedback from human.
    /// </summary>
    Task<HumanFeedbackResponse> RequestFeedbackAsync(
        HumanFeedbackRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Requests approval for an action.
    /// </summary>
    Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Interface for human-in-the-loop orchestration.
/// </summary>
public interface IHumanInTheLoopOrchestrator
{
    /// <summary>
    /// Executes a plan with human oversight.
    /// </summary>
    Task<Result<ExecutionResult, string>> ExecuteWithHumanOversightAsync(
        Plan plan,
        HumanInTheLoopConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Refines a plan interactively with human feedback.
    /// </summary>
    Task<Result<Plan, string>> RefinePlanInteractivelyAsync(
        Plan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Sets the human feedback provider.
    /// </summary>
    void SetFeedbackProvider(IHumanFeedbackProvider provider);
}

/// <summary>
/// Default console-based human feedback provider for testing.
/// </summary>
public sealed class ConsoleFeedbackProvider : IHumanFeedbackProvider
{
    /// <inheritdoc/>
    public async Task<HumanFeedbackResponse> RequestFeedbackAsync(
        HumanFeedbackRequest request,
        CancellationToken ct = default)
    {
        Console.WriteLine($"\n=== Human Feedback Required ===");
        Console.WriteLine($"Context: {request.Context}");
        Console.WriteLine($"Question: {request.Question}");

        if (request.Options != null && request.Options.Any())
        {
            Console.WriteLine("Options:");
            for (int i = 0; i < request.Options.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {request.Options[i]}");
            }
        }

        Console.Write("Your response: ");
        string response = await Task.Run(() => Console.ReadLine() ?? "", ct);

        return new HumanFeedbackResponse(
            request.RequestId,
            response,
            null,
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken ct = default)
    {
        Console.WriteLine($"\n=== Approval Required ===");
        Console.WriteLine($"Action: {request.Action}");
        Console.WriteLine($"Parameters: {System.Text.Json.JsonSerializer.Serialize(request.Parameters)}");
        Console.WriteLine($"Rationale: {request.Rationale}");
        Console.Write("Approve? (y/n): ");

        string response = await Task.Run(() => Console.ReadLine() ?? "n", ct);
        bool approved = response.ToLowerInvariant() == "y";

        return new ApprovalResponse(
            request.RequestId,
            approved,
            approved ? null : "User rejected",
            null,
            DateTime.UtcNow);
    }
}

/// <summary>
/// Implementation of human-in-the-loop orchestration.
/// </summary>
public sealed class HumanInTheLoopOrchestrator : IHumanInTheLoopOrchestrator
{
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private IHumanFeedbackProvider _feedbackProvider;

    public HumanInTheLoopOrchestrator(
        IMetaAIPlannerOrchestrator orchestrator,
        IHumanFeedbackProvider? feedbackProvider = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _feedbackProvider = feedbackProvider ?? new ConsoleFeedbackProvider();
    }

    /// <summary>
    /// Sets the human feedback provider.
    /// </summary>
    public void SetFeedbackProvider(IHumanFeedbackProvider provider)
    {
        _feedbackProvider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Executes a plan with human oversight.
    /// </summary>
    public async Task<Result<ExecutionResult, string>> ExecuteWithHumanOversightAsync(
        Plan plan,
        HumanInTheLoopConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new HumanInTheLoopConfig(
            DefaultTimeout: TimeSpan.FromMinutes(5),
            CriticalActionPatterns: new List<string> { "delete", "remove", "drop", "terminate" });

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        List<StepResult> stepResults = new List<StepResult>();
        List<string> approvalHistory = new List<string>();

        try
        {
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    break;

                PlanStep step = plan.Steps[i];

                // Check if step requires approval
                if (config.RequireApprovalForCriticalSteps && IsCriticalStep(step, config))
                {
                    ApprovalResponse approval = await RequestStepApprovalAsync(step, i, ct);

                    if (!approval.Approved)
                    {
                        approvalHistory.Add($"Step {i} rejected: {approval.Reason}");

                        stepResults.Add(new StepResult(
                            step,
                            false,
                            "",
                            $"Human rejected: {approval.Reason}",
                            TimeSpan.Zero,
                            new Dictionary<string, object> { ["human_rejected"] = true }));

                        continue;
                    }

                    // Apply any modifications from approval
                    if (approval.Modifications != null && approval.Modifications.Any())
                    {
                        step = ApplyModifications(step, approval.Modifications);
                        approvalHistory.Add($"Step {i} modified by user");
                    }
                }

                // Execute step
                Result<ExecutionResult, string> executionResult = await _orchestrator.ExecuteAsync(
                    new Plan(plan.Goal, new List<PlanStep> { step }, plan.ConfidenceScores, DateTime.UtcNow),
                    ct);

                if (executionResult.IsSuccess)
                {
                    stepResults.AddRange(executionResult.Value.StepResults);
                }
                else
                {
                    stepResults.Add(new StepResult(
                        step,
                        false,
                        "",
                        executionResult.Error,
                        TimeSpan.Zero,
                        new Dictionary<string, object>()));
                }
            }

            sw.Stop();

            bool overallSuccess = stepResults.All(r => r.Success);
            string finalOutput = string.Join("\n", stepResults.Select(r => r.Output));

            ExecutionResult execution = new ExecutionResult(
                plan,
                stepResults,
                overallSuccess,
                finalOutput,
                new Dictionary<string, object>
                {
                    ["human_oversight"] = true,
                    ["approvals"] = approvalHistory
                },
                sw.Elapsed);

            return Result<ExecutionResult, string>.Success(execution);
        }
        catch (Exception ex)
        {
            return Result<ExecutionResult, string>.Failure($"Human oversight execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Refines a plan interactively with human feedback.
    /// </summary>
    public async Task<Result<Plan, string>> RefinePlanInteractivelyAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        try
        {
            // Present plan to human
            HumanFeedbackRequest feedbackRequest = new HumanFeedbackRequest(
                Guid.NewGuid().ToString(),
                $"Plan for: {plan.Goal}",
                "Review the plan. Provide feedback or type 'approve' to proceed.",
                new List<string>
                {
                    "approve",
                    "add step",
                    "remove step",
                    "modify step",
                    "replan"
                },
                DateTime.UtcNow,
                TimeSpan.FromMinutes(5));

            HumanFeedbackResponse feedback = await _feedbackProvider.RequestFeedbackAsync(feedbackRequest, ct);

            string response = feedback.Response.ToLowerInvariant();

            if (response.Contains("approve"))
            {
                return Result<Plan, string>.Success(plan);
            }
            else if (response.Contains("replan"))
            {
                // Request replanning
                Result<Plan, string> replanResult = await _orchestrator.PlanAsync(plan.Goal, null, ct);
                return replanResult;
            }
            else if (response.Contains("add"))
            {
                // Request details for new step
                HumanFeedbackRequest stepRequest = new HumanFeedbackRequest(
                    Guid.NewGuid().ToString(),
                    "Add step",
                    "Describe the step to add (format: action|parameters|expected)",
                    null,
                    DateTime.UtcNow,
                    TimeSpan.FromMinutes(5));

                HumanFeedbackResponse stepFeedback = await _feedbackProvider.RequestFeedbackAsync(stepRequest, ct);
                PlanStep newStep = ParseStepFromFeedback(stepFeedback.Response);

                plan.Steps.Add(newStep);
                return Result<Plan, string>.Success(plan);
            }
            else if (response.Contains("modify"))
            {
                // Interactive modification
                // Simplified - in production would be more sophisticated
                return Result<Plan, string>.Success(plan);
            }

            return Result<Plan, string>.Success(plan);
        }
        catch (Exception ex)
        {
            return Result<Plan, string>.Failure($"Interactive refinement failed: {ex.Message}");
        }
    }

    private bool IsCriticalStep(PlanStep step, HumanInTheLoopConfig config)
    {
        string actionLower = step.Action.ToLowerInvariant();

        return config.CriticalActionPatterns.Any(pattern =>
            actionLower.Contains(pattern.ToLowerInvariant()));
    }

    private async Task<ApprovalResponse> RequestStepApprovalAsync(
        PlanStep step,
        int stepIndex,
        CancellationToken ct)
    {
        ApprovalRequest request = new ApprovalRequest(
            Guid.NewGuid().ToString(),
            step.Action,
            step.Parameters,
            $"Step {stepIndex + 1}: {step.ExpectedOutcome}",
            DateTime.UtcNow);

        return await _feedbackProvider.RequestApprovalAsync(request, ct);
    }

    private PlanStep ApplyModifications(PlanStep step, Dictionary<string, object> modifications)
    {
        Dictionary<string, object> newParams = new Dictionary<string, object>(step.Parameters);

        foreach ((string key, object value) in modifications)
        {
            newParams[key] = value;
        }

        return step with { Parameters = newParams };
    }

    private PlanStep ParseStepFromFeedback(string feedback)
    {
        // Simple parsing - in production use more sophisticated approach
        string[] parts = feedback.Split('|');

        return new PlanStep(
            parts.Length > 0 ? parts[0].Trim() : "custom_step",
            parts.Length > 1 ? new Dictionary<string, object> { ["input"] = parts[1].Trim() } : new(),
            parts.Length > 2 ? parts[2].Trim() : "User-defined outcome",
            0.8);
    }
}
