#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// NextNode Tool - Symbolic Next-Step Enumeration
// Uses MeTTa to determine valid next nodes in execution
// ==========================================================

using System.Text.Json;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tools;

/// <summary>
/// Tool for enumerating valid next execution nodes using symbolic MeTTa reasoning.
/// Translates current plan/state into MeTTa, queries for valid next steps, and updates state.
/// </summary>
public sealed class NextNodeTool : ITool
{
    private readonly IMeTTaEngine _engine;
    private readonly MeTTaRepresentation _representation;
    private readonly ToolRegistry _registry;

    /// <inheritdoc />
    public string Name => "next_node";

    /// <inheritdoc />
    public string Description =>
        "Enumerate valid next execution nodes (steps/tools/subplans) using symbolic reasoning. " +
        "Translates current plan and state into MeTTa facts, queries for valid successors, " +
        "and returns candidates with confidence scores.";

    /// <inheritdoc />
    public string? JsonSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""current_step_id"": {
                ""type"": ""string"",
                ""description"": ""The ID of the current step in execution""
            },
            ""plan_goal"": {
                ""type"": ""string"",
                ""description"": ""The goal of the current plan""
            },
            ""context"": {
                ""type"": ""object"",
                ""description"": ""Current execution context (state, variables, etc.)""
            },
            ""constraints"": {
                ""type"": ""array"",
                ""description"": ""Optional MeTTa constraint rules to apply"",
                ""items"": { 
                    ""type"": ""string"" 
                }
            }
        },
        ""required"": [""current_step_id"", ""plan_goal""]
    }";

    public NextNodeTool(IMeTTaEngine engine, ToolRegistry registry)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _representation = new MeTTaRepresentation(engine);
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // Parse input
            Result<NextNodeRequest, string> request = ParseInput(input);
            if (request.IsFailure)
                return Result<string, string>.Failure(request.Error);

            NextNodeRequest req = request.Value;

            // Add any constraint rules
            if (req.Constraints != null)
            {
                foreach (string constraint in req.Constraints)
                {
                    await _representation.AddConstraintAsync(constraint, ct);
                }
            }

            // Query for next nodes
            Result<List<NextNodeCandidate>, string> nextNodes = await _representation.QueryNextNodesAsync(
                req.CurrentStepId,
                req.Context ?? new Dictionary<string, object>(),
                ct
            );

            if (nextNodes.IsFailure)
                return Result<string, string>.Failure(nextNodes.Error);

            // Query for recommended tools
            Result<List<string>, string> toolsResult = await _representation.QueryToolsForGoalAsync(req.PlanGoal, ct);
            List<string> recommendedTools = toolsResult.GetValueOrDefault(new List<string>());

            // Build response
            NextNodeResponse response = new NextNodeResponse
            {
                NextSteps = nextNodes.Value,
                RecommendedTools = recommendedTools,
                Timestamp = DateTime.UtcNow
            };

            return Result<string, string>.Success(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"NextNode tool error: {ex.Message}");
        }
    }

    private Result<NextNodeRequest, string> ParseInput(string input)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(input);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("current_step_id", out JsonElement stepIdElement))
                return Result<NextNodeRequest, string>.Failure("Missing required field: current_step_id");

            if (!root.TryGetProperty("plan_goal", out JsonElement goalElement))
                return Result<NextNodeRequest, string>.Failure("Missing required field: plan_goal");

            string stepId = stepIdElement.GetString() ?? string.Empty;
            string goal = goalElement.GetString() ?? string.Empty;

            // Parse optional context
            Dictionary<string, object>? context = null;
            if (root.TryGetProperty("context", out JsonElement contextElement))
            {
                context = new Dictionary<string, object>();
                foreach (JsonProperty prop in contextElement.EnumerateObject())
                {
                    context[prop.Name] = prop.Value.ToString();
                }
            }

            // Parse optional constraints
            List<string>? constraints = null;
            if (root.TryGetProperty("constraints", out JsonElement constraintsElement))
            {
                constraints = new List<string>();
                foreach (JsonElement item in constraintsElement.EnumerateArray())
                {
                    string? value = item.GetString();
                    if (value != null)
                        constraints.Add(value);
                }
            }

            return Result<NextNodeRequest, string>.Success(
                new NextNodeRequest(stepId, goal, context, constraints)
            );
        }
        catch (Exception ex)
        {
            return Result<NextNodeRequest, string>.Failure(
                $"Failed to parse input: {ex.Message}"
            );
        }
    }

    private sealed record NextNodeRequest(
        string CurrentStepId,
        string PlanGoal,
        Dictionary<string, object>? Context,
        List<string>? Constraints
    );

    private sealed class NextNodeResponse
    {
        public List<NextNodeCandidate> NextSteps { get; set; } = new();
        public List<string> RecommendedTools { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
