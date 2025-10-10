// ==========================================================
// Meta-AI Layer v3.0 - MeTTa-First Representation Layer
// Translates orchestrator concepts to MeTTa symbolic atoms
// ==========================================================

using System.Text;
using LangChainPipeline.Tools.MeTTa;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Translates orchestrator concepts (plans, steps, tools, state) into MeTTa symbolic representation.
/// Enables symbolic reasoning over orchestration flow.
/// </summary>
public sealed class MeTTaRepresentation
{
    private readonly IMeTTaEngine _engine;
    private readonly Dictionary<string, string> _stateAtoms = new();

    public MeTTaRepresentation(IMeTTaEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Translates a plan into MeTTa atoms and adds them to the knowledge base.
    /// </summary>
    public async Task<Result<Unit, string>> TranslatePlanAsync(Plan plan, CancellationToken ct = default)
    {
        try
        {
            var sb = new StringBuilder();

            // Add plan goal as a fact
            var planId = $"plan_{Guid.NewGuid():N}";
            sb.AppendLine($"(goal {planId} \"{EscapeMeTTa(plan.Goal)}\")");

            // Add each step as a fact with ordering
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                var stepId = $"step_{i}";

                sb.AppendLine($"(step {planId} {stepId} {i} \"{EscapeMeTTa(step.Action)}\")");
                sb.AppendLine($"(expected {stepId} \"{EscapeMeTTa(step.ExpectedOutcome)}\")");
                sb.AppendLine($"(confidence {stepId} {step.ConfidenceScore:F2})");

                // Add step parameters
                foreach (var param in step.Parameters)
                {
                    var value = param.Value?.ToString() ?? "null";
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
            var factResult = await _engine.AddFactAsync(sb.ToString(), ct);
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
            var sb = new StringBuilder();
            var execId = $"exec_{Guid.NewGuid():N}";

            sb.AppendLine($"(execution {execId} {(execution.Success ? "success" : "failure")})");
            sb.AppendLine($"(duration {execId} {execution.Duration.TotalSeconds:F2})");

            // Add step results
            for (int i = 0; i < execution.StepResults.Count; i++)
            {
                var stepResult = execution.StepResults[i];
                var resultId = $"result_{i}";

                sb.AppendLine($"(step-result {execId} {resultId} {(stepResult.Success ? "success" : "failure")})");

                if (!string.IsNullOrEmpty(stepResult.Error))
                {
                    sb.AppendLine($"(error {resultId} \"{EscapeMeTTa(stepResult.Error)}\")");
                }

                // Add observed state
                foreach (var state in stepResult.ObservedState)
                {
                    var value = state.Value?.ToString() ?? "null";
                    sb.AppendLine($"(observed {resultId} \"{EscapeMeTTa(state.Key)}\" \"{EscapeMeTTa(value)}\")");
                }
            }

            var result = await _engine.AddFactAsync(sb.ToString(), ct);
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
            var sb = new StringBuilder();

            foreach (var tool in tools.All)
            {
                var toolId = $"tool_{tool.Name.Replace("_", "-")}";
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

            var result = await _engine.AddFactAsync(sb.ToString(), ct);
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
            var query = $@"!(match &self 
                (and 
                    (step $plan {currentStepId} $order $action)
                    (step $plan $next-step $next-order $next-action)
                    (> $next-order $order)
                )
                (cons $next-step $next-action))";

            var queryResult = await _engine.ExecuteQueryAsync(query, ct);

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
        var result = await _engine.AddFactAsync(constraint, ct);
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
        var query = $@"!(match &self 
            (and 
                (goal $plan ""{EscapeMeTTa(goal)}"")
                (capability $tool $cap)
            )
            $tool)";

        var result = await _engine.ExecuteQueryAsync(query, ct);

        return result.Match(
            success => Result<List<string>, string>.Success(ParseToolList(success)),
            error => Result<List<string>, string>.Failure($"Tool query failed: {error}")
        );
    }

    private List<NextNodeCandidate> ParseNextNodeCandidates(string mettaOutput)
    {
        var candidates = new List<NextNodeCandidate>();

        // Parse MeTTa output format: (cons step_id action)
        var lines = mettaOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
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
        var tools = new List<string>();
        var lines = mettaOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
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
