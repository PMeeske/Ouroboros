// ==========================================================
// Meta-AI Layer v3.0 - MeTTa-First Orchestrator
// Integrates symbolic reasoning with neural planning
// ==========================================================

using LangChainPipeline.Tools.MeTTa;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Meta-AI v3.0 orchestrator with MeTTa-first representation layer.
/// Mirrors all orchestration concepts as MeTTa atoms and uses symbolic reasoning for next-node selection.
/// </summary>
public sealed class MeTTaOrchestrator : IMetaAIPlannerOrchestrator
{
    private readonly IChatCompletionModel _llm;
    private readonly ToolRegistry _tools;
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly IUncertaintyRouter _router;
    private readonly ISafetyGuard _safety;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly MeTTaRepresentation _representation;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();

    public MeTTaOrchestrator(
        IChatCompletionModel llm,
        ToolRegistry tools,
        IMemoryStore memory,
        ISkillRegistry skills,
        IUncertaintyRouter router,
        ISafetyGuard safety,
        IMeTTaEngine mettaEngine)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _representation = new MeTTaRepresentation(mettaEngine);
    }

    /// <summary>
    /// Plans with MeTTa symbolic representation.
    /// </summary>
    public async Task<Result<Plan, string>> PlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return Result<Plan, string>.Failure("Goal cannot be empty");

        try
        {
            var sw = Stopwatch.StartNew();

            // Get past experiences and skills
            var query = new MemoryQuery(goal, context, MaxResults: 5, MinSimilarity: 0.7);
            var pastExperiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);
            var matchingSkills = await _skills.FindMatchingSkillsAsync(goal, context);

            // Generate initial plan using LLM
            var planPrompt = BuildPlanPrompt(goal, context, pastExperiences, matchingSkills);
            var planText = await _llm.GenerateTextAsync(planPrompt, ct);
            var plan = ParsePlan(planText, goal);

            // Translate plan to MeTTa representation
            var translationResult = await _representation.TranslatePlanAsync(plan, ct);
            if (translationResult.IsFailure)
            {
                Console.WriteLine($"Warning: Failed to translate plan to MeTTa: {translationResult.Error}");
            }

            // Translate tools to MeTTa
            var toolTranslation = await _representation.TranslateToolsAsync(_tools, ct);
            if (toolTranslation.IsFailure)
            {
                Console.WriteLine($"Warning: Failed to translate tools to MeTTa: {toolTranslation.Error}");
            }

            // Validate plan safety
            foreach (var step in plan.Steps)
            {
                var safetyCheck = _safety.CheckSafety(
                    step.Action,
                    step.Parameters,
                    PermissionLevel.UserDataWithConfirmation);

                if (!safetyCheck.Safe)
                {
                    return Result<Plan, string>.Failure(
                        $"Plan step '{step.Action}' failed safety check: {string.Join(", ", safetyCheck.Violations)}");
                }
            }

            RecordMetric("planner", sw.ElapsedMilliseconds, true);
            return Result<Plan, string>.Success(plan);
        }
        catch (Exception ex)
        {
            RecordMetric("planner", 1.0, false);
            return Result<Plan, string>.Failure($"Planning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes plan with symbolic next-node selection.
    /// </summary>
    public async Task<Result<ExecutionResult, string>> ExecuteAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        var stepResults = new List<StepResult>();
        var sw = Stopwatch.StartNew();
        var metadata = new Dictionary<string, object>();

        try
        {
            // Execute each step with MeTTa-guided selection
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                var stepId = $"step_{i}";

                // Query MeTTa for next node validation
                var context = new Dictionary<string, object>
                {
                    ["step_index"] = i,
                    ["total_steps"] = plan.Steps.Count
                };

                if (i > 0)
                {
                    // Use MeTTa to validate this is a valid next step
                    var nextNodes = await _representation.QueryNextNodesAsync(
                        $"step_{i-1}", 
                        context, 
                        ct
                    );

                    if (nextNodes.IsSuccess)
                    {
                        var validNext = nextNodes.Value.Any(n => n.NodeId == stepId);
                        metadata[$"step_{i}_metta_validated"] = validNext;
                    }
                }

                // Execute the step
                var stepResult = await ExecuteStepAsync(step, ct);
                stepResults.Add(stepResult);

                // Update MeTTa with execution results
                var execResult = new ExecutionResult(
                    plan,
                    stepResults.ToList(),
                    stepResult.Success,
                    stepResult.Output,
                    metadata,
                    sw.Elapsed
                );

                await _representation.TranslateExecutionStateAsync(execResult, ct);

                if (!stepResult.Success && !string.IsNullOrEmpty(stepResult.Error))
                {
                    RecordMetric("executor", sw.ElapsedMilliseconds, false);
                    return Result<ExecutionResult, string>.Success(
                        new ExecutionResult(plan, stepResults, false, stepResult.Error, metadata, sw.Elapsed));
                }
            }

            sw.Stop();
            RecordMetric("executor", sw.ElapsedMilliseconds, true);

            var finalOutput = stepResults.LastOrDefault()?.Output ?? string.Empty;
            return Result<ExecutionResult, string>.Success(
                new ExecutionResult(plan, stepResults, true, finalOutput, metadata, sw.Elapsed));
        }
        catch (Exception ex)
        {
            RecordMetric("executor", sw.ElapsedMilliseconds, false);
            return Result<ExecutionResult, string>.Failure($"Execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies execution with MeTTa symbolic reasoning.
    /// </summary>
    public async Task<Result<VerificationResult, string>> VerifyAsync(
        ExecutionResult execution,
        CancellationToken ct = default)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            // Build verification prompt
            var verifyPrompt = BuildVerificationPrompt(execution);
            var verificationText = await _llm.GenerateTextAsync(verifyPrompt, ct);

            // Parse verification result
            var verification = ParseVerification(execution, verificationText);

            // Use MeTTa for symbolic plan verification if available
            var planMetta = FormatPlanForMeTTa(execution.Plan);
            var mettaVerification = await _mettaEngine.VerifyPlanAsync(planMetta, ct);

            if (mettaVerification.IsSuccess)
            {
                verification = verification with 
                { 
                    Improvements = verification.Improvements
                        .Append($"MeTTa verification: {(mettaVerification.Value ? "PASSED" : "FAILED")}")
                        .ToList()
                };
            }

            sw.Stop();
            RecordMetric("verifier", sw.ElapsedMilliseconds, true);

            return Result<VerificationResult, string>.Success(verification);
        }
        catch (Exception ex)
        {
            RecordMetric("verifier", 1.0, false);
            return Result<VerificationResult, string>.Failure($"Verification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Learns from execution and updates MeTTa knowledge base.
    /// </summary>
    public void LearnFromExecution(VerificationResult verification)
    {
        // Create experience for memory
        var experience = new Experience(
            Id: Guid.NewGuid(),
            Goal: verification.Execution.Plan.Goal,
            Plan: verification.Execution.Plan,
            Execution: verification.Execution,
            Verification: verification,
            Timestamp: DateTime.UtcNow,
            Metadata: verification.Execution.Metadata
        );

        // Store in memory asynchronously
        _ = _memory.StoreExperienceAsync(experience);

        // Update metrics
        RecordMetric("learner", 1.0, verification.Verified);
    }

    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
    {
        return _metrics;
    }

    // Helper methods
    private async Task<StepResult> ExecuteStepAsync(PlanStep step, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var observedState = new Dictionary<string, object>();

        try
        {
            // Find and execute the tool
            var toolOption = _tools.GetTool(step.Action);
            if (!toolOption.HasValue)
            {
                return new StepResult(
                    step, false, string.Empty, 
                    $"Tool '{step.Action}' not found", 
                    sw.Elapsed, observedState);
            }

            var tool = toolOption.Value!;
            var toolInput = System.Text.Json.JsonSerializer.Serialize(step.Parameters);
            var result = await tool.InvokeAsync(toolInput, ct);

            return result.Match(
                output => new StepResult(step, true, output, null, sw.Elapsed, observedState),
                error => new StepResult(step, false, string.Empty, error, sw.Elapsed, observedState)
            );
        }
        catch (Exception ex)
        {
            return new StepResult(step, false, string.Empty, ex.Message, sw.Elapsed, observedState);
        }
    }

    private string BuildPlanPrompt(
        string goal,
        Dictionary<string, object>? context,
        List<Experience> pastExperiences,
        List<Skill> matchingSkills)
    {
        var prompt = $"Create a detailed plan to accomplish: {goal}\n\n";
        
        if (context?.Any() == true)
        {
            prompt += "Context:\n";
            foreach (var item in context)
                prompt += $"- {item.Key}: {item.Value}\n";
            prompt += "\n";
        }

        prompt += "Available tools:\n";
        foreach (var tool in _tools.All)
            prompt += $"- {tool.Name}: {tool.Description}\n";
        prompt += "\n";

        if (pastExperiences.Count > 0)
        {
            prompt += "Relevant past experiences:\n";
            foreach (var exp in pastExperiences.Take(3))
                prompt += $"- {exp.Goal} (success: {exp.Verification.Verified}, quality: {exp.Verification.QualityScore:F2})\n";
            prompt += "\n";
        }

        prompt += @"Provide a plan as JSON array of steps:
[
  {
    ""action"": ""tool_name"",
    ""parameters"": {},
    ""expected_outcome"": ""what should happen"",
    ""confidence"": 0.9
  }
]";

        return prompt;
    }

    private Plan ParsePlan(string planText, string goal)
    {
        var steps = new List<PlanStep>();
        var confidenceScores = new Dictionary<string, double>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(planText);
            var array = doc.RootElement;

            if (array.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var element in array.EnumerateArray())
                {
                    var action = element.GetProperty("action").GetString() ?? "";
                    var expected = element.GetProperty("expected_outcome").GetString() ?? "";
                    var confidence = element.TryGetProperty("confidence", out var conf) 
                        ? conf.GetDouble() 
                        : 0.5;

                    var parameters = new Dictionary<string, object>();
                    if (element.TryGetProperty("parameters", out var paramsElement))
                    {
                        foreach (var prop in paramsElement.EnumerateObject())
                        {
                            parameters[prop.Name] = prop.Value.ToString();
                        }
                    }

                    steps.Add(new PlanStep(action, parameters, expected, confidence));
                }
            }
        }
        catch
        {
            // Fallback: create simple plan
            steps.Add(new PlanStep(
                "llm_direct",
                new Dictionary<string, object> { ["goal"] = goal },
                "Direct LLM response",
                0.5));
        }

        confidenceScores["overall"] = steps.Any() ? steps.Average(s => s.ConfidenceScore) : 0.5;
        return new Plan(goal, steps, confidenceScores, DateTime.UtcNow);
    }

    private string BuildVerificationPrompt(ExecutionResult execution)
    {
        var prompt = $"Verify the execution of plan: {execution.Plan.Goal}\n\n";
        prompt += $"Success: {execution.Success}\n";
        prompt += $"Duration: {execution.Duration.TotalSeconds:F2}s\n\n";
        
        prompt += "Steps executed:\n";
        foreach (var result in execution.StepResults)
        {
            prompt += $"- {result.Step.Action}: {(result.Success ? "✓" : "✗")} {result.Output}\n";
        }

        prompt += "\nProvide verification in JSON format:\n";
        prompt += @"{
  ""verified"": true/false,
  ""quality_score"": 0.0-1.0,
  ""issues"": [""issue1"", ""issue2""],
  ""improvements"": [""suggestion1"", ""suggestion2""]
}";

        return prompt;
    }

    private VerificationResult ParseVerification(ExecutionResult execution, string verificationText)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(verificationText);
            var root = doc.RootElement;

            var verified = root.GetProperty("verified").GetBoolean();
            var qualityScore = root.GetProperty("quality_score").GetDouble();

            var issues = new List<string>();
            if (root.TryGetProperty("issues", out var issuesArray))
            {
                foreach (var issue in issuesArray.EnumerateArray())
                {
                    issues.Add(issue.GetString() ?? "");
                }
            }

            var improvements = new List<string>();
            if (root.TryGetProperty("improvements", out var improvArray))
            {
                foreach (var improvement in improvArray.EnumerateArray())
                {
                    improvements.Add(improvement.GetString() ?? "");
                }
            }

            return new VerificationResult(execution, verified, qualityScore, issues, improvements, null);
        }
        catch
        {
            return new VerificationResult(
                execution, 
                execution.Success, 
                execution.Success ? 0.7 : 0.3, 
                new List<string>(), 
                new List<string>(), 
                null);
        }
    }

    private string FormatPlanForMeTTa(Plan plan)
    {
        var steps = string.Join(" ", plan.Steps.Select((s, i) => $"(step {i} {s.Action})"));
        return $"(plan {steps})";
    }

    private void RecordMetric(string component, double latencyMs, bool success)
    {
        _metrics.AddOrUpdate(
            component,
            _ => new PerformanceMetrics(
                ResourceName: component,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()
            ),
            (_, old) =>
            {
                var totalCalls = old.ExecutionCount + 1;
                var successCalls = (int)(old.SuccessRate * old.ExecutionCount) + (success ? 1 : 0);
                var avgLatency = (old.AverageLatencyMs * old.ExecutionCount + latencyMs) / totalCalls;
                var successRate = (double)successCalls / totalCalls;

                return new PerformanceMetrics(
                    ResourceName: component,
                    ExecutionCount: totalCalls,
                    AverageLatencyMs: avgLatency,
                    SuccessRate: successRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: old.CustomMetrics
                );
            });
    }
}
