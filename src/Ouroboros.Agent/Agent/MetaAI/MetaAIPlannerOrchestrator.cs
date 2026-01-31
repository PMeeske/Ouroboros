#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-AI Planner Orchestrator Implementation
// Implements plan-execute-verify loop with continual learning
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of the Meta-AI v2 planner/executor/verifier orchestrator.
/// Coordinates planning, execution, verification, and learning in a continuous loop.
/// </summary>
public sealed class MetaAIPlannerOrchestrator : IMetaAIPlannerOrchestrator
{
    private readonly IChatCompletionModel _llm;
    private readonly ToolRegistry _tools;
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly IUncertaintyRouter _router;
    private readonly ISafetyGuard _safety;
    private readonly IEthicsFramework _ethics;
    private readonly ISkillExtractor? _skillExtractor;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new();

    public MetaAIPlannerOrchestrator(
        IChatCompletionModel llm,
        ToolRegistry tools,
        IMemoryStore memory,
        ISkillRegistry skills,
        IUncertaintyRouter router,
        ISafetyGuard safety,
        IEthicsFramework ethics,
        ISkillExtractor? skillExtractor = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _ethics = ethics ?? throw new ArgumentNullException(nameof(ethics));
        _skillExtractor = skillExtractor ?? new SkillExtractor(llm, skills, ethics);
    }

    /// <summary>
    /// Plans how to accomplish a goal using available skills and past experience.
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
            // Check if we have relevant past experiences
            MemoryQuery query = new MemoryQuery(goal, context, MaxResults: 5, MinSimilarity: 0.7);
            List<Experience> pastExperiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);

            // Find matching skills
            List<Skill> matchingSkills = await _skills.FindMatchingSkillsAsync(goal, context);

            // Generate plan using LLM with past experience and skills
            string planPrompt = BuildPlanPrompt(goal, context, pastExperiences, matchingSkills);
            string planText = await _llm.GenerateTextAsync(planPrompt, ct);

            // Parse plan from LLM response
            Plan plan = ParsePlan(planText, goal);

            // Ethics evaluation - foundational gate that runs BEFORE safety checks
            // This ensures ethical constraints are the first and primary consideration
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
                    }).ToArray(),
                    ConfidenceScores = plan.ConfidenceScores,
                    CreatedAt = plan.CreatedAt
                },
                ActionContext = new ActionContext
                {
                    AgentId = "meta-ai-planner",
                    UserId = null, // Set if available from context
                    Environment = context?.ContainsKey("environment") == true 
                        ? context["environment"]?.ToString() ?? "planning" 
                        : "planning",
                    State = context != null 
                        ? new Dictionary<string, object>(context) 
                        : new Dictionary<string, object>()
                }
            };

            var ethicsResult = await _ethics.EvaluatePlanAsync(planContext, ct);

            if (ethicsResult.IsFailure)
            {
                return Result<Plan, string>.Failure(
                    $"Plan failed ethics evaluation: {ethicsResult.Error}");
            }

            if (!ethicsResult.Value.IsPermitted)
            {
                return Result<Plan, string>.Failure(
                    $"Plan blocked by ethics framework: {ethicsResult.Value.Reasoning}");
            }

            if (ethicsResult.Value.Level == EthicalClearanceLevel.RequiresHumanApproval)
            {
                return Result<Plan, string>.Failure(
                    $"Plan requires human approval: {ethicsResult.Value.Reasoning}");
            }

            // Safety checks - secondary validation after ethics clearance
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

            RecordMetric("planner", 1.0, true);
            return Result<Plan, string>.Success(plan);
        }
        catch (Exception ex)
        {
            RecordMetric("planner", 1.0, false);
            return Result<Plan, string>.Failure($"Planning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a plan step by step with monitoring and safety checks.
    /// Supports optional parallel execution of independent steps.
    /// </summary>
    public async Task<Result<ExecutionResult, string>> ExecuteAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        if (plan == null)
            return Result<ExecutionResult, string>.Failure("Plan cannot be null");

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Check if parallel execution is beneficial
        ParallelExecutor parallelExecutor = new ParallelExecutor(_safety, ExecuteStepAsync);
        double estimatedSpeedup = parallelExecutor.EstimateSpeedup(plan);

        List<StepResult> stepResults;
        bool overallSuccess;
        string finalOutput;

        try
        {
            // Use parallel execution if speedup is significant
            if (estimatedSpeedup > 1.5)
            {
                (stepResults, overallSuccess, finalOutput) = await parallelExecutor.ExecuteParallelAsync(plan, ct);
            }
            else
            {
                // Sequential execution
                stepResults = new List<StepResult>();
                overallSuccess = true;
                finalOutput = "";

                foreach (PlanStep step in plan.Steps)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    // Apply safety sandbox
                    PlanStep sandboxedStep = _safety.SandboxStep(step);

                    // Execute step
                    StepResult stepResult = await ExecuteStepAsync(sandboxedStep, ct);
                    stepResults.Add(stepResult);

                    if (!stepResult.Success)
                    {
                        overallSuccess = false;
                        // Continue with remaining steps even if one fails
                    }

                    finalOutput += stepResult.Output + "\n";
                }

                finalOutput = finalOutput.Trim();
            }

            stopwatch.Stop();

            ExecutionResult execution = new ExecutionResult(
                plan,
                stepResults,
                overallSuccess,
                finalOutput,
                new Dictionary<string, object>
                {
                    ["steps_completed"] = stepResults.Count,
                    ["steps_total"] = plan.Steps.Count,
                    ["parallel_execution"] = estimatedSpeedup > 1.5,
                    ["estimated_speedup"] = estimatedSpeedup
                },
                stopwatch.Elapsed);

            RecordMetric("executor", stopwatch.Elapsed.TotalMilliseconds, overallSuccess);
            return Result<ExecutionResult, string>.Success(execution);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMetric("executor", stopwatch.Elapsed.TotalMilliseconds, false);
            return Result<ExecutionResult, string>.Failure($"Execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies execution results and provides feedback for improvement.
    /// </summary>
    public async Task<Result<VerificationResult, string>> VerifyAsync(
        ExecutionResult execution,
        CancellationToken ct = default)
    {
        if (execution == null)
            return Result<VerificationResult, string>.Failure("Execution cannot be null");

        try
        {
            // Build verification prompt
            string verifyPrompt = BuildVerificationPrompt(execution);
            string verificationText = await _llm.GenerateTextAsync(verifyPrompt, ct);

            // Parse verification result
            VerificationResult verification = ParseVerification(execution, verificationText);

            RecordMetric("verifier", 1.0, verification.Verified);
            return Result<VerificationResult, string>.Success(verification);
        }
        catch (Exception ex)
        {
            RecordMetric("verifier", 1.0, false);
            return Result<VerificationResult, string>.Failure($"Verification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Learns from execution experience to improve future planning.
    /// </summary>
    public void LearnFromExecution(VerificationResult verification)
    {
        if (verification == null)
            return;

        // Store experience in memory
        Experience experience = new Experience(
            Guid.NewGuid(),
            verification.Execution.Plan.Goal,
            verification.Execution.Plan,
            verification.Execution,
            verification,
            DateTime.UtcNow,
            new Dictionary<string, object>
            {
                ["quality_score"] = verification.QualityScore,
                ["verified"] = verification.Verified
            });

        _ = _memory.StoreExperienceAsync(experience);

        // If execution was successful and high quality, extract a skill
        if (verification.Verified && verification.QualityScore > 0.8 && _skillExtractor != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    bool shouldExtract = await _skillExtractor.ShouldExtractSkillAsync(verification);
                    if (shouldExtract)
                    {
                        Result<Skill, string> skillResult = await _skillExtractor.ExtractSkillAsync(
                            verification.Execution,
                            verification);

                        skillResult.Match(
                            skill =>
                            {
                                RecordMetric("skill_extraction_success", 1.0, true);
                                Console.WriteLine($"✓ Extracted skill: {skill.Name} (Quality: {skill.SuccessRate:P0})");
                            },
                            error =>
                            {
                                RecordMetric("skill_extraction_failure", 1.0, false);
                                Console.WriteLine($"✗ Skill extraction failed: {error}");
                            });
                    }
                }
                catch (Exception ex)
                {
                    RecordMetric("skill_extraction_error", 1.0, false);
                    Console.WriteLine($"✗ Skill extraction error: {ex.Message}");
                }
            });
        }

        RecordMetric("learning", 1.0, true);
    }

    /// <summary>
    /// Gets performance metrics for the orchestrator.
    /// </summary>
    public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        => new Dictionary<string, PerformanceMetrics>(_metrics);

    // Private helper methods

    private async Task<StepResult> ExecuteStepAsync(PlanStep step, CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate step action is not empty
            if (string.IsNullOrWhiteSpace(step.Action))
            {
                stopwatch.Stop();
                return new StepResult(
                    step,
                    false,
                    "",
                    "Step action/tool name cannot be empty",
                    stopwatch.Elapsed,
                    new Dictionary<string, object> { ["error"] = "empty_action" });
            }

            // Check if this is a tool invocation
            ITool? tool = _tools.Get(step.Action);
            if (tool != null)
            {
                string args = System.Text.Json.JsonSerializer.Serialize(step.Parameters);
                Result<string, string> toolResult = await tool.InvokeAsync(args);

                stopwatch.Stop();

                return toolResult.Match(
                    output => new StepResult(
                        step,
                        true,
                        output,
                        null,
                        stopwatch.Elapsed,
                        new Dictionary<string, object>
                        {
                            ["tool"] = step.Action,
                            ["success"] = true
                        }),
                    error => new StepResult(
                        step,
                        false,
                        "",
                        error,
                        stopwatch.Elapsed,
                        new Dictionary<string, object>
                        {
                            ["tool"] = step.Action,
                            ["success"] = false
                        }));
            }

            // If not a tool, try to execute as LLM task
            string prompt = $"Execute the following task: {step.Action}\nParameters: {System.Text.Json.JsonSerializer.Serialize(step.Parameters)}";
            string output = await _llm.GenerateTextAsync(prompt, ct);

            stopwatch.Stop();

            return new StepResult(
                step,
                true,
                output,
                null,
                stopwatch.Elapsed,
                new Dictionary<string, object>
                {
                    ["type"] = "llm_task"
                });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new StepResult(
                step,
                false,
                "",
                ex.Message,
                stopwatch.Elapsed,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
    }

    private string BuildPlanPrompt(
        string goal,
        Dictionary<string, object>? context,
        List<Experience> pastExperiences,
        List<Skill> matchingSkills)
    {
        string prompt = $@"You are an AI planner. Create a detailed plan to accomplish this goal:

GOAL: {goal}

";

        if (context != null && context.Any())
        {
            prompt += $"CONTEXT: {System.Text.Json.JsonSerializer.Serialize(context)}\n\n";
        }

        if (matchingSkills.Any())
        {
            prompt += "AVAILABLE SKILLS:\n";
            foreach (Skill? skill in matchingSkills.Take(3))
            {
                prompt += $"- {skill.Name}: {skill.Description} (Success rate: {skill.SuccessRate:P0})\n";
            }
            prompt += "\n";
        }

        if (pastExperiences.Any())
        {
            prompt += "PAST EXPERIENCE:\n";
            foreach (Experience? exp in pastExperiences.Take(3))
            {
                prompt += $"- Goal: {exp.Goal}, Quality: {exp.Verification.QualityScore:P0}, Verified: {exp.Verification.Verified}\n";
            }
            prompt += "\n";
        }

        prompt += $@"AVAILABLE TOOLS:
{string.Join("\n", _tools.All.Select(t => $"- {t.Name}: {t.Description}"))}

Create a plan with specific steps. For each step, specify:
1. Action (tool name or task description)
2. Parameters (as JSON object with ACTUAL CONCRETE VALUES)
3. Expected outcome
4. Confidence score (0-1)

CRITICAL PARAMETER RULES:
- Use ACTUAL CONCRETE VALUES in parameters - never placeholder descriptions
- URLs must be real URLs (https://example.com), not descriptions like 'URL from search'
- Search queries must be actual text, not 'query about topic'
- If you don't have a real value yet, SKIP that step or mark confidence as 0

WRONG parameters: {{""url"": ""URL of the search result"", ""query"": ""search for topic""}}
CORRECT parameters: {{""url"": ""https://example.com/page"", ""query"": ""Ouroboros mythology serpent""}}

Format your response as:
STEP 1: [action]
PARAMETERS: {{...}}
EXPECTED: [outcome]
CONFIDENCE: [0-1]

STEP 2: ...
";

        return prompt;
    }

    private Plan ParsePlan(string planText, string goal)
    {
        List<PlanStep> steps = new List<PlanStep>();
        Dictionary<string, double> confidenceScores = new Dictionary<string, double>();

        // Simple parsing - in production, use more robust parser
        string[] lines = planText.Split('\n');
        string? currentAction = null;
        Dictionary<string, object>? currentParams = null;
        string? currentExpected = null;
        double currentConfidence = 0.8;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    steps.Add(new PlanStep(currentAction, currentParams ?? new(), currentExpected ?? "", currentConfidence));
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
                currentParams = new();
                currentExpected = "";
                currentConfidence = 0.8;
            }
            else if (trimmed.StartsWith("PARAMETERS:"))
            {
                string paramsJson = trimmed.Substring("PARAMETERS:".Length).Trim();
                try
                {
                    currentParams = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson) ?? new();
                }
                catch
                {
                    currentParams = new Dictionary<string, object> { ["raw"] = paramsJson };
                }
            }
            else if (trimmed.StartsWith("EXPECTED:"))
            {
                currentExpected = trimmed.Substring("EXPECTED:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFIDENCE:"))
            {
                string confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, out double conf))
                {
                    currentConfidence = conf;
                }
            }
        }

        if (currentAction != null)
        {
            steps.Add(new PlanStep(currentAction, currentParams ?? new(), currentExpected ?? "", currentConfidence));
        }

        // If no steps were parsed, create a simple default plan
        if (steps.Count == 0)
        {
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
        return $@"Verify the following execution result:

GOAL: {execution.Plan.Goal}

PLAN:
{string.Join("\n", execution.Plan.Steps.Select((s, i) => $"{i + 1}. {s.Action}"))}

EXECUTION RESULTS:
{string.Join("\n", execution.StepResults.Select((r, i) =>
    $"{i + 1}. {(r.Success ? "✓" : "✗")} {r.Output.Substring(0, Math.Min(100, r.Output.Length))}"))}

FINAL OUTPUT:
{execution.FinalOutput}

Please verify if:
1. The goal was accomplished
2. All steps executed correctly
3. The output quality is acceptable

Provide:
- VERIFIED: yes/no
- QUALITY_SCORE: 0-1
- ISSUES: (list any problems)
- IMPROVEMENTS: (list suggestions)
- REVISED_PLAN: (if needed)
";
    }

    private VerificationResult ParseVerification(ExecutionResult execution, string verificationText)
    {
        bool verified = verificationText.Contains("VERIFIED: yes", StringComparison.OrdinalIgnoreCase);

        // Extract quality score
        double qualityScore = 0.7;
        System.Text.RegularExpressions.Match qualityMatch = System.Text.RegularExpressions.Regex.Match(
            verificationText,
            @"QUALITY_SCORE:\s*([0-9.]+)");
        if (qualityMatch.Success && double.TryParse(qualityMatch.Groups[1].Value, out double score))
        {
            qualityScore = score;
        }

        List<string> issues = new List<string>();
        List<string> improvements = new List<string>();
        string? revisedPlan = null;

        // Simple parsing - in production use more robust approach
        return new VerificationResult(
            execution,
            verified,
            qualityScore,
            issues,
            improvements,
            revisedPlan);
    }

    private void RecordMetric(string component, double latencyMs, bool success)
    {
        _metrics.AddOrUpdate(
            component,
            _ => new PerformanceMetrics(
                component,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),
            (_, existing) =>
            {
                int newCount = existing.ExecutionCount + 1;
                double newAvgLatency = ((existing.AverageLatencyMs * existing.ExecutionCount) + latencyMs) / newCount;
                double newSuccessRate = ((existing.SuccessRate * existing.ExecutionCount) + (success ? 1.0 : 0.0)) / newCount;

                return new PerformanceMetrics(
                    component,
                    ExecutionCount: newCount,
                    AverageLatencyMs: newAvgLatency,
                    SuccessRate: newSuccessRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: existing.CustomMetrics);
            });
    }
}
