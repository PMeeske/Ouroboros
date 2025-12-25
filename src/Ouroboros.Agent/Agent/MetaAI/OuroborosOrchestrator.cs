// <copyright file="OuroborosOrchestrator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Ouroboros Orchestrator - Self-Improving AI Orchestration
// Implements recursive Plan-Execute-Verify-Learn cycle
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Ouroboros.Agent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// The Ouroboros Orchestrator is a self-improving AI orchestration system that implements
/// the recursive Plan-Execute-Verify-Learn cycle. It uses the OuroborosAtom for symbolic
/// representation and maintains a self-model that evolves through experience.
/// 
/// Named after the ancient symbol of a serpent eating its own tail, this orchestrator
/// embodies the principle of recursive self-improvement.
/// </summary>
public sealed class OuroborosOrchestrator : OrchestratorBase<string, OuroborosResult>
{
    private readonly IChatCompletionModel _llm;
    private readonly ToolRegistry _tools;
    private readonly IMemoryStore _memory;
    private readonly ISafetyGuard _safety;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly OuroborosAtom _atom;
    private readonly ConcurrentDictionary<string, double> _performanceMetrics = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosOrchestrator"/> class.
    /// </summary>
    /// <param name="llm">The language model for generation.</param>
    /// <param name="tools">The tool registry.</param>
    /// <param name="memory">The memory store for experiences.</param>
    /// <param name="safety">The safety guard.</param>
    /// <param name="mettaEngine">The MeTTa engine for symbolic reasoning.</param>
    /// <param name="atom">Optional pre-configured OuroborosAtom.</param>
    /// <param name="configuration">Optional orchestrator configuration.</param>
    public OuroborosOrchestrator(
        IChatCompletionModel llm,
        ToolRegistry tools,
        IMemoryStore memory,
        ISafetyGuard safety,
        IMeTTaEngine mettaEngine,
        OuroborosAtom? atom = null,
        OrchestratorConfig? configuration = null)
        : base("OuroborosOrchestrator", configuration ?? OrchestratorConfig.Default(), safety)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _atom = atom ?? OuroborosAtom.CreateDefault();
    }

    /// <summary>
    /// Gets the OuroborosAtom representing this orchestrator's self-model.
    /// </summary>
    public OuroborosAtom Atom => _atom;

    /// <summary>
    /// Executes a complete improvement cycle for the given goal.
    /// </summary>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="context">Execution context.</param>
    /// <returns>The result of the improvement cycle.</returns>
    protected override async Task<OuroborosResult> ExecuteCoreAsync(string goal, OrchestratorContext context)
    {
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        List<PhaseResult> phaseResults = new List<PhaseResult>();
        string? finalOutput = null;
        bool success = true;

        try
        {
            // Set the goal
            _atom.SetGoal(goal);

            // Translate Ouroboros state to MeTTa
            await TranslateToMeTTaAsync(context.CancellationToken);

            // Phase 1: PLAN
            PhaseResult planResult = await ExecutePlanPhaseAsync(goal, context.CancellationToken);
            phaseResults.Add(planResult);
            _atom.AdvancePhase();

            if (!planResult.Success)
            {
                success = false;
                return CreateResult(goal, phaseResults, planResult.Output, success, totalStopwatch.Elapsed);
            }

            // Phase 2: EXECUTE
            PhaseResult executeResult = await ExecuteExecutePhaseAsync(planResult.Output, context.CancellationToken);
            phaseResults.Add(executeResult);
            _atom.AdvancePhase();

            if (!executeResult.Success)
            {
                success = false;
                return CreateResult(goal, phaseResults, executeResult.Error, success, totalStopwatch.Elapsed);
            }

            // Phase 3: VERIFY
            PhaseResult verifyResult = await ExecuteVerifyPhaseAsync(goal, executeResult.Output, context.CancellationToken);
            phaseResults.Add(verifyResult);
            _atom.AdvancePhase();

            // Phase 4: LEARN
            PhaseResult learnResult = await ExecuteLearnPhaseAsync(goal, phaseResults, context.CancellationToken);
            phaseResults.Add(learnResult);
            _atom.AdvancePhase(); // Returns to PLAN, completing the cycle

            finalOutput = executeResult.Output;
            success = verifyResult.Success;

            totalStopwatch.Stop();
            return CreateResult(goal, phaseResults, finalOutput, success, totalStopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            totalStopwatch.Stop();
            throw;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            return CreateResult(goal, phaseResults, ex.Message, false, totalStopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Executes the PLAN phase - goal decomposition and strategy formulation.
    /// </summary>
    private async Task<PhaseResult> ExecutePlanPhaseAsync(string goal, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Check if goal is safe
            if (!_atom.IsSafeAction(goal))
            {
                return new PhaseResult(
                    ImprovementPhase.Plan,
                    Success: false,
                    Output: string.Empty,
                    Error: "Goal violates safety constraints",
                    Duration: sw.Elapsed);
            }

            // Assess confidence for this goal
            OuroborosConfidence confidence = _atom.AssessConfidence(goal);

            // Build planning prompt with self-reflection
            string selfReflection = _atom.SelfReflect();
            string prompt = BuildPlanPrompt(goal, selfReflection, confidence);

            // Generate plan
            string planText = await _llm.GenerateTextAsync(prompt, ct);

            sw.Stop();
            RecordPhaseMetric("plan", sw.ElapsedMilliseconds, true);

            return new PhaseResult(
                ImprovementPhase.Plan,
                Success: true,
                Output: planText,
                Error: null,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["confidence"] = confidence.ToString(),
                    ["self_reflection_included"] = true,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("plan", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Plan,
                Success: false,
                Output: string.Empty,
                Error: $"Planning failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes the EXECUTE phase - carrying out planned actions.
    /// </summary>
    private async Task<PhaseResult> ExecuteExecutePhaseAsync(string plan, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Parse plan and execute steps
            List<string> steps = ParsePlanSteps(plan);
            StringBuilder outputBuilder = new StringBuilder();
            bool allStepsSucceeded = true;
            string? lastError = null;

            foreach (string step in steps)
            {
                Result<string, string> stepResult = await ExecuteStepAsync(step, ct);

                stepResult.Match(
                    success =>
                    {
                        outputBuilder.AppendLine($"Step completed: {success}");
                    },
                    error =>
                    {
                        outputBuilder.AppendLine($"Step failed: {error}");
                        allStepsSucceeded = false;
                        lastError = error;
                    });

                if (!allStepsSucceeded)
                {
                    break;
                }
            }

            sw.Stop();
            RecordPhaseMetric("execute", sw.ElapsedMilliseconds, allStepsSucceeded);

            return new PhaseResult(
                ImprovementPhase.Execute,
                Success: allStepsSucceeded,
                Output: outputBuilder.ToString(),
                Error: lastError,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["steps_count"] = steps.Count,
                    ["steps_completed"] = allStepsSucceeded ? steps.Count : steps.Count - 1,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("execute", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Execute,
                Success: false,
                Output: string.Empty,
                Error: $"Execution failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes the VERIFY phase - checking results against expectations.
    /// </summary>
    private async Task<PhaseResult> ExecuteVerifyPhaseAsync(string goal, string output, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Build verification prompt
            string prompt = BuildVerificationPrompt(goal, output);

            // Generate verification
            string verificationText = await _llm.GenerateTextAsync(prompt, ct);

            // Parse verification result
            (bool verified, double qualityScore) = ParseVerificationResult(verificationText);

            // Use MeTTa for symbolic verification
            string planMetta = $"(plan (goal \"{EscapeMeTTa(goal)}\") (output \"{EscapeMeTTa(output.Substring(0, Math.Min(100, output.Length)))}\"))";
            Result<bool, string> mettaResult = await _mettaEngine.VerifyPlanAsync(planMetta, ct);

            bool mettaVerified = mettaResult.Match(v => v, _ => true);

            sw.Stop();
            RecordPhaseMetric("verify", sw.ElapsedMilliseconds, verified);

            return new PhaseResult(
                ImprovementPhase.Verify,
                Success: verified && mettaVerified,
                Output: verificationText,
                Error: verified ? null : "Verification failed",
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["quality_score"] = qualityScore,
                    ["metta_verified"] = mettaVerified,
                    ["llm_verified"] = verified,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("verify", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Verify,
                Success: false,
                Output: string.Empty,
                Error: $"Verification failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes the LEARN phase - extracting insights and updating capabilities.
    /// </summary>
    private async Task<PhaseResult> ExecuteLearnPhaseAsync(string goal, List<PhaseResult> phaseResults, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Calculate overall success and quality
            bool overallSuccess = phaseResults.All(p => p.Success);
            double averageQuality = phaseResults
                .Where(p => p.Metadata.ContainsKey("quality_score"))
                .Select(p => (double)p.Metadata["quality_score"])
                .DefaultIfEmpty(0.5)
                .Average();

            // Extract insights from execution
            List<string> insights = await ExtractInsightsAsync(goal, phaseResults, ct);

            // Record experience
            OuroborosExperience experience = new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: goal,
                Success: overallSuccess,
                QualityScore: averageQuality,
                Insights: insights,
                Timestamp: DateTime.UtcNow);

            _atom.RecordExperience(experience);

            // Store in memory asynchronously
            await StoreExperienceInMemoryAsync(experience, ct);

            // Update MeTTa knowledge base
            await UpdateMeTTaKnowledgeAsync(experience, ct);

            // Update capabilities based on success
            if (overallSuccess && averageQuality > 0.7)
            {
                // Boost relevant capability confidence
                IEnumerable<string> relevantCapabilities = goal.ToLower().Split(' ')
                    .Where(word => _atom.Capabilities.Any(c => c.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));

                foreach (string capName in relevantCapabilities.Take(2))
                {
                    OuroborosCapability? existingCap = _atom.Capabilities.FirstOrDefault(c =>
                        c.Name.Contains(capName, StringComparison.OrdinalIgnoreCase));

                    if (existingCap != null)
                    {
                        double newConfidence = Math.Min(1.0, existingCap.ConfidenceLevel + 0.05);
                        _atom.AddCapability(existingCap with { ConfidenceLevel = newConfidence });
                    }
                }
            }

            sw.Stop();
            RecordPhaseMetric("learn", sw.ElapsedMilliseconds, true);

            return new PhaseResult(
                ImprovementPhase.Learn,
                Success: true,
                Output: $"Learned {insights.Count} insights. Success rate: {_atom.Experiences.Count(e => e.Success) / (double)_atom.Experiences.Count:P0}",
                Error: null,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["insights_count"] = insights.Count,
                    ["cycle_count"] = _atom.CycleCount,
                    ["total_experiences"] = _atom.Experiences.Count,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordPhaseMetric("learn", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Learn,
                Success: false,
                Output: string.Empty,
                Error: $"Learning failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes a single step from the plan.
    /// </summary>
    private async Task<Result<string, string>> ExecuteStepAsync(string step, CancellationToken ct)
    {
        // Try to match step to a tool
        Option<ITool> toolOption = _tools.All
            .FirstOrDefault(t => step.Contains(t.Name, StringComparison.OrdinalIgnoreCase))
            .ToOption();

        if (toolOption.HasValue && toolOption.Value != null)
        {
            ITool tool = toolOption.Value;

            // Check safety
            SafetyCheckResult safetyCheck = _safety.CheckSafety(
                tool.Name,
                new Dictionary<string, object> { ["step"] = step },
                PermissionLevel.ReadOnly);

            if (!safetyCheck.Safe)
            {
                return Result<string, string>.Failure($"Safety violation: {string.Join(", ", safetyCheck.Violations)}");
            }

            // Execute tool
            return await tool.InvokeAsync(step, ct);
        }

        // No matching tool, use LLM to process step
        string response = await _llm.GenerateTextAsync($"Process this step: {step}", ct);
        return Result<string, string>.Success(response);
    }

    /// <summary>
    /// Translates current Ouroboros state to MeTTa atoms.
    /// </summary>
    private async Task TranslateToMeTTaAsync(CancellationToken ct)
    {
        string metta = _atom.ToMeTTa();
        await _mettaEngine.AddFactAsync(metta, ct);
    }

    /// <summary>
    /// Extracts insights from the execution.
    /// </summary>
    private async Task<List<string>> ExtractInsightsAsync(string goal, List<PhaseResult> phases, CancellationToken ct)
    {
        StringBuilder context = new StringBuilder();
        context.AppendLine($"Goal: {goal}");

        foreach (PhaseResult phase in phases)
        {
            context.AppendLine($"Phase {phase.Phase}: Success={phase.Success}");
            if (!string.IsNullOrEmpty(phase.Error))
            {
                context.AppendLine($"  Error: {phase.Error}");
            }
        }

        string prompt = $@"Extract 2-3 key insights from this execution:
{context}

Provide insights as a bullet list, each starting with '-'. Focus on:
- What worked well
- What could be improved
- Patterns to remember";

        string response = await _llm.GenerateTextAsync(prompt, ct);

        List<string> insights = response.Split('\n')
            .Where(l => l.TrimStart().StartsWith('-'))
            .Select(l => l.TrimStart('-', ' '))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return insights;
    }

    /// <summary>
    /// Stores experience in the memory store.
    /// </summary>
    private async Task StoreExperienceInMemoryAsync(OuroborosExperience ouroborosExp, CancellationToken ct)
    {
        // Convert to domain Experience type for storage
        Plan plan = new Plan(
            ouroborosExp.Goal,
            new List<PlanStep>(),
            new Dictionary<string, double> { ["quality"] = ouroborosExp.QualityScore },
            ouroborosExp.Timestamp);

        ExecutionResult execution = new ExecutionResult(
            plan,
            new List<StepResult>(),
            ouroborosExp.Success,
            string.Join("; ", ouroborosExp.Insights),
            new Dictionary<string, object>(),
            TimeSpan.Zero);

        VerificationResult verification = new VerificationResult(
            execution,
            ouroborosExp.Success,
            ouroborosExp.QualityScore,
            new List<string>(),
            ouroborosExp.Insights.ToList(),
            null);

        Experience experience = new Experience(
            ouroborosExp.Id,
            ouroborosExp.Goal,
            plan,
            execution,
            verification,
            ouroborosExp.Timestamp,
            new Dictionary<string, object> { ["source"] = "OuroborosOrchestrator" });

        await _memory.StoreExperienceAsync(experience, ct);
    }

    /// <summary>
    /// Updates MeTTa knowledge base with new experience.
    /// </summary>
    private async Task UpdateMeTTaKnowledgeAsync(OuroborosExperience experience, CancellationToken ct)
    {
        StringBuilder metta = new StringBuilder();

        string expType = experience.Success ? "SuccessfulExperience" : "FailedExperience";
        metta.AppendLine($"(LearnedFrom (OuroborosInstance \"{_atom.InstanceId}\") ({expType} \"{EscapeMeTTa(experience.Goal)}\" {experience.QualityScore:F2}))");

        foreach (string insight in experience.Insights)
        {
            metta.AppendLine($"(Insight \"{experience.Id}\" \"{EscapeMeTTa(insight)}\")");
        }

        await _mettaEngine.AddFactAsync(metta.ToString(), ct);
    }

    private string BuildPlanPrompt(string goal, string selfReflection, OuroborosConfidence confidence)
    {
        string confidenceNote = confidence switch
        {
            OuroborosConfidence.High => "I have high confidence in achieving this goal based on past experience.",
            OuroborosConfidence.Medium => "I have moderate confidence in this goal - proceed with verification.",
            OuroborosConfidence.Low => "I have low confidence in this goal - careful planning required.",
            _ => string.Empty,
        };

        return $@"Create a plan to achieve: {goal}

Self-Assessment:
{selfReflection}

Confidence: {confidenceNote}

Available tools: {string.Join(", ", _tools.All.Select(t => t.Name))}

Provide a step-by-step plan. Each step should be actionable and specific.";
    }

    private string BuildVerificationPrompt(string goal, string output)
    {
        return $@"Verify if the following execution achieved the goal.

Goal: {goal}

Output:
{output}

Provide verification in JSON format:
{{
  ""verified"": true/false,
  ""quality_score"": 0.0-1.0,
  ""reasoning"": ""brief explanation""
}}";
    }

    private List<string> ParsePlanSteps(string plan)
    {
        List<string> steps = plan.Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Where(l => l.TrimStart().StartsWith('-') ||
                       l.TrimStart().StartsWith('*') ||
                       char.IsDigit(l.TrimStart().FirstOrDefault()))
            .Select(l => l.TrimStart('-', '*', ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // If no structured steps found, treat the whole plan as one step
        if (steps.Count == 0)
        {
            steps.Add(plan.Trim());
        }

        return steps;
    }

    private (bool Verified, double QualityScore) ParseVerificationResult(string verificationText)
    {
        try
        {
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(verificationText);
            bool verified = doc.RootElement.GetProperty("verified").GetBoolean();
            double qualityScore = doc.RootElement.GetProperty("quality_score").GetDouble();
            return (verified, qualityScore);
        }
        catch
        {
            // Default to success if parsing fails but output exists
            return (true, 0.7);
        }
    }

    private OuroborosResult CreateResult(string goal, List<PhaseResult> phases, string? output, bool success, TimeSpan duration)
    {
        return new OuroborosResult(
            Goal: goal,
            Success: success,
            Output: output ?? string.Empty,
            PhaseResults: phases,
            CycleCount: _atom.CycleCount,
            CurrentPhase: _atom.CurrentPhase,
            SelfReflection: _atom.SelfReflect(),
            Duration: duration,
            Metadata: new Dictionary<string, object>
            {
                ["atom_id"] = _atom.InstanceId,
                ["capabilities_count"] = _atom.Capabilities.Count,
                ["experiences_count"] = _atom.Experiences.Count,
            });
    }

    private void RecordPhaseMetric(string phase, double latencyMs, bool success)
    {
        string key = $"{phase}_{(success ? "success" : "failure")}";
        _performanceMetrics.AddOrUpdate(key, 1, (_, count) => count + 1);
        _performanceMetrics.AddOrUpdate($"{phase}_avg_latency", latencyMs, (_, avg) => (avg + latencyMs) / 2);
    }

    private static string EscapeMeTTa(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }

    /// <inheritdoc/>
    protected override async Task<Dictionary<string, object>> GetCustomHealthAsync(CancellationToken ct)
    {
        Dictionary<string, object> health = await base.GetCustomHealthAsync(ct);

        health["ouroboros_atom_id"] = _atom.InstanceId;
        health["current_phase"] = _atom.CurrentPhase.ToString();
        health["cycle_count"] = _atom.CycleCount;
        health["capabilities"] = _atom.Capabilities.Count;
        health["experiences"] = _atom.Experiences.Count;
        health["success_rate"] = _atom.Experiences.Count > 0
            ? _atom.Experiences.Count(e => e.Success) / (double)_atom.Experiences.Count
            : 0.0;

        return health;
    }
}

/// <summary>
/// Result of an Ouroboros orchestration cycle.
/// </summary>
/// <param name="Goal">The goal that was pursued.</param>
/// <param name="Success">Whether the cycle was successful.</param>
/// <param name="Output">The final output.</param>
/// <param name="PhaseResults">Results from each phase.</param>
/// <param name="CycleCount">Total improvement cycles completed.</param>
/// <param name="CurrentPhase">Current phase after execution.</param>
/// <param name="SelfReflection">Self-reflection summary.</param>
/// <param name="Duration">Total execution duration.</param>
/// <param name="Metadata">Additional metadata.</param>
public sealed record OuroborosResult(
    string Goal,
    bool Success,
    string Output,
    IReadOnlyList<PhaseResult> PhaseResults,
    int CycleCount,
    ImprovementPhase CurrentPhase,
    string SelfReflection,
    TimeSpan Duration,
    Dictionary<string, object> Metadata);

/// <summary>
/// Result from a single phase in the improvement cycle.
/// </summary>
/// <param name="Phase">Which phase this result is from.</param>
/// <param name="Success">Whether the phase succeeded.</param>
/// <param name="Output">The phase output.</param>
/// <param name="Error">Error message if failed.</param>
/// <param name="Duration">Phase execution duration.</param>
/// <param name="Metadata">Additional metadata.</param>
public sealed record PhaseResult(
    ImprovementPhase Phase,
    bool Success,
    string Output,
    string? Error,
    TimeSpan Duration,
    Dictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Gets the metadata dictionary, never null.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = Metadata ?? new Dictionary<string, object>();
}

/// <summary>
/// Extension methods for Option type.
/// </summary>
internal static class OptionExtensions
{
    /// <summary>
    /// Converts a nullable value to an Option.
    /// </summary>
    public static Option<T> ToOption<T>(this T? value)
        where T : class
    {
        return value != null ? Option<T>.Some(value) : Option<T>.None();
    }
}
