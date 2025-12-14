#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-AI Layer v3.0 - MeTTa-First Orchestrator
// Integrates symbolic reasoning with neural planning
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using LangChainPipeline.Tools.MeTTa;

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
            Stopwatch sw = Stopwatch.StartNew();

            // Get past experiences and skills
            MemoryQuery query = new MemoryQuery(goal, context, MaxResults: 5, MinSimilarity: 0.7);
            List<Experience> pastExperiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);
            List<Skill> matchingSkills = await _skills.FindMatchingSkillsAsync(goal, context);

            // Generate initial plan using LLM
            string planPrompt = BuildPlanPrompt(goal, context, pastExperiences, matchingSkills);
            string planText = await _llm.GenerateTextAsync(planPrompt, ct);
            Plan plan = ParsePlan(planText, goal);

            // Translate plan to MeTTa representation
            Result<Unit, string> translationResult = await _representation.TranslatePlanAsync(plan, ct);
            if (translationResult.IsFailure)
            {
                Console.WriteLine($"Warning: Failed to translate plan to MeTTa: {translationResult.Error}");
            }

            // Translate tools to MeTTa
            Result<Unit, string> toolTranslation = await _representation.TranslateToolsAsync(_tools, ct);
            if (toolTranslation.IsFailure)
            {
                Console.WriteLine($"Warning: Failed to translate tools to MeTTa: {toolTranslation.Error}");
            }

            // Validate plan safety
            foreach (PlanStep step in plan.Steps)
            {
                SafetyCheckResult safetyCheck = _safety.CheckSafety(
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
        List<StepResult> stepResults = new List<StepResult>();
        Stopwatch sw = Stopwatch.StartNew();
        Dictionary<string, object> metadata = new Dictionary<string, object>();

        try
        {
            // Execute each step with MeTTa-guided selection
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                PlanStep step = plan.Steps[i];
                string stepId = $"step_{i}";

                // Query MeTTa for next node validation
                Dictionary<string, object> context = new Dictionary<string, object>
                {
                    ["step_index"] = i,
                    ["total_steps"] = plan.Steps.Count
                };

                if (i > 0)
                {
                    // Use MeTTa to validate this is a valid next step
                    Result<List<NextNodeCandidate>, string> nextNodes = await _representation.QueryNextNodesAsync(
                        $"step_{i - 1}",
                        context,
                        ct
                    );

                    if (nextNodes.IsSuccess)
                    {
                        bool validNext = nextNodes.Value.Any(n => n.NodeId == stepId);
                        metadata[$"step_{i}_metta_validated"] = validNext;
                    }
                }

                // Execute the step
                StepResult stepResult = await ExecuteStepAsync(step, ct);
                stepResults.Add(stepResult);

                // Update MeTTa with execution results
                ExecutionResult execResult = new ExecutionResult(
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

            string finalOutput = stepResults.LastOrDefault()?.Output ?? string.Empty;
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
            Stopwatch sw = Stopwatch.StartNew();

            // Build verification prompt
            string verifyPrompt = BuildVerificationPrompt(execution);
            string verificationText = await _llm.GenerateTextAsync(verifyPrompt, ct);

            // Parse verification result
            VerificationResult verification = ParseVerification(execution, verificationText);

            // Use MeTTa for symbolic plan verification if available
            string planMetta = FormatPlanForMeTTa(execution.Plan);
            Result<bool, string> mettaVerification = await _mettaEngine.VerifyPlanAsync(planMetta, ct);

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
        Experience experience = new Experience(
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

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
    {
        return _metrics;
    }

    // Helper methods
    private async Task<StepResult> ExecuteStepAsync(PlanStep step, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Dictionary<string, object> observedState = new Dictionary<string, object>();

        try
        {
            // Validate step action is not empty
            if (string.IsNullOrWhiteSpace(step.Action))
            {
                return new StepResult(
                    step, false, string.Empty,
                    "Step action/tool name cannot be empty",
                    sw.Elapsed, observedState);
            }

            // Find and execute the tool
            Option<ITool> toolOption = _tools.GetTool(step.Action);
            if (!toolOption.HasValue)
            {
                return new StepResult(
                    step, false, string.Empty,
                    $"Tool '{step.Action}' not found",
                    sw.Elapsed, observedState);
            }

            ITool tool = toolOption.Value!;
            string toolInput = System.Text.Json.JsonSerializer.Serialize(step.Parameters);
            Result<string, string> result = await tool.InvokeAsync(toolInput, ct);

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
        string prompt = $"Create a detailed plan to accomplish: {goal}\n\n";

        if (context?.Any() == true)
        {
            prompt += "Context:\n";
            foreach (KeyValuePair<string, object> item in context)
                prompt += $"- {item.Key}: {item.Value}\n";
            prompt += "\n";
        }

        prompt += "Available tools:\n";
        foreach (ITool tool in _tools.All)
            prompt += $"- {tool.Name}: {tool.Description}\n";
        prompt += "\n";

        if (pastExperiences.Count > 0)
        {
            prompt += "Relevant past experiences:\n";
            foreach (Experience? exp in pastExperiences.Take(3))
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
]

CRITICAL PARAMETER RULES:
- Use ACTUAL CONCRETE VALUES in parameters - never placeholder descriptions
- URLs must be real URLs (https://example.com), not descriptions like 'URL from search'
- Search queries must be actual text, not 'query about topic'
- If you don't have a real value, SKIP that step or ask for it

WRONG parameters: {""url"": ""URL of the search result"", ""query"": ""search for topic""}
CORRECT parameters: {""url"": ""https://example.com/page"", ""query"": ""Ouroboros mythology serpent""}";

        return prompt;
    }

    private Plan ParsePlan(string planText, string goal)
    {
        List<PlanStep> steps = new List<PlanStep>();
        Dictionary<string, double> confidenceScores = new Dictionary<string, double>();

        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(planText);
            System.Text.Json.JsonElement array = doc.RootElement;

            if (array.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (System.Text.Json.JsonElement element in array.EnumerateArray())
                {
                    string action = element.GetProperty("action").GetString() ?? "";
                    string expected = element.GetProperty("expected_outcome").GetString() ?? "";
                    double confidence = element.TryGetProperty("confidence", out System.Text.Json.JsonElement conf)
                        ? conf.GetDouble()
                        : 0.5;

                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    if (element.TryGetProperty("parameters", out System.Text.Json.JsonElement paramsElement))
                    {
                        foreach (System.Text.Json.JsonProperty prop in paramsElement.EnumerateObject())
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
        string prompt = $"Verify the execution of plan: {execution.Plan.Goal}\n\n";
        prompt += $"Success: {execution.Success}\n";
        prompt += $"Duration: {execution.Duration.TotalSeconds:F2}s\n\n";

        prompt += "Steps executed:\n";
        foreach (StepResult result in execution.StepResults)
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
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(verificationText);
            System.Text.Json.JsonElement root = doc.RootElement;

            bool verified = root.GetProperty("verified").GetBoolean();
            double qualityScore = root.GetProperty("quality_score").GetDouble();

            List<string> issues = new List<string>();
            if (root.TryGetProperty("issues", out System.Text.Json.JsonElement issuesArray))
            {
                foreach (System.Text.Json.JsonElement issue in issuesArray.EnumerateArray())
                {
                    issues.Add(issue.GetString() ?? "");
                }
            }

            List<string> improvements = new List<string>();
            if (root.TryGetProperty("improvements", out System.Text.Json.JsonElement improvArray))
            {
                foreach (System.Text.Json.JsonElement improvement in improvArray.EnumerateArray())
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
        string steps = string.Join(" ", plan.Steps.Select((s, i) => $"(step {i} {s.Action})"));
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
                int totalCalls = old.ExecutionCount + 1;
                int successCalls = (int)(old.SuccessRate * old.ExecutionCount) + (success ? 1 : 0);
                double avgLatency = (old.AverageLatencyMs * old.ExecutionCount + latencyMs) / totalCalls;
                double successRate = (double)successCalls / totalCalls;

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
