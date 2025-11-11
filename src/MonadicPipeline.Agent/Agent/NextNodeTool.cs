// <copyright file="NextNodeTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

using System.Text.Json;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Tools.MeTTa;

/// <summary>
/// Tool for enumerating valid next execution nodes using symbolic MeTTa reasoning.
/// Translates current plan/state into MeTTa, queries for valid next steps, and updates state.
/// </summary>
public sealed class NextNodeTool : ITool
{
    private readonly IMeTTaEngine engine;
    private readonly MeTTaRepresentation representation;
    private readonly ToolRegistry registry;

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
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.representation = new MeTTaRepresentation(engine);
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // Parse input
            var request = this.ParseInput(input);
            if (request.IsFailure)
            {
                return Result<string, string>.Failure(request.Error);
            }

            var req = request.Value;

            // Add any constraint rules
            if (req.Constraints != null)
            {
                foreach (var constraint in req.Constraints)
                {
                    await this.representation.AddConstraintAsync(constraint, ct);
                }
            }

            // Query for next nodes
            var nextNodes = await this.representation.QueryNextNodesAsync(
                req.CurrentStepId,
                req.Context ?? new Dictionary<string, object>(),
                ct);

            if (nextNodes.IsFailure)
            {
                return Result<string, string>.Failure(nextNodes.Error);
            }

            // Query for recommended tools
            var toolsResult = await this.representation.QueryToolsForGoalAsync(req.PlanGoal, ct);
            var recommendedTools = toolsResult.GetValueOrDefault(new List<string>());

            // Build response
            var response = new NextNodeResponse
            {
                NextSteps = nextNodes.Value,
                RecommendedTools = recommendedTools,
                Timestamp = DateTime.UtcNow,
            };

            return Result<string, string>.Success(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            if (!root.TryGetProperty("current_step_id", out var stepIdElement))
            {
                return Result<NextNodeRequest, string>.Failure("Missing required field: current_step_id");
            }

            if (!root.TryGetProperty("plan_goal", out var goalElement))
            {
                return Result<NextNodeRequest, string>.Failure("Missing required field: plan_goal");
            }

            var stepId = stepIdElement.GetString() ?? string.Empty;
            var goal = goalElement.GetString() ?? string.Empty;

            // Parse optional context
            Dictionary<string, object>? context = null;
            if (root.TryGetProperty("context", out var contextElement))
            {
                context = new Dictionary<string, object>();
                foreach (var prop in contextElement.EnumerateObject())
                {
                    context[prop.Name] = prop.Value.ToString();
                }
            }

            // Parse optional constraints
            List<string>? constraints = null;
            if (root.TryGetProperty("constraints", out var constraintsElement))
            {
                constraints = new List<string>();
                foreach (var item in constraintsElement.EnumerateArray())
                {
                    var value = item.GetString();
                    if (value != null)
                    {
                        constraints.Add(value);
                    }
                }
            }

            return Result<NextNodeRequest, string>.Success(
                new NextNodeRequest(stepId, goal, context, constraints));
        }
        catch (Exception ex)
        {
            return Result<NextNodeRequest, string>.Failure(
                $"Failed to parse input: {ex.Message}");
        }
    }

    private sealed record NextNodeRequest(
        string CurrentStepId,
        string PlanGoal,
        Dictionary<string, object>? Context,
        List<string>? Constraints);

    private sealed class NextNodeResponse
    {
        public List<NextNodeCandidate> NextSteps { get; set; } = new();

        public List<string> RecommendedTools { get; set; } = new();

        public DateTime Timestamp { get; set; }
    }
}
