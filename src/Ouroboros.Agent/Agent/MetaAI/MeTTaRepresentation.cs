#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-AI Layer v3.0 - MeTTa-First Representation Layer
// Translates orchestrator concepts to MeTTa symbolic atoms
// Now with Laws of Form integration for certainty tracking
// ==========================================================

using System.Text;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Tools.MeTTa;
using Unit = Ouroboros.Tools.MeTTa.Unit;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Translates orchestrator concepts (plans, steps, tools, state) into MeTTa symbolic representation.
/// Enables symbolic reasoning over orchestration flow.
/// Supports Laws of Form integration for distinction-gated planning.
/// </summary>
public sealed class MeTTaRepresentation
{
    private readonly IMeTTaEngine _engine;
    private readonly FormMeTTaBridge? _formBridge;
    private readonly Dictionary<string, string> _stateAtoms = new();
    private readonly Dictionary<string, Form> _stepCertainty = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaRepresentation"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine.</param>
    /// <param name="formBridge">Optional Laws of Form bridge for certainty tracking.</param>
    public MeTTaRepresentation(IMeTTaEngine engine, FormMeTTaBridge? formBridge = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _formBridge = formBridge;
    }

    /// <summary>
    /// Gets whether Laws of Form reasoning is available.
    /// </summary>
    public bool FormReasoningEnabled => _formBridge != null;

    /// <summary>
    /// Gets the certainty (Form) for a step if tracked.
    /// </summary>
    /// <param name="stepId">The step identifier.</param>
    /// <returns>The Form representing certainty, or null if not tracked.</returns>
    public Form? GetStepCertainty(string stepId)
    {
        return _stepCertainty.TryGetValue(stepId, out var form) ? form : null;
    }

    /// <summary>
    /// Marks a step as certain (draws a distinction).
    /// </summary>
    /// <param name="stepId">The step identifier.</param>
    /// <returns>The resulting Form.</returns>
    public Form MarkStepCertain(string stepId)
    {
        var form = _formBridge?.DrawDistinction(stepId) ?? Form.Mark;
        _stepCertainty[stepId] = form;
        return form;
    }

    /// <summary>
    /// Marks a step as uncertain (creates re-entry/imaginary state).
    /// </summary>
    /// <param name="stepId">The step identifier.</param>
    /// <returns>The resulting Form.</returns>
    public Form MarkStepUncertain(string stepId)
    {
        var form = _formBridge?.CreateReEntry(stepId) ?? Form.Imaginary;
        _stepCertainty[stepId] = form;
        return form;
    }

    /// <summary>
    /// Translates a plan into MeTTa atoms and adds them to the knowledge base.
    /// If FormBridge is available, also tracks certainty for each step.
    /// </summary>
    public async Task<Result<Unit, string>> TranslatePlanAsync(Plan plan, CancellationToken ct = default)
    {
        try
        {
            StringBuilder sb = new StringBuilder();

            // Add plan goal as a fact
            string planId = $"plan_{Guid.NewGuid():N}";
            sb.AppendLine($"(goal {planId} \"{EscapeMeTTa(plan.Goal)}\")");

            // Add each step as a fact with ordering
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                PlanStep step = plan.Steps[i];
                string stepId = $"step_{i}";

                sb.AppendLine($"(step {planId} {stepId} {i} \"{EscapeMeTTa(step.Action)}\")");
                sb.AppendLine($"(expected {stepId} \"{EscapeMeTTa(step.ExpectedOutcome)}\")");
                sb.AppendLine($"(confidence {stepId} {step.ConfidenceScore:F2})");

                // Track certainty using Laws of Form if available
                if (_formBridge != null)
                {
                    // High confidence (>= 0.8) = Mark (certain), low confidence = Imaginary (uncertain)
                    if (step.ConfidenceScore >= 0.8)
                    {
                        MarkStepCertain(stepId);
                        sb.AppendLine($"(certainty {stepId} Mark)");
                    }
                    else if (step.ConfidenceScore <= 0.2)
                    {
                        _stepCertainty[stepId] = Form.Void; // Negated/unlikely
                        sb.AppendLine($"(certainty {stepId} Void)");
                    }
                    else
                    {
                        MarkStepUncertain(stepId);
                        sb.AppendLine($"(certainty {stepId} Imaginary)");
                    }
                }

                // Add step parameters
                foreach (KeyValuePair<string, object> param in step.Parameters)
                {
                    string value = param.Value?.ToString() ?? "null";
                    sb.AppendLine($"(param {stepId} \"{EscapeMeTTa(param.Key)}\" \"{EscapeMeTTa(value)}\")");
                }

                // Add ordering constraint
                if (i > 0)
                {
                    sb.AppendLine($"(before step_{i - 1} {stepId})");
                }
            }

            // Add temporal constraint
            sb.AppendLine($"(created {planId} {plan.CreatedAt.Ticks})");

            // Store the plan ID for reference
            _stateAtoms[plan.Goal] = planId;

            // Add all facts to MeTTa
            Result<Unit, string> factResult = await _engine.AddFactAsync(sb.ToString(), ct);
            return factResult.MapError(_ => "Failed to add plan facts to MeTTa");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Plan translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates execution state into MeTTa atoms.
    /// </summary>
    public async Task<Result<Unit, string>> TranslateExecutionStateAsync(
        ExecutionResult execution,
        CancellationToken ct = default)
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            string execId = $"exec_{Guid.NewGuid():N}";

            sb.AppendLine($"(execution {execId} {(execution.Success ? "success" : "failure")})");
            sb.AppendLine($"(duration {execId} {execution.Duration.TotalSeconds:F2})");

            // Add step results
            for (int i = 0; i < execution.StepResults.Count; i++)
            {
                StepResult stepResult = execution.StepResults[i];
                string resultId = $"result_{i}";

                sb.AppendLine($"(step-result {execId} {resultId} {(stepResult.Success ? "success" : "failure")})");

                if (!string.IsNullOrEmpty(stepResult.Error))
                {
                    sb.AppendLine($"(error {resultId} \"{EscapeMeTTa(stepResult.Error)}\")");
                }

                // Add observed state
                foreach (KeyValuePair<string, object> state in stepResult.ObservedState)
                {
                    string value = state.Value?.ToString() ?? "null";
                    sb.AppendLine($"(observed {resultId} \"{EscapeMeTTa(state.Key)}\" \"{EscapeMeTTa(value)}\")");
                }
            }

            Result<Unit, string> result = await _engine.AddFactAsync(sb.ToString(), ct);
            return result.MapError(_ => "Failed to add execution state to MeTTa");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Execution state translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates tool registry into MeTTa atoms for reasoning about available tools.
    /// </summary>
    public async Task<Result<Unit, string>> TranslateToolsAsync(
        ToolRegistry tools,
        CancellationToken ct = default)
    {
        try
        {
            StringBuilder sb = new StringBuilder();

            foreach (ITool tool in tools.All)
            {
                string toolId = $"tool_{tool.Name.Replace("_", "-")}";
                sb.AppendLine($"(tool {toolId} \"{EscapeMeTTa(tool.Name)}\")");
                sb.AppendLine($"(tool-desc {toolId} \"{EscapeMeTTa(tool.Description)}\")");

                // Add capability inference rules
                if (tool.Name.Contains("search") || tool.Name.Contains("query"))
                {
                    sb.AppendLine($"(capability {toolId} information-retrieval)");
                }
                if (tool.Name.Contains("write") || tool.Name.Contains("create"))
                {
                    sb.AppendLine($"(capability {toolId} content-creation)");
                }
                if (tool.Name.Contains("metta"))
                {
                    sb.AppendLine($"(capability {toolId} symbolic-reasoning)");
                }
            }

            Result<Unit, string> result = await _engine.AddFactAsync(sb.ToString(), ct);
            return result.MapError(_ => "Failed to add tool facts to MeTTa");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Tool translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Queries MeTTa for valid next nodes (steps/tools) given current state.
    /// </summary>
    public async Task<Result<List<NextNodeCandidate>, string>> QueryNextNodesAsync(
        string currentStepId,
        Dictionary<string, object> context,
        CancellationToken ct = default)
    {
        try
        {
            // Build query to find valid next nodes
            string query = $@"!(match &self
                (and
                    (step $plan {currentStepId} $order $action)
                    (step $plan $next-step $next-order $next-action)
                    (> $next-order $order)
                )
                (cons $next-step $next-action))";

            Result<string, string> queryResult = await _engine.ExecuteQueryAsync(query, ct);

            return queryResult.Match(
                success => ParseNextNodeCandidates(success),
                error => Result<List<NextNodeCandidate>, string>.Failure($"Next node query failed: {error}")
            );
        }
        catch (Exception ex)
        {
            return Result<List<NextNodeCandidate>, string>.Failure($"Query error: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a constraint rule to the knowledge base.
    /// </summary>
    public async Task<Result<Unit, string>> AddConstraintAsync(
        string constraint,
        CancellationToken ct = default)
    {
        Result<Unit, string> result = await _engine.AddFactAsync(constraint, ct);
        return result.Match(
            _ => Result<Unit, string>.Success(Unit.Value),
            error => Result<Unit, string>.Failure($"Failed to add constraint: {constraint} - {error}")
        );
    }

    /// <summary>
    /// Queries for tool recommendations based on goal and context.
    /// </summary>
    public async Task<Result<List<string>, string>> QueryToolsForGoalAsync(
        string goal,
        CancellationToken ct = default)
    {
        string query = $@"!(match &self
            (and
                (goal $plan ""{EscapeMeTTa(goal)}"")
                (capability $tool $cap)
            )
            $tool)";

        Result<string, string> result = await _engine.ExecuteQueryAsync(query, ct);

        return result.Match(
            success => Result<List<string>, string>.Success(ParseToolList(success)),
            error => Result<List<string>, string>.Failure($"Tool query failed: {error}")
        );
    }

    private List<NextNodeCandidate> ParseNextNodeCandidates(string mettaOutput)
    {
        List<NextNodeCandidate> candidates = new List<NextNodeCandidate>();

        // Parse MeTTa output format: (cons step_id action)
        string[] lines = mettaOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                line,
                @"\(cons\s+(\S+)\s+""?([^""]+)""?\)"
            );

            if (match.Success)
            {
                candidates.Add(new NextNodeCandidate(
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    1.0 // Default confidence
                ));
            }
        }

        return candidates;
    }

    private List<string> ParseToolList(string mettaOutput)
    {
        List<string> tools = new List<string>();
        string[] lines = mettaOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("tool_"))
            {
                tools.Add(trimmed.Replace("tool_", "").Replace("-", "_"));
            }
        }

        return tools;
    }

    private string EscapeMeTTa(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}

/// <summary>
/// Represents a candidate next node in the execution graph.
/// </summary>
public sealed record NextNodeCandidate(
    string NodeId,
    string Action,
    double Confidence
);
