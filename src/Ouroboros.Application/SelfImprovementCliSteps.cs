#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Self-Improvement CLI Steps
// DSL tokens for autonomous learning, capability tracking, and self-execution
// ==========================================================

using System.Text;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Network;
using Ouroboros.Domain.Events;

namespace Ouroboros.Application;

/// <summary>
/// DSL steps for self-improvement, capability tracking, and autonomous execution.
/// </summary>
public static class SelfImprovementCliSteps
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CAPABILITY TRACKING STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tracks capability usage during pipeline execution.
    /// Enables self-improvement by monitoring success/failure rates.
    /// Usage: TrackCapability('planning') | ...rest of pipeline...
    /// </summary>
    [PipelineToken("TrackCapability", "Capability")]
    public static Step<CliPipelineState, CliPipelineState> TrackCapability(string? args = null)
    {
        var capabilityName = CliSteps.ParseString(args ?? "general");
        return TrackedStep.Wrap(
            async s =>
            {
                var startTime = DateTime.UtcNow;

                // Store tracking info in branch metadata
                s.Branch = s.Branch.WithIngestEvent(
                    $"capability:start:{capabilityName}",
                    new[] { startTime.ToString("O") });

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Tracking capability: {capabilityName}");
                }

                return s;
            },
            "TrackCapability",
            new[] { "Capability" },
            nameof(SelfImprovementCliSteps),
            "Tracks capability usage for self-improvement",
            args);
    }

    /// <summary>
    /// Completes capability tracking and records success/failure.
    /// Usage: ...pipeline... | EndCapability('success') or EndCapability('failure')
    /// </summary>
    [PipelineToken("EndCapability", "CompleteCapability")]
    public static Step<CliPipelineState, CliPipelineState> EndCapability(string? args = null)
    {
        var success = args?.ToLowerInvariant().Contains("success") ?? true;
        return TrackedStep.Wrap(
            async s =>
            {
                var endTime = DateTime.UtcNow;

                s.Branch = s.Branch.WithIngestEvent(
                    $"capability:end:{(success ? "success" : "failure")}",
                    new[] { endTime.ToString("O") });

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Capability tracking completed: {(success ? "SUCCESS" : "FAILURE")}");
                }

                return s;
            },
            "EndCapability",
            new[] { "CompleteCapability" },
            nameof(SelfImprovementCliSteps),
            "Completes capability tracking",
            args);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // REIFICATION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enables full network state reification for the pipeline.
    /// All subsequent steps will be tracked in the MerkleDag.
    /// Usage: Reify | ...rest of pipeline...
    /// </summary>
    [PipelineToken("Reify", "EnableReification", "TrackNetwork")]
    public static Step<CliPipelineState, CliPipelineState> Reify(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                // Enable network tracking if not already active
                s = s.WithNetworkTracking();

                if (s.Trace)
                {
                    Console.WriteLine("[trace] Network reification enabled - all steps will be tracked in MerkleDag");
                }

                return s;
            },
            "Reify",
            new[] { "EnableReification", "TrackNetwork" },
            nameof(SelfImprovementCliSteps),
            "Enables network state reification",
            args);

    /// <summary>
    /// Creates a checkpoint in the network state for replay/debugging.
    /// Usage: Checkpoint('milestone-name')
    /// </summary>
    [PipelineToken("Checkpoint", "SaveState")]
    public static Step<CliPipelineState, CliPipelineState> Checkpoint(string? args = null)
    {
        var checkpointName = CliSteps.ParseString(args ?? $"checkpoint-{DateTime.UtcNow:HHmmss}");
        return TrackedStep.Wrap(
            async s =>
            {
                // Update network state with checkpoint
                if (s.NetworkTracker != null)
                {
                    var snapshot = s.NetworkTracker.CreateSnapshot();
                    s.Branch = s.Branch.WithIngestEvent(
                        $"checkpoint:{checkpointName}",
                        new[] { snapshot.TotalNodes.ToString(), snapshot.TotalTransitions.ToString() });
                }

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Checkpoint created: {checkpointName}");
                }

                return s;
            },
            "Checkpoint",
            new[] { "SaveState" },
            nameof(SelfImprovementCliSteps),
            "Creates a checkpoint in the network state",
            args);
    }

    /// <summary>
    /// Shows the current network state summary.
    /// Usage: NetworkStatus
    /// </summary>
    [PipelineToken("NetworkStatus", "DagStatus")]
    public static Step<CliPipelineState, CliPipelineState> NetworkStatus(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                var summary = s.GetNetworkStateSummary();

                if (summary != null)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n=== Network State ===");
                    Console.WriteLine(summary);
                    Console.WriteLine("====================\n");
                    Console.ResetColor();

                    s.Output = summary;
                }
                else
                {
                    Console.WriteLine("[info] Network tracking not enabled. Use 'Reify' first.");
                }

                return s;
            },
            "NetworkStatus",
            new[] { "DagStatus" },
            nameof(SelfImprovementCliSteps),
            "Shows the current network state summary",
            args);

    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-IMPROVEMENT STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Evaluates the current pipeline output and suggests improvements.
    /// Uses LLM to analyze output quality and provide feedback.
    /// Usage: SelfEvaluate or SelfEvaluate('criteria')
    /// </summary>
    [PipelineToken("SelfEvaluate", "Evaluate")]
    public static Step<CliPipelineState, CliPipelineState> SelfEvaluate(string? args = null)
    {
        var criteria = CliSteps.ParseString(args ?? "quality, completeness, accuracy");
        return TrackedStep.Wrap(
            async s =>
            {
                var currentOutput = s.Output ?? s.Context;

                if (string.IsNullOrWhiteSpace(currentOutput))
                {
                    if (s.Trace)
                    {
                        Console.WriteLine("[trace] No output to evaluate");
                    }
                    return s;
                }

                var prompt = $@"Evaluate the following output based on these criteria: {criteria}

OUTPUT TO EVALUATE:
{currentOutput}

Provide a structured evaluation with:
1. Overall score (1-10)
2. Strengths
3. Weaknesses
4. Specific improvement suggestions

Be concise but thorough.";

                var evaluation = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    "self-evaluate:complete",
                    new[] { criteria, evaluation[..Math.Min(200, evaluation.Length)] });

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n=== Self-Evaluation ===");
                    Console.WriteLine(evaluation);
                    Console.WriteLine("=======================\n");
                    Console.ResetColor();
                }

                // Append evaluation to output
                s.Output = $"{currentOutput}\n\n--- Self-Evaluation ---\n{evaluation}";

                return s;
            },
            "SelfEvaluate",
            new[] { "Evaluate" },
            nameof(SelfImprovementCliSteps),
            "Evaluates the current output and suggests improvements",
            args);
    }

    /// <summary>
    /// Attempts to improve the current output based on self-evaluation.
    /// Usage: SelfImprove or SelfImprove('2') for multiple iterations
    /// </summary>
    [PipelineToken("SelfImprove", "Improve")]
    public static Step<CliPipelineState, CliPipelineState> SelfImprove(string? args = null)
    {
        var iterations = 1;
        if (int.TryParse(CliSteps.ParseString(args ?? "1"), out var parsed))
        {
            iterations = Math.Max(1, Math.Min(5, parsed)); // Clamp 1-5
        }

        return TrackedStep.Wrap(
            async s =>
            {
                var currentOutput = s.Output ?? s.Context;
                if (string.IsNullOrWhiteSpace(currentOutput))
                {
                    return s;
                }

                for (int i = 0; i < iterations; i++)
                {
                    var prompt = $@"Improve the following output. Make it clearer, more accurate, and more complete.
Keep the same general structure but enhance the quality.

CURRENT OUTPUT:
{currentOutput}

IMPROVED VERSION:";

                    var improved = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                    s.Branch = s.Branch.WithIngestEvent(
                        $"self-improve:iteration:{i + 1}",
                        new[] { currentOutput.Length.ToString(), improved.Length.ToString() });

                    currentOutput = improved;

                    if (s.Trace)
                    {
                        Console.WriteLine($"[trace] Self-improvement iteration {i + 1} complete");
                    }
                }

                s.Output = currentOutput;
                return s;
            },
            "SelfImprove",
            new[] { "Improve" },
            nameof(SelfImprovementCliSteps),
            "Iteratively improves the current output",
            args);
    }

    /// <summary>
    /// Learns from the current pipeline execution by extracting patterns.
    /// Usage: Learn('topic-name')
    /// </summary>
    [PipelineToken("Learn", "ExtractLearning")]
    public static Step<CliPipelineState, CliPipelineState> Learn(string? args = null)
    {
        var topic = CliSteps.ParseString(args ?? "general");
        return TrackedStep.Wrap(
            async s =>
            {
                var context = s.Context;
                var output = s.Output;

                var prompt = $@"Extract key learnings from this pipeline execution.

TOPIC: {topic}
CONTEXT: {context}
OUTPUT: {output}

Provide:
1. Key insights (bullet points)
2. Patterns observed
3. Potential improvements for next time
4. Knowledge to remember

Format as structured text for storage.";

                var learnings = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    $"learn:{topic}",
                    new[] { learnings[..Math.Min(500, learnings.Length)] });

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n=== Learnings Extracted ===");
                    Console.WriteLine(learnings);
                    Console.WriteLine("===========================\n");
                    Console.ResetColor();
                }

                // Store learnings in output for potential persistence
                s.Output = $"{s.Output}\n\n--- Learnings ---\n{learnings}";

                return s;
            },
            "Learn",
            new[] { "ExtractLearning" },
            nameof(SelfImprovementCliSteps),
            "Extracts learnings from the pipeline execution",
            args);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUTONOMOUS EXECUTION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Plans a complex task by decomposing it into sub-steps.
    /// Usage: Plan('task description')
    /// </summary>
    [PipelineToken("Plan", "Decompose")]
    public static Step<CliPipelineState, CliPipelineState> Plan(string? args = null)
    {
        var task = CliSteps.ParseString(args ?? "");
        return TrackedStep.Wrap(
            async s =>
            {
                var taskToUse = string.IsNullOrWhiteSpace(task) ? s.Query : task;

                if (string.IsNullOrWhiteSpace(taskToUse))
                {
                    return s;
                }

                var prompt = $@"Decompose this task into a step-by-step plan:

TASK: {taskToUse}

Provide a numbered list of concrete steps that could be executed as a pipeline.
Use DSL-style notation where possible (e.g., Set('topic') | UseDraft | UseCritique).
Each step should be clear and actionable.";

                var plan = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    "plan:created",
                    new[] { taskToUse, plan[..Math.Min(300, plan.Length)] });

                s.Output = plan;
                s.Context = plan;

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n=== Execution Plan ===");
                    Console.WriteLine(plan);
                    Console.WriteLine("=====================\n");
                    Console.ResetColor();
                }

                return s;
            },
            "Plan",
            new[] { "Decompose" },
            nameof(SelfImprovementCliSteps),
            "Plans a complex task by decomposing into steps",
            args);
    }

    /// <summary>
    /// Executes a dynamically generated plan step by step.
    /// Usage: ExecutePlan
    /// </summary>
    [PipelineToken("ExecutePlan", "RunPlan")]
    public static Step<CliPipelineState, CliPipelineState> ExecutePlan(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                var plan = s.Context;

                if (string.IsNullOrWhiteSpace(plan))
                {
                    Console.WriteLine("[error] No plan available. Use 'Plan' first.");
                    return s;
                }

                // Extract DSL commands from plan
                var dslPattern = new System.Text.RegularExpressions.Regex(
                    @"([A-Z][a-zA-Z]+(?:\([^)]*\))?(?:\s*\|\s*[A-Z][a-zA-Z]+(?:\([^)]*\))?)*)",
                    System.Text.RegularExpressions.RegexOptions.Multiline);

                var matches = dslPattern.Matches(plan);
                var results = new StringBuilder();

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var dsl = match.Value.Trim();
                    if (dsl.Length > 3 && dsl.Contains('|'))
                    {
                        try
                        {
                            if (s.Trace)
                            {
                                Console.WriteLine($"[trace] Executing DSL: {dsl}");
                            }

                            var step = PipelineDsl.Build(dsl);
                            s = await step(s);
                            results.AppendLine($"✓ {dsl}");
                        }
                        catch (Exception ex)
                        {
                            results.AppendLine($"✗ {dsl}: {ex.Message}");
                        }
                    }
                }

                s.Branch = s.Branch.WithIngestEvent(
                    "plan:executed",
                    new[] { matches.Count.ToString() });

                s.Output = results.ToString();

                return s;
            },
            "ExecutePlan",
            new[] { "RunPlan" },
            nameof(SelfImprovementCliSteps),
            "Executes a dynamically generated plan",
            args);

    /// <summary>
    /// Reflects on the pipeline execution and generates insights.
    /// Usage: Reflect
    /// </summary>
    [PipelineToken("Reflect", "Introspect")]
    public static Step<CliPipelineState, CliPipelineState> Reflect(string? args = null)
        => TrackedStep.Wrap(
            async s =>
            {
                // Gather execution history
                var events = s.Branch.Events.TakeLast(20).ToList();
                var eventSummary = string.Join("\n", events.Select(e => $"- {e.GetType().Name}"));

                var prompt = $@"Reflect on this pipeline execution:

EXECUTION EVENTS:
{eventSummary}

TOPIC: {s.Topic}
QUERY: {s.Query}
OUTPUT LENGTH: {s.Output?.Length ?? 0} chars

Provide introspective analysis:
1. What went well?
2. What could be improved?
3. What patterns emerged?
4. Confidence in the output (1-10)?";

                var reflection = await s.Llm.InnerModel.GenerateTextAsync(prompt);

                s.Branch = s.Branch.WithIngestEvent(
                    "reflect:complete",
                    new[] { reflection[..Math.Min(200, reflection.Length)] });

                if (s.Trace)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("\n=== Reflection ===");
                    Console.WriteLine(reflection);
                    Console.WriteLine("==================\n");
                    Console.ResetColor();
                }

                s.Output = $"{s.Output}\n\n--- Reflection ---\n{reflection}";

                return s;
            },
            "Reflect",
            new[] { "Introspect" },
            nameof(SelfImprovementCliSteps),
            "Reflects on the pipeline execution",
            args);

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBINED SELF-IMPROVING PIPELINE STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Complete self-improving reasoning cycle: Draft → Critique → Improve → Evaluate → Learn.
    /// Usage: SelfImprovingCycle('topic')
    /// </summary>
    [PipelineToken("SelfImprovingCycle", "SIC")]
    public static Step<CliPipelineState, CliPipelineState> SelfImprovingCycle(string? args = null)
    {
        var topic = CliSteps.ParseString(args ?? "");
        return TrackedStep.Wrap(
            async s =>
            {
                s.Topic = string.IsNullOrWhiteSpace(topic) ? s.Topic : topic;

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Starting self-improving cycle for: {s.Topic}");
                }

                // Enable reification
                s = s.WithNetworkTracking();

                // Build the self-improving pipeline
                var pipeline = new StepDefinition<CliPipelineState, CliPipelineState>(state => state)
                    | TrackCapability("reasoning")
                    | ReasoningCliSteps.UseDraft()
                    | Checkpoint("draft-complete")
                    | ReasoningCliSteps.UseCritique()
                    | Checkpoint("critique-complete")
                    | ReasoningCliSteps.UseImprove()
                    | Checkpoint("improve-complete")
                    | SelfEvaluate("clarity, accuracy, completeness")
                    | Learn(s.Topic)
                    | EndCapability("success")
                    | Reflect();

                s = await pipeline.Build()(s);

                return s;
            },
            "SelfImprovingCycle",
            new[] { "SIC" },
            nameof(SelfImprovementCliSteps),
            "Complete self-improving reasoning cycle",
            args);
    }

    /// <summary>
    /// Autonomous problem-solving with planning and execution.
    /// Usage: AutoSolve('problem description')
    /// </summary>
    [PipelineToken("AutoSolve", "Autonomous")]
    public static Step<CliPipelineState, CliPipelineState> AutoSolve(string? args = null)
    {
        var problem = CliSteps.ParseString(args ?? "");
        return TrackedStep.Wrap(
            async s =>
            {
                s.Query = string.IsNullOrWhiteSpace(problem) ? s.Query : problem;

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Starting autonomous problem-solving: {s.Query}");
                }

                // Enable reification
                s = s.WithNetworkTracking();

                // Phase 1: Planning
                s = await Plan(s.Query)(s);
                var plan = s.Output;

                // Phase 2: Self-improving execution
                s = await SelfImprovingCycle(s.Query)(s);

                // Phase 3: Final reflection and learning
                s.Context = $"Plan:\n{plan}\n\nExecution Result:\n{s.Output}";
                s = await Reflect()(s);

                return s;
            },
            "AutoSolve",
            new[] { "Autonomous" },
            nameof(SelfImprovementCliSteps),
            "Autonomous problem-solving with planning and execution",
            args);
    }}
